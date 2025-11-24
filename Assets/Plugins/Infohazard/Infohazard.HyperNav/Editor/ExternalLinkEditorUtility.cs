// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Infohazard.HyperNav.Editor {
    /// <summary>
    /// Utilities for generating the external links of <see cref="NavVolume"/>s.
    /// </summary>
    public static class ExternalLinkEditorUtility {
        /// <summary>
        /// Clear the external links for a volume.
        /// </summary>
        public static void ClearExternalLinks(INavArea volume) {
            Undo.RecordObject((Object)volume, "Clear External Links");
            volume.ClearExternalLinks();
        }

        /// <summary>
        /// Get all loaded <see cref="NavAreaBase"/>s that are relevant to link baking.
        /// If in prefab mode, only returns areas in the prefab.
        /// </summary>
        /// <returns>The loaded areas.</returns>
        public static NavAreaBase[] GetAllLoadedAreas() {
            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            return prefabStage != null
                ? prefabStage.prefabContentsRoot.GetComponentsInChildren<NavAreaBase>(true)
                : Object.FindObjectsOfType<NavAreaBase>();
        }

        /// <summary>
        /// Generate the external links for all loaded <see cref="INavArea"/>s.
        /// </summary>
        public static void GenerateAllExternalLinks() {
            NavAreaBase[] areas = GetAllLoadedAreas();

            GenerateExternalLinks(areas);
        }

        /// <summary>
        /// Generate the external links for a specific <see cref="INavArea"/>.
        /// </summary>
        /// <param name="area"></param>
        public static void GenerateExternalLinks(INavArea area) {
            GenerateExternalLinks(new[] { area });
        }

        /// <summary>
        /// Generate the external links for a list of <see cref="INavArea"/>s.
        /// </summary>
        /// <param name="areas"></param>
        public static void GenerateExternalLinks(IReadOnlyList<INavArea> areas) {
            NavAreaBase[] allAreas = GetAllLoadedAreas();

            foreach (NavAreaBase other in allAreas) {
                other.Register();
            }

            NavVolume.UpdateAllTransforms();
            NavSurface.UpdateAllTransforms();

            List<INavArea> areasToGenerate = new();
            for (int i = 0; i < areas.Count; i++) {
                INavArea area = areas[i];
                if ((Object)area.Data == null || !area.Data.IsBaked) continue;
                areasToGenerate.Add(area);
            }

            if (areasToGenerate.Count == 0) {
                Debug.LogWarning("No areas to generate external links for.");
                return;
            }

            GenerateExternalLinksAsync(areasToGenerate).Forget();
        }

        private static async UniTask GenerateExternalLinksAsync(IReadOnlyList<INavArea> areas) {
            bool hasUnloadedScenes =
                areas.Any(v => v.ExternalLinks.Any(l => !string.IsNullOrEmpty(l.ConnectedScenePath) &&
                                                        !SceneManager.GetSceneByPath(l.ConnectedScenePath).isLoaded));

            bool keepLinks = false;
            if (hasUnloadedScenes) {
                if (EditorUtility.DisplayDialog("Unloaded Scenes Detected",
                    "Some external links connect to scenes that are not loaded. " +
                    "Do you want to keep these links?", "Yes", "No")) {
                    keepLinks = true;
                }
            }

            EditorUtility.DisplayProgressBar("Baking External Links", "Waiting for job to complete...", 0f);
            try {
                await NavAreaExternalLinkUpdate.GenerateExternalLinks(areas, true, keepLinks);
            } finally {
                EditorUtility.ClearProgressBar();
            }

            foreach (INavArea volume in areas) {
                EditorUtility.SetDirty((Object)volume);
                if (volume.Transform.gameObject.scene.IsValid()) {
                    EditorSceneManager.MarkSceneDirty(volume.Transform.gameObject.scene);
                }
            }

            AssetDatabase.SaveAssets();
        }
    }
}
