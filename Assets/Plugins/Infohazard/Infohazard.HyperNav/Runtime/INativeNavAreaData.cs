// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using Infohazard.Core;
using Infohazard.HyperNav.Jobs.Utility;
using Unity.Mathematics;
using UnityEngine;

namespace Infohazard.HyperNav {
    public interface INativeNavAreaData {
        public bool IsCreated { get; }

        public int InternalLinkCount { get; }

        public int ExternalLinkCount { get; }

        public int RegionCount { get; }

        public int Layer { get; }

        public float4x4 Transform { get; }

        public NativeNavExternalLinkData GetExternalLinkData(int index);

        public SerializableRange GetExternalLinkRange(int index);

        public NativeBounds GetRegionBounds(int index);

        public void UpdateExternalLinksInPlace(UnsafeArray<NativeNavExternalLinkData> links,
            ReadOnlySpan<SerializableRange> ranges);
    }
}
