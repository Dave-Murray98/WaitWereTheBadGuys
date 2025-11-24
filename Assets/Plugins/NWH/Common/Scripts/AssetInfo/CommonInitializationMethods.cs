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
using UnityEditor.Build;
using UnityEngine;

#endregion

namespace NWH.Common.AssetInfo
{
    /// <summary>
    /// Base class providing editor initialization utilities for NWH packages.
    /// Handles scripting defines and welcome window display on package import.
    /// </summary>
    public class CommonInitializationMethods
    {
        private static Queue<AssetInfo> _welcomeWindowQueue = new Queue<AssetInfo>();
        private static bool _queueProcessScheduled = false;
        /// <summary>
        /// Adds a scripting define symbol to the current build target if not already present.
        /// </summary>
        /// <param name="symbol">Scripting define symbol to add (e.g., "NWH_NVP2")</param>
        protected static void AddDefines(string symbol)
        {
            NamedBuildTarget namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            string currentSymbols =
                PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);
            string newSymbols = string.Join(";", new HashSet<string>(currentSymbols.Split(';')) { symbol, });
            if (currentSymbols != newSymbols)
            {
                PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, newSymbols);
            }
        }


        /// <summary>
        /// Displays welcome window for specified asset on first import or version update.
        /// Uses EditorPrefs to track display state per version. If NWH_ALWAYS_SHOW_WELCOME_WINDOW
        /// is defined, always displays window regardless of EditorPrefs state.
        /// Windows are queued and displayed sequentially to support multiple packages.
        /// Only shows on editor start, not on script reload (uses SessionState).
        /// </summary>
        /// <param name="assetName">Display name of the asset (must match AssetInfo.assetName)</param>
        protected static void ShowWelcomeWindow(string assetName)
        {
            // Skip if welcome windows have already been processed this editor session (prevents script reload triggers)
            if (SessionState.GetBool("NWH_WelcomeWindows_Processed", false))
            {
                return;
            }

            if (!GetAssetInfo(assetName, out AssetInfo assetInfo))
            {
                return;
            }

#if NWH_ALWAYS_SHOW_WELCOME_WINDOW
            _welcomeWindowQueue.Enqueue(assetInfo);
#else
            string key = $"{assetInfo.assetName}_{assetInfo.version}_WW"; // Welcome Window key
            if (EditorPrefs.GetBool(key, false) == false)
            {
                EditorPrefs.SetBool(key, true);
                _welcomeWindowQueue.Enqueue(assetInfo);
            }
#endif

            // Schedule queue processing after all InitializeOnLoadMethod callbacks complete
            if (!_queueProcessScheduled)
            {
                _queueProcessScheduled = true;
                EditorApplication.delayCall += ProcessWelcomeWindowQueue;
            }
        }


        /// <summary>
        /// Processes queued welcome windows. Marks session as processed and shows first window.
        /// Called via EditorApplication.delayCall after all InitializeOnLoadMethod callbacks complete.
        /// </summary>
        private static void ProcessWelcomeWindowQueue()
        {
            // Mark as processed to prevent script reload from showing windows again
            SessionState.SetBool("NWH_WelcomeWindows_Processed", true);

            // Show first window in queue
            ShowNextWelcomeWindow();
        }


        /// <summary>
        /// Shows next welcome window from queue. Called when previous window closes.
        /// </summary>
        internal static void ShowNextWelcomeWindow()
        {
            if (_welcomeWindowQueue.Count > 0)
            {
                AssetInfo assetInfo = _welcomeWindowQueue.Dequeue();
                ConstructWelcomeWindow(assetInfo);
            }
        }


        /// <summary>
        /// Creates and displays WelcomeMessageWindow with specified AssetInfo.
        /// Uses CreateInstance to allow multiple independent window instances.
        /// </summary>
        /// <param name="assetInfo">AssetInfo containing package metadata and URLs</param>
        private static void ConstructWelcomeWindow(AssetInfo assetInfo)
        {
            WelcomeMessageWindow window = ScriptableObject.CreateInstance<WelcomeMessageWindow>();
            window.assetInfo = assetInfo;
            window.titleContent = new GUIContent(assetInfo.assetName);
            //window.onCloseCallback      = ShowNextWelcomeWindow;
            window.Show();
        }


        /// <summary>
        /// Locates and loads AssetInfo asset by name using AssetDatabase search.
        /// Works with packages in both Assets/ and Packages/ folders.
        /// </summary>
        /// <param name="assetName">Asset name to search for</param>
        /// <param name="assetInfo">Loaded AssetInfo if found, null otherwise</param>
        /// <returns>True if AssetInfo was found and loaded successfully</returns>
        private static bool GetAssetInfo(string assetName, out AssetInfo assetInfo)
        {
            string searchFilter = $"{assetName} AssetInfo t:AssetInfo";
            string[] guids = AssetDatabase.FindAssets(searchFilter);

            if (guids.Length == 0)
            {
                Debug.LogWarning($"Could not find AssetInfo for '{assetName}'");
                assetInfo = null;
                return false;
            }

            string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            assetInfo = AssetDatabase.LoadAssetAtPath<AssetInfo>(assetPath);

            if (assetInfo == null)
            {
                Debug.LogWarning($"Could not load AssetInfo at path {assetPath}");
                return false;
            }

            return true;
        }
    }
}
#endif