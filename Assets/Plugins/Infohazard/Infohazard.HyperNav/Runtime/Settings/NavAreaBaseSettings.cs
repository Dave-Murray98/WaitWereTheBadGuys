// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using Infohazard.Core;
using UnityEngine;

namespace Infohazard.HyperNav.Settings {
    /// <summary>
    /// Base class for all NavArea settings assets.
    /// </summary>
    public class NavAreaBaseSettingsAsset : ScriptableObject {
        /// <summary>
        /// This is used to refer to the names of private fields in this class from a custom Editor.
        /// </summary>
        public static class PropNames {
            public const string Data = "_data";
        }
    }

    /// <summary>
    /// Generic base class for NavArea settings assets.
    /// </summary>
    /// <typeparam name="TData">Type of the settings data.</typeparam>
    public class NavAreaBaseSettingsAsset<TData> : NavAreaBaseSettingsAsset where TData : NavAreaBaseSettings {
        [SerializeField]
        private TData _data;

        /// <summary>
        /// The settings data.
        /// </summary>
        public TData Data => _data;
    }

    /// <summary>
    /// Base settings data for NavAreas.
    /// </summary>
    [Serializable]
    public class NavAreaBaseSettings {
        [SerializeField]
        [Tooltip("Which layers are considered impassible for pathfinding.")]
        [HelpBox("This is generally the layers that the agents will collide with and must therefore avoid.")]
        private LayerMask _blockingLayers = 1;

        [SerializeField]
        [Tooltip("Whether only static objects should be included in the baked data.")]
        [HelpBox("Generally, you probably won't be updating the area data at runtime, and moving objects should " +
                 "not be included in the results. For example, the player should not be baked into the nav data. " +
                 "However, if you will update the data at runtime, you may need to disable this to enable all " +
                 "objects to be included.")]
        private bool _staticOnly = true;

        [SerializeField]
        [Min(0)]
        [Tooltip("The maximum size of agents using this area.")]
        [HelpBox("For volumes, agents are assumed to be spherical, and this defines the radius of that sphere." +
                 "For surfaces, agents are assumed to be capsules, and this defines the radius of the capsule.")]
        private float _maxAgentRadius = 0.5f;

        [SerializeField]
        [Tooltip("Layer this area exists in.")]
        private NavLayer _layer;

        [SerializeField]
        [Min(0)]
        [Tooltip("The maximum distance that external links can extend to other volumes.")]
        [HelpBox("External links connecting to other volumes will not extend beyond this distance. " +
                 "It's best to keep this quite small and place areas near each other, so as to avoid " +
                 "agents travelling through unmapped space.")]
        private float _maxExternalLinkDistanceToVolume = 1;

        [SerializeField]
        [Min(0)]
        [Tooltip("The maximum distance that external links can extend to other surfaces.")]
        [HelpBox("External links connecting to other surfaces will not extend beyond this distance. " +
                 "It's best to keep this quite small and place areas near each other, so as to avoid " +
                 "agents travelling through unmapped space.")]
        private float _maxExternalLinkDistanceToSurface = 1;

        [SerializeField]
        [Tooltip("Layers that external links from this area can connect to.")]
        private NavLayerMask _externalLinkTargetLayers = NavLayerMask.All;

        [SerializeField]
        [Min(0)]
        [Tooltip("The voxel size of this area.")]
        [HelpBox("Area data generation begins with voxelizing the area inside the bounds into navigable " +
                 "and non-navigable space. This defines the size of those voxels. Smaller voxels are more " +
                 "precise, but greatly increase the baking cost cubically (half the voxel size may increase " +
                 "the bake cost by a factor of 8).")]
        private float _voxelSize = 1;

        /// <summary>
        /// This is used to refer to the names of private fields in this class from a custom Editor.
        /// </summary>
        public static class PropNames {
            public const string BlockingLayers = nameof(_blockingLayers);
            public const string StaticOnly = nameof(_staticOnly);
            public const string MaxAgentRadius = nameof(_maxAgentRadius);
            public const string Layer = nameof(_layer);
            public const string MaxExternalLinkDistanceToVolume = nameof(_maxExternalLinkDistanceToVolume);
            public const string MaxExternalLinkDistanceToSurface = nameof(_maxExternalLinkDistanceToSurface);
            public const string ExternalLinkTargetLayers = nameof(_externalLinkTargetLayers);
            public const string VoxelSize = nameof(_voxelSize);
        }

        /// <summary>
        /// The voxel size of this area, which determines the precision but also baking cost.
        /// </summary>
        public float VoxelSize {
            get => _voxelSize;
            set => _voxelSize = value;
        }

        /// <summary>
        /// The maximum size of agents using this area.
        /// </summary>
        public float MaxAgentRadius {
            get => _maxAgentRadius;
            set => _maxAgentRadius = value;
        }

        /// <summary>
        /// Layer this area exists in.
        /// </summary>
        public NavLayer Layer {
            get => _layer;
            set => _layer = value;
        }

        /// <summary>
        /// The maximum distance that external links can extend to other volumes.
        /// </summary>
        public float MaxExternalLinkDistanceToVolume {
            get => _maxExternalLinkDistanceToVolume;
            set => _maxExternalLinkDistanceToVolume = value;
        }

        /// <summary>
        /// The maximum distance that external links can extend to other surfaces.
        /// </summary>
        public float MaxExternalLinkDistanceToSurface {
            get => _maxExternalLinkDistanceToSurface;
            set => _maxExternalLinkDistanceToSurface = value;
        }

        /// <summary>
        /// Layers that external links from this area can connect to.
        /// </summary>
        public NavLayerMask ExternalLinkTargetLayers {
            get => _externalLinkTargetLayers;
            set => _externalLinkTargetLayers = value;
        }

        /// <summary>
        /// Which layers are considered impassible for pathfinding.
        /// </summary>
        public LayerMask BlockingLayers {
            get => _blockingLayers;
            set => _blockingLayers = value;
        }

        /// <summary>
        /// Whether only static objects should be included in the baked data.
        /// </summary>
        public bool StaticOnly {
            get => _staticOnly;
            set => _staticOnly = value;
        }
    }

    /// <summary>
    /// Extension with self-cloning capabilities.
    /// </summary>
    /// <typeparam name="TSelf">Type of self.</typeparam>
    [Serializable]
    public class NavAreaBaseSettings<TSelf> : NavAreaBaseSettings where TSelf : NavAreaBaseSettings, new() {
        /// <summary>
        /// Clone this settings object.
        /// </summary>
        /// <returns>A clone of this settings object.</returns>
        public virtual TSelf Clone() {
            return new TSelf {
                VoxelSize = VoxelSize,
                MaxAgentRadius = MaxAgentRadius,
                MaxExternalLinkDistanceToVolume = MaxExternalLinkDistanceToVolume,
                MaxExternalLinkDistanceToSurface = MaxExternalLinkDistanceToSurface,
                BlockingLayers = BlockingLayers,
                StaticOnly = StaticOnly
            };
        }
    }
}
