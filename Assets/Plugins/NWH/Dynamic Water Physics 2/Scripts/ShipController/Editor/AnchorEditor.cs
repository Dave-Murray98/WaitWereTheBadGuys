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
    /// Custom inspector for Anchor.
    /// </summary>
    [CustomEditor(typeof(Anchor))]
    [CanEditMultipleObjects]
    public class AnchorEditor : DWP2NUIEditor
    {
        /// <summary>
        /// Draws custom inspector GUI for Anchor.
        /// </summary>
        public override bool OnInspectorNUI()
        {
            if (!base.OnInspectorNUI())
            {
                return false;
            }

            Anchor anchor = (Anchor)target;

            if (Application.isPlaying)
            {
                drawer.Label($"Dropped: {anchor.Dropped}");
            }

            drawer.Field("dropOnStart");
            drawer.Field("forceCoefficient");
            drawer.Field("zeroForceRadius");
            drawer.Field("dragForce");
            drawer.Field("localAnchorPoint");

            drawer.EndEditor(this);
            return true;
        }
    }
}

#endif