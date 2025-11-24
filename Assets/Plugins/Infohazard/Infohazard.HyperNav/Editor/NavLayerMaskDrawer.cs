// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System.Linq;
using Infohazard.HyperNav.Settings;
using UnityEditor;
using UnityEngine;

namespace Infohazard.HyperNav.Editor {
    [CustomPropertyDrawer(typeof(NavLayerMask))]
    public class NavLayerMaskDrawer : PropertyDrawer {
        private static readonly string[] NavLayerNames = new string[HyperNavSettings.LayerCount];

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            HyperNavSettings settings = HyperNavSettings.Instance;
            if (settings) {
                for (int i = 0; i < NavLayerNames.Length; i++) {
                    NavLayerNames[i] = settings.GetLayerName(i);
                }
            }

            SerializedProperty index = property.FindPropertyRelative("_value");
            GUIContent subLabel = EditorGUI.BeginProperty(position, label, property);
            position = EditorGUI.PrefixLabel(position, subLabel);
            int newValue = EditorGUI.MaskField(position, GUIContent.none, (int)index.uintValue, NavLayerNames);
            index.uintValue = (uint)newValue;
            EditorGUI.EndProperty();
        }
    }
}
