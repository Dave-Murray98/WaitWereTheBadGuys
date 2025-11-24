// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using UnityEngine;
using UnityEngine.UI;

#endregion

namespace NWH.Common.Demo
{
    /// <summary>
    /// Controls the display of a welcome message panel in demo scenes.
    /// Shows the message when running outside the editor.
    /// </summary>
    public class DemoWelcomeMessage : MonoBehaviour
    {
        /// <summary>
        /// Button used to close the welcome message panel.
        /// </summary>
        public Button     closeButton;

        /// <summary>
        /// GameObject containing the welcome message UI.
        /// </summary>
        public GameObject welcomeMessageGO;


        private void Start()
        {
            if (!Application.isEditor)
            {
                welcomeMessageGO.SetActive(true);
            }

            closeButton.onClick.AddListener(Close);
        }


        private void Close()
        {
            welcomeMessageGO.SetActive(false);
        }
    }
}