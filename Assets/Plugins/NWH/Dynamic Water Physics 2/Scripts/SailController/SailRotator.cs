// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using NWH.DWP2.ShipController;
using UnityEngine;
#if UNITY_EDITOR
using NWH.NUI;
using UnityEditor;
#endif

#endregion

namespace NWH.DWP2.SailController
{
    /// <summary>
    /// Rotates a transform based on player input from the AdvancedShipController.
    /// Used to control sail orientation in response to the RotateSail input axis.
    /// Typically attached to a sail boom or mast that rotates horizontally.
    /// Requires an AdvancedShipController component on a parent GameObject.
    /// </summary>
    public class SailRotator : MonoBehaviour
    {
        /// <summary>
        /// Local axis around which the transform rotates.
        /// Default (0, 1, 0) rotates around the Y-axis.
        /// Use negative values to reverse rotation direction.
        /// Magnitude is ignored - only direction matters.
        /// </summary>
        [Tooltip("Rotation axis of this transform.\r\nUse -1 to reverse the rotation.")]
        public Vector3 rotationAxis = new(0, 1, 0);

        /// <summary>
        /// Maximum rotation speed in degrees per second when input is at full deflection.
        /// Higher values allow faster sail adjustment.
        /// Combined with rotationAxis and player input to determine final rotation.
        /// </summary>
        [Tooltip(
            "Rotation speed of this transform in deg/s.\r\nMultiplied by the rotationAxis to get the final rotation.")]
        public float rotationSpeed = 50f;

        private AdvancedShipController _shipController;


        private void Awake()
        {
            _shipController = GetComponentInParent<AdvancedShipController>();
            Debug.Assert(_shipController != null, "SailController requires the AdvancedShipController to" +
                                                  " be attached to one of the parents (does not have to be direct parent).");
        }


        private void Update()
        {
            float rotationAngle = _shipController.input.RotateSail * rotationSpeed * Time.deltaTime;
            transform.Rotate(rotationAngle * rotationAxis);
        }
    }
}

#if UNITY_EDITOR
namespace NWH.DWP2.SailController
{
    [CustomEditor(typeof(SailRotator))]
    [CanEditMultipleObjects]
    public class SailRotatorEditor : DWP2NUIEditor
    {
        public override bool OnInspectorNUI()
        {
            if (!base.OnInspectorNUI())
            {
                return false;
            }

            drawer.Field("rotationSpeed");
            drawer.Field("rotationAxis");

            drawer.EndEditor(this);
            return true;
        }
    }
}
#endif