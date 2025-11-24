// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using System.Collections.Generic;
using System.Linq;
using Infohazard.Core;
using UnityEngine;

namespace Infohazard.HyperNav.Settings {
    [CreateAssetMenu(fileName = "HyperNavSettings", menuName = "HyperNav/Project Settings", order = 100)]
    public class HyperNavSettings : SingletonAsset<HyperNavSettings> {
        public override string ResourceFolderPath => "Infohazard.Core.Data/Resources";
        public override string ResourcePath => "HyperNavSettings.asset";

        public const int LayerCount = 32;

        [SerializeField]
        private string[] _navLayerNames = new string[LayerCount];

        private static readonly string[] DefaultLayerNames = Enumerable.Range(0, 32).Select(i => $"Layer {i}").ToArray();

        public string GetLayerName(int layer) {
            if (layer < 0 || layer >= _navLayerNames.Length) {
                throw new ArgumentOutOfRangeException(nameof(layer));
            }

            string s = _navLayerNames[layer];
            if (string.IsNullOrEmpty(s)) {
                return DefaultLayerNames[layer];
            }

            return s;
        }

        /// <summary>
        /// Try to get the layer index corresponding to the given name.
        /// </summary>
        /// <param name="layerName">Layer name to search for.</param>
        /// <param name="layerIndex">Layer index with that name, or -1 if not found.</param>
        /// <returns>Whether the layer with that name exists.</returns>
        public bool TryGetLayerIndex(string layerName, out int layerIndex) {
            if (string.IsNullOrEmpty(layerName)) {
                Debug.LogError("Layer name cannot be null or empty.");
                layerIndex = -1;
                return false;
            }

            layerIndex = Array.IndexOf(_navLayerNames, layerName);
            return layerIndex >= 0;
        }

        public static class PropNames {
            public const string NavLayerNames = nameof(_navLayerNames);
        }
    }
}
