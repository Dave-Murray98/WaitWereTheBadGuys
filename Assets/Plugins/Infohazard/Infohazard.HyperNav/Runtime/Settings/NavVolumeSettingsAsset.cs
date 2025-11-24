// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using UnityEngine;

namespace Infohazard.HyperNav.Settings {
    /// <summary>
    /// Settings asset for baking NavVolumes.
    /// </summary>
    [CreateAssetMenu(fileName = "NewNavVolumeSettings", menuName = "HyperNav/Nav Volume Settings")]
    public class NavVolumeSettingsAsset : NavAreaBaseSettingsAsset<NavVolumeSettings> { }
}
