// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using UnityEditor;
using UnityEngine;

#endregion

namespace NWH.Common.Demo
{
    /// <summary>
    /// Sets Time.fixedDeltaTime to a specific value for demo scenes.
    /// Default is 0.008333s (120Hz) for optimal physics performance.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class DemoDtSetter : MonoBehaviour
    {
        /// <summary>
        /// Target physics update rate in seconds. Default 0.008333s equals 120Hz.
        /// </summary>
        public float fixedDeltaTime = 0.008333f; // 120Hz default.


        private void Awake()
        {
            #if UNITY_EDITOR
            if (fixedDeltaTime <= 0.0001f)
            {
                return;
            }

            if (Time.fixedDeltaTime <= 0.0001f)
            {
                return;
            }

            if (!EditorPrefs.GetBool("DemoDtSetter Warning"))
            {
                Debug.Log(
                    $"[Show Once] DemoDtSetter: Setting Time.fixedDeltaTime to {fixedDeltaTime} ({1f / fixedDeltaTime} Hz) " +
                    $"from the current {Time.fixedDeltaTime} ({1f / Time.fixedDeltaTime} Hz). " +
                    "Remove the script from the __SceneManager to disable this, but note that the Sports Car damper stiffness " +
                    "might need to be reduced to prevent jitter.");
                Time.fixedDeltaTime = fixedDeltaTime;
                EditorPrefs.SetBool("DemoDtSetter Warning", true);
            }
            #endif
        }
    }
}