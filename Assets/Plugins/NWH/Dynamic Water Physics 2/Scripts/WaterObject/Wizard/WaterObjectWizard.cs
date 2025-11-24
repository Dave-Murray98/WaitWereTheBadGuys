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
    /// Configuration for the WaterObject setup wizard.
    /// Used by the editor to automate WaterObject component setup.
    /// </summary>
    public class WaterObjectWizard : MonoBehaviour
    {
        /// <summary>
        /// Should the wizard add a WaterParticleSystem component for splash effects.
        /// </summary>
        public bool addWaterParticleSystem;
    }
}