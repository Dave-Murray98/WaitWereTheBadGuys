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

namespace NWH.DWP2
{
    /// <summary>
    /// Simple utility script that enables Unity fog when the main camera goes underwater.
    /// Attach this script to any GameObject in the scene and assign the water surface transform.
    /// </summary>
    public class UnderwaterFog : MonoBehaviour
    {
        /// <summary>
        /// Transform of the water surface. Used to determine if camera is underwater.
        /// </summary>
        public Transform waterTransform;


        private void Update()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            if (mainCamera.transform.position.y < waterTransform.position.y + 0.001f)
            {
                RenderSettings.fog = true;
            }
            else
            {
                RenderSettings.fog = false;
            }
        }
    }
}