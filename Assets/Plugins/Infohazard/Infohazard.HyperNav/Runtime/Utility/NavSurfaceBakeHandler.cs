// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Infohazard.Core;
using Infohazard.HyperNav.Jobs.Utility;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Infohazard.HyperNav {
    /// <summary>
    /// Manages the baking process for a <see cref="NavSurface"/>. Cannot be reused.
    /// </summary>
    public class NavSurfaceBakeHandler : NavAreaBaseBakeHandler {
        private const int TotalSteps = (int)NavSurfaceBakeStep.PopulateData + 1;

        private readonly NavSurface _surface;
        private readonly NavSurfaceVisualizationMode _visualizationMode;

        /// <summary>
        /// Create a new bake handler for a volume.
        /// </summary>
        /// <param name="surface">Surface to bake.</param>
        /// <param name="sanityChecks">Whether to run sanity checks to catch baking issues.</param>
        /// <param name="updateSerializedData">Whether to update the serialized data after baking.</param>
        /// <param name="visualizationMode">Visualization mode to use during baking.</param>
        public NavSurfaceBakeHandler(NavSurface surface, bool sanityChecks, bool updateSerializedData,
                                     NavSurfaceVisualizationMode? visualizationMode = null)
            : base(surface, sanityChecks, updateSerializedData) {
            _surface = surface;
            _visualizationMode = visualizationMode ?? surface.VisualizationMode;
        }

        protected override UniTask GenerateData(NativeCancellationToken token) {
            return NavSurfaceUpdate.GenerateSurfaceData(_surface, UpdateSerializedData, token, HandleBakeProgressUpdate);
        }

        private void HandleBakeProgressUpdate(NavSurfaceBakeStep step, NavBakeStepTiming timing,
                                              in NavSurfaceBakeData data) {
            string stepString = step.ToString().SplitCamelCase();
            if (timing == NavBakeStepTiming.Before) {
                UpdateBakeProgress($"{stepString} [{(int)step + 1}/{TotalSteps}]", (float)step / TotalSteps);
                Stopwatch.Restart();
            } else {
                LogStopwatch(stepString);

                if (step == NavSurfaceBakeStep.PopulateData) {
                    UpdateBakeProgress("Finalizing...", 1);
                }

                if (SanityChecks) {
                    RunSanityChecks(step, data);
                }

                UpdatePreviewMesh(step, data);
            }
        }

        private void RunSanityChecks(NavSurfaceBakeStep step, NavSurfaceBakeData data) { }

        private void UpdatePreviewMesh(NavSurfaceBakeStep step, NavSurfaceBakeData data) {
            int visIter = _surface.VisualizedFilterIteration;
            NavSurfaceBakeStep stepForMode = StepForVisMode(_visualizationMode);

            bool generateMesh = step <= stepForMode;

            if (step is >= NavSurfaceBakeStep.ShrinkwrapMesh and <= NavSurfaceBakeStep.SplitOrRemoveTriangles &&
                _visualizationMode >= NavSurfaceVisualizationMode.ShrinkwrappedMesh) {
                generateMesh = visIter > data.FilterData.Iteration ||
                               (visIter == data.FilterData.Iteration && step <= stepForMode);
            }

            if (!generateMesh) return;

            switch (step) {
                case NavSurfaceBakeStep.CalculateBlockedAndWalkableVoxels:
                    NavAreaPreviewUtility.BuildVoxelPreviewMesh(_surface, data.Voxels);
                    break;
                case NavSurfaceBakeStep.TriangulateVoxels:
                case NavSurfaceBakeStep.ShrinkwrapMesh:
                case NavSurfaceBakeStep.ShrinkwrapMeshAfterSplit:
                    UpdatePreviewMeshForTriangles(data, false, true);
                    break;
                case NavSurfaceBakeStep.FilterUprightDirections:
                case NavSurfaceBakeStep.FilterCollisions:
                    UpdatePreviewMeshForTriangles(data, true, false);
                    break;
                case NavSurfaceBakeStep.SplitOrRemoveTriangles:
                case NavSurfaceBakeStep.SplitVertices:
                case NavSurfaceBakeStep.DecimateTriangles:
                case NavSurfaceBakeStep.PruneSmallTriangles:
                    UpdatePreviewMeshForTriangles(data, false, false);
                    break;
                case NavSurfaceBakeStep.CreateGroups:
                    UpdatePreviewMeshForGroups(data);
                    break;
                case NavSurfaceBakeStep.PopulateData:
                    NavAreaPreviewUtility.RebuildPreviewMesh(_surface);
                    break;
            }
        }

        private void UpdatePreviewMeshForGroups(NavSurfaceBakeData data) {
            NativeArray<UnsafeList<int>> groupTris = new(data.MeshInfo.GroupTriangles.Length, Allocator.Temp);

            for (int i = 0; i < data.MeshInfo.GroupTriangles.Length; i++) {
                HybridIntList dataValue = data.MeshInfo.GroupTriangles[i];
                UnsafeList<int> trisForGroup = new(dataValue.Count, Allocator.Temp);
                for (int j = 0; j < dataValue.Count; j++) {
                    trisForGroup.Add(dataValue[j]);
                }

                groupTris[i] = trisForGroup;
            }

            NavAreaPreviewUtility.BuildGroupEdgesPreviewMesh(
                data.Surface,
                UnsafeArray<float4>.ToPointer(data.MeshInfo.Vertices.AsArray()),
                UnsafeArray<int>.ToPointer(data.MeshInfo.TriangleList.AsArray()),
                UnsafeArray<UnsafeList<int>>.ToPointer(groupTris),
                true);
        }

        private unsafe void UpdatePreviewMeshForTriangles(NavSurfaceBakeData data, bool addUpDirections,
                                                          bool addNormals) {
            NativeArray<UnsafeList<int>> triListTemp = new(1, Allocator.Temp);
            UnsafeList<int> triListItem = new(data.MeshInfo.TriangleList.Length, Allocator.Temp);
            triListItem.CopyFrom(data.MeshInfo.TriangleList.AsArray());

            List<int> removeTriIndices =
                ExtractFailingTrianglesToList(ref triListItem, data.FilterData.TrianglesToRemove);
            List<int> splitTriIndices =
                ExtractFailingTrianglesToList(ref triListItem, data.FilterData.TrianglesToSplit);

            RemoveNegativeValues(ref triListItem);

            triListTemp[0] = triListItem;

            NavAreaPreviewUtility.BuildTriangulationPreviewMesh(_surface, data.MeshInfo.Vertices, triListTemp);

            if (_surface.VisualizeNormals) {
                UnsafeList<int> triListForNormals = *data.MeshInfo.TriangleList.GetUnsafeList();
                if (addUpDirections) {
                    NavAreaPreviewUtility.AddNormalsToPreviewMesh(_surface, data.MeshInfo.Vertices, triListForNormals,
                        data.MeshInfo.TriangleUprightWorldDirections.AsArray(), false);
                } else if (addNormals) {
                    NavAreaPreviewUtility.AddNormalsToPreviewMesh(_surface, data.MeshInfo.Vertices, triListForNormals,
                        data.MeshInfo.TriangleNormals.AsArray(), true);
                }
            }

            if (removeTriIndices.Count > 0) {
                CreateFailingTriangleSubMesh(removeTriIndices, "HyperNav/RemovedTrianglePreviewMaterial");
            }

            if (splitTriIndices.Count > 0) {
                CreateFailingTriangleSubMesh(splitTriIndices, "HyperNav/SplitTrianglePreviewMaterial");
            }

            triListTemp.Dispose();
        }

        private List<int> ExtractFailingTrianglesToList(ref UnsafeList<int> list, NativeHashSet<int> failingTriangles) {
            List<int> newTriangleIndices = new();

            if (!failingTriangles.IsCreated) return newTriangleIndices;

            foreach (int triStart in failingTriangles) {
                for (int i = 0; i < 3; i++) {
                    int indexInTris = triStart + i;
                    int vIndex = list[indexInTris];
                    if (vIndex < 0) continue;

                    newTriangleIndices.Add(vIndex);
                    list[indexInTris] = -1;
                }
            }

            return newTriangleIndices;
        }

        private void RemoveNegativeValues(ref UnsafeList<int> list) {
            // Remove negative values from list (preserve order)
            int writeIndex = 0;
            for (int i = 0; i < list.Length; i++) {
                if (list[i] < 0) continue;
                list[writeIndex] = list[i];
                writeIndex++;
            }

            list.Length = writeIndex;
        }

        private void CreateFailingTriangleSubMesh(List<int> newTriangleIndices, string matName) {
            int newSubMesh = _surface.PreviewMesh.subMeshCount;
            _surface.PreviewMesh.subMeshCount += 1;
            _surface.PreviewMesh.SetTriangles(newTriangleIndices, newSubMesh);

            Material[] mats = _surface.PreviewMaterials;
            Array.Resize(ref mats, _surface.PreviewMesh.subMeshCount);
            mats[newSubMesh] = Resources.Load<Material>(matName);
            _surface.PreviewMaterials = mats;
        }

        private NavSurfaceBakeStep StepForVisMode(NavSurfaceVisualizationMode visMode) {
            return visMode switch {
                NavSurfaceVisualizationMode.None => (NavSurfaceBakeStep)(-1),
                NavSurfaceVisualizationMode.Voxels => NavSurfaceBakeStep.CalculateBlockedAndWalkableVoxels,
                NavSurfaceVisualizationMode.WalkableTriangles => NavSurfaceBakeStep.TriangulateVoxels,
                NavSurfaceVisualizationMode.ShrinkwrappedMesh => NavSurfaceBakeStep.ShrinkwrapMesh,
                NavSurfaceVisualizationMode.FilteredUprightDirections => NavSurfaceBakeStep.FilterUprightDirections,
                NavSurfaceVisualizationMode.FilteredCollisions => NavSurfaceBakeStep.FilterCollisions,
                NavSurfaceVisualizationMode.SplitOrRemovedTriangles => NavSurfaceBakeStep.SplitOrRemoveTriangles,
                NavSurfaceVisualizationMode.SplitVertexGroups => NavSurfaceBakeStep.SplitVertices,
                NavSurfaceVisualizationMode.ShrinkwrappedMeshAfterSplit => NavSurfaceBakeStep.ShrinkwrapMeshAfterSplit,
                NavSurfaceVisualizationMode.PrunedSmallTriangles => NavSurfaceBakeStep.PruneSmallTriangles,
                NavSurfaceVisualizationMode.ErodedEdges => NavSurfaceBakeStep.ErodeEdges,
                NavSurfaceVisualizationMode.Decimation => NavSurfaceBakeStep.DecimateTriangles,
                NavSurfaceVisualizationMode.Groups => NavSurfaceBakeStep.CreateGroups,
                _ => NavSurfaceBakeStep.PopulateData,
            };
        }
    }
}