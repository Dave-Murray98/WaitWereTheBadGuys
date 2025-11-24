// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#if UNITY_EDITOR

#region

using UnityEditor;
using UnityEngine;

#endregion

namespace NWH.NUI
{
    /// <summary>
    /// Custom NWH.NUI property drawer with links to documentation.
    /// </summary>
    public class NUIPropertyDrawer : PropertyDrawer
    {
        protected NUIDrawer drawer = new();


        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return drawer.GetHeight(NUIDrawer.GenerateKey(property));
        }


        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            OnNUI(position, property, label);
        }


        public virtual string GetDocumentationBaseURL()
        {
            return "http://nwhvehiclephysics.com";
        }


        public virtual bool OnNUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (drawer == null)
            {
                drawer = new NUIDrawer();
            }

            drawer.documentationBaseURL = GetDocumentationBaseURL();
            drawer.BeginProperty(position, property, label);

            string name = property.FindPropertyRelative("name")?.stringValue;
            if (string.IsNullOrEmpty(name))
            {
                name = property.displayName;
            }

            if (!drawer.Header(name))
            {
                drawer.EndProperty();
                return false;
            }

            return true;
        }
    }
}

#endif