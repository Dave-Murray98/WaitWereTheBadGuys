// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using System;
using System.Collections.Generic;
using NWH.Common.Vehicles;
using NWH.DWP2.WaterObjects;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

#endregion

namespace NWH.DWP2.ShipController
{
    /// <summary>
    /// Main controller for ships, boats and other water vessels.
    /// Manages propulsion through engines and propellers, steering via rudders, and additional control through bow/stern thrusters.
    /// Includes stabilization systems, anchor functionality, and multiplayer support.
    /// Can be used for everything from small boats to large ships.
    /// </summary>
    /// <seealso cref="Engine"/>
    /// <seealso cref="Rudder"/>
    /// <seealso cref="Thruster"/>
    /// <seealso cref="Anchor"/>
    /// <seealso cref="ShipInputHandler"/>
    /// <seealso cref="NWH.DWP2.WaterObjects.WaterObject"/>
    /// <seealso cref="NWH.Common.Vehicles.Vehicle"/>
    [RequireComponent(typeof(Anchor))]
    [Serializable]
    [DefaultExecutionOrder(90)]
    public class AdvancedShipController : Vehicle
    {
        /// <summary>
        /// Should the anchor be dropped when the ship is deactivated?
        /// </summary>
        [Tooltip("Should the anchor be dropped when the ship is deactivated?")]
        public bool dropAnchorWhenInactive = true;

        /// <summary>
        /// Ship's engines.
        /// </summary>
        [Tooltip(
            "List of engines. Each engine is a propulsion system in itself consisting of the engine and the propeller.")]
        [SerializeField]
        public List<Engine> engines = new();

        /// <summary>
        /// Class that handles all of the user input.
        /// </summary>
        [SerializeField]
        [Tooltip("Class that handles all of the user input.")]
        public ShipInputHandler input = new();

        /// <summary>
        /// Angle at which roll stabilization torque reaches maximum.
        /// </summary>
        [Tooltip("Angle at which roll stabilization torque reaches maximum.")]
        public float maxStabilizationTorqueAngle = 20f;

        /// <summary>
        /// Called after ship initialization.
        /// </summary>
        [Tooltip("Called after ship initialization.")]
        public UnityEvent onShipInitialized = new();

        /// <summary>
        /// Torque that will be applied to stabilize pitch when the ship pitch angle reaches maxStabilizationTorqueAngle.
        /// </summary>
        [Tooltip(
            "Torque that will be applied to stabilize pitch when the ship pitch angle reaches maxStabilizationTorqueAngle.")]
        public float pitchStabilizationMaxTorque;

        [FormerlySerializedAs("ReferenceWaterObject")]
        /// <summary>
        /// Reference to the primary WaterObject component used for water height queries.
        /// </summary>
        public WaterObject referenceWaterObject;

        /// <summary>
        /// Torque that will be applied to stabilize roll when the ship roll angle reaches maxStabilizationTorqueAngle.
        /// </summary>
        [Tooltip(
            "Torque that will be applied to stabilize roll when the ship roll angle reaches maxStabilizationTorqueAngle.")]
        public float rollStabilizationMaxTorque = 3000f;

        /// <summary>
        /// Ship's rudders.
        /// </summary>
        [SerializeField]
        [Tooltip("Ship's rudders.")]
        public List<Rudder> rudders = new();

        /// <summary>
        /// Should the ship pitch be stabilized?
        /// </summary>
        [Tooltip("Should the ship pitch be stabilized?")]
        public bool stabilizePitch;

        /// <summary>
        /// Should the ship roll be stabilized?
        /// </summary>
        [Tooltip("Should the ship roll be stabilized?")]
        public bool stabilizeRoll;

        /// <summary>
        /// Bow or stern thrusters that a ship has.
        /// </summary>
        [SerializeField]
        [Tooltip("Bow or stern thrusters that a ship has.")]
        public List<Thruster> thrusters = new();

        /// <summary>
        /// Should the anchor be weighed/lifted when the ship is activated?
        /// </summary>
        [Tooltip("Should the anchor be weighed/lifted when the ship is activated?")]
        public bool weighAnchorWhenActive = true;

        private Vector3 _stabilizationTorque = Vector3.zero;

        /// <summary>
        /// Anchor script.
        /// </summary>
        public Anchor Anchor { get; private set; }

        /// <summary>
        /// Speed in knots.
        /// </summary>
        public float SpeedKnots
        {
            get { return Speed * 1.944f; }
        }


