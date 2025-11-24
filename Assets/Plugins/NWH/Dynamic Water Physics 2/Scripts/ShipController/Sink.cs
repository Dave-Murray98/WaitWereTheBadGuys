// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using System.Collections;
using NWH.Common.CoM;
using UnityEngine;

#endregion

namespace NWH.DWP2.ShipController
{
    /// <summary>
    /// Simulates a ship taking on water and sinking by gradually increasing mass over time.
    /// Works with VariableCenterOfMass component to affect buoyancy and cause the ship to sink.
    /// Can be triggered to simulate hull breaches or flooding.
    /// </summary>
    public class Sink : MonoBehaviour, IMassAffector
    {
        /// <summary>
        /// Rate at which mass is added per second as a percentage of maxAdditionalMass.
        /// Higher values cause faster sinking.
        /// </summary>
        [Tooltip("Percentage of initial mass that will be added each second to imitate water ingress")]
        public float addedMassPercentPerSecond = 0.1f;

        /// <summary>
        /// Maximum mass in kg that can be added through flooding.
        /// This represents the total water mass that can enter the ship.
        /// </summary>
        [Tooltip("Maximum added mass after water ingress. 1f = 100% of orginal mass, 2f = 200% of original mass, etc.")]
        public float maxAdditionalMass = 100000f;

        private float                _mass;
        private Coroutine            _sinkCoroutine;
        private VariableCenterOfMass _variableCenterOfMass;


        public float GetMass()
        {
            return _mass;
        }


        public Vector3 GetWorldCenterOfMass()
        {
            return transform.position;
        }


        public Transform GetTransform()
        {
            return transform;
        }


        private void Start()
        {
            _variableCenterOfMass                        = GetComponentInParent<VariableCenterOfMass>();
            _variableCenterOfMass.useMassAffectors       = true;
            _variableCenterOfMass.useDefaultMass         = false;
            _variableCenterOfMass.useDefaultCenterOfMass = false;
            _mass                                        = 0f;
        }


        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(transform.position, 0.2f);
        }


        /// <summary>
        /// Coroutine that gradually increases mass to simulate water ingress.
        /// </summary>
        public IEnumerator SinkCoroutine()
        {
            while (true)
            {
                _mass += maxAdditionalMass * addedMassPercentPerSecond;
                _mass =  Mathf.Clamp(_mass, 0f, maxAdditionalMass);
                yield return new WaitForSeconds(1f);
            }
        }


        /// <summary>
        /// Begins the sinking process by starting mass increase over time.
        /// </summary>
        public void StartSinking()
        {
            if (_sinkCoroutine == null)
            {
                _sinkCoroutine = StartCoroutine(SinkCoroutine());
            }
        }


        /// <summary>
        /// Stops the sinking process. Added mass remains until reset.
        /// </summary>
        public void StopSinking()
        {
            if (_sinkCoroutine != null)
            {
                StopCoroutine(_sinkCoroutine);
                _sinkCoroutine = null;
            }
        }


        /// <summary>
        /// Resets the added mass to zero, returning the ship to its original mass.
        /// </summary>
        public void ResetMass()
        {
            _mass = 0f;
        }
    }
}