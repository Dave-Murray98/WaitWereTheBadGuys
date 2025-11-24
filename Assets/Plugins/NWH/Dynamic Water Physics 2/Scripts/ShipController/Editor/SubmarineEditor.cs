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

#endregion

namespace NWH.DWP2.WaterObjects
{
    /// <summary>
    /// Custom inspector for Submarine.
    /// </summary>
    [CustomEditor(typeof(Submarine))]
    [CanEditMultipleObjects]
    public class SubmarineEditor : DWP2NUIEditor
    {
        /// <summary>
        /// Draws custom inspector GUI for Submarine.
        /// </summary>
        public override bool OnInspectorNUI()
        {
            if (!base.OnInspectorNUI())
            {
                return false;
            }

            drawer.Info("To make submarine surface faster lower the Rigidbody mass.\n " +
                        "To make submarine dive faster increase the 'maxMassFactor'.");
            drawer.Field("maxBallastMass");
            drawer.Field("ballastChangeSpeed");
            drawer.Info("Too low 'maxBallastMass' value will prevent the submarine from diving.");
            drawer.EndSubsection();

            drawer.BeginSubsection("Keep Horizontal");
            drawer.Field("keepHorizontal");
            drawer.Field("keepHorizontalSensitivity");
            drawer.Field("maxMassOffset");
            drawer.Info("Max Mass Offset [m] should not be larger than ~1/3 of the length of the submarine.");
            drawer.EndSubsection();

            drawer.EndEditor(this);
            return true;
        }
    }
}

#endif