// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using UnityEngine;
#if UNITY_EDITOR
using NWH.NUI;
using UnityEditor;
#endif

#endregion

namespace NWH.Common.Cameras
{
    /// <summary>
    /// Base class for vehicle camera implementations with automatic target detection.
    /// </summary>
    /// <seealso cref="CameraChanger"/>
    /// <seealso cref="CameraInsideVehicle"/>
    /// <seealso cref="CameraMouseDrag"/>
    public class VehicleCamera : MonoBehaviour
    {
        /// <summary>
        /// Transform to track. Auto-detects parent Rigidbody if not assigned.
        /// </summary>
        [Tooltip(
            "Transform that this script is targeting. Can be left empty if head movement is not being used.")]
        public Transform target;


        public virtual void Awake()
        {
            if (target == null)
            {
                target = GetComponentInParent<Rigidbody>()?.transform;
            }
        }
    }
}

#if UNITY_EDITOR

namespace NWH.Common.Cameras
{
    [CustomEditor(typeof(VehicleCamera))]
    [CanEditMultipleObjects]
    public class VehicleCameraEditor : NUIEditor
    {
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