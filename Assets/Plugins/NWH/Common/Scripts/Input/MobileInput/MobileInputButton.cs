// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#endregion

namespace NWH.Common.Input
{
    /// <summary>
    /// Extended Unity UI Button with state tracking for mobile input handling.
    /// Provides hasBeenClicked and isPressed flags for easier input polling.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class MobileInputButton : Button
    {
        /// <summary>
        /// True for one frame after the button is clicked. Automatically resets to false.
        /// </summary>
        public bool hasBeenClicked;

        /// <summary>
        /// True while the button is being held down. Updates every frame.
        /// </summary>
        public bool isPressed;


        private void Update()
        {
            isPressed      = IsPressed();
            hasBeenClicked = false;
        }


        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);

            hasBeenClicked = true;
        }
    }
}