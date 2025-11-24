// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using NWH.Common.Input;
using UnityEngine;
using UnityEngine.EventSystems;

#endregion

namespace NWH.Common.Demo
{
    /// <summary>
    /// Simple first-person controller using physics-based movement.
    /// Useful for testing and navigating demo scenes on foot.
    /// </summary>
    /// <remarks>
    /// Based on Unity Community Wiki example. Uses Rigidbody for physics-accurate movement
    /// with mouse-look camera control.
    /// </remarks>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    public class RigidbodyFPSController : MonoBehaviour
    {
        /// <summary>
        /// Downward acceleration force in m/s^2.
        /// </summary>
        public float gravity           = 10.0f;

        /// <summary>
        /// Maximum height of jumps in meters.
        /// </summary>
        public float jumpHeight        = 2.0f;

        /// <summary>
        /// Maximum upward look angle in degrees.
        /// </summary>
        public float maximumY          = 60f;

        /// <summary>
        /// Maximum velocity change per fixed update, controls acceleration responsiveness.
        /// </summary>
        public float maxVelocityChange = 10.0f;

        /// <summary>
        /// Maximum downward look angle in degrees.
        /// </summary>
        public float minimumY          = -60f;

        /// <summary>
        /// Horizontal mouse look sensitivity.
        /// </summary>
        public float sensitivityX      = 15f;

        /// <summary>
        /// Vertical mouse look sensitivity.
        /// </summary>
        public float sensitivityY      = 15f;

        /// <summary>
        /// Movement speed in meters per second.
        /// </summary>
        public  float   speed = 10.0f;
        private Vector2 _cameraRotationInput;

        private bool _grounded;

        private Vector2   _movement;
        private Rigidbody _rb;
        private float     _rotationY;

        private bool PointerOverUI
        {
            get { return EventSystem.current.IsPointerOverGameObject(); }
        }


        private void Awake()
        {
            _rb                = GetComponent<Rigidbody>();
            _rb.freezeRotation = true;
            _rb.useGravity     = false;
        }


        private void LateUpdate()
        {
            _movement            = InputProvider.CombinedInput<SceneInputProviderBase>(i => i.CharacterMovement());
            _cameraRotationInput = InputProvider.CombinedInput<SceneInputProviderBase>(i => i.CameraRotation());

            if (_grounded)
            {
                // Calculate how fast we should be moving
                Vector3 targetVelocity = new(_movement.x, 0, _movement.y);
                targetVelocity =  transform.TransformDirection(targetVelocity);
                targetVelocity *= speed;

                // Apply a force that attempts to reach our target velocity
                Vector3 velocity       = _rb.linearVelocity;
                Vector3 velocityChange = targetVelocity - velocity;
                velocityChange.x = Mathf.Clamp(velocityChange.x, -maxVelocityChange, maxVelocityChange);
                velocityChange.z = Mathf.Clamp(velocityChange.z, -maxVelocityChange, maxVelocityChange);
                velocityChange.y = 0;
                _rb.AddForce(velocityChange, ForceMode.VelocityChange);
            }

            float timeFactor = Time.deltaTime * 20f;
            float rotationX  = transform.localEulerAngles.y + _cameraRotationInput.x * sensitivityX * timeFactor;
            _rotationY                 += _cameraRotationInput.y * sensitivityY * timeFactor;
            _rotationY                 =  Mathf.Clamp(_rotationY, minimumY, maximumY);
            transform.localEulerAngles =  new Vector3(-_rotationY, rotationX, 0);
            _rb.AddForce(new Vector3(0, -gravity * _rb.mass, 0));

            _grounded = false;
        }


        private float CalculateJumpVerticalSpeed()
        {
            // From the jump height and gravity we deduce the upwards speed 
            // for the character to reach at the apex.
            return Mathf.Sqrt(2 * jumpHeight * gravity);
        }


        private void OnCollisionStay()
        {
            _grounded = true;
        }
    }
}