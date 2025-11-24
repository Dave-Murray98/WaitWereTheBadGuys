// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using UnityEngine;
#if UNITY_EDITOR
using NWH.NUI;
using UnityEditor;
#endif

#endregion

namespace NWH.DWP2.SailController
{
    /// <summary>
    /// Defines the aerodynamic characteristics of a sail through lift and drag coefficient curves.
    /// Contains the relationship between angle of attack and force generation.
    /// Different sail types (square, lateen, bermuda, etc.) can be represented by different presets.
    /// Can be created via Assets > Create > NWH > DWP2 > SailPreset.
    /// </summary>
    [CreateAssetMenu(fileName = "SailPreset", menuName = "NWH/DWP2/SailPreset", order = 1)]
    public class SailPreset : ScriptableObject
    {
        /// <summary>
        /// Optional description of the sail type and its characteristics.
        /// </summary>
        public string         description;

        /// <summary>
        /// Defines how drag coefficient varies with angle of attack.
        /// X-axis represents angle of attack in degrees (-180 to 180).
        /// Y-axis represents the drag coefficient multiplier.
        /// Drag acts in the direction of the apparent wind.
        /// </summary>
        public AnimationCurve dragCoefficientVsAoACurve = new();

        /// <summary>
        /// Global multiplier for all drag forces.
        /// Values greater than 1 increase drag, values less than 1 reduce it.
        /// Does not affect the shape of the drag curve.
        /// </summary>
        public float          dragScale                 = 1f;

        /// <summary>
        /// Defines how lift coefficient varies with angle of attack.
        /// X-axis represents angle of attack in degrees (-180 to 180).
        /// Y-axis represents the lift coefficient multiplier.
        /// Lift acts perpendicular to the sail plane.
        /// </summary>
        public AnimationCurve liftCoefficientVsAoACurve = new();

        /// <summary>
        /// Global multiplier for all lift forces.
        /// Values greater than 1 increase lift, values less than 1 reduce it.
        /// Does not affect the shape of the lift curve.
        /// </summary>
        public float          liftScale                 = 1f;


        private void Reset()
        {
            liftCoefficientVsAoACurve = GetDefaultLiftCurve();
            dragCoefficientVsAoACurve = GetDefaultDragCurve();
        }


        private AnimationCurve GetDefaultDragCurve()
        {
            AnimationCurve dragCurve = new();

            for (float angle = -180f; angle <= 180f; angle += 20f)
            {
                float angleRadians     = angle * Mathf.Deg2Rad;
                float forceCoefficient = Mathf.Sin(angleRadians);
                dragCurve.AddKey(angle, forceCoefficient);
            }

            return dragCurve;
        }


        private AnimationCurve GetDefaultLiftCurve()
        {
            AnimationCurve liftCurve = new();

            for (float angle = -180f; angle <= 180f; angle += 20f)
            {
                float angleRadians     = angle * Mathf.Deg2Rad;
                float forceCoefficient = Mathf.Cos(angleRadians * 2f);
                liftCurve.AddKey(angle, forceCoefficient);
            }

            return liftCurve;
        }
    }
}

#if UNITY_EDITOR
namespace NWH.DWP2.SailController
{
    [CustomEditor(typeof(SailPreset))]
    [CanEditMultipleObjects]
    public class SailPresetEditor : DWP2NUIEditor
    {
        public override bool OnInspectorNUI()
        {
            if (!base.OnInspectorNUI())
            {
                return false;
            }

            SailPreset sailPreset = (SailPreset)target;

            EditorGUILayout.Space(30f);
            EditorGUILayout.LabelField("Description:");
            sailPreset.description = EditorGUILayout.TextArea(sailPreset.description, GUILayout.Height(60f));
            drawer.Space(100f);

            drawer.BeginSubsection("Drag");
            drawer.Field("dragScale",                 true, "x100%");
            drawer.Field("dragCoefficientVsAoACurve", true, null, "Drag Coeff. vs AoA");
            drawer.EndSubsection();

            drawer.BeginSubsection("Lift");
            drawer.Field("liftScale",                 true, "x100%");
            drawer.Field("liftCoefficientVsAoACurve", true, null, "Lift Coeff. vs AoA");
            drawer.EndSubsection();

            drawer.EndEditor(this);
            return true;
        }
    }
}
#endif