        public void Start()
        {
            if (referenceWaterObject == null)
            {
                referenceWaterObject = GetComponentInChildren<WaterObject>();
                if (referenceWaterObject == null)
                {
                    Debug.LogWarning("ShipController has no child WaterObjects.");
                }
            }

            foreach (Thruster thruster in thrusters)
            {
                thruster.Initialize(this);
            }

            foreach (Rudder rudder in rudders)
            {
                rudder.Initialize(this);
            }

            foreach (Engine engine in engines)
            {
                engine.Initialize(this);
            }

            Anchor = GetComponent<Anchor>();
            if (Anchor == null)
            {
                Debug.LogWarning(
                    $"Object {name} is missing 'Anchor' component which is required for AdvancedShipController to work properly.");
                Anchor = gameObject.AddComponent<Anchor>();
            }

            onShipInitialized.Invoke();
        }


        public void Update()
        {
            if (!MultiplayerIsRemote)
            {
                input.Update();
            }
        }


        public override void FixedUpdate()
        {
            base.FixedUpdate();

            foreach (Engine engine in engines)
            {
                engine.Update();
            }

            foreach (Rudder rudder in rudders)
            {
                rudder.Update();
            }

            foreach (Thruster thruster in thrusters)
            {
                thruster.Update();
            }

            if (!MultiplayerIsRemote)
            {
                if (input.Anchor)
                {
                    if (Anchor.Dropped)
                    {
                        Anchor.Weigh();
                    }
                    else
                    {
                        Anchor.Drop();
                    }
                }

                // Reset bool inputs
                input.Anchor          = false;
                input.EngineStartStop = false;

                if (stabilizePitch || stabilizeRoll)
                {
                    _stabilizationTorque = Vector3.zero;

                    if (stabilizeRoll)
                    {
                        _stabilizationTorque.z = -Mathf.Clamp(GetRollAngle() / maxStabilizationTorqueAngle, -1f, 1f) *
                                                 rollStabilizationMaxTorque;
                    }

                    if (stabilizePitch)
                    {
                        _stabilizationTorque.x = Mathf.Clamp(GetPitchAngle() / maxStabilizationTorqueAngle, -1f, 1f) *
                                                 pitchStabilizationMaxTorque;
                    }

                    vehicleRigidbody.AddTorque(transform.TransformVector(_stabilizationTorque));
                }
            }
        }


        private void OnDrawGizmos()
        {
            Start();

            foreach (Rudder rudder in rudders)
            {
                Gizmos.color = Color.magenta;
            }

            foreach (Engine e in engines)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(e.ThrustPosition, 0.2f);
                Gizmos.DrawRay(new Ray(e.ThrustPosition, e.ThrustDirection));
            }

            foreach (Thruster thruster in thrusters)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawSphere(transform.TransformPoint(thruster.position), 0.2f);
                Gizmos.DrawRay(new Ray(thruster.WorldPosition, transform.right));
            }
        }


        private void OnValidate()
        {
            if (engines != null)
            {
                foreach (Engine engine in engines)
                {
                    if (engine.thrustDirection.magnitude == 0)
                    {
                        Debug.LogWarning($"{name}: Engine {engine.name} thrust direction is [0,0,0]! " +
                                         "Set it to e.g. [0,0,1] for forward thrust.");
                    }
                }
            }
        }


        public override void OnDisable()
        {
            base.OnDisable();

            foreach (Engine e in engines)
            {
                e.StopEngine();
            }

            if (dropAnchorWhenInactive && Anchor != null)
            {
                Anchor.Drop();
            }
        }


        public override void OnEnable()
        {
            base.OnEnable();

            foreach (Engine e in engines)
            {
                e.StartEngine();
            }

            if (weighAnchorWhenActive && Anchor != null)
            {
                Anchor.Weigh();
            }
        }


        /// <summary>
        /// Returns pitch angle of the ship in degrees.
        /// Positive values indicate bow up, negative values indicate bow down.
        /// </summary>
        /// <returns>Pitch angle in degrees.</returns>
        public float GetPitchAngle()
        {
            Vector3 right = transform.right;
            right.y =  0;
            right   *= Mathf.Sign(transform.up.y);
            Vector3 fwd = Vector3.Cross(right, Vector3.up).normalized;
            return Vector3.Angle(fwd, transform.forward) * Mathf.Sign(transform.forward.y);
        }


        /// <summary>
        /// Returns roll angle of the ship in degrees.
        /// Positive values indicate starboard side down, negative values indicate port side down.
        /// </summary>
        /// <returns>Roll angle in degrees.</returns>
        public float GetRollAngle()
        {
            Vector3 fwd = transform.forward;
            fwd.y =  0;
            fwd   *= Mathf.Sign(transform.up.y);
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
            return Vector3.Angle(right, transform.right) * Mathf.Sign(transform.right.y);
        }


        private float WrapAngle(float angle)
        {
            return angle > 180 ? angle - 360 : angle;
        }
    }
}