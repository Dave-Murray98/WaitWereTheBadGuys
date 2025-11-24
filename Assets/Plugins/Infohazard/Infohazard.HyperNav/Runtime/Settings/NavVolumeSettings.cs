// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using Infohazard.Core;
using UnityEngine;

namespace Infohazard.HyperNav.Settings {
    /// <summary>
    /// Settings for baking NavVolumes.
    /// </summary>
    [Serializable]
    public class NavVolumeSettings : NavAreaBaseSettings<NavVolumeSettings> {
        [SerializeField]
        [Tooltip("Whether to enable multiple physics queries per voxel to get a more accurate result.")]
        [HelpBox("Toggles whether multiple physics queries are performed per voxel. " +
                 "Otherwise, only one sample is performed at the center of the voxel, which can result in " +
                 "thinking agents can travel to spots they may not actually be able to reach.")]
        private bool _enableMultiQuery = true;

        /// <summary>
        /// Serialized property names for NavVolumeSettings.
        /// </summary>
        public new static class PropNames {
            public const string EnableMultiQuery = nameof(_enableMultiQuery);
        }

        /// <summary>
        /// Whether to enable multiple physics queries per voxel to get a more accurate result.
        /// </summary>
        public bool EnableMultiQuery {
            get => _enableMultiQuery;
            set => _enableMultiQuery = value;
        }

        /// <inheritdoc />
        public override NavVolumeSettings Clone() {
            NavVolumeSettings clone = base.Clone();
            clone.EnableMultiQuery = _enableMultiQuery;
            return clone;
        }
    }
}
