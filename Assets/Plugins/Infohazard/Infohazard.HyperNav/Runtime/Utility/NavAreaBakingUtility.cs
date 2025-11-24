using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Infohazard.HyperNav {
    /// <summary>
    /// Utility methods used for baking NavAreas in both the editor and at runtime.
    /// </summary>
    public static class NavAreaBakingUtility {
        /// <summary>
        /// Get the set of instance IDs for all static colliders relevant to baking.
        /// In the editor, if editing a prefab, only colliders in the prefab are considered.
        /// Otherwise, all colliders in the scene are considered.
        /// </summary>
        /// <returns>A set of instance IDs for all static colliders relevant to baking.</returns>
        public static NativeHashSet<int> GetStaticCollidersForBaking() {
            Collider[] allColliders;

#if UNITY_EDITOR
            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null) {
                allColliders = prefabStage.prefabContentsRoot.GetComponentsInChildren<Collider>(true);
            } else {
                allColliders =
                    Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            }
#else
            allColliders = Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#endif


            NativeHashSet<int> colliderStaticDict = new(allColliders.Length, Allocator.Persistent);
            foreach (Collider collider in allColliders) {
                if (collider.gameObject.isStatic) {
                    colliderStaticDict.Add(collider.GetInstanceID());
                }
            }

            return colliderStaticDict;
        }
    }
}
