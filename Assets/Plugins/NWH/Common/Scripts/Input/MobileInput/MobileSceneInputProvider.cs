// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using UnityEngine;
#if UNITY_EDITOR
using NWH.NUI;
using UnityEditor;
#endif

#endregion

namespace NWH.Common.Input
{
    /// <summary>
    /// Scene input provider for mobile platforms using on-screen UI buttons.
    /// Requires MobileInputButton components assigned to changeCameraButton and changeVehicleButton fields.
    /// </summary>
    public class MobileSceneInputProvider : SceneInputProviderBase
    {
        /// <summary>
        /// UI button for changing camera. Should reference a MobileInputButton in the scene.
        /// </summary>
        public MobileInputButton changeCameraButton;

        /// <summary>
        /// UI button for changing vehicle. Should reference a MobileInputButton in the scene.
        /// </summary>
        public MobileInputButton changeVehicleButton;


        public override bool ChangeCamera()
        {
            return changeCameraButton != null && changeCameraButton.hasBeenClicked;
        }


        public override bool ChangeVehicle()
        {
            return changeVehicleButton != null && changeVehicleButton.hasBeenClicked;
        }


        public override Vector2 CharacterMovement()
        {
            return Vector2.zero;
        }


        public override bool ToggleGUI()
        {
            return false;
        }


        public override Vector2 CameraRotation()
        {
            return Vector2.zero;
        }


        public override Vector2 CameraPanning()
        {
            return Vector2.zero;
        }


        public override bool CameraRotationModifier()
        {
            return false;
        }


        public override bool CameraPanningModifier()
        {
            return false;
        }


        public override float CameraZoom()
        {
            return 0;
        }
    }
}

#if UNITY_EDITOR
namespace NWH.Common.Input
{
    /// <summary>
    /// Editor for MobileInputProvider.
    /// </summary>
    [CustomEditor(typeof(MobileSceneInputProvider))]
    public class MobileSceneInputProviderEditor : NUIEditor
    {
        public override bool OnInspectorNUI()
        {
            if (!base.OnInspectorNUI())
            {
                return false;
            }

            drawer.BeginSubsection("Scene Buttons");
            drawer.Field("changeVehicleButton");
            drawer.Field("changeCameraButton");
            drawer.EndSubsection();

            drawer.EndEditor(this);
            return true;
        }


        public override bool UseDefaultMargins()
        {
            return false;
        }
    }
}

#endif