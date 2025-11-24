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

#endregion

namespace NWH.NUI
{
    /// <summary>
    /// Base custom editor class for NWH NUI (NWH User Interface) system.
    /// Provides common infrastructure for drawing foldable inspector sections with documentation links.
    /// </summary>
    [CanEditMultipleObjects]
    public class NUIEditor : Editor
    {
        /// <summary>
        /// NUIDrawer instance used to render inspector GUI.
        /// </summary>
        public NUIDrawer drawer = new();


        /// <summary>
        /// Unity callback to draw inspector GUI. Delegates to OnInspectorNUI.
        /// </summary>
        public override void OnInspectorGUI()
        {
            OnInspectorNUI();
        }


        /// <summary>
        /// Draws custom NUI inspector. Override this method in derived classes to add custom GUI.
        /// Initializes drawer and renders collapsible header.
        /// </summary>
        /// <returns>True if header is expanded and GUI should continue, false if collapsed</returns>
        public virtual bool OnInspectorNUI()
        {
            if (drawer == null)
            {
                drawer = new NUIDrawer();
            }

            drawer.documentationBaseURL = GetDocumentationBaseURL();

            drawer.BeginEditor(serializedObject);
            if (!drawer.Header(serializedObject.targetObject.GetType().Name))
            {
                drawer.EndEditor();
                return false;
            }

            return true;
        }


        /// <summary>
        /// Gets base URL for documentation links. Override in derived classes to specify package-specific docs.
        /// </summary>
        /// <returns>Base documentation URL</returns>
        public virtual string GetDocumentationBaseURL()
        {
            return "http://nwhvehiclephysics.com";
        }


        /// <summary>
        /// Disables default Unity inspector margins for custom NUI layout control.
        /// </summary>
        /// <returns>Always false to disable default margins</returns>
        public override bool UseDefaultMargins()
        {
            return false;
        }
    }
}

#endif