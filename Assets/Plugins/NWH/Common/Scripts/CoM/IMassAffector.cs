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

namespace NWH.Common.CoM
{
    /// <summary>
    /// Interface for objects that contribute mass and affect vehicle center of mass calculations.
    /// Implemented by fuel tanks, cargo systems, and other variable mass components.
    /// </summary>
    /// <remarks>
    /// Mass affectors allow dynamic vehicle physics by contributing their mass and position
    /// to the overall center of mass calculation. As fuel depletes or cargo loads change,
    /// the vehicle's handling characteristics update automatically.
    /// </remarks>
    public interface IMassAffector
    {
        /// <summary>
        /// Current mass of this affector in kilograms.
        /// Should return variable values for fuel tanks, cargo, etc.
        /// </summary>
        /// <returns>Mass in kg</returns>
        float GetMass();

        /// <summary>
        /// World position of this affector's center of mass.
        /// Used for weighted center of mass calculations.
        /// </summary>
        Vector3 GetWorldCenterOfMass();


        /// <summary>
        /// Returns transform of the mass affector.
        /// </summary>
        Transform GetTransform();
    }
}