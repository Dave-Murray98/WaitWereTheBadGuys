// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using System;
using UnityEngine;
#if UNITY_EDITOR
using NWH.NUI;
using NWH.DWP2.ShipController;
using UnityEditor;
#endif

#endregion

namespace NWH.DWP2.ShipController
{
    /// <summary>
    /// Container for all ship input states.
    /// Stores current values for all ship controls including throttle, steering, thrusters, and special inputs.
    /// </summary>
    [Serializable]
    public struct ShipInputStates
    {
        /// <summary>
        /// Steering input from -1 (port/left) to 1 (starboard/right).
        /// </summary>
        [Range(-1, 1)]
        public float steering;

        /// <summary>
        /// Primary throttle from -1 (full reverse) to 1 (full forward).
        /// </summary>
        [Range(-1, 1)]
        public float throttle;

        /// <summary>
        /// Secondary throttle for independent engine control.
        /// </summary>
        [Range(-1, 1)]
        public float throttle2;

        /// <summary>
        /// Tertiary throttle for independent engine control.
        /// </summary>
        [Range(-1, 1)]
        public float throttle3;

        /// <summary>
        /// Quaternary throttle for independent engine control.
        /// </summary>
        [Range(-1, 1)]
        public float throttle4;

        /// <summary>
        /// Stern thruster input from -1 to 1.
        /// </summary>
        [Range(-1, 1)]
        public float sternThruster;

        /// <summary>
        /// Bow thruster input from -1 to 1.
        /// </summary>
        [Range(-1, 1)]
        public float bowThruster;

        /// <summary>
        /// Submarine depth control from 0 (surface) to 1 (dive).
        /// </summary>
        [Range(0, 1)]
        public float submarineDepth;

        /// <summary>
        /// Sail rotation input for sailing vessels.
        /// </summary>
        [Range(-1, 1)]
        public float rotateSail;

        /// <summary>
        /// Engine start/stop toggle state.
        /// </summary>
        public bool engineStartStop;

        /// <summary>
        /// Anchor drop/weigh toggle state.
        /// </summary>
        public bool anchor;

        /// <summary>
        /// Change ship input for demo scenes.
        /// </summary>
        public bool changeShip;

        /// <summary>
        /// Change camera input for demo scenes.
        /// </summary>
        public bool changeCamera;


        /// <summary>
        /// Resets all input states to default values.
        /// </summary>
        public void Reset()
        {
            throttle        = 0;
            throttle2       = 0;
            throttle3       = 0;
            throttle4       = 0;
            sternThruster   = 0;
            bowThruster     = 0;
            submarineDepth  = 0;
            engineStartStop = false;
            anchor          = false;
        }
    }
}

#if UNITY_EDITOR
namespace NWH.DWP2.WaterObjects
{
    /// <summary>
    /// Property drawer for InputStates.
    /// </summary>
    [CustomPropertyDrawer(typeof(ShipInputStates))]
    public class ShipInputStatesDrawer : NUIPropertyDrawer
    {
        public override bool OnNUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (!base.OnNUI(position, property, label))
            {
                return false;
            }

            drawer.Field("steering");
            drawer.Field("throttle");
            drawer.Field("throttle2");
            drawer.Field("throttle3");
            drawer.Field("throttle4");
            drawer.Field("bowThruster");
            drawer.Field("sternThruster");
            drawer.Field("submarineDepth");
            drawer.Field("rotateSail");
            drawer.Field("engineStartStop");
            drawer.Field("anchor");
            EditorGUI.EndDisabledGroup();

            drawer.EndProperty();
            return true;
        }
    }
}

#endif