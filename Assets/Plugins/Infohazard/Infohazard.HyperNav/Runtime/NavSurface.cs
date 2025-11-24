// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using Cysharp.Threading.Tasks;
using Infohazard.Core;
using Infohazard.HyperNav.Jobs.Baking.Surface;
using Infohazard.HyperNav.Jobs.Utility;
using Infohazard.HyperNav.Settings;
using JetBrains.Annotations;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.XR;
using Object = UnityEngine.Object;

namespace Infohazard.HyperNav {
    [ExecuteAlways]
    public class NavSurface : NavArea<NavSurface, NavSurfaceData, NativeNavSurfaceData, NativeNavSurfaceDataPointers,
        NavSurfaceSettings> {
        #region Serialized Fields

        // Visualization Settings

        [SerializeField, Tooltip("Stage at which to visualize the volume bake process in the editor.")]
        [HelpBox("How to visualize the baked data of the surface. This can be used to debug the baking process, " +
                 "or to understand how the data is constructed. In addition to the step you select, " +
                 "all previous steps will be shown during baking. Note that only the Final mode works " +
                 "after baking has finished, since the intermediate data is not saved.")]
        private NavSurfaceVisualizationMode _visualizationMode = NavSurfaceVisualizationMode.Final;

        [SerializeField]
        [Tooltip("Which filter iteration to visualize.")]
        [HelpBox("If you enable triangle divisions, this controls which division level is visualized.")]
        private int _visualizedFilterIteration;

        [SerializeField]
        [Tooltip("Visualize the normals of the surface.")]
        [HelpBox("If enabled, the visualization mesh will include normal vectors at certain steps where those " +
                 "normals are relevant.")]
        private bool _visualizeNormals;

        /// <summary>
        /// This is used to refer to the names of private fields in this class from a custom Editor.
        /// </summary>
        public new static class PropNames {
            public const string VisualizationMode = nameof(_visualizationMode);
            public const string VisualizedFilterIteration = nameof(_visualizedFilterIteration);
            public const string VisualizeNormals = nameof(_visualizeNormals);
        }

        #endregion

        #region Serialized Fields - Deprecated

        // These fields are maintained for migration from previous versions.
        // They should not be used directly as they have no effect.
        [SerializeField, HideInInspector, UsedImplicitly]
        private float _maxAgentHeight = 2;

        [SerializeField, HideInInspector, UsedImplicitly]
        private float _erosionDistance = 0.25f;

        [SerializeField, HideInInspector, UsedImplicitly]
        private LayerMask _walkableLayers = 1;

        [SerializeField, HideInInspector, UsedImplicitly]
        private NavSurfaceUprightDirectionMode _uprightDirectionMode = NavSurfaceUprightDirectionMode.HitNormal;

        [SerializeField, HideInInspector, UsedImplicitly]
        private Vector3 _fixedUprightDirection = Vector3.up;

        [SerializeField, HideInInspector, UsedImplicitly]
        private float _slopeAngleLimit = 45;

        [SerializeField, HideInInspector, UsedImplicitly]
        private Object _customUprightDirectionHandler;

        [SerializeField, HideInInspector, UsedImplicitly]
        private float _maxAngleBetweenUpDirectionsWithinTriangle = 30;

        [SerializeField, HideInInspector, UsedImplicitly]
        private float _maxAngleBetweenUpDirectionsBetweenTriangles = 30;

        [SerializeField, HideInInspector, UsedImplicitly]
        private int _maxTriangleDivisions = 1;

        [SerializeField, HideInInspector, UsedImplicitly]
        private float _minIslandSurfaceArea = 0;

        [SerializeField, HideInInspector, UsedImplicitly]
        private float _decimationThreshold = 0.5f;

        [SerializeField, HideInInspector, UsedImplicitly]
        private float _boundaryTriangleClippingThreshold = 0.6f;

        [SerializeField, HideInInspector, UsedImplicitly]
        private float _minBoundaryTriangleAreaFraction = 0.5f;

        [SerializeField, HideInInspector, UsedImplicitly]
        private float _maxSurfaceVolumeApproachAngle = 60;

        #endregion

        #region Serialized Field Accessor Properties

        /// <summary>
        /// Stage at which to visualize the volume bake process in the scene view.
        /// </summary>
        public NavSurfaceVisualizationMode VisualizationMode {
            get => _visualizationMode;
            set => _visualizationMode = value;
        }

        /// <summary>
        /// Which filter iteration to visualize.
        /// </summary>
        public int VisualizedFilterIteration {
            get => _visualizedFilterIteration;
            set => _visualizedFilterIteration = value;
        }

        /// <summary>
        /// Visualize the normals of the surface.
        /// </summary>
        public bool VisualizeNormals {
            get => _visualizeNormals;
            set => _visualizeNormals = value;
        }

        #endregion

        public override Type SettingsAssetType => typeof(NavSurfaceSettingsAsset);

        #region Public Methods

        public NavSurface() {
            // Override default values.
            InstanceSettings = new NavSurfaceSettings {
                MaxExternalLinkDistanceToSurface = 0.2f,
                MaxExternalLinkDistanceToVolume = 1.5f,
                VoxelSize = 0.5f,
                MaxAgentRadius = 0.5f,
            };
        }

        protected override NativeNavSurfaceData RebuildNativeData() {
            Transform t = transform;

            NativeNavSurfaceData oldData = NativeData;

            return new NativeNavSurfaceData(
                oldData.ID,
                t.localToWorldMatrix,
                t.worldToLocalMatrix,
                oldData.Bounds,
                oldData.Layer,
                oldData.Vertices,
                oldData.Regions,
                oldData.TriangleIndices,
                oldData.InternalLinks,
                oldData.ExternalLinks,
                oldData.LinkVertices,
                oldData.LinkEdges);
        }

        #endregion
    }

