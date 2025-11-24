// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using NWH.Common.Input;
using NWH.Common.Vehicles;
using NWH.DWP2.ShipController;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#endregion

namespace NWH.DWP2.DemoContent
{
    public class GUIHandler : MonoBehaviour
    {
        public Image anchorImage;
        public bool  reset;
        public Text  rudderText;
        public Text  speedText;

        private AdvancedShipController activeShip;


        private void Update()
        {
            bool toggleGUI = InputProvider
               .CombinedInput<SceneInputProviderBase>(i => i.ToggleGUI());
            if (toggleGUI)
            {
                Canvas canvas = GetComponent<Canvas>();
                if (canvas != null)
                {
                    canvas.enabled = !canvas.enabled;
                }
            }

            activeShip = Vehicle.ActiveVehicle as AdvancedShipController;
            if (activeShip != null)
            {
                float speed = activeShip.SpeedKnots;
                speedText.text = "SPEED: " + $"{speed:0.0}" + "kts";

                if (activeShip.rudders.Count > 0)
                {
                    float rudderAngle = activeShip.rudders[0].Angle;
                    rudderText.text = "RUDDER: " + $"{rudderAngle:0.0}" + "°";
                }

                if (activeShip.Anchor != null)
                {
                    if (activeShip.Anchor.Dropped)
                    {
                        anchorImage.gameObject.SetActive(true);
                    }
                    else
                    {
                        anchorImage.gameObject.SetActive(false);
                    }
                }
            }
        }


        public void ResetScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.name);
        }
    }
}