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
    /// Utility methods for safe input retrieval with automatic fallback to default keys.
    /// Prevents errors when Input Manager bindings are missing.
    /// </summary>
    public class InputUtils
    {
        private static int _warningCount;


        /// <summary>
        /// Attempts to retrieve button state from Input Manager, falls back to KeyCode if binding is missing.
        /// </summary>
        /// <param name="buttonName">Input Manager button name to query.</param>
        /// <param name="altKey">Fallback KeyCode to use if binding is missing.</param>
        /// <param name="showWarning">Display warning message when falling back to default key.</param>
        /// <returns>True if button is currently held down.</returns>
        public static bool TryGetButton(string buttonName, KeyCode altKey, bool showWarning = true)
        {
            try
            {
                return UnityEngine.Input.GetButton(buttonName);
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

                return UnityEngine.Input.GetKey(altKey);
            }
        }


        /// <summary>
        /// Attempts to retrieve button press from Input Manager, falls back to KeyCode if binding is missing.
        /// </summary>
        /// <param name="buttonName">Input Manager button name to query.</param>
        /// <param name="altKey">Fallback KeyCode to use if binding is missing.</param>
        /// <param name="showWarning">Display warning message when falling back to default key.</param>
        /// <returns>True on the frame the button was pressed.</returns>
        public static bool TryGetButtonDown(string buttonName, KeyCode altKey, bool showWarning = true)
        {
            try
            {
                return UnityEngine.Input.GetButtonDown(buttonName);
            }
            catch
            {
                if (_warningCount < 100 && showWarning)
                {
                    Debug.LogWarning(buttonName +
                                     " input binding missing, falling back to default. Check Input section in manual for more info.");
                    _warningCount++;
                }

                return UnityEngine.Input.GetKeyDown(altKey);
            }
        }


        /// <summary>
        /// Attempts to retrieve axis value from Input Manager, returns 0 if binding is missing.
        /// </summary>
        /// <param name="axisName">Input Manager axis name to query.</param>
        /// <param name="showWarning">Display warning message when axis is missing.</param>
        /// <returns>Axis value between -1 and 1, or 0 if binding is missing.</returns>
        public static float TryGetAxis(string axisName, bool showWarning = true)
        {
            try
            {
                return UnityEngine.Input.GetAxis(axisName);
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
        /// Attempts to retrieve raw axis value from Input Manager, returns 0 if binding is missing.
        /// Raw axes return only -1, 0, or 1 without smoothing.
        /// </summary>
        /// <param name="axisName">Input Manager axis name to query.</param>
        /// <param name="showWarning">Display warning message when axis is missing.</param>
        /// <returns>Raw axis value (-1, 0, or 1), or 0 if binding is missing.</returns>
        public static float TryGetAxisRaw(string axisName, bool showWarning = true)
        {
            try
            {
                return UnityEngine.Input.GetAxisRaw(axisName);
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
    }
}