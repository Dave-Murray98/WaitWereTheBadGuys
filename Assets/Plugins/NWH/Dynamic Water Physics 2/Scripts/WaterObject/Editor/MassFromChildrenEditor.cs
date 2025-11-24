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
    /// Custom inspector for MassFromChildren.
    /// </summary>
    [CustomEditor(typeof(MassFromChildren))]
    [CanEditMultipleObjects]
    public class MassFromChildrenEditor : DWP2NUIEditor
    {
        private MassFromChildren _massFromChildren;


        /// <summary>
        /// Draws custom inspector GUI for MassFromChildren.
        /// </summary>
        public override bool OnInspectorNUI()
        {
            _massFromChildren = (MassFromChildren)target;

            if (!base.OnInspectorNUI() || _massFromChildren == null)
            {
                return false;
            }

            drawer.Info("Sums mass of all 'MassFromVolume's attached to this and child objects.");

            if (drawer.Button("Calculate Mass From Children"))
            {
                _massFromChildren.Calculate();
            }

            drawer.EndEditor(this);
            return true;
        }
    }
}

#endif