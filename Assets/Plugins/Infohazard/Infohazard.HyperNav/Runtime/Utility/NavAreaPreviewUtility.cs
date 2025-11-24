// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using System.Collections.Generic;
using System.Linq;
using Infohazard.HyperNav.Jobs.Baking.Surface;
using Infohazard.HyperNav.Jobs.Utility;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

namespace Infohazard.HyperNav {
    public static class NavAreaPreviewUtility {
        // Materials used for preview meshes.
        private static Material _blockedVoxelPreviewMaterial;
        private static Material _walkableVoxelPreviewMaterial;
        private static Material _voxelDistancePreviewMaterial;
        private static Material _regionIDPreviewMaterial;
        private static Material _blockedVoxelOutlinePreviewMaterial;
        private static Material _walkableVoxelOutlinePreviewMaterial;
        private static Material _triangulationPreviewMaterial;
        private static Material _coloredTriangulationPreviewMaterial;
        private static Material _triangulationOutlinePreviewMaterial;
        private static Material _surfaceUpPreviewMaterial;

        private static readonly int[] VertexIndicesForCubeFaces = {
            3, 2, 0, 1, // left
            6, 7, 5, 4, // right
            0, 4, 5, 1, // bottom
            3, 7, 6, 2, // top
            2, 6, 4, 0, // back
            1, 5, 7, 3, // front
        };

        private static readonly int[] VertexIndicesForCubeEdges = {
            0, 4, 4, 5, 5, 1, 1, 0, // bottom
            3, 7, 7, 6, 6, 2, 2, 3, // top
            0, 2, 4, 6, 5, 7, 1, 3, // sides
        };

        private static readonly Vector3Int[] VerticesForCube = {
            new(-1, -1, -1), // 0
            new(-1, -1, 1), // 1
            new(-1, 1, -1), // 2
            new(-1, 1, 1), // 3
            new(1, -1, -1), // 4
            new(1, -1, 1), // 5
            new(1, 1, -1), // 6
            new(1, 1, 1), // 7
        };

        public static void RebuildPreviewMesh(NavAreaBase area) {
            if (area is
                NavVolume { VisualizationMode: NavVolumeVisualizationMode.Blocking or NavVolumeVisualizationMode.Final }
                    volume) {
                BuildTriangulationPreviewMesh(volume, volume.NativeData, volume.VisualizationMode,
                                              volume.VisualizationSoloRegion);
            } else if (area is NavSurface { VisualizationMode: NavSurfaceVisualizationMode.Final } surface) {
                BuildGroupEdgesPreviewMesh(surface, surface.NativeData, false);
            }
        }

        public static void BuildGroupEdgesPreviewMesh(NavSurface surface, NativeNavSurfaceData data, bool colors) {
            UnsafeArray<UnsafeList<int>> triIndicesPerRegion = new(data.Regions.Length, Allocator.Temp);

            for (int i = 0; i < data.Regions.Length; i++) {
                NativeNavSurfaceRegionData region = data.Regions[i];
                UnsafeList<int> tris = new(region.TriangleIndexRange.Length / 3, Allocator.Temp);

                for (int j = 0; j < region.TriangleIndexRange.Length; j += 3) {
                    tris.Add(region.TriangleIndexRange.Start + j);
                }

                triIndicesPerRegion[i] = tris;
            }

            BuildGroupEdgesPreviewMesh(surface, data.Vertices, data.TriangleIndices, triIndicesPerRegion, colors);
        }

