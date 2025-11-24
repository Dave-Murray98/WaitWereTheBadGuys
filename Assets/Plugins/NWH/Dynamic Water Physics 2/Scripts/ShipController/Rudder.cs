// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using System;
using UnityEngine;

#endregion

namespace NWH.DWP2.ShipController
{
    /// <summary>
    /// Represents a ship rudder that rotates based on steering input.
    /// Provides visual rotation only - actual steering forces are applied by WaterObject components on the rudder itself.
    /// Multiple rudders can be used on a single ship.
    /// </summary>
    [Serializable]
    public class Rudder
    {
        /// <summary>
        /// Axis around which the rudder will be rotated.
        /// </summary>
        [Tooltip("Axis around which the rudder will be rotated.")]
        public Vector3 localRotationAxis = new(0, 1, 0);

        /// <summary>
        /// Max angle in degrees rudder will be able to reach.
        /// </summary>
        [Tooltip("Max angle in degrees rudder will be able to reach.")]
        public float maxAngle = 45f;

        /// <summary>
        /// Name of the rudder. Can be any string.
        /// </summary>
        [Tooltip("Name of the rudder. Can be any string.")]
        public string name = "Rudder";

        /// <summary>
        /// Rotation speed in degrees per second.
        /// </summary>
        [Tooltip("Rotation speed in degrees per second.")]
        public float rotationSpeed = 20f;

        /// <summary>
        /// Transform representing the rudder.
        /// </summary>
        [Tooltip("Transform representing the rudder.")]
        public Transform rudderTransform;

        private AdvancedShipController _sc;

        /// <summary>
        /// Current angle of the rudder in degrees.
        /// Positive angles indicate starboard turn, negative angles indicate port turn.
        /// </summary>
        public float Angle { get; private set; }

        /// <summary>
        /// Current rudder angle as a normalized value between -1 and 1.
        /// </summary>
        public float AnglePercent
        {
            get { return Angle / maxAngle; }
        }


        /// <summary>
        /// Initializes the rudder with a reference to its parent ship controller.
        /// </summary>
        /// <param name="sc">The ship controller this rudder belongs to.</param>
        public void Initialize(AdvancedShipController sc)
        {
            _sc = sc;
        }


        public virtual void Update()
        {
            if (rudderTransform != null)
            {
                float targetAngle = -_sc.input.Steering * maxAngle;
                Angle = Mathf.MoveTowardsAngle(Angle, targetAngle, rotationSpeed * Time.fixedDeltaTime);
                rudderTransform.localRotation = Quaternion.Euler(Angle * localRotationAxis.x,
                                                                 Angle * localRotationAxis.y,
                                                                 Angle * localRotationAxis.z);
            }
        }
    }
}