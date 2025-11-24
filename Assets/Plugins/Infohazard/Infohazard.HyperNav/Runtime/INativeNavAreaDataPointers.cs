// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using Infohazard.HyperNav.Jobs.Utility;
using Unity.Collections;

namespace Infohazard.HyperNav {
    public interface INativeNavAreaDataPointers {
        public NativeArray<NativeNavExternalLinkData> ExternalLinksData { get; set; }
    }
}