        public static void BuildGroupEdgesPreviewMesh(NavSurface surface,
                                                      UnsafeArray<float4> vertices,
                                                      UnsafeArray<int> triList,
                                                      UnsafeArray<UnsafeList<int>> triIndicesPerRegion,
                                                      bool useColoredTriangles) {

            NativeList<float4> verticesTemp = new(vertices.Length * 2, Allocator.Temp);
            NativeList<int> vertexGroupIDsTemp = new(vertices.Length * 2, Allocator.Temp);
            UnsafeList<int> triListItem = new(triList.Length * 2, Allocator.Temp);

            UnsafeHashMap<int, int> indicesForGroup = new(256, Allocator.Temp);
            UnsafeHashMap<int, int> triRegions = new(triList.Length / 3, Allocator.Temp);

            for (int i = 0; i < triIndicesPerRegion.Length; i++) {
                UnsafeList<int> trisForRegion = triIndicesPerRegion[i];
                indicesForGroup.Clear();

                for (int j = 0; j < trisForRegion.Length; j++) {
                    int triStart = trisForRegion[j];
                    triRegions[triStart] = i;

                    for (int k = 0; k < 3; k++) {
                        int vertexIndex = triList[triStart + k];

                        if (!indicesForGroup.TryGetValue(vertexIndex, out int newIndex)) {
                            newIndex = verticesTemp.Length;
                            indicesForGroup[vertexIndex] = newIndex;
                            verticesTemp.Add(vertices[vertexIndex]);
                            vertexGroupIDsTemp.Add(i);
                        }

                        triListItem.Add(newIndex);
                    }
                }
            }

            NativeArray<UnsafeList<int>> triListTemp = new(1, Allocator.Temp);
            triListTemp[0] = triListItem;

            List<int> edgeIndices = new();

            UnsafeHashMap<Edge, int2> edgeRegions = new(1024, Allocator.Temp);
            for (int i = 0; i < triList.Length; i += 3) {
                int region = triRegions[i];

                for (int j = 0; j < 3; j++) {
                    int vertexIndex1 = triList[i + j];
                    int vertexIndex2 = triList[i + (j + 1) % 3];

                    Edge edge = new(vertexIndex1, vertexIndex2);
                    if (edgeRegions.TryGetValue(edge, out int2 regions)) {
                        if (regions.x < 0) {
                            regions.x = region;
                        } else if (regions.y < 0) {
                            regions.y = region;
                        }
                    } else {
                        regions = new int2(region, -1);
                    }

                    edgeRegions[edge] = regions;
                }
            }

            foreach (KVPair<Edge, int2> pair in edgeRegions) {
                if (pair.Value.y == pair.Value.x) continue;

                int oldIndex1 = pair.Key.Vertex1;
                int oldIndex2 = pair.Key.Vertex2;

                int newIndex1 = verticesTemp.Length;
                verticesTemp.Add(vertices[oldIndex1]);
                vertexGroupIDsTemp.Add(-1);

                int newIndex2 = verticesTemp.Length;
                verticesTemp.Add(vertices[oldIndex2]);
                vertexGroupIDsTemp.Add(-1);

                edgeIndices.Add(newIndex1);
                edgeIndices.Add(newIndex2);
            }

            BuildTriangulationPreviewMesh(surface, verticesTemp, triListTemp);

            surface.PreviewMesh.SetIndices(edgeIndices, MeshTopology.Lines, 1);

            Material[] mats = surface.PreviewMaterials;
            if (useColoredTriangles) {
                AddGroupIDToPreviewMesh(surface, vertexGroupIDsTemp.AsArray());
                if (!_coloredTriangulationPreviewMaterial)
                    _coloredTriangulationPreviewMaterial =
                        Resources.Load<Material>("HyperNav/TriangulationColoredPreviewMaterial");
                mats[0] = _coloredTriangulationPreviewMaterial;
            } else {
                if (!_triangulationPreviewMaterial)
                    _triangulationPreviewMaterial = Resources.Load<Material>("HyperNav/TriangulationPreviewMaterial");
                mats[0] = _triangulationPreviewMaterial;
            }
            surface.PreviewMaterials = mats;
        }

