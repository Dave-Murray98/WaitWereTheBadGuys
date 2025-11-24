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

namespace NWH.Common.Input
{
    /// <summary>
    /// InputProvider for scene and camera related behavior.
    /// </summary>
    public abstract class SceneInputProviderBase : InputProvider
    {
        /// <summary>
        /// If true a button press will be required to unlock camera panning.
        /// </summary>
        [Tooltip("    If true a button press will be required to unlock camera panning.")]
        public bool requireCameraPanningModifier = true;

        /// <summary>
        /// If true a button press will be required to unlock camera rotation.
        /// </summary>
        [Tooltip("    If true a button press will be required to unlock camera rotation.")]
        public bool requireCameraRotationModifier = true;


        /// <summary>
        /// Returns true when the change camera button is pressed.
        /// </summary>
        public virtual bool ChangeCamera()
        {
            return false;
        }


        /// <summary>
        /// Returns camera rotation input as a Vector2 (x = horizontal, y = vertical).
        /// </summary>
        public virtual Vector2 CameraRotation()
        {
            return Vector2.zero;
        }


        /// <summary>
        /// Returns camera panning input as a Vector2 (x = horizontal, y = vertical).
        /// </summary>
        public virtual Vector2 CameraPanning()
        {
            return Vector2.zero;
        }


        /// <summary>
        /// Returns true when the camera rotation modifier button is held.
        /// If requireCameraRotationModifier is false, always returns true.
        /// </summary>
        public virtual bool CameraRotationModifier()
        {
            return !requireCameraRotationModifier;
        }


        /// <summary>
        /// Returns true when the camera panning modifier button is held.
        /// If requireCameraPanningModifier is false, always returns true.
        /// </summary>
        public virtual bool CameraPanningModifier()
        {
            return !requireCameraPanningModifier;
        }


        /// <summary>
        /// Returns camera zoom input value. Positive = zoom in, negative = zoom out.
        /// </summary>
        public virtual float CameraZoom()
        {
            return 0;
        }


        /// <summary>
        /// Returns true when the change vehicle button is pressed.
        /// </summary>
        public virtual bool ChangeVehicle()
        {
            return false;
        }


        /// <summary>
        /// Returns character movement input as a Vector2 (x = horizontal, y = forward/back).
        /// </summary>
        public virtual Vector2 CharacterMovement()
        {
            return Vector2.zero;
        }


        /// <summary>
        /// Returns true when the toggle GUI button is pressed.
        /// </summary>
        public virtual bool ToggleGUI()
        {
            return false;
        }
    }
}