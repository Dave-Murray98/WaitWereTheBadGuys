// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using System;
using NWH.Common.Input;
using UnityEngine;
using UnityEngine.Events;
#if UNITY_EDITOR
using NWH.NUI;
using NWH.DWP2.ShipController;
using UnityEditor;
#endif

#endregion

namespace NWH.DWP2.ShipController
{
    /// <summary>
    /// Manages ship input by retrieving values from active ShipInputProviders and storing them in ShipInputStates.
    /// Automatically polls all registered input providers and combines their inputs.
    /// Can be disabled for manual input control via scripting or AI.
    /// </summary>
    [Serializable]
    public class ShipInputHandler
    {
        /// <summary>
        /// When enabled input will be auto-retrieved from the InputProviders present in the scene.
        /// Disable to manualy set the input through external scripts, i.e. AI controller.
        /// </summary>
        [Tooltip(
            "When enabled input will be auto-retrieved from the InputProviders present in the scene.\r\nDisable to manualy set the input through external scripts, i.e. AI controller.")]
        public bool autoSetInput = true;

        /// <summary>
        /// Callback invoked after input is processed each frame.
        /// Use this to modify input values programmatically before they are used.
        /// </summary>
        public UnityEvent modifyInputCallback = new();

        /// <summary>
        /// All the input states of the vehicle. Can be used to set input through scripting or copy the inputs
        /// over from other vehicle, such as truck to trailer.
        /// </summary>
        [Tooltip(
            "All the input states of the vehicle. Can be used to set input through scripting or copy the inputs\r\nover from other vehicle, such as truck to trailer.")]
        public ShipInputStates states;

        /// <summary>
        /// Primary throttle input from -1 (full reverse) to 1 (full forward).
        /// </summary>
        public float Throttle
        {
            get { return states.throttle; }
            set { states.throttle = Mathf.Clamp(value, -1f, 1f); }
        }

        /// <summary>
        /// Secondary throttle for independent engine control.
        /// </summary>
        public float Throttle2
        {
            get { return states.throttle2; }
            set { states.throttle2 = Mathf.Clamp(value, -1f, 1f); }
        }

        /// <summary>
        /// Tertiary throttle for independent engine control.
        /// </summary>
        public float Throttle3
        {
            get { return states.throttle3; }
            set { states.throttle3 = Mathf.Clamp(value, -1f, 1f); }
        }

        /// <summary>
        /// Quaternary throttle for independent engine control.
        /// </summary>
        public float Throttle4
        {
            get { return states.throttle4; }
            set { states.throttle4 = Mathf.Clamp(value, -1f, 1f); }
        }

        /// <summary>
        /// Steering input from -1 (port/left) to 1 (starboard/right).
        /// </summary>
        public float Steering
        {
            get { return states.steering; }
            set { states.steering = Mathf.Clamp(value, -1f, 1f); }
        }

        /// <summary>
        /// Stern thruster input from -1 to 1.
        /// </summary>
        public float SternThruster
        {
            get { return states.sternThruster; }
            set { states.sternThruster = Mathf.Clamp(value, -1f, 1f); }
        }

        /// <summary>
        /// Bow thruster input from -1 to 1.
        /// </summary>
        public float BowThruster
        {
            get { return states.bowThruster; }
            set { states.bowThruster = Mathf.Clamp(value, -1f, 1f); }
        }

        /// <summary>
        /// Submarine depth control from -1 (surface) to 1 (dive).
        /// </summary>
        public float SubmarineDepth
        {
            get { return states.submarineDepth; }
            set { states.submarineDepth = Mathf.Clamp01(value); }
        }

        /// <summary>
        /// Anchor toggle state. True for one frame when anchor input is pressed.
        /// </summary>
        public bool Anchor
        {
            get { return states.anchor; }
            set { states.anchor = value; }
        }

        /// <summary>
        /// Engine start/stop toggle state. True for one frame when toggle input is pressed.
        /// </summary>
        public bool EngineStartStop
        {
            get { return states.engineStartStop; }
            set { states.engineStartStop = value; }
        }

        /// <summary>
        /// Sail rotation input for sailing vessels.
        /// </summary>
        public float RotateSail
        {
            get { return states.rotateSail; }
            set { states.rotateSail = value; }
        }


        /// <summary>
        /// Updates all input values from registered input providers.
        /// Called automatically by AdvancedShipController.
        /// </summary>
        public void Update()
        {
            if (!autoSetInput)
            {
                return;
            }

            Steering        =  InputProvider.CombinedInput<ShipInputProvider>(i => i.Steering());
            Throttle        =  InputProvider.CombinedInput<ShipInputProvider>(i => i.Throttle());
            Throttle2       =  InputProvider.CombinedInput<ShipInputProvider>(i => i.Throttle2());
            Throttle3       =  InputProvider.CombinedInput<ShipInputProvider>(i => i.Throttle3());
            Throttle4       =  InputProvider.CombinedInput<ShipInputProvider>(i => i.Throttle4());
            BowThruster     =  InputProvider.CombinedInput<ShipInputProvider>(i => i.BowThruster());
            SternThruster   =  InputProvider.CombinedInput<ShipInputProvider>(i => i.SternThruster());
            SubmarineDepth  =  InputProvider.CombinedInput<ShipInputProvider>(i => i.SubmarineDepth());
            RotateSail      =  InputProvider.CombinedInput<ShipInputProvider>(i => i.RotateSail());
            EngineStartStop |= InputProvider.CombinedInput<ShipInputProvider>(i => i.EngineStartStop());
            Anchor          |= InputProvider.CombinedInput<ShipInputProvider>(i => i.Anchor());

            modifyInputCallback.Invoke();
        }
    }
}

#if UNITY_EDITOR
namespace NWH.DWP2.WaterObjects
{
    /// <summary>
    /// Property drawer for Input.
    /// </summary>
    [CustomPropertyDrawer(typeof(ShipInputHandler))]
    public class ShipInputHandlerDrawer : NUIPropertyDrawer
    {
        public override bool OnNUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (!base.OnNUI(position, property, label))
            {
                return false;
            }

            drawer.Field("autoSetInput");
            drawer.Field("states");

            drawer.EndProperty();
            return true;
        }
    }
}

#endif