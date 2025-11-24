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
    /// Applies wind drag forces to the hull of a sailing vessel based on the global wind conditions.
    /// Calculates the projected area of the hull facing the wind and applies an appropriate drag force.
    /// Does not account for the waterline - dimensions should represent only the hull portion above water.
    /// Requires a WindGenerator to be present in the scene.
    /// </summary>
    public class HullWindApplicator : MonoBehaviour
    {
        /// <summary>
        /// Physical dimensions of the hull above the waterline.
        /// X represents width, Y represents height, Z represents length.
        /// These dimensions are used to calculate the projected area when wind hits the hull from different angles.
        /// </summary>
        [Tooltip("The dimensions of the object (x = width, y = height, z = length).")]
        [SerializeField]
        public Vector3 dimensions;

        /// <summary>
        /// Drag coefficient applied to the wind force calculation.
        /// Higher values result in stronger wind resistance.
        /// Typical values range from 0.5 to 2.0 depending on hull shape and surface characteristics.
        /// </summary>
        [Tooltip("The drag coefficient of the object.")]
        [SerializeField]
        public float dragCoefficient = 1.0f;

        private Rigidbody _rb;


        private void Start()
        {
            _rb = GetComponentInParent<Rigidbody>();
            Debug.Assert(_rb != null, "Rigidbody not found on self or parents.");
        }


        private void FixedUpdate()
        {
            if (WindGenerator.Instance == null)
            {
                return;
            }

            // Calculate the frontal area of the object facing the wind
            Vector3 windDirection = WindGenerator.Instance.CurrentWind.normalized;
            float   projectedArea = Mathf.Abs(Vector3.Dot(dimensions, windDirection));

            // Calculate the wind force
            float   windSpeed          = WindGenerator.Instance.CurrentWind.magnitude;
            float   windForceMagnitude = 0.5f * dragCoefficient * projectedArea * Mathf.Pow(windSpeed, 2);
            Vector3 windForce          = windDirection * windForceMagnitude;

            // Apply the wind force to the Rigidbody
            _rb.AddForce(windForce);
        }
    }
}

#if UNITY_EDITOR
namespace NWH.DWP2.SailController
{
    [CustomEditor(typeof(HullWindApplicator))]
    [CanEditMultipleObjects]
    public class HullWindApplicatorEditor : DWP2NUIEditor
    {
        public override bool OnInspectorNUI()
        {
            if (!base.OnInspectorNUI())
            {
                return false;
            }

            drawer.Field("dimensions");
            drawer.Field("dragCoefficient");

            drawer.EndEditor(this);
            return true;
        }
    }
}
#endif