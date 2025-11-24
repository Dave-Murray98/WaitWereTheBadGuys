// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using NWH.Common.CoM;
using NWH.Common.Input;
using NWH.DWP2.WaterObjects;
using UnityEngine;
using UnityEngine.Serialization;

#endregion

namespace NWH.DWP2.ShipController
{
    /// <summary>
    /// Enables submarine functionality by controlling ballast mass for diving and surfacing.
    /// Manages depth control through variable mass and can automatically maintain horizontal orientation.
    /// Works with VariableCenterOfMass to adjust buoyancy for depth changes.
    /// </summary>
    [RequireComponent(typeof(AdvancedShipController))]
    [RequireComponent(typeof(VariableCenterOfMass))]
    [DefaultExecutionOrder(500)]
    public class Submarine : MonoBehaviour, IMassAffector
    {
        /// <summary>
        /// Speed of change of the ballast mass, as a percentage of maxBallastMass.
        /// </summary>
        [Tooltip("Speed of change of the ballast mass, as a percentage of maxBallastMass.")]
        public float ballastChangeSpeed = 0.05f;

        /// <summary>
        /// If enabled submarine will try to keep horizontal by shifting the center of mass.
        /// </summary>
        [Tooltip("If enabled submarine will try to keep horizontal by shifting the center of mass.")]
        public bool keepHorizontal;

        /// <summary>
        /// Sensitivity of calculation trying to keep the submarine horizontal. Higher number will mean faster reaction.
        /// </summary>
        [Tooltip(
            "Sensitivity of calculation trying to keep the submarine horizontal. Higher number will mean faster reaction.")]
        public float keepHorizontalSensitivity = 1f;

        /// <summary>
        /// Maximum ballast mass in kg that can be added to make the submarine sink.
        /// Higher values allow diving deeper and faster but require more time to surface.
        /// </summary>
        [FormerlySerializedAs("maxAdditionalMass")]
        [FormerlySerializedAs("maxMassFactor")]
        [Tooltip(
            "Maximum additional mass that can be added (taking on water) to the base mass of the rigidbody to make submarine sink.")]
        public float maxBallastMass = 200000f;

        /// <summary>
        /// Maximum rigidbody center of mass offset that can be used to keep the submarine level.
        /// </summary>
        [Tooltip("Maximum rigidbody center of mass offset that can be used to keep the submarine level.")]
        public float maxMassOffset = 5f;

        /// <summary>
        /// Reference to the WaterObject used for water level detection.
        /// </summary>
        public  WaterObject ReferenceWaterObject;
        private Vector3     _centerOfMass;

        private float _mass;

        private VariableCenterOfMass _vcom;
        private float                _zOffset;

        [HideInInspector]
        [SerializeField]
        private float depthInput;

        /// <summary>
        /// Input for depth control from -1 (surface) to 1 (dive).
        /// Positive values add ballast mass to sink, negative values reduce it to surface.
        /// </summary>
        public float DepthInput
        {
            get { return depthInput; }
            set { depthInput = Mathf.Clamp(value, -1f, 1f); }
        }


        public float GetMass()
        {
            return _mass;
        }


        public Vector3 GetWorldCenterOfMass()
        {
            return _centerOfMass;
        }


        public Transform GetTransform()
        {
            return transform;
        }


        private void Awake()
        {
            if (ReferenceWaterObject == null)
            {
                ReferenceWaterObject = GetComponentInChildren<WaterObject>();
            }
        }


        private void Start()
        {
            _vcom = GetComponentInParent<VariableCenterOfMass>();
            if (_vcom == null)
            {
                Debug.LogError(
                    $"VariableCenterOfMass script not found on object {name}. If updating from older versions" +
                    "of DWP2 replace CenterOfMass [deprecated] script with VariableCenterOfMass [new] script.");
            }

            _vcom.useMassAffectors       = true;
            _vcom.useDefaultMass         = false;
            _vcom.useDefaultCenterOfMass = false;

            Debug.Assert(ReferenceWaterObject != null, "ReferenceWaterObject not set.");
        }


        private void FixedUpdate()
        {
            DepthInput = InputProvider.CombinedInput<ShipInputProvider>
                (i => i.SubmarineDepth());

            _mass -= DepthInput * maxBallastMass * ballastChangeSpeed * Time.fixedDeltaTime;
            _mass =  Mathf.Clamp(_mass, 0f, Mathf.Infinity);

            if (keepHorizontal)
            {
                float angle = Vector3.SignedAngle(transform.up, Vector3.up, transform.right);
                _zOffset = Mathf.Clamp(Mathf.Sign(angle) * Mathf.Pow(angle * 0.2f, 2f) * keepHorizontalSensitivity,
                                       -maxMassOffset, maxMassOffset);
                Vector3 position = transform.position;
                _centerOfMass = new Vector3(position.x, position.y, position.z + _zOffset);
            }
        }
    }
}