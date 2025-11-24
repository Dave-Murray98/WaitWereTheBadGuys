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
    /// Script to calculate mass from mesh volume and material density.
    /// Removes need for guessing mass and ensures that all objects of same density are submerged equally.
    /// </summary>
    [RequireComponent(typeof(WaterObject))]
    public class MassFromVolume : MonoBehaviour
    {
        /// <summary>
        /// Calculated density of the material in kg/m³.
        /// </summary>
        public float               density;

        /// <summary>
        /// Calculated mass of the object in kg.
        /// </summary>
        public float               mass;

        /// <summary>
        /// Material preset containing density value.
        /// </summary>
        public WaterObjectMaterial material;

        /// <summary>
        /// Calculated volume of the simulation mesh in m³.
        /// </summary>
        public float               volume;

        private WaterObject _waterObject;


        private void Awake()
        {
            _waterObject = GetComponent<WaterObject>();
            if (_waterObject == null)
            {
                Debug.LogError("MassFromVolume requires WaterObject.");
            }

            if (material == null)
            {
                SetDefaultAsMaterial();
            }
        }


        /// <summary>
        /// Sets the default WaterObjectMaterial from Resources.
        /// </summary>
        public void SetDefaultAsMaterial()
        {
            material = Resources.Load<WaterObjectMaterial>("Dynamic Water Physics 2/DefaultWaterObjectMaterial");
        }


        private void Reset()
        {
            SetDefaultAsMaterial();
        }


        /// <summary>
        /// Gets volume of the simulation mesh. Scale-sensitive.
        /// </summary>
        public void CalculateSimulationMeshVolume()
        {
            if (_waterObject == null)
            {
                _waterObject = GetComponent<WaterObject>();
            }

            if (_waterObject.SimulationMesh == null)
            {
                Debug.LogWarning(
                    "No simulation mesh assigned/generated. Make sure that simulation mesh of WaterObject is not empty - " +
                    "if this is the first time setup try clicking 'Update Simulation Mesh' on WaterObject.");
            }

            volume = _waterObject.SimulationMesh == null
                         ? 0.00000001f
                         : Mathf.Clamp(MeshUtility.VolumeOfMesh(_waterObject.SimulationMesh, _waterObject.transform),
                                       0f, Mathf.Infinity);
        }


        /// <summary>
        /// Calculates mass from the assigned material's density and applies it to the Rigidbody.
        /// </summary>
        /// <returns>Calculated mass value.</returns>
        public float CalculateAndApplyFromMaterial()
        {
            return CalculateAndApplyFromDensity(material.density);
        }


        /// <summary>
        /// Calculates mass from the given density and applies it to the Rigidbody.
        /// </summary>
        /// <param name="density">Material density in kg/m³.</param>
        /// <returns>Calculated mass value.</returns>
        public float CalculateAndApplyFromDensity(float density)
        {
            mass = -1;
            if (material != null)
            {
                if (_waterObject == null)
                {
                    _waterObject = GetComponent<WaterObject>();
                }

                CalculateSimulationMeshVolume();

                mass = density * volume;
                if (_waterObject.targetRigidbody != null && mass > 0)
                {
                    _waterObject.targetRigidbody.mass = mass;
                }
            }

            return mass;
        }
    }
}