// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using UnityEngine;

#endregion

namespace NWH.NUI
{
    /// <summary>
    /// Global configuration settings for NWH NUI (NWH User Interface) editor system.
    /// Defines layout constants and color scheme used across all NWH custom editors.
    /// </summary>
    public static class NUISettings
    {
        /// <summary>
        /// Standard height for inspector fields in pixels.
        /// </summary>
        public const float  fieldHeight   = 23f;

        /// <summary>
        /// Vertical spacing between inspector fields in pixels.
        /// </summary>
        public const float  fieldSpacing  = 3f;

        /// <summary>
        /// Resources folder path for NUI assets.
        /// </summary>
        public const string resourcesPath = "NUI/";

        /// <summary>
        /// Margin around text elements in pixels.
        /// </summary>
        public const float  textMargin    = 2f;

        /// <summary>
        /// Header background color for ScriptableObject editors.
        /// </summary>
        public static Color scriptableObjectHeaderColor = new Color32(220, 122, 32,  255);

        /// <summary>
        /// Header background color for MonoBehaviour editors.
        /// </summary>
        public static Color editorHeaderColor           = new Color32(20,  125, 211, 255);

        /// <summary>
        /// Header background color for property drawers.
        /// </summary>
        public static Color propertyHeaderColor         = new Color32(78,  152, 213, 255);

        /// <summary>
        /// UI tint color indicating disabled state.
        /// </summary>
        public static Color disabledColor  = new(1f, 0.5f, 0.5f);

        /// <summary>
        /// UI tint color indicating enabled state.
        /// </summary>
        public static Color enabledColor   = new(0.5f, 1f, 0.5f);

        /// <summary>
        /// Light blue accent color for UI elements.
        /// </summary>
        public static Color lightBlueColor = new Color32(70,  170, 220, 255);

        /// <summary>
        /// Light grey color for secondary UI elements.
        /// </summary>
        public static Color lightGreyColor = new Color32(192, 192, 192, 255);
    }
}