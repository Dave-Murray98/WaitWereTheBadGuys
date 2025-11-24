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

namespace NWH.Common.CoM
{
    /// <summary>
    /// Simple mass affector implementation that contributes a fixed mass at its transform position
    /// to the vehicle's center of mass calculations.
    /// </summary>
    /// <remarks>
    /// Use this component for static mass contributions like passengers, cargo, or equipment.
    /// For dynamic masses like fuel tanks, create a custom IMassAffector implementation that
    /// returns varying mass values.
    /// </remarks>
    public class MassAffector : MonoBehaviour, IMassAffector
    {
        /// <summary>
        /// Mass contribution of this affector in kilograms.
        /// </summary>
        public float mass = 100.0f;


        /// <summary>
        /// Returns the mass of this affector.
        /// </summary>
        /// <returns>Mass in kilograms.</returns>
        public float GetMass()
        {
            return mass;
        }


        /// <summary>
        /// Returns the transform of this mass affector.
        /// </summary>
        public Transform GetTransform()
        {
            return transform;
        }


        /// <summary>
        /// Returns the world position of this mass affector's center of mass.
        /// </summary>
        public Vector3 GetWorldCenterOfMass()
        {
            return transform.position;
        }
    }
}

#if UNITY_EDITOR
namespace NWH.Common.CoM
{
    [CustomEditor(typeof(MassAffector))]
    [CanEditMultipleObjects]
    public class MassAffectorEditor : NUIEditor
    {
        public override bool OnInspectorNUI()
        {
            if (!base.OnInspectorNUI())
            {
                return false;
            }

            drawer.Field("mass", true, "kg");

            drawer.EndEditor(this);
            return true;
        }
    }
}
#endif