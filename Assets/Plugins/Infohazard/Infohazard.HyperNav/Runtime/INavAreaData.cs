// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System.Collections.Generic;
using Infohazard.Core;
using UnityEngine;

namespace Infohazard.HyperNav {
    public interface INavAreaData {

        public ulong Version { get; }

        public bool IsBaked { get; }

        public int RegionCount { get; }

        public void Clear();
    }

    public interface INavAreaData<TNativeData, TPointers> : INavAreaData where TNativeData : INativeNavAreaData {

        public bool ToNativeData(INavArea area, out TNativeData data, out TPointers pointers);

        public void UpdateFromNativeData(in TNativeData data);
    }
}