        /// <summary>
        /// Build a preview mesh for the given region based on the given native data.
        /// </summary>
        /// <param name="area">Volume to set preview mesh and materials for.</param>
        /// <param name="data">Native data to create visualization for.</param>
        /// <param name="mode">Visualization mode (must be Blocking or Final).</param>
        /// <param name="soloRegion">If set, only build mesh for the given region.</param>
        public static void BuildTriangulationPreviewMesh(NavAreaBase area, in NativeNavVolumeData data,
                                                         NavVolumeVisualizationMode mode, int soloRegion = -1) {
            if (mode is not (NavVolumeVisualizationMode.Blocking or NavVolumeVisualizationMode.Final)) {
                Debug.LogError(
                    $"Cannot build preview mesh for mode {mode} - only Blocking and Final modes are supported.");
                return;
            }

            UnsafeArray<float4> vertices = data.Vertices;
            UnsafeArray<UnsafeList<int>> triLists;

            if (mode == NavVolumeVisualizationMode.Final) {
                triLists = new UnsafeArray<UnsafeList<int>>(data.Regions.Length, Allocator.Temp);
                for (int i = 0; i < data.Regions.Length; i++) {
                    NativeNavVolumeRegionData region = data.Regions[i];
                    UnsafeList<int> tris = new(region.TriangleIndexRange.Length, Allocator.Temp);
                    CopyTriangleIndices(data.TriangleIndices, ref tris, region.TriangleIndexRange.Start,
                                        region.TriangleIndexRange.Length);
                    triLists[i] = tris;
                }
            } else {
                triLists = new UnsafeArray<UnsafeList<int>>(1, Allocator.Temp);
                UnsafeList<int> tris = new(data.BlockingTriangleIndexCount, Allocator.Temp);
                CopyTriangleIndices(data.TriangleIndices, ref tris, 0, data.BlockingTriangleIndexCount);
                triLists[0] = tris;
            }

            BuildTriangulationPreviewMesh(area, vertices, triLists, soloRegion);

            for (int i = 0; i < triLists.Length; i++) {
                triLists[i].Dispose();
            }

            triLists.Dispose();
        }

        private static void CopyTriangleIndices(UnsafeArray<int> source, ref UnsafeList<int> dest, int start,
                                                int count) {
            for (int i = 0; i < count; i++) {
                dest.Add(source[start + i]);
            }
        }

        /// <summary>
        /// Build a preview mesh for the given volume based on the given list of vertices and lists of triangles.
        /// </summary>
        /// <param name="area">The volume to build for.</param>
        /// <param name="vertices">The list of all vertices.</param>
        /// <param name="triLists">The list of all triangle lists.</param>
        /// <param name="soloRegion">If set, only build mesh for the given region.</param>
        public static unsafe void BuildTriangulationPreviewMesh(NavAreaBase area, NativeList<float4> vertices,
                                                                NativeArray<UnsafeList<int>> triLists,
                                                                int soloRegion = -1) {
            UnsafeArray<float4> verticesArray = new((IntPtr) vertices.GetUnsafePtr(), vertices.Length);
            UnsafeArray<UnsafeList<int>> triListsArray = new((IntPtr) triLists.GetUnsafePtr(), triLists.Length);

            BuildTriangulationPreviewMesh(area, verticesArray, triListsArray, soloRegion);
        }

        /// <summary>
        /// Build a preview mesh for the given volume based on the given list of vertices and lists of triangles.
        /// </summary>
        /// <param name="area">The volume to build for.</param>
        /// <param name="vertices">The list of all vertices.</param>
        /// <param name="triLists">The list of all triangle lists.</param>
        /// <param name="soloRegion">If set, only build mesh for the given region.</param>
        public static void BuildTriangulationPreviewMesh(NavAreaBase area, UnsafeArray<float4> vertices,
                                                         UnsafeArray<UnsafeList<int>> triLists, int soloRegion = -1) {
            Mesh mesh = new();

            mesh.indexFormat = IndexFormat.UInt32; // 32 bit to support large meshes.

            if (soloRegion >= 0) {
                mesh.subMeshCount = 2; // 1 mesh for triangles, 1 for edges.
            } else {
                mesh.subMeshCount = triLists.Length * 2; // 1 mesh per region for triangles, 1 for edges.
            }

            // Set vertices without modification.
            List<Vector3> meshVertices = new(vertices.Length);
            for (int i = 0; i < vertices.Length; i++) {
                meshVertices.Add(vertices[i].xyz);
            }

            mesh.SetVertices(meshVertices);

            int triMeshIndex = 0;
            int lineMeshIndex = mesh.subMeshCount / 2;

            // Loop through triangles and add indices.
            for (int i = 0; i < triLists.Length; i++) {
                if (soloRegion >= 0 && soloRegion != i) continue;

                // Set triangle submesh indices directly.
                UnsafeList<int> triList = triLists[i];

                List<int> meshIndices = new(triList.Length);
                for (int j = 0; j < triList.Length; j++) {
                    if (triList[j] < 0) continue;
                    meshIndices.Add(triList[j]);
                }

                mesh.SetIndices(meshIndices, MeshTopology.Triangles, triMeshIndex++);

                // Calculate and set indices for line submesh.
                HashSet<Edge> addedEdges = new();
                List<int> lineIndices = new();
                int triCount = triList.Length / 3;
                for (int j = 0; j < triCount; j++) {
                    int triBase = j * 3;
                    for (int k = 0; k < 3; k++) {
                        int index1 = triBase + k;
                        int index2 = triBase + ((k + 1) % 3);

                        if (triList[index1] < 0 || triList[index2] < 0) continue;

                        // Ensure each edge is only added once.
                        if (!addedEdges.Add(new Edge(triList[index1], triList[index2]))) continue;

                        lineIndices.Add(triList[index1]);
                        lineIndices.Add(triList[index2]);
                    }
                }

                mesh.SetIndices(lineIndices, MeshTopology.Lines, lineMeshIndex++);
            }

            area.PreviewMesh = mesh;

            // Load and assign materials.
            if (!_triangulationPreviewMaterial)
                _triangulationPreviewMaterial = Resources.Load<Material>("HyperNav/TriangulationPreviewMaterial");

            if (!_triangulationOutlinePreviewMaterial)
                _triangulationOutlinePreviewMaterial =
                    Resources.Load<Material>("HyperNav/TriangulationOutlinePreviewMaterial");

            area.PreviewMaterials =
                Enumerable.Repeat(_triangulationPreviewMaterial, mesh.subMeshCount / 2)
                          .Concat(Enumerable.Repeat(_triangulationOutlinePreviewMaterial, mesh.subMeshCount / 2))
                          .ToArray();
        }

