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
using NWH.DWP2.ShipController;
using UnityEditor;
#endif

#endregion

namespace NWH.DWP2.ShipController
{
    /// <summary>
    /// Ship input provider for Unity's legacy Input Manager.
    /// Handles keyboard and gamepad input through Input.GetAxis and Input.GetButton.
    /// Requires proper axis and button setup in Project Settings > Input Manager.
    /// Falls back to hardcoded keys if input bindings are not configured.
    /// </summary>
    [DisallowMultipleComponent]
    public class InputManagerShipInputProvider : ShipInputProvider
    {
        #if ENABLE_LEGACY_INPUT_MANAGER
        private static int _warningCount;


        public override float Steering()
        {
            return TryGetAxis("Steering");
        }


        public override float Throttle()
        {
            return TryGetAxis("Throttle");
        }


        public override float Throttle2()
        {
            return TryGetAxis("Throttle2");
        }


        public override float Throttle3()
        {
            return TryGetAxis("Throttle3");
        }


        public override float Throttle4()
        {
            return TryGetAxis("Throttle4");
        }


        public override float SternThruster()
        {
            return TryGetAxis("SternThruster");
        }


        public override float BowThruster()
        {
            return TryGetAxis("BowThruster");
        }


        public override float SubmarineDepth()
        {
            return TryGetAxis("SubmarineDepth");
        }


        public override bool EngineStartStop()
        {
            return TryGetButtonDown("EngineStartStop", KeyCode.E);
        }


        public override bool Anchor()
        {
            return TryGetButtonDown("Anchor", KeyCode.T);
        }


        public override Vector2 DragObjectPosition()
        {
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        }


        public override bool DragObjectModifier()
        {
            return TryGetButton("DragObjectModifier", KeyCode.LeftControl);
        }


        public override float RotateSail()
        {
            return TryGetAxis("RotateSail");
        }


        /// <summary>
        /// Attempts to read button state from Input Manager, falling back to direct key check if binding is missing.
        /// </summary>
        /// <param name="buttonName">Name of the button binding in Input Manager.</param>
        /// <param name="altKey">Fallback key to use if binding is not configured.</param>
        /// <param name="showWarning">Whether to log a warning on missing binding.</param>
        /// <returns>True if button is pressed.</returns>
        private static bool TryGetButton(string buttonName, KeyCode altKey, bool showWarning = true)
        {
            try
            {
                return Input.GetButton(buttonName);
            }
            catch
            {
                // Make sure warning is not spammed as some users tend to ignore the warning and never set up the input,
                // resulting in bad performance in editor.
                if (_warningCount < 100 && showWarning)
                {
                    Debug.LogWarning(buttonName +
                                     " input binding missing, falling back to default. Check Input section in manual for more info.");
                    _warningCount++;
                }

                return Input.GetKey(altKey);
            }
        }


        /// <summary>
        /// Attempts to read button down state from Input Manager, falling back to direct key check if binding is missing.
        /// </summary>
        /// <param name="buttonName">Name of the button binding in Input Manager.</param>
        /// <param name="altKey">Fallback key to use if binding is not configured.</param>
        /// <param name="showWarning">Whether to log a warning on missing binding.</param>
        /// <returns>True if button was pressed this frame.</returns>
        private static bool TryGetButtonDown(string buttonName, KeyCode altKey, bool showWarning = true)
        {
            try
            {
                return Input.GetButtonDown(buttonName);
            }
            catch
            {
                if (_warningCount < 100 && showWarning)
                {
                    Debug.LogWarning(buttonName +
                                     " input binding missing, falling back to default. Check Input section in manual for more info.");
                    _warningCount++;
                }

                return Input.GetKeyDown(altKey);
            }
        }


        /// <summary>
        /// Attempts to read axis value from Input Manager, returning 0 if binding is missing.
        /// </summary>
        /// <param name="axisName">Name of the axis binding in Input Manager.</param>
        /// <param name="showWarning">Whether to log a warning on missing binding.</param>
        /// <returns>Axis value between -1 and 1, or 0 if binding is missing.</returns>
        private static float TryGetAxis(string axisName, bool showWarning = true)
        {
            try
            {
                return Input.GetAxis(axisName);
            }
            catch
            {
                if (_warningCount < 100 && showWarning)
                {
                    Debug.LogWarning(axisName +
                                     " input binding missing. Check Input section in manual for more info.");
                    _warningCount++;
                }
            }

            return 0;
        }


        /// <summary>
        /// Attempts to read raw axis value from Input Manager, returning 0 if binding is missing.
        /// Raw values are unsmoothed and return -1, 0, or 1 immediately.
        /// </summary>
        /// <param name="axisName">Name of the axis binding in Input Manager.</param>
        /// <param name="showWarning">Whether to log a warning on missing binding.</param>
        /// <returns>Raw axis value, or 0 if binding is missing.</returns>
        private static float TryGetAxisRaw(string axisName, bool showWarning = true)
        {
            try
            {
                return Input.GetAxisRaw(axisName);
            }
            catch
            {
                if (_warningCount < 100 && showWarning)
                {
                    Debug.LogWarning(axisName +
                                     " input binding missing. Check Input section in manual for more info.");
                    _warningCount++;
                }
            }

            return 0;
        }
        #endif
    }
}

#if UNITY_EDITOR
namespace NWH.DWP2.WaterObjects
{
    [CustomEditor(typeof(InputManagerShipInputProvider))]
    public class InputManagerShipInputProviderEditor : DWP2NUIEditor
    {
        public override bool OnInspectorNUI()
        {
            if (!base.OnInspectorNUI())
            {
                return false;
            }

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