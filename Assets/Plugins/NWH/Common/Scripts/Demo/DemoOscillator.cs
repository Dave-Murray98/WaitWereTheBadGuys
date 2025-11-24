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
    /// Moves a Rigidbody in a sinusoidal oscillation pattern.
    /// Useful for creating moving platforms or obstacles in demo scenes.
    /// </summary>
    public class DemoOscillator : MonoBehaviour
    {
        /// <summary>
        /// Speed of the oscillation in Hz. Higher values result in faster movement.
        /// </summary>
        public  float     speed = 1f;

        /// <summary>
        /// Maximum displacement from the starting position in each axis.
        /// </summary>
        public  Vector3   travel;
        private Rigidbody _rb;

        private Vector3 initPos;


        private void Start()
        {
            _rb     = GetComponent<Rigidbody>();
            initPos = transform.position;
        }


        private void FixedUpdate()
        {
            float sinValue = Mathf.Sin(Time.time * speed);
            _rb.MovePosition(initPos + travel * sinValue);
        }
    }
}