        public static void AddNormalsToPreviewMesh(NavAreaBase area, NativeList<float4> vertices,
                                                   UnsafeList<int> triList, NativeArray<float4> normals,
                                                   bool normalsAreInLocalSpace) {
            Mesh mesh = area.PreviewMesh;

            float4x4 matrix = normalsAreInLocalSpace ? float4x4.identity : area.transform.worldToLocalMatrix;

            List<Vector3> newVertices = new(mesh.vertices);
            List<int> newIndices = new();

            int triCount = triList.Length / 3;

            for (int i = 0; i < triCount ; i++) {
                int triBase = i * 3;

                int index1 = triList[triBase + 0];
                int index2 = triList[triBase + 1];
                int index3 = triList[triBase + 2];

                if (index1 < 0 || index2 < 0 || index3 < 0) continue;

                float4 v1 = vertices[index1];
                float4 v2 = vertices[index2];
                float4 v3 = vertices[index3];

                float4 worldUp = normals[i];
                float4 localUp = math.mul(matrix, worldUp);
                float4 center = (v1 + v2 + v3) / 3;

                int firstNewIndex = newVertices.Count;
                newVertices.Add(center.xyz);
                newVertices.Add((center + localUp * area.BaseSettings.VoxelSize * 0.5f).xyz);

                newIndices.Add(firstNewIndex);
                newIndices.Add(firstNewIndex + 1);
            }

            mesh.SetVertices(newVertices);
            mesh.subMeshCount += 1;
            mesh.SetIndices(newIndices, MeshTopology.Lines, mesh.subMeshCount - 1);

            if (!_surfaceUpPreviewMaterial)
                _surfaceUpPreviewMaterial = Resources.Load<Material>("HyperNav/SurfaceUpPreviewMaterial");

            Material[] materials = area.PreviewMaterials;
            Array.Resize(ref materials, mesh.subMeshCount);
            materials[mesh.subMeshCount - 1] = _surfaceUpPreviewMaterial;

            area.PreviewMaterials = materials;
        }

        public static void AddGroupIDToPreviewMesh(NavAreaBase area, NativeArray<int> groupIDs) {
            Mesh mesh = area.PreviewMesh;

            Color[] colors = new Color[groupIDs.Length];

            int r = Random.Range(0, 1000000);
            for (int i = 0; i < groupIDs.Length; i++) {
                if (groupIDs[i] < 0) {
                    colors[i] = Color.white;
                    continue;
                }

                Random.InitState(r + groupIDs[i]);
                colors[i] = new Color(Random.value, Random.value, Random.value);
            }

            mesh.SetColors(colors);
        }

