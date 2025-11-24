// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using Infohazard.HyperNav.Jobs.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Infohazard.HyperNav.Jobs.Baking.Volume {
    [BurstCompile]
    public struct TriangulateRegionsJob : IJobParallelFor {
        [ReadOnly]
        public Fast3DArray Regions;

        [WriteOnly]
        public NativeArray<UnsafeList<int4>> Vertices;

        public void Execute(int index) {
            UnsafeList<int4> vertexList = default;

            for (int x = -1; x < Regions.SizeX; x++) {
                for (int y = -1; y < Regions.SizeY; y++) {
                    for (int z = -1; z < Regions.SizeZ; z++) {
                        int4 currentV = new(2 * x + 1, 2 * y + 1, 2 * z + 1, 0);

                        // Find index in marching cubes tables.
                        byte caseIndex = MarchingCubes.GetMarchingCubesIndex(Regions, index, x, y, z);

                        if (!vertexList.IsCreated) {
                            vertexList = new UnsafeList<int4>(128, Allocator.Persistent);
                        }

                        // Use Marching Cubes tables to determine edge indices to connect.
                        TriTableEntry edgeIndices = MarchingCubesTables.TriTable[caseIndex];
                        int triCount = edgeIndices.Length / 3;

                        // Add all triangles to mesh.
                        for (int triIndex = 0; triIndex < triCount; triIndex++) {
                            int triStart = triIndex * 3;

                            int crossPoint1 = -1;
                            int crossPoint2 = -1;
                            int nonCrossPoint = -1;

                            // Loop through edges of triangle to create.
                            // If an edge passes through the center, split it and create two triangles.
                            // This avoids most cases where opposing quads from bordering regions have the opposite
                            // triangulation, and thus have no shared faces.
                            for (int i = 0; i < 3; i++) {
                                int n = (i + 1) % 3;
                                int o = (i + 2) % 3;

                                if (MarchingCubesTables.AcrossCenterEdges[edgeIndices[triStart + i]] !=
                                    edgeIndices[triStart + n])
                                    continue;

                                crossPoint1 = triStart + i;
                                crossPoint2 = triStart + n;
                                nonCrossPoint = triStart + o;
                                break;
                            }

                            if (crossPoint1 == -1) {
                                // Get vertices based on the edge index.
                                int4 vertex1 =
                                    currentV + MarchingCubes.GetMarchingCubesVertex(
                                        edgeIndices[triStart + 0], caseIndex);
                                int4 vertex2 =
                                    currentV + MarchingCubes.GetMarchingCubesVertex(
                                        edgeIndices[triStart + 1], caseIndex);
                                int4 vertex3 =
                                    currentV + MarchingCubes.GetMarchingCubesVertex(
                                        edgeIndices[triStart + 2], caseIndex);

                                // Add vertices to the mesh.
                                vertexList.Add(vertex1);
                                vertexList.Add(vertex2);
                                vertexList.Add(vertex3);
                            } else {
                                // Get vertices based on the edge index, plus center point.
                                int4 vertexCross1 =
                                    currentV + MarchingCubes.GetMarchingCubesVertex(
                                        edgeIndices[crossPoint1], caseIndex);
                                int4 vertexCross2 =
                                    currentV + MarchingCubes.GetMarchingCubesVertex(
                                        edgeIndices[crossPoint2], caseIndex);
                                int4 vertexNonCross =
                                    currentV + MarchingCubes.GetMarchingCubesVertex(
                                        edgeIndices[nonCrossPoint], caseIndex);

                                int4 vertexCenter = currentV + new int4(1, 1, 1, 0);

                                // Add vertices to the mesh.
                                vertexList.Add(vertexCross1);
                                vertexList.Add(vertexCenter);
                                vertexList.Add(vertexNonCross);
                                vertexList.Add(vertexCenter);
                                vertexList.Add(vertexCross2);
                                vertexList.Add(vertexNonCross);
                            }
                        }
                    }
                }
            }

            Vertices[index] = vertexList;
        }
    }
}
