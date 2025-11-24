// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using Infohazard.HyperNav.Settings;
using UnityEditor;
using UnityEngine;

namespace Infohazard.HyperNav.Editor {
    [CustomEditor(typeof(HyperNavSettings))]
    public class HyperNavSettingsEditor : UnityEditor.Editor {
        [MenuItem("Tools/Infohazard/HyperNav Settings")]
        public static void ShowSettings() {
            HyperNavSettings navSettings = HyperNavSettings.Instance;

            if (!navSettings) {
                if (Application.isPlaying) {
                    EditorUtility.DisplayDialog("Cannot Create Settings",
                                                "Cannot create HyperNavSettings asset while in play mode.", "Ok");
                } else {
                    Debug.LogError("HyperNavSettings is null for an unknown reason. Try creating the asset yourself under Resources.");
                }

                return;
            }

            EditorUtility.OpenPropertyEditor(navSettings);
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();

            SerializedProperty layerNamesProp = serializedObject.FindProperty(HyperNavSettings.PropNames.NavLayerNames);
            layerNamesProp.arraySize = HyperNavSettings.LayerCount;

            layerNamesProp.isExpanded = EditorGUILayout.Foldout(layerNamesProp.isExpanded, "Nav Layer Names");
            if (layerNamesProp.isExpanded) {
                for (int i = 0; i < HyperNavSettings.LayerCount; i++) {
                    SerializedProperty layerProp = layerNamesProp.GetArrayElementAtIndex(i);
                    EditorGUILayout.PropertyField(layerProp);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
