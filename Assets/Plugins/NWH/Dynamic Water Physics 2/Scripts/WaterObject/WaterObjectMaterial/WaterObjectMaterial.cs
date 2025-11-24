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

namespace NWH.DWP2.WaterObjects
{
    /// <summary>
    /// ScriptableObject defining material properties for mass calculation.
    /// Contains density value used by MassFromVolume to calculate object mass.
    /// Create via Assets > Create > Dynamic Water Physics 2 > Water Object Material.
    /// </summary>
    [CreateAssetMenu(fileName = "WaterObjectMaterial", menuName = "Dynamic Water Physics 2/Water Object Material",
                     order = 0)]
    public class WaterObjectMaterial : ScriptableObject
    {
        /// <summary>
        /// Material density in kg/m³.
        /// Used with mesh volume to calculate realistic object mass.
        /// Examples: Wood ~600, Ice ~920, Aluminum ~2700, Steel ~7850.
        /// </summary>
        public float density = 600;
    }
}