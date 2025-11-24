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
using UnityEditor;
using NWH.NUI;
#endif

#endregion

namespace NWH.Common.Vehicles
{
    /// <summary>
    /// Automatically enables/disables a GameObject based on parent vehicle's active state.
    /// Useful for optimizing performance by disabling effects, audio, or visuals when a vehicle is inactive.
    /// </summary>
    /// <remarks>
    /// Attach this component to child objects that should only be active when the vehicle is being
    /// simulated. When the vehicle is put to sleep (disabled), attached objects are also disabled,
    /// saving processing time for effects that wouldn't be visible anyway.
    /// </remarks>
    [DefaultExecutionOrder(21)]
    public partial class FollowVehicleState : MonoBehaviour
    {
        private Vehicle _vc;


        private void OnEnable()
        {
            _vc = GetComponentInParent<Vehicle>();
            if (_vc == null)
            {
                Debug.LogError("VehicleController not found.");
            }

            _vc.onEnable.AddListener(OnVehicleWake);
            _vc.onDisable.AddListener(OnVehicleSleep);

            if (_vc.enabled)
            {
                OnVehicleWake();
            }
            else
            {
                OnVehicleSleep();
            }
        }


        private void OnDisable()
        {
            // Clean up event listeners to prevent memory leaks
            if (_vc != null)
            {
                _vc.onEnable.RemoveListener(OnVehicleWake);
                _vc.onDisable.RemoveListener(OnVehicleSleep);
            }
        }


        private void OnVehicleWake()
        {
            gameObject.SetActive(true);
        }


        private void OnVehicleSleep()
        {
            gameObject.SetActive(false);
        }
    }
}

#if UNITY_EDITOR
namespace NWH.Common.Vehicles
{
    [CustomEditor(typeof(FollowVehicleState))]
    [CanEditMultipleObjects]
    public partial class FollowVehicleStateEditor : NUIEditor
    {
        public override bool OnInspectorNUI()
        {
            if (!base.OnInspectorNUI())
            {
                return false;
            }

            drawer.Info("Enables/disables the GameObject based on Vehicle state (awake/asleep).");

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