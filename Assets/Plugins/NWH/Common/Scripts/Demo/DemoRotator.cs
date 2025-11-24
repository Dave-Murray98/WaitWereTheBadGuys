// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using UnityEngine;

#endregion

namespace NWH.Common.Demo
{
    /// <summary>
    /// Continuously rotates a Rigidbody at a constant rate.
    /// Useful for rotating platforms or visual elements in demo scenes.
    /// </summary>
    public class DemoRotator : MonoBehaviour
    {
        /// <summary>
        /// Rotation speed in degrees per second for each axis.
        /// </summary>
        public  Vector3   rotation;
        private Transform _cachedTransform;
        private Rigidbody _rb;
        private Vector3   _scaledRotation;


        private void Start()
        {
            _rb              = GetComponent<Rigidbody>();
            _cachedTransform = transform;
        }


        private void FixedUpdate()
        {
            _scaledRotation = rotation * Time.fixedDeltaTime;
            _rb.MoveRotation(_cachedTransform.rotation * Quaternion.Euler(_scaledRotation));
        }
    }
}