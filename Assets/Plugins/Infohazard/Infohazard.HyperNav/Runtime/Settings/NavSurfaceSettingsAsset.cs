// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using UnityEngine;

namespace Infohazard.HyperNav.Settings {
    /// <summary>
    /// Settings asset for baking NavSurfaces.
    /// </summary>
    [CreateAssetMenu(fileName = "NewNavSurfaceSettings", menuName = "HyperNav/Nav Surface Settings")]
    public class NavSurfaceSettingsAsset : NavAreaBaseSettingsAsset<NavSurfaceSettings> { }
}