        /// <summary>
        /// Build a preview mesh for the given volume based on the given list of voxels.
        /// </summary>
        /// <remarks>
        /// All voxels are represented as small cubes in the mesh, with each region assigned a random color.
        /// </remarks>
        /// <param name="volume">Volume to build preview for.</param>
        /// <param name="regions">Voxel data.</param>
        /// <param name="regionCount">Total number of regions.</param>
        /// <param name="soloRegion">If set, only build mesh for the given region.</param>
        public static void BuildRegionIDPreviewMesh(NavVolume volume, Fast3DArray regions, int regionCount,
                                                    int soloRegion = -1) {
            Mesh mesh = new();

            int sizeX = regions.SizeX;
            int sizeY = regions.SizeY;
            int sizeZ = regions.SizeZ;

            List<Vector3> positions = new();
            List<Color> colors = new();

            List<int>[] quadIndices = new List<int>[regionCount];

            Vector3 boxSize = volume.Settings.VoxelSize * Vector3.one * 0.2f;

            Dictionary<int, Color> regionColors = new();

            // Add all cubes to the mesh as both triangles and lines.
            for (int x = 0; x < sizeX; x++) {
                for (int y = 0; y < sizeY; y++) {
                    for (int z = 0; z < sizeZ; z++) {
                        int region = regions[x, y, z];
                        if (region < 0 || (soloRegion >= 0 && region != soloRegion)) continue;
                        Vector3 voxelPos = volume.Bounds.min +
                                           new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) * volume.Settings.VoxelSize;

                        int firstIndex = positions.Count;
                        AddBoxPositions(positions, voxelPos, boxSize);

                        List<int> indices = quadIndices[region];
                        if (indices == null) {
                            indices = new List<int>();
                            quadIndices[region] = indices;
                        }

                        AddBoxIndices(indices, firstIndex);

                        // Vertex color by region.
                        if (!regionColors.TryGetValue(region, out Color color)) {
                            color = new Color(Random.value, Random.value, Random.value);
                            regionColors[region] = color;
                        }

                        AddRepeatingData(colors, color, 8);
                    }
                }
            }

            // Set all vertices and indices.
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.subMeshCount = regionCount;
            mesh.SetVertices(positions);
            mesh.SetColors(colors);

            for (int i = 0; i < regionCount; i++) {
                mesh.SetIndices(quadIndices[i], MeshTopology.Quads, i);
            }

            volume.PreviewMesh = mesh;

            // Load and assign materials.
            if (!_regionIDPreviewMaterial)
                _regionIDPreviewMaterial = Resources.Load<Material>("HyperNav/RegionIDPreviewMaterial");

            volume.PreviewMaterials = Enumerable.Repeat(_regionIDPreviewMaterial, regionCount).ToArray();
        }

        /// <summary>
        /// Build a preview mesh for the given volume based on the given list of voxels.
        /// </summary>
        /// <remarks>
        /// Voxels with a value of 0 are included in the mesh, all other values are ignored.
        /// </remarks>
        /// <param name="volume">Volume to build preview for.</param>
        /// <param name="voxels">Voxel data.</param>
        public static void BuildVoxelPreviewMesh(NavVolume volume, Fast3DArray voxels) {
            Mesh mesh = new();

            int sizeX = voxels.SizeX;
            int sizeY = voxels.SizeY;
            int sizeZ = voxels.SizeZ;

            List<Vector3> positions = new List<Vector3>();
            List<int> quadIndices = new List<int>();
            List<int> lineIndices = new List<int>();

            Span<int> cubeVertIndices = stackalloc int[8];

            float voxelSize = volume.Settings.VoxelSize;


            // Loop through all voxels, and if blocked, add box positions and indices for quads and lines.
            for (int x = 0; x < sizeX; x++) {
                for (int y = 0; y < sizeY; y++) {
                    for (int z = 0; z < sizeZ; z++) {
                        if (voxels[x, y, z] != 0) continue;
                        Vector3 voxelCenterPos = volume.Bounds.min +
                                           new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) * volume.Settings.VoxelSize;

                        for (int i = 0; i < 8; i++) {
                            cubeVertIndices[i] = -1;
                        }

                        AddBoxFacesWithHiddenFaceRemoval(positions, quadIndices, voxelCenterPos,
                                                         voxelSize, voxels, new Vector3Int(x, y, z),
                                                         cubeVertIndices);

                        AddBoxEdgesWithHiddenAndFlatEdgeRemoval(positions, lineIndices, voxelCenterPos,
                                                                voxelSize, voxels, new Vector3Int(x, y, z),
                                                                cubeVertIndices);
                    }
                }
            }

