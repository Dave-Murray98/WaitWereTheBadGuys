// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#if UNITY_EDITOR

#region

using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

#endregion

namespace NWH.NUI
{
    public static class EditorCache
    {
        // Workaround to get around the issue of .NET 2.0, asmdef and dynamic.
        private static readonly Dictionary<string, float> heightCache = new();

        private static readonly Dictionary<string, ReorderableList> reorderableListCache = new();

        private static readonly Dictionary<string, bool>      guiWasEnabledCache = new();
        private static readonly Dictionary<string, NUIEditor> nuiEditorCache     = new();
        private static readonly Dictionary<string, bool>      isExpandedCache    = new();
        private static readonly Dictionary<string, int>       tabIndexCache      = new();
        private static readonly Dictionary<string, Texture2D> texture2DCache     = new();

        private static readonly Dictionary<string, SerializedProperty> serializedPropertyCache = new();

        // Auto-clear cache on assembly reload to prevent unbounded growth
        [UnityEditor.Callbacks.DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            ClearAll();
        }


        public static bool GetHeightCacheValue(string key, ref float value)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            return heightCache.TryGetValue(key, out value);
        }


        public static bool GetReorderableListCacheValue(string key, ref ReorderableList value)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            return reorderableListCache.TryGetValue(key, out value);
        }


        public static bool GetGuiWasEnabledCValue(string key, ref bool value)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            return guiWasEnabledCache.TryGetValue(key, out value);
        }


        public static bool GetNUIEditorCacheValue(string key, ref NUIEditor value)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            return nuiEditorCache.TryGetValue(key, out value);
        }


        public static bool GetIsExpandedCacheValue(string key, ref bool value)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            return isExpandedCache.TryGetValue(key, out value);
        }


        public static bool GetTabIndexCacheValue(string key, ref int value)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            return tabIndexCache.TryGetValue(key, out value);
        }


        public static bool GetTexture2DCacheValue(string key, ref Texture2D value)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            return texture2DCache.TryGetValue(key, out value);
        }


        public static bool GetSerializedPropertyCacheValue(string key, ref SerializedProperty value)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            return serializedPropertyCache.TryGetValue(key, out value);
        }


        public static bool SetHeightCacheValue(string key, float value)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            heightCache[key] = value;
            return true;
        }


        public static bool SetReorderableListCacheValue(string key, ReorderableList value)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            reorderableListCache[key] = value;
            return true;
        }


        public static bool SetGuiWasEnabledCacheValue(string key, bool value)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            guiWasEnabledCache[key] = value;
            return true;
        }


        public static bool SetNUIEditorCacheValue(string key, NUIEditor value)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            nuiEditorCache[key] = value;
            return true;
        }


        public static bool SetIsExpandedCacheValue(string key, bool value)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            isExpandedCache[key] = value;
            return true;
        }


        public static bool SetTabIndexCacheValue(string key, int value)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            tabIndexCache[key] = value;
            return true;
        }


        public static bool SetTexture2DCacheValue(string key, Texture2D value)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            texture2DCache[key] = value;
            return true;
        }


        public static bool SetSerializedPropertyCacheValue(string key, SerializedProperty value)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            serializedPropertyCache[key] = value;
            return true;
        }


        /// <summary>
        /// Clears all cached data. Useful to prevent unbounded memory growth during long editor sessions.
        /// </summary>
        public static void ClearAll()
        {
            heightCache.Clear();
            reorderableListCache.Clear();
            guiWasEnabledCache.Clear();
            nuiEditorCache.Clear();
            isExpandedCache.Clear();
            tabIndexCache.Clear();
            texture2DCache.Clear();
            serializedPropertyCache.Clear();
        }


        // // Store data for each property as property drawer gets reused multiple times and local values overwritten
        // private static readonly Dictionary<string, dynamic> Cache = new Dictionary<string, dynamic>
        // {
        //     {"height", new Dictionary<string, float>()},
        //     {"ReorderableList", new Dictionary<string, ReorderableList>()},
        //     {"guiWasEnabled", new Dictionary<string, bool>()},
        //     {"NUIEditor", new Dictionary<string, NUIEditor>()},
        //     {"isExpanded", new Dictionary<string, bool>()},
        //     {"tabIndex", new Dictionary<string, int>()},
        //     {"Texture2D", new Dictionary<string, Texture2D>()},
        //     {"SerializedProperty", new Dictionary<string, SerializedProperty>()},
        // };

        // public static bool GetCachedValue<T>(string variableName, ref T value, string key)
        // {
        //     if (string.IsNullOrEmpty(key))
        //     {
        //         return false;
        //     }
        //
        //     if (!Cache.ContainsKey(variableName) || !Cache[variableName].ContainsKey(key))
        //     {
        //         return false;
        //     }
        //
        //     value = Cache[variableName][key];
        //     return true;
        // }
        //
        //
        // public static bool SetCachedValue<T>(string variableName, T value, string key)
        // {
        //     if (string.IsNullOrEmpty(key))
        //     {
        //         return false;
        //     }
        //
        //     if (Cache.ContainsKey(variableName))
        //     {
        //         if (!Cache[variableName].ContainsKey(key))
        //         {
        //             Cache[variableName].Add(key, value);
        //         }
        //         else
        //         {
        //             Cache[variableName][key] = value;
        //         }
        //
        //         return true;
        //     }
        //
        //     return false;
        // }
    }
}

#endif