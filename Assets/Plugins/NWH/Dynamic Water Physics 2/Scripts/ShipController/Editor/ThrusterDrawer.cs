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
    /// Property drawer for Thruster.
    /// </summary>
    [CustomPropertyDrawer(typeof(Thruster))]
    public class ThrusterDrawer : DWP2NUIPropertyDrawer
    {
        /// <summary>
        /// Draws property drawer for Thruster.
        /// </summary>
        public override bool OnNUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (!base.OnNUI(position, property, label))
            {
                return false;
            }

            drawer.Field("name");
            drawer.Field("position");
            drawer.Field("maxThrust");
            drawer.Field("spinUpSpeed");
            drawer.Field("thrusterPosition");

            drawer.BeginSubsection("Propeller");
            drawer.Field("propellerTransform");
            drawer.Field("propellerRotationDirection");
            drawer.Field("propellerRotationSpeed");
            drawer.EndSubsection();

            drawer.EndProperty();
            return true;
        }
    }
}

#endif