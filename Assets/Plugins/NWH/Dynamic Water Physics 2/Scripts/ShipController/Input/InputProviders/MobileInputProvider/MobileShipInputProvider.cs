// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using NWH.Common.Input;
using UnityEngine.UI;
#if UNITY_EDITOR
using NWH.NUI;
using NWH.DWP2.ShipController;
using UnityEditor;
#endif

#endregion

namespace NWH.DWP2.ShipController
{
    /// <summary>
    /// Ship input provider for mobile devices using on-screen UI controls.
    /// Reads input from Unity UI Sliders and MobileInputButtons placed in the scene.
    /// All control assignments are optional - assign only the controls you need for your ship.
    /// </summary>
    public class MobileShipInputProvider : ShipInputProvider
    {
        /// <summary>
        /// Button for toggling anchor.
        /// </summary>
        public MobileInputButton anchorButton;

        /// <summary>
        /// Slider for bow thruster control.
        /// </summary>
        public Slider            bowThrusterSlider;

        /// <summary>
        /// Button for changing camera in demo scenes.
        /// </summary>
        public MobileInputButton changeCameraButton;

        /// <summary>
        /// Button for changing ship in demo scenes.
        /// </summary>
        public MobileInputButton changeShipButton;

        /// <summary>
        /// Button for starting/stopping engines.
        /// </summary>
        public MobileInputButton engineStartStopButton;

        /// <summary>
        /// Slider for steering control.
        /// </summary>
        public Slider steeringSlider;

        /// <summary>
        /// Slider for stern thruster control.
        /// </summary>
        public Slider sternThrusterSlider;

        /// <summary>
        /// Slider for submarine depth control.
        /// </summary>
        public Slider submarineDepthSlider;

        /// <summary>
        /// Slider for primary throttle control.
        /// </summary>
        public Slider throttleSlider;

        /// <summary>
        /// Slider for secondary throttle control.
        /// </summary>
        public Slider throttleSlider2;

        /// <summary>
        /// Slider for tertiary throttle control.
        /// </summary>
        public Slider throttleSlider3;

        /// <summary>
        /// Slider for quaternary throttle control.
        /// </summary>
        public Slider throttleSlider4;


        public override float Steering()
        {
            if (steeringSlider != null)
            {
                return steeringSlider.value;
            }

            return 0;
        }


        public override float Throttle()
        {
            if (throttleSlider != null)
            {
                return throttleSlider.value;
            }

            return 0;
        }


        public override float Throttle2()
        {
            if (throttleSlider2 != null)
            {
                return throttleSlider2.value;
            }

            return 0;
        }


        public override float Throttle3()
        {
            if (throttleSlider3 != null)
            {
                return throttleSlider3.value;
            }

            return 0;
        }


        public override float Throttle4()
        {
            if (throttleSlider4 != null)
            {
                return throttleSlider4.value;
            }

            return 0;
        }


        public override float SternThruster()
        {
            if (sternThrusterSlider != null)
            {
                return sternThrusterSlider.value;
            }

            return 0;
        }


        public override float BowThruster()
        {
            if (bowThrusterSlider != null)
            {
                return bowThrusterSlider.value;
            }

            return 0;
        }


        public override float SubmarineDepth()
        {
            if (submarineDepthSlider != null)
            {
                return submarineDepthSlider.value;
            }

            return 0;
        }


        public override bool EngineStartStop()
        {
            if (engineStartStopButton != null)
            {
                return engineStartStopButton.hasBeenClicked;
            }

            return false;
        }


        public override bool Anchor()
        {
            if (anchorButton != null)
            {
                return anchorButton.hasBeenClicked;
            }

            return false;
        }
    }
}

#if UNITY_EDITOR
namespace NWH.DWP2.WaterObjects
{
    /// <summary>
    /// Editor for MobileInputProvider.
    /// </summary>
    [CustomEditor(typeof(MobileShipInputProvider))]
    public class MobileShipInputProviderEditor : DWP2NUIEditor
    {
        public override bool OnInspectorNUI()
        {
            if (!base.OnInspectorNUI())
            {
                return false;
            }

            drawer.Info("None of the buttons are mandatory. If you do not wish to use an input leave the field empty.");

            MobileShipInputProvider mip = target as MobileShipInputProvider;
            if (mip == null)
            {
                drawer.EndEditor(this);
                return false;
            }

            drawer.BeginSubsection("Vehicle");
            drawer.Field("steeringSlider");
            drawer.Field("throttleSlider");
            drawer.Field("throttleSlider2");
            drawer.Field("throttleSlider3");
            drawer.Field("throttleSlider4");
            drawer.Field("bowThrusterSlider");
            drawer.Field("sternThrusterSlider");
            drawer.Field("submarineDepthSlider");
            drawer.Field("engineStartStopButton");
            drawer.Field("anchorButton");
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