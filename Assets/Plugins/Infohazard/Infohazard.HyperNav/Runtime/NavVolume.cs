// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using System.Collections.Generic;
using System.Linq;
using Infohazard.Core;
using Infohazard.HyperNav.Jobs;
using Infohazard.HyperNav.Jobs.Utility;
using Infohazard.HyperNav.Settings;
using JetBrains.Annotations;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Serialization;
using Random = System.Random;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Experimental.SceneManagement;
#endif

namespace Infohazard.HyperNav {
    /// <summary>
    /// A volume of space in which HyperNav pathfinding can occur.
    /// </summary>
    /// <remarks>
    /// Each NavVolume is divided into convex regions that form pathfinding nodes.
    /// A volume's regions can have connections to each other, and to regions of other volumes.
    /// The information in a NavVolume must be baked in the editor - it cannot be calculated at runtime (for now).
    /// </remarks>
    [ExecuteAlways]
    public class NavVolume : NavArea<NavVolume, NavVolumeData, NativeNavVolumeData, NativeNavVolumeDataPointers,
        NavVolumeSettings> {
        #region Serialized Fields

        // Visualization Settings

        [SerializeField]
        [Tooltip("Stage at which to visualize the volume bake process in the editor.")]
        [HelpBox("How to visualize the baked data of the volume. This can be used to debug the baking process, " +
                 "or to understand how the data is constructed. In addition to the step you select, " +
                 "all previous steps will be shown during baking. Note that only the Final and Blocking modes work " +
                 "after baking has finished, since the intermediate data is not saved.")]
        private NavVolumeVisualizationMode _visualizationMode = NavVolumeVisualizationMode.Final;

        public new static class PropNames {
            public const string VisualizationMode = nameof(_visualizationMode);
        }

        #endregion

        #region Serialized Fields - Deprecated

        // These fields are maintained for migration from previous versions.
        // They should not be used directly as they have no effect.
        [SerializeField, HideInInspector, UsedImplicitly]
        private bool _enableMultiQuery = true;

        #endregion

        #region Serialized Field Accessor Properties

        /// <summary>
        /// Stage at which to visualize the volume bake process in the scene view.
        /// </summary>
        public NavVolumeVisualizationMode VisualizationMode {
            get => _visualizationMode;
            set => _visualizationMode = value;
        }

        #endregion

        public override Type SettingsAssetType => typeof(NavVolumeSettingsAsset);

        #region Public Methods

        public NavVolume() {
            // Override default values.
            InstanceSettings = new NavVolumeSettings {
                MaxExternalLinkDistanceToSurface = 1.5f,
            };
        }

        /// <summary>
        /// Cast a ray against the blocking triangles of the volume, and return the nearest hit.
        /// </summary>
        /// <param name="start">The position (in world space) to start the query at.</param>
        /// <param name="end">The position (in world space) to end the query at.</param>
        /// <param name="hit">If the query hits a triangle, the ratio between start and end at which the hit occurred.</param>
        /// <returns>Whether a triangle was hit.</returns>
        public bool Raycast(Vector3 start, Vector3 end, out float hit) {
            float4 localStart = math.mul(NativeData.InverseTransform, new float4(start, 1));
            float4 localEnd = math.mul(NativeData.InverseTransform, new float4(end, 1));
            return NativeMathUtility.NavVolumeRaycast(localStart, localEnd, false, NativeData, out hit);
        }

        /// <summary>
        /// Cast a ray against the blocking triangles of the volume, and return if there was a hit.
        /// </summary>
        /// <remarks>
        /// More performant than <see cref="Raycast(Vector3, Vector3, out float)"/> if you don't need the hit position,
        /// because it can return as soon as any hit is detected.
        /// </remarks>
        /// <param name="start">The position (in world space) to start the query at.</param>
        /// <param name="end">The position (in world space) to end the query at.</param>
        /// <returns>Whether a triangle was hit.</returns>
        public bool Raycast(Vector3 start, Vector3 end) {
            float4 localStart = math.mul(NativeData.InverseTransform, new float4(start, 1));
            float4 localEnd = math.mul(NativeData.InverseTransform, new float4(end, 1));
            return NativeMathUtility.NavVolumeRaycast(localStart, localEnd, true, NativeData, out _);
        }

        protected override NativeNavVolumeData RebuildNativeData() {
            Transform t = transform;

            NativeNavVolumeData oldData = NativeData;

            return new NativeNavVolumeData(
                oldData.ID,
                t.localToWorldMatrix,
                t.worldToLocalMatrix,
                oldData.Bounds,
                oldData.Layer,
                oldData.Vertices,
                oldData.Regions,
                oldData.TriangleIndices,
                oldData.BlockingTriangleIndexCount,
                oldData.BoundPlanes,
                oldData.InternalLinks,
                oldData.ExternalLinks,
                oldData.LinkVertices,
                oldData.LinkEdges,
                oldData.LinkTriangles);
        }

        #endregion
    }

    /// <summary>
    /// The various modes available to generate a preview mesh in the editor for visualization.
    /// </summary>
    public enum NavVolumeVisualizationMode {
        /// <summary>
        /// Do not generate a preview mesh.
        /// </summary>
        None,

        /// <summary>
        /// (Requires Re-bake) Show open and blocked voxels.
        /// </summary>
        Voxels,

        /// <summary>
        /// (Requires Re-bake) Show which region each voxel belongs to before regions are split to be convex.
        /// </summary>
        InitialRegions,

        /// <summary>
        /// (Requires Re-bake) Show which region each voxel belongs to after regions are split to be convex.
        /// </summary>
        ConvexRegions,

        /// <summary>
        /// (Requires Re-bake) Show which region each voxel belongs to after compatible regions are merged.
        /// </summary>
        CombinedRegions,

        /// <summary>
        /// (Requires Re-bake) Show the meshes generated by triangulating the voxel regions.
        /// </summary>
        RegionTriangulation,

        /// <summary>
        /// (Requires Re-bake) Show the triangulation meshes with unnecessary vertices removed.
        /// </summary>
        Decimation,

        /// <summary>
        /// Show the blocking (impassible) areas.
        /// </summary>
        Blocking,

        /// <summary>
        /// Show the final serialized data for the volume.
        /// </summary>
        Final,
    }
}
