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
using NWH.DWP2.WaterObjects;
using UnityEditor;

#endregion

namespace NWH.DWP2
{
    /// <summary>
    /// Custom inspector for WaterObjectMaterial.
    /// </summary>
    [CustomEditor(typeof(WaterObjectMaterial))]
    [CanEditMultipleObjects]
    public class WaterObjectMaterialEditor : DWP2NUIEditor
    {
        /// <summary>
        /// Draws custom inspector GUI for WaterObjectMaterial.
        /// </summary>
        public override bool OnInspectorNUI()
        {
            if (!base.OnInspectorNUI())
            {
                return false;
            }

            drawer.Field("density");

            drawer.EndEditor(this);
            return true;
        }
    }
}

#endif