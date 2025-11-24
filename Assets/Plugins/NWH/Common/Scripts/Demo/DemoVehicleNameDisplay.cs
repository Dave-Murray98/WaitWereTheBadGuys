// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using System.Collections;
using NWH.Common.Vehicles;
using UnityEngine;
using UnityEngine.UI;

#endregion

namespace NWH.Common.Demo
{
    /// <summary>
    /// Displays the name and type of the currently active vehicle in a Text component.
    /// Updates every 0.1 seconds.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class DemoVehicleNameDisplay : MonoBehaviour
    {
        private Text vehicleText;


        private void Awake()
        {
            vehicleText = GetComponent<Text>();
            StartCoroutine(VehicleNameCoroutine());
        }


        private IEnumerator VehicleNameCoroutine()
        {
            while (true)
            {
                Vehicle vehicle = Vehicle.ActiveVehicle;
                if (vehicle != null)
                {
                    vehicleText.text = $"{vehicle.name} [{vehicle.GetType().Name}]";
                }
                else
                {
                    vehicleText.text = "[no active vehicle]";
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