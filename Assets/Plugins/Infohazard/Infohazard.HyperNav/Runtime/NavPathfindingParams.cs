// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

namespace Infohazard.HyperNav {
    public struct NavPathfindingParams {
        public NavAreaTypes AreaTypeMask { get; set; }
        public NavLayerMask LayerMask { get; set; }
        public float CostToChangeToVolume { get; set; }
        public float CostToChangeToSurface { get; set; }
        public float VolumeCostMultiplier { get; set; }
        public float SurfaceCostMultiplier { get; set; }

        public static NavPathfindingParams Default => new() {
            AreaTypeMask = NavAreaTypes.All,
            LayerMask = NavLayerMask.All,
            CostToChangeToVolume = 1,
            CostToChangeToSurface = 1,
            VolumeCostMultiplier = 0,
            SurfaceCostMultiplier = 0,
        };
    }
}
