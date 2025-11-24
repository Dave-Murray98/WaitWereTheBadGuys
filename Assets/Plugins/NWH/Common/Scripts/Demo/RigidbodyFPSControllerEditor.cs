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

namespace NWH.Common.Demo.Editor
{
    /// <summary>
    /// Custom inspector for RigidbodyFPSController demo component.
    /// </summary>
    [CustomEditor(typeof(RigidbodyFPSController))]
    [CanEditMultipleObjects]
    public class RigidbodyFPSControllerEditor : NUIEditor
    {
        /// <summary>
        /// Draws custom inspector GUI for RigidbodyFPSController.
        /// </summary>
        /// <returns>True if inspector should continue drawing</returns>
        public override bool OnInspectorNUI()
        {
            if (!base.OnInspectorNUI())
            {
                return false;
            }

            drawer.Field("gravity");
            drawer.Field("maximumY");
            drawer.Field("maxVelocityChange");
            drawer.Field("minimumY");
            drawer.Field("sensitivityX");
            drawer.Field("sensitivityY");
            drawer.Field("speed");

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