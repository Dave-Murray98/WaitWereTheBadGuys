// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using NWH.Common.Vehicles;
using UnityEngine;
#if UNITY_EDITOR
using NWH.NUI;
using UnityEditor;
#endif

#endregion

namespace NWH.Common.Cameras
{
    /// <summary>
    /// Empty component that should be attached to the cameras that are inside the vehicle if interior sound change is to
    /// be used.
    /// </summary>
    public class CameraInsideVehicle : MonoBehaviour
    {
        /// <summary>
        /// Is the camera inside vehicle?
        /// </summary>
        [Tooltip("    Is the camera inside vehicle?")]
        public bool isInsideVehicle = true;

        private Vehicle _vehicle;


        private void Awake()
        {
            _vehicle = GetComponentInParent<Vehicle>();
            Debug.Assert(_vehicle != null,
                         "CameraInsideVehicle needs to be attached to an object containing a Vehicle script.");
        }


        private void Update()
        {
            _vehicle.CameraInsideVehicle = isInsideVehicle;
        }
    }
}

#if UNITY_EDITOR

namespace NWH.Common.Cameras
{
    [CustomEditor(typeof(CameraInsideVehicle))]
    [CanEditMultipleObjects]
    public class CameraInsideVehicleEditor : NUIEditor
    {
        public override bool OnInspectorNUI()
        {
            if (!base.OnInspectorNUI())
            {
                return false;
            }

            drawer.Field("isInsideVehicle");

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