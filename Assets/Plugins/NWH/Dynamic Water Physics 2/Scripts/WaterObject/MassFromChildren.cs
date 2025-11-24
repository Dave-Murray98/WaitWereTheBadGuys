// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using NWH.Common.CoM;
using UnityEngine;

#endregion

namespace NWH.DWP2.WaterObjects
{
    /// <summary>
    /// Calculates mass of a Rigidbody from the children that have MassFromVolume script attached.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class MassFromChildren : MonoBehaviour
    {
        private Rigidbody _rb;
        private string    _result;


        /// <summary>
        /// Calculates the total mass from all child MassFromVolume components and applies it to the Rigidbody.
        /// Also updates the VariableCenterOfMass component if present.
        /// </summary>
        public void Calculate()
        {
            _rb = GetComponent<Rigidbody>();
            float massSum = 0;

            _result = "Calculated mass from: ";
            foreach (MassFromVolume mam in GetComponentsInChildren<MassFromVolume>())
            {
                massSum += mam.mass;
                _result += $"{mam.name} ({mam.mass})";
            }

            _result += $". Total mass: {massSum}.";
            Debug.Log(_result);

            if (massSum > 0.001f)
            {
                _rb.mass = massSum;

                VariableCenterOfMass vcom = GetComponent<VariableCenterOfMass>();
                if (vcom != null)
                {
                    vcom.baseMass = massSum;
                }
            }
        }
    }
}