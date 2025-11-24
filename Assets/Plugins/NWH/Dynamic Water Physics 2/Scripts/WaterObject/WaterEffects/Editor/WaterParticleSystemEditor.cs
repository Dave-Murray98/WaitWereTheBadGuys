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
using UnityEngine;

#endregion

namespace NWH.DWP2.WaterEffects
{
    /// <summary>
    /// Custom inspector for WaterParticleSystem.
    /// </summary>
    [CustomEditor(typeof(WaterParticleSystem))]
    [CanEditMultipleObjects]
    public class WaterParticleSystemEditor : DWP2NUIEditor
    {
        private WaterParticleSystem wps;


        /// <summary>
        /// Draws custom inspector GUI for WaterParticleSystem.
        /// </summary>
        public override bool OnInspectorNUI()
        {
            if (!base.OnInspectorNUI())
            {
                return false;
            }

            wps = (WaterParticleSystem)target;

            // Draw logo texture
            Rect logoRect = drawer.positionRect;
            logoRect.height = 60f;
            drawer.DrawEditorTexture(logoRect, "Dynamic Water Physics 2/Logos/WaterParticleSystemLogo");
            drawer.AdvancePosition(logoRect.height);

            drawer.BeginSubsection("Particle Settings");
            drawer.Field("emit");
            drawer.Field("renderQueue");
            drawer.Field("startSize");
            drawer.Field("sleepThresholdVelocity");
            drawer.Field("initialVelocityModifier");
            drawer.Field("maxInitialAlpha");
            drawer.Field("initialAlphaModifier");
            drawer.Field("emitPerCycle");
            drawer.Field("emitTimeInterval");
            drawer.Field("positionExtrapolationFrames");
            drawer.Field("surfaceElevation");
            drawer.EndSubsection();

            /* // TODO - move this from editor script
            if(!wps.GetComponent<ParticleSystem>())
            {
                GameObject waterParticleSystemPrefab = Resources.Load<GameObject>("Dynamic Water Physics
                2/WaterParticleSystemPrefab");
                if (waterParticleSystemPrefab == null)
                {
                    Debug.LogError("Could not load WaterParticleSystemPrefab from Resources.");
                }
                else
                {
                    UnityEditorInternal.ComponentUtility.CopyComponent(waterParticleSystemPrefab
                        .GetComponent<ParticleSystem>());
                    UnityEditorInternal.ComponentUtility.PasteComponentAsNew(wps.gameObject);
                }
            }
            */

            drawer.EndEditor(this);
            return true;
        }
    }
}

#endif