// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using UnityEngine;

namespace Infohazard.HyperNav {
    public struct ChangeNavAreaData : IDisposable {
        /// <summary>
        /// Number of places that are modifying volume data.
        /// </summary>
        public static int ChangingCount { get; private set; }

        /// <summary>
        /// Event that is invoked immediately before active volume data changes.
        /// </summary>
        public static event Action DataChanging;

        /// <summary>
        /// Event that is invoked immediately after active volume data changes.
        /// </summary>
        public static event Action DataChanged;

        public static ChangeNavAreaData Instance() {
            ChangingCount++;

            if (ChangingCount == 1) {
                DataChanging?.Invoke();
            }

            return new ChangeNavAreaData();
        }

        public void Dispose() {
            if (ChangingCount < 1) {
                Debug.LogError("Over-disposing ChangeNavAreaData.");
                return;
            }

            ChangingCount--;

            if (ChangingCount == 0) {
                DataChanged?.Invoke();
            }
        }
    }
}