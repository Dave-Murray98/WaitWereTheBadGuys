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
using UnityEngine.Serialization;

#endregion

namespace NWH.DWP2.ShipController
{
    /// <summary>
    /// Lateral thruster for sideways ship movement.
    /// Bow thrusters are mounted at the front for tight turning and docking, stern thrusters at the rear.
    /// Multiple thrusters of each type can be added for increased lateral control.
    /// </summary>
    [Serializable]
    public class Thruster
    {
        /// <summary>
        /// Visual rotation direction of the thruster propeller.
        /// </summary>
        public enum RotationDirection
        {
            Left,
            Right,
        }

        /// <summary>
        /// Position of the thruster on the ship.
        /// Determines which input controls it and the direction of thrust application.
        /// </summary>
        public enum ThrusterPosition
        {
            BowThruster,
            SternThruster,
        }

        /// <summary>
        /// Maximum thrust force in Newtons.
        /// </summary>
        [Tooltip("Max thrust in [N].")]
        public float maxThrust;

        /// <summary>
        /// Display name for this thruster.
        /// </summary>
        [Tooltip("Name of the thruster - can be any string.")]
        public string name = "Thruster";

        /// <summary>
        /// Position relative to ship transform where thrust force is applied.
        /// </summary>
        [Tooltip("Relative force application position.")]
        public Vector3 position;

        [FormerlySerializedAs("rotationDirection")]
        [Tooltip("Rotation direction of the propeller. Visual only.")]
        public RotationDirection propellerRotationDirection = RotationDirection.Right;

        [Tooltip("Rotation speed of the propeller if assigned. Visual only.")]
        public float propellerRotationSpeed = 1000f;

        [Tooltip("Optional. Transform representing a propeller. Visual only.")]
        public Transform propellerTransform;

        [Tooltip("Time needed to reach maxThrust.")]
        public float spinUpSpeed = 1f;

        /// <summary>
        /// Whether this is a bow or stern thruster.
        /// </summary>
        public ThrusterPosition thrusterPosition = ThrusterPosition.BowThruster;

        private AdvancedShipController sc;

        private float thrust;

        /// <summary>
        /// World space position where thrust force is applied.
        /// </summary>
        public Vector3 WorldPosition
        {
            get { return sc.transform.TransformPoint(position); }
        }

        /// <summary>
        /// Current input value for this thruster from -1 to 1.
        /// Automatically retrieves from the appropriate input channel based on thrusterPosition.
        /// </summary>
        public float Input
        {
            get
            {
                float input = 0;
                if (thrusterPosition == ThrusterPosition.BowThruster)
                {
                    input = -sc.input.BowThruster;
                }
                else
                {
                    input = -sc.input.SternThruster;
                }

                return input;
            }
        }


        /// <summary>
        /// Initializes the thruster with a reference to its parent ship controller.
        /// </summary>
        /// <param name="sc">The ship controller this thruster belongs to.</param>
        public void Initialize(AdvancedShipController sc)
        {
            this.sc = sc;
        }


        public virtual void Update()
        {
            float newThurst = maxThrust * -Input;
            thrust = Mathf.MoveTowards(thrust, newThurst, spinUpSpeed * maxThrust * Time.fixedDeltaTime);
            sc.vehicleRigidbody.AddForceAtPosition(thrust * sc.transform.right, WorldPosition);

            if (propellerTransform != null)
            {
                float zRotation = Input * propellerRotationSpeed * Time.fixedDeltaTime;
                if (propellerRotationDirection == RotationDirection.Right)
                {
                    zRotation = -zRotation;
                }

                propellerTransform.RotateAround(propellerTransform.position, propellerTransform.forward, zRotation);
            }
        }
    }
}