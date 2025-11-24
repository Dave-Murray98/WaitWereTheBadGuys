// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;

namespace Infohazard.HyperNav {
    /// <summary>
    /// Type of NavArea.
    /// </summary>
    [Flags]
    public enum NavAreaTypes {
        Nothing = 0,
        Volume = 1 << 0,
        Surface = 1 << 1,

        All = Volume | Surface,
    }
}
