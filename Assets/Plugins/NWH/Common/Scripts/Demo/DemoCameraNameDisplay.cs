// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

#endregion

namespace NWH.Common.Demo
{
    /// <summary>
    /// Displays the name of the currently active main camera in a Text component.
    /// Updates every 0.1 seconds.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class DemoCameraNameDisplay : MonoBehaviour
    {
        private Text cameraText;


        private void Awake()
        {
            cameraText = GetComponent<Text>();
            StartCoroutine(CameraNameCoroutine());
        }


        private IEnumerator CameraNameCoroutine()
        {
            while (true)
            {
                Camera cameraMain = Camera.main;
                if (cameraMain != null)
                {
                    cameraText.text = cameraMain.name;
                }
                else
                {
                    cameraText.text = "[no main camera]";
                }

                yield return new WaitForSeconds(0.1f);
            }
        }


        private void OnDestroy()
        {
            StopAllCoroutines();
        }
    }
}