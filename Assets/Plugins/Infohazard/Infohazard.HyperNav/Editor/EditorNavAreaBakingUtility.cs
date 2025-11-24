// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Infohazard.Core.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Infohazard.HyperNav.Editor {
    /// <summary>
    /// Manages multiple <see cref="NavAreaBaseBakeHandler"/>s for baking <see cref="NavAreaBase"/>s.
    /// </summary>
    public static class EditorNavAreaBakingUtility {
        /// <summary>
        /// The coroutine for each volume currently being baked.
        /// </summary>
        public static readonly Dictionary<NavAreaBase, NavAreaBaseBakeHandler> BakeHandlers = new();

        /// <summary>
        /// Invoked when the bake progress for a NavVolume changes.
        /// </summary>
        public static event Action<NavAreaBase> BakeProgressUpdated;

        #region Public Methods

        /// <summary>
        /// Get the NavVolumeData for a given volume, or create and save the object if it doesn't exist yet.
        /// </summary>
        /// <param name="area">The NavAreaBase component.</param>
        public static void GetOrCreateData(NavAreaBase area) {
            // If volume already has a valid data object just return it.
            Object curData = (Object) ((INavArea) area).Data;
            if (curData != null) {
                // Ensure that two areas do not share the same data, such as if an area is duplicated.
                string dataName = $"{area.GetType().Name}_{area.InstanceID}";
                if (curData.name == dataName) {
                    return;
                }
            }

            SerializedObject serializedObject = new(area);
            SerializedProperty dataProp = serializedObject.FindProperty(NavAreaBase.PropNames.Data);

            Type areaType = area.GetType();
            string name = $"{areaType.Name}_{area.InstanceID}";

            PropertyInfo propertyInfo = areaType.GetProperty(nameof(NavVolume.Data));

            // Create the new data object.
            ScriptableObject data = ScriptableObject.CreateInstance(propertyInfo!.PropertyType);
            data.name = name;

            // Set the volume's Data reference.
            dataProp.objectReferenceValue = data;
            serializedObject.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(area);

            // Save it in a folder relative to the GameObject path.
            // If in prefab at path Assets/Prefabs/Prefab1, volume data path is Assets/Prefabs/HyperNavVolume_XXXXX.
            // If in scene at path Assets/Scenes/Level1, volume data path is Assets/Scenes/Level1/HyperNavVolume_XXXXX.
            string saveFolder = GetFolderForAreaData(area.gameObject);
            if (string.IsNullOrEmpty(saveFolder)) {
                Debug.LogError($"Could not find folder to save volume for object {area.gameObject}.");
                return;
            }

            // Ensure folder exists.
            if (!AssetDatabase.IsValidFolder(saveFolder)) {
                int lastSlash = saveFolder.LastIndexOf('/');
                string parentFolder = saveFolder.Substring(0, lastSlash);
                string folderName = saveFolder.Substring(lastSlash + 1);
                AssetDatabase.CreateFolder(parentFolder, folderName);
            }

            // Save the asset.
            string assetPath = $"{saveFolder}/{name}.asset";
            AssetDatabase.CreateAsset(data, assetPath);
        }

        private static string GetFolderForAreaData(GameObject gameObject) {
            // First check for prefab instance.
            string path = null;
            GameObject prefab = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
            if (prefab != null) {
                path = AssetDatabase.GetAssetPath(prefab);
            }

            // If not a prefab instance, check if a prefab stage.
            if (string.IsNullOrEmpty(path)) {
                PrefabStage stage = PrefabStageUtility.GetPrefabStage(gameObject);
                path = stage ? stage.assetPath : null;
            }

            // If a prefab or stage, get the containing folder.
            if (!string.IsNullOrEmpty(path)) {
                return path.Substring(0, path.LastIndexOf('/'));
            }

            // Lastly, check if in a scene.
            path = gameObject.scene.path;
            if (!string.IsNullOrEmpty(path)) {
                return path.Substring(0, path.LastIndexOf('.'));
            }

            return null;
        }

        /// <summary>
        /// Bake the NavVolumeData for a given volume.
        /// </summary>
        /// <param name="area">The area to bake.</param>
        /// <param name="sanityChecks">If true, run sanity checks after each step.</param>
        /// <param name="profile">If true, run the profiler during baking.</param>
        /// <param name="dependency">Optional task to wait for before baking.</param>
        /// <returns>A completion source that will be set when the baking is done.</returns>
        public static UniTaskCompletionSource BakeDataWithCompletionSource(
            NavAreaBase area, bool sanityChecks = false, bool profile = false, UniTask? dependency = null) {

            UniTaskCompletionSource completionSource = new();
            BakeDataWithCompletionSource(area, completionSource, sanityChecks, profile, dependency).Forget();
            return completionSource;
        }

        private static async UniTask BakeDataWithCompletionSource(
            NavAreaBase area, UniTaskCompletionSource completionSource, bool sanityChecks = false, bool profile = false,
            UniTask? dependency = null) {

            try {
                await BakeDataAsync(area, sanityChecks, profile, dependency);
                completionSource.TrySetResult();
            } catch (OperationCanceledException) {
                completionSource.TrySetCanceled();
            } catch (Exception e) {
                completionSource.TrySetException(e);
            }
        }

        /// <summary>
        /// Bake the NavVolumeData for a given volume.
        /// </summary>
        /// <param name="area">The area to bake.</param>
        /// <param name="sanityChecks">If true, run sanity checks after each step.</param>
        /// <param name="profile">If true, run the profiler during baking.</param>
        /// <param name="dependency">Optional task to wait for before baking.</param>
        public static async UniTask BakeDataAsync(NavAreaBase area, bool sanityChecks = false, bool profile = false,
                                                  UniTask? dependency = null) {
            NavAreaBaseBakeHandler handler = area switch {
                NavVolume volume => new NavVolumeBakeHandler(volume, sanityChecks, true),
                NavSurface surface => new NavSurfaceBakeHandler(surface, sanityChecks, true),
                _ => throw new ArgumentException("Unsupported NavAreaBase type for baking.")
            };

            handler.ProgressUpdated += () => BakeProgressUpdated?.Invoke(area);

            BakeHandlers[area] = handler;
            try {
                if (dependency.HasValue) {
                    try {
                        await dependency.Value;
                    } catch {
                        // ignored
                    }
                }

                if (profile) {
                    CoreEditorUtility.SetProfilerWindowRecording(true);
                }

                await handler.RunAsync();

                if (profile) {
                    CoreEditorUtility.SetProfilerWindowRecording(false);
                }
            } finally {
                BakeHandlers.Remove(area);
                BakeProgressUpdated?.Invoke(area);
            }
        }

        /// <summary>
        /// Cancel an actively baking volume and clear out its data.
        /// </summary>
        /// <param name="area">The volume being baked.</param>
        public static void CancelBake(NavAreaBase area) {
            if (!BakeHandlers.TryGetValue(area, out NavAreaBaseBakeHandler handler)) return;

            handler.Cancel();
            BakeHandlers.Remove(area);
            BakeProgressUpdated?.Invoke(area);

            ClearData(area);
        }

        /// <summary>
        /// Get the bake progress of a volume.
        /// </summary>
        /// <param name="area">The volume to check.</param>
        /// <param name="progress">The current bake progress for the volume.</param>
        /// <returns>If the volume is currently being baked.</returns>
        public static bool TryGetBakeProgress(NavAreaBase area, out NavAreaBakeProgress progress) {
            if (!BakeHandlers.TryGetValue(area, out NavAreaBaseBakeHandler handler)) {
                progress = default;
                return false;
            }

            progress = handler.Progress;
            return true;
        }

        /// <summary>
        /// Clear out the baked data of a volume (does not destroy or un-assign the actual data object).
        /// </summary>
        /// <param name="area"></param>
        public static void ClearData(NavAreaBase area) {
            Undo.RecordObject(area, "Clear HyperNav Data");
            INavArea navArea = area;
            navArea.Data.Clear();

            area.ClearExternalLinks();
            area.PreviewMesh = null;
            area.Deregister(true);
            EditorUtility.SetDirty((Object) navArea.Data);
            AssetDatabase.SaveAssets();
        }

        #endregion
    }
}
