// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#if UNITY_EDITOR

#region

using NWH.NUI;
using NWH.DWP2.WaterObjects;
using UnityEditor;

#endregion

namespace NWH.DWP2
{
    /// <summary>
    /// Custom inspector for MassFromVolume.
    /// </summary>
    [CustomEditor(typeof(MassFromVolume))]
    [CanEditMultipleObjects]
    public class MassFromVolumeEditor : DWP2NUIEditor
    {
        private MassFromVolume _massFromVolume;


        public void OnEnable()
        {
            _massFromVolume = (MassFromVolume)target;
        }


        /// <summary>
        /// Draws custom inspector GUI for MassFromVolume.
        /// </summary>
        public override bool OnInspectorNUI()
        {
            if (!base.OnInspectorNUI() || _massFromVolume == null)
            {
                return false;
            }

            _massFromVolume = (MassFromVolume)target;

            // Material settings
            drawer.Field("mass",   true,  "kg");
            drawer.Field("volume", false, "m3");
            drawer.Info("Volume is auto-calculated from the mesh when either Calculate option is used.");

            drawer.BeginSubsection("Density");
            drawer.Field("density", true, "kg/m3");
            if (drawer.Button("Calculate Mass From Density"))
            {
                foreach (MassFromVolume mfm in targets)
                {
                    mfm.CalculateAndApplyFromDensity(mfm.density);
                }
            }

            drawer.EndSubsection();

            drawer.BeginSubsection("Material");
            drawer.Field("material");
            if (drawer.Button("Calculate Mass From Material"))
            {
                foreach (MassFromVolume mfm in targets)
                {
                    mfm.CalculateAndApplyFromMaterial();
                }
            }

            drawer.EndSubsection();

            drawer.EndEditor(this);
            return true;
        }
    }
}

#endif