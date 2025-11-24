// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System.Linq;
using Infohazard.HyperNav.Settings;
using UnityEditor;
using UnityEngine;

namespace Infohazard.HyperNav.Editor {
    [CustomPropertyDrawer(typeof(NavLayer))]
    public class NavLayerDrawer : PropertyDrawer {
        private static readonly GUIContent[] NavLayerNames = new GUIContent[HyperNavSettings.LayerCount];
        private static readonly int[] NavLayerValues = Enumerable.Range(0, HyperNavSettings.LayerCount).ToArray();

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            HyperNavSettings settings = HyperNavSettings.Instance;
            if (settings) {
                for (int i = 0; i < NavLayerNames.Length; i++) {
                    string layerName = settings.GetLayerName(i);
                    if (NavLayerNames[i] != null && NavLayerNames[i].text == layerName) continue;
                    NavLayerNames[i] = new GUIContent(settings.GetLayerName(i));
                }
            }

            SerializedProperty index = property.FindPropertyRelative("_index");
            EditorGUI.IntPopup(position, index, NavLayerNames, NavLayerValues, label);
        }
    }
}
