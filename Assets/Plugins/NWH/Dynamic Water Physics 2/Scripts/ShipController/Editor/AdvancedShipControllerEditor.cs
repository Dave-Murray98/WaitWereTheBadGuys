// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#if UNITY_EDITOR

#region

using NWH.NUI;
using NWH.DWP2.ShipController;
using UnityEditor;
using UnityEngine;

#endregion

namespace NWH.DWP2.WaterObjects
{
    /// <summary>
    /// Custom inspector for AdvancedShipController.
    /// </summary>
    [CustomEditor(typeof(AdvancedShipController))]
    [CanEditMultipleObjects]
    public class AdvancedShipControllerEditor : DWP2NUIEditor
    {
        /// <summary>
        /// Draws custom inspector GUI for AdvancedShipController.
        /// </summary>
        public override bool OnInspectorNUI()
        {
            if (!base.OnInspectorNUI())
            {
                return false;
            }

            // Draw logo texture
            Rect logoRect = drawer.positionRect;
            logoRect.height = 60f;
            drawer.DrawEditorTexture(logoRect, "Dynamic Water Physics 2/Logos/AdvancedShipControllerLogo");
            drawer.AdvancePosition(logoRect.height);

            // Draw the menu
            int categoryTab = drawer.HorizontalToolbar("categoryTab",
                                                       new[]
                                                       {
                                                           "Input", "Engines", "Rudders", "Thrusters", "Settings",
                                                       });

            switch (categoryTab)
            {
                case 0:
                    DrawInput();
                    break;
                case 1:
                    DrawEngines();
                    break;
                case 2:
                    DrawRudders();
                    break;
                case 3:
                    DrawThrusters();
                    break;
                case 4:
                    DrawSettings();
                    break;
                default:
                    DrawInput();
                    break;
            }

            drawer.EndEditor(this);
            return true;
        }


        private void DrawInput()
        {
            drawer.Property("input");
        }


        private void DrawEngines()
        {
            drawer.ReorderableList("engines");
        }


        private void DrawThrusters()
        {
            drawer.ReorderableList("thrusters");
        }


        private void DrawRudders()
        {
            drawer.ReorderableList("rudders");
        }


        private void DrawSettings()
        {
            drawer.BeginSubsection("Settings");
            drawer.Field("dropAnchorWhenInactive");
            drawer.Field("weighAnchorWhenActive");
            drawer.EndSubsection();

            drawer.BeginSubsection("Stabilization");
            if (drawer.Field("stabilizeRoll").boolValue)
            {
                drawer.Field("rollStabilizationMaxTorque");
            }

            if (drawer.Field("stabilizePitch").boolValue)
            {
                drawer.Field("pitchStabilizationMaxTorque");
            }

            drawer.EndSubsection();
        }


        public override bool UseDefaultMargins()
        {
            return false;
        }
    }
}

#endif