            // Build mesh.
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.subMeshCount = 2;
            mesh.SetVertices(positions);
            mesh.SetIndices(quadIndices, MeshTopology.Quads, 0);
            mesh.SetIndices(lineIndices, MeshTopology.Lines, 1);

            volume.PreviewMesh = mesh;

            // Load and assign materials.
            if (!_blockedVoxelPreviewMaterial) {
                _blockedVoxelPreviewMaterial = Resources.Load<Material>("HyperNav/BlockedVoxelPreviewMaterial");
            }

            if (!_blockedVoxelOutlinePreviewMaterial) {
                _blockedVoxelOutlinePreviewMaterial =
                    Resources.Load<Material>("HyperNav/BlockedVoxelOutlinePreviewMaterial");
            }

            volume.PreviewMaterials = new[] { _blockedVoxelPreviewMaterial, _blockedVoxelOutlinePreviewMaterial };
        }

        public static void BuildVoxelPreviewMesh(NavSurface surface, Fast3DArray voxels) {
            Mesh mesh = new();

            int sizeX = voxels.SizeX;
            int sizeY = voxels.SizeY;
            int sizeZ = voxels.SizeZ;
            int3 size = new(sizeX, sizeY, sizeZ);

            List<Vector3> positions = new List<Vector3>();
            List<int> blockingQuadIndices = new List<int>();
            List<int> blockingLineIndices = new List<int>();
            List<int> walkableQuadIndices = new List<int>();
            List<int> walkableLineIndices = new List<int>();

            Span<int> cubeVertIndices = stackalloc int[8];
            float voxelSize = surface.Settings.VoxelSize;

            // Loop through all voxels, and if blocked, add box positions and indices for quads and lines.
            for (int x = 0; x < sizeX; x++) {
                for (int y = 0; y < sizeY; y++) {
                    for (int z = 0; z < sizeZ; z++) {
                        int value = voxels[x, y, z];
                        if (value == PopulateSurfaceVoxelsFromHitsJob.ResultOpen) continue;
                        Vector3 voxelCenterPos = surface.Bounds.min +
                                           new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) * voxelSize;

                        for (int i = 0; i < 8; i++) {
                            cubeVertIndices[i] = -1;
                        }

                        if (value == PopulateSurfaceVoxelsFromHitsJob.ResultBlocked) {
                            AddBoxFacesWithHiddenFaceRemoval(positions, blockingQuadIndices, voxelCenterPos,
                                                             voxelSize, voxels, new Vector3Int(x, y, z),
                                                             cubeVertIndices);

                            AddBoxEdgesWithHiddenAndFlatEdgeRemoval(positions, blockingLineIndices, voxelCenterPos,
                                                                    voxelSize, voxels, new Vector3Int(x, y, z),
                                                                    cubeVertIndices);
                        } else {
                            AddBoxFacesWithHiddenFaceRemoval(positions, walkableQuadIndices, voxelCenterPos,
                                                             voxelSize, voxels, new Vector3Int(x, y, z),
                                                             cubeVertIndices);

                            AddBoxEdgesWithHiddenAndFlatEdgeRemoval(positions, walkableLineIndices, voxelCenterPos,
                                                                    voxelSize, voxels, new Vector3Int(x, y, z),
                                                                    cubeVertIndices);
                        }
                    }
                }
            }

            // Build mesh.
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.subMeshCount = 4;
            mesh.SetVertices(positions);
            mesh.SetIndices(blockingQuadIndices, MeshTopology.Quads, 0);
            mesh.SetIndices(walkableQuadIndices, MeshTopology.Quads, 1);
            mesh.SetIndices(blockingLineIndices, MeshTopology.Lines, 2);
            mesh.SetIndices(walkableLineIndices, MeshTopology.Lines, 3);

            surface.PreviewMesh = mesh;

            // Load and assign materials.
            if (!_blockedVoxelPreviewMaterial) {
                _blockedVoxelPreviewMaterial = Resources.Load<Material>("HyperNav/BlockedVoxelPreviewMaterial");
            }

            if (!_walkableVoxelPreviewMaterial) {
                _walkableVoxelPreviewMaterial = Resources.Load<Material>("HyperNav/WalkableVoxelPreviewMaterial");
            }

            if (!_blockedVoxelOutlinePreviewMaterial) {
                _blockedVoxelOutlinePreviewMaterial =
                    Resources.Load<Material>("HyperNav/BlockedVoxelOutlinePreviewMaterial");
            }

            if (!_walkableVoxelOutlinePreviewMaterial) {
                _walkableVoxelOutlinePreviewMaterial =
                    Resources.Load<Material>("HyperNav/WalkableVoxelOutlinePreviewMaterial");
            }

            surface.PreviewMaterials = new[] {
                _blockedVoxelPreviewMaterial,
                _walkableVoxelPreviewMaterial,
                _blockedVoxelOutlinePreviewMaterial,
                _walkableVoxelOutlinePreviewMaterial,
            };
        }

        private static void AddBoxFacesWithHiddenFaceRemoval(List<Vector3> positions, List<int> indices,
                                                             Vector3 center, float boxSize,
                                                             Fast3DArray voxels, Vector3Int voxelPos,
                                                             Span<int> cubeVertIndices) {

            int3 size = new(voxels.SizeX, voxels.SizeY, voxels.SizeZ);
            int value = voxels[voxelPos.x, voxelPos.y, voxelPos.z];

            int faceIndex = -1;
            for (int dir = 0; dir < 3; dir++) {
                for (int sign = -1; sign <= 1; sign += 2) {
                    faceIndex++;

                    int3 neighborPos = new(voxelPos.x, voxelPos.y, voxelPos.z);
                    neighborPos[dir] += sign;

                    if (!NativeMathUtility.IsOutOfBounds(size, neighborPos) &&
                        voxels[neighborPos.x, neighborPos.y, neighborPos.z] == value) {
                        continue;
                    }

                    int firstIndexInFace = faceIndex * 4;
                    for (int i = 0; i < 4; i++) {
                        int vertIndexInCubeIndicesArray = firstIndexInFace + i;
                        int vertIndexInVerticesArray =
                            VertexIndicesForCubeFaces[vertIndexInCubeIndicesArray];

                        int vertIndex = cubeVertIndices[vertIndexInVerticesArray];
                        if (vertIndex < 0) {
                            vertIndex = positions.Count;
                            cubeVertIndices[vertIndexInVerticesArray] = vertIndex;
                            Vector3 offset = (Vector3) VerticesForCube[vertIndexInVerticesArray] *
                                             (0.5f * boxSize);
                            positions.Add(center + offset);
                        }

                        indices.Add(vertIndex);
                    }
                }
            }
        }

        private static void AddBoxEdgesWithHiddenAndFlatEdgeRemoval(List<Vector3> positions, List<int> indices,
                                                                    Vector3 center, float boxSize,
                                                                    Fast3DArray voxels, Vector3Int voxelPos,
                                                                    Span<int> cubeVertIndices) {

            int x = voxelPos.x;
            int y = voxelPos.y;
            int z = voxelPos.z;

            int value = voxels[x, y, z];

            int3 size = new(voxels.SizeX, voxels.SizeY, voxels.SizeZ);

            for (int edgeIndex = 0; edgeIndex < 12; edgeIndex++) {
                int i1 = VertexIndicesForCubeEdges[edgeIndex * 2 + 0];
                int i2 = VertexIndicesForCubeEdges[edgeIndex * 2 + 1];

                Vector3Int v1 = VerticesForCube[i1];
                Vector3Int v2 = VerticesForCube[i2];

                Vector3Int neighborOffsetBothAxes = (v1 + v2) / 2;
                Vector3Int neighborOffset1;
                Vector3Int neighborOffset2;

                if (neighborOffsetBothAxes.x == 0) {
                    neighborOffset1 = new Vector3Int(0, neighborOffsetBothAxes.y, 0);
                    neighborOffset2 = new Vector3Int(0, 0, neighborOffsetBothAxes.z);
                } else if (neighborOffsetBothAxes.y == 0) {
                    neighborOffset1 = new Vector3Int(neighborOffsetBothAxes.x, 0, 0);
                    neighborOffset2 = new Vector3Int(0, 0, neighborOffsetBothAxes.z);
                } else {
                    neighborOffset1 = new Vector3Int(neighborOffsetBothAxes.x, 0, 0);
                    neighborOffset2 = new Vector3Int(0, neighborOffsetBothAxes.y, 0);
                }

                int3 neighborPos1 = new(x + neighborOffset1.x, y + neighborOffset1.y, z + neighborOffset1.z);
                int3 neighborPos2 = new(x + neighborOffset2.x, y + neighborOffset2.y, z + neighborOffset2.z);
                int3 neighborPosBoth = new(x + neighborOffsetBothAxes.x, y + neighborOffsetBothAxes.y,
                                           z + neighborOffsetBothAxes.z);

                int value1 = NativeMathUtility.IsOutOfBounds(size, neighborPos1)
                    ? -1
                    : voxels[neighborPos1.x, neighborPos1.y, neighborPos1.z];

                int value2 = NativeMathUtility.IsOutOfBounds(size, neighborPos2)
                    ? -1
                    : voxels[neighborPos2.x, neighborPos2.y, neighborPos2.z];

                int valueBoth = NativeMathUtility.IsOutOfBounds(size, neighborPosBoth)
                    ? -1
                    : voxels[neighborPosBoth.x, neighborPosBoth.y, neighborPosBoth.z];

                int cntSame = 0;
                if (value1 == value) cntSame++;
                if (value2 == value) cntSame++;
                if (valueBoth == value) cntSame++;

                switch (cntSame) {
                    case 0:
                    case 2:
                        break;
                    case 1:
                        if (valueBoth != value)
                            continue;
                        break;
                    case 3:
                        continue;
                }

                int vertIndex1 = cubeVertIndices[i1];
                int vertIndex2 = cubeVertIndices[i2];

                if (vertIndex1 < 0) {
                    Vector3 offset = (Vector3)v1 * (0.5f * boxSize);
                    vertIndex1 = positions.Count;
                    positions.Add(center + offset);
                    cubeVertIndices[i1] = vertIndex1;
                }

                if (vertIndex2 < 0) {
                    Vector3 offset = (Vector3)v2 * (0.5f * boxSize);
                    vertIndex2 = positions.Count;
                    positions.Add(center + offset);
                    cubeVertIndices[i2] = vertIndex2;
                }

                indices.Add(vertIndex1);
                indices.Add(vertIndex2);
            }
        }

        // Add a value to a list a given number of times.
        private static void AddRepeatingData<T>(List<T> values, T value, int count) {
            for (int i = 0; i < count; i++) {
                values.Add(value);
            }
        }

        // Add the corner positions of a box to a list of positions.
        private static void AddBoxPositions(List<Vector3> positions, Vector3 center, Vector3 size) {
            Vector3 ext = size * 0.5f;

            positions.Add(center + new Vector3(-ext.x, -ext.y, -ext.z));
            positions.Add(center + new Vector3(-ext.x, -ext.y, +ext.z));
            positions.Add(center + new Vector3(-ext.x, +ext.y, -ext.z));
            positions.Add(center + new Vector3(-ext.x, +ext.y, +ext.z));
            positions.Add(center + new Vector3(+ext.x, -ext.y, -ext.z));
            positions.Add(center + new Vector3(+ext.x, -ext.y, +ext.z));
            positions.Add(center + new Vector3(+ext.x, +ext.y, -ext.z));
            positions.Add(center + new Vector3(+ext.x, +ext.y, +ext.z));
        }

        // Add the indices for the quads of a box to a list of indices.
        private static void AddBoxIndices(List<int> indices, int firstVertex) {
            int nnn = firstVertex + 0;
            int nnp = firstVertex + 1;
            int npn = firstVertex + 2;
            int npp = firstVertex + 3;
            int pnn = firstVertex + 4;
            int pnp = firstVertex + 5;
            int ppn = firstVertex + 6;
            int ppp = firstVertex + 7;

            AddQuadIndices(indices, nnn, pnn, pnp, nnp); // bottom
            AddQuadIndices(indices, npp, ppp, ppn, npn); // top
            AddQuadIndices(indices, npn, ppn, pnn, nnn); // back
            AddQuadIndices(indices, nnp, pnp, ppp, npp); // front
            AddQuadIndices(indices, npp, npn, nnn, nnp); // left
            AddQuadIndices(indices, ppn, ppp, pnp, pnn); // right
        }

        // Add the indices of a quad to a list of indices.
        private static void AddQuadIndices(List<int> indices, int v1, int v2, int v3, int v4) {
            indices.Add(v1);
            indices.Add(v2);
            indices.Add(v3);
            indices.Add(v4);
        }
    }
}
