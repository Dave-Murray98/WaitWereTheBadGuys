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
using UnityEditor;

#endregion

namespace NWH.Common.Demo
{
    /// <summary>
    /// Custom inspector for DragObject demo component.
    /// </summary>
    [CustomEditor(typeof(DragObject))]
    [CanEditMultipleObjects]
    public class DragObjectEditor : NUIEditor
    {
        /// <summary>
        /// Draws custom inspector GUI for DragObject.
        /// </summary>
        /// <returns>True if inspector should continue drawing</returns>
        public override bool OnInspectorNUI()
        {
            if (!base.OnInspectorNUI())
            {
                return false;
            }

            drawer.EndEditor(this);
            return true;
        }


        public override bool UseDefaultMargins()
        {
            return false;
        }
    }
}

#endif