    /// <summary>
    /// The various modes available to generate a preview mesh in the editor for visualization.
    /// </summary>
    public enum NavSurfaceVisualizationMode {
        /// <summary>
        /// Do not generate a preview mesh.
        /// </summary>
        None,

        /// <summary>
        /// (Requires Re-bake) Show open, blocked, and walkable voxels.
        /// </summary>
        Voxels,

        /// <summary>
        /// (Requires Re-bake) Show triangulation of walkable volume.
        /// </summary>
        WalkableTriangles,

        /// <summary>
        /// (Requires Re-bake) Show triangles projected onto the surface.
        /// </summary>
        ShrinkwrappedMesh,

        /// <summary>
        /// (Requires Re-bake) Show triangles after filtering based on upright directions.
        /// </summary>
        FilteredUprightDirections,

        /// <summary>
        /// (Requires Re-bake) Show triangles after filtering based on collisions.
        /// </summary>
        FilteredCollisions,

        /// <summary>
        /// (Requires Re-bake) Show triangles after splitting or removing based on the results of the previous checks.
        /// </summary>
        SplitOrRemovedTriangles,

        /// <summary>
        /// (Requires Re-bake) Show the triangles after splitting vertices based on normal angle.
        /// </summary>
        SplitVertexGroups,

        /// <summary>
        /// (Requires Re-bake) Show triangles projected onto the surface after filtering.
        /// </summary>
        ShrinkwrappedMeshAfterSplit,

        /// <summary>
        /// (Requires Re-bake) Show the simplified mesh after decimation is applied.
        /// </summary>
        Decimation,

        /// <summary>
        /// (Requires Re-bake) Show the simplified mesh after small boundary triangles are removed.
        /// </summary>
        PrunedSmallTriangles,

        /// <summary>
        /// (Requires Re-bake) Show the mesh after bound vertices are pushed away from bounds.
        /// </summary>
        ErodedEdges,

        /// <summary>
        /// (Requires Re-bake) Show the triangles grouped into regions.
        /// </summary>
        Groups,

        /// <summary>
        /// Show the final serialized data for the volume.
        /// </summary>
        Final,
    }

    public enum NavSurfaceUprightDirectionMode {
        /// <summary>
        /// Use a world direction.
        /// </summary>
        FixedWorldDirection,

        /// <summary>
        /// Use a local direction relative to the surface object.
        /// </summary>
        FixedLocalDirection,

        /// <summary>
        /// Use the normal of the query hit.
        /// </summary>
        HitNormal,

        /// <summary>
        /// Use a custom <see cref="ISurfaceUprightDirectionHandler"/> to determine the normal.
        /// </summary>
        Custom,
    }

    /// <summary>
    /// Interface for custom logic to determine the upright direction of a surface.
    /// </summary>
    public interface ISurfaceUprightDirectionHandler {
        /// <summary>
        /// Calculate the upright directions for each triangle of the surface,
        /// and store the results in
        /// <see cref="NavSurfaceBakeData"/>.<see cref="NavSurfaceBakeData.MeshInfo"/>.<see cref="BakingNavSurfaceMeshInfo.TriangleUprightWorldDirections"/>.
        /// </summary>
        /// <remarks>
        /// This should be done using a parallel job if possible.
        /// Any triangles that fail custom checks should be added to
        /// <see cref="NavSurfaceBakeData"/>.<see cref="NavSurfaceBakeData.FilterData"/>'s
        /// <see cref="NavSurfaceFilterData.TrianglesToSplit"/> or <see cref="NavSurfaceFilterData.TrianglesToRemove"/>.
        /// </remarks>
        /// <param name="bakeData">The in-progress data for the surface.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <param name="queries">The raycast queries that were performed.</param>
        /// <param name="resultsPerQuery">How many hits in the results array correspond to each query.</param>
        /// <param name="results">The results of the raycast queries.</param>
        /// <returns>The updated bake data (can be the same as the input if only referenced values are updated).</returns>
        public UniTask<NavSurfaceBakeData> CalculateUprightDirections(
            NavSurfaceBakeData bakeData,
            NativeArray<RaycastCommand> queries,
            int resultsPerQuery,
            NativeArray<RaycastHit> results,
            NativeCancellationToken cancellationToken);
    }
}
