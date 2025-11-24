// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using NWH.DWP2.WaterObjects;
using UnityEngine;

#endregion

namespace NWH.DWP2.WaterData
{
    /// <summary>
    /// Base class for providing water surface data to WaterObjects.
    /// Implementations provide water height, flow velocity, and surface normal information.
    /// Uses a trigger collider to automatically detect and register WaterObjects that enter the water.
    /// Inherit from this class to create adapters for different water systems.
    /// </summary>
    public abstract class WaterDataProvider : MonoBehaviour
    {
        protected float[]   _singleHeightArray;
        protected Vector3[] _singlePointArray;
        protected Collider  _triggerCollider;


        public virtual void Awake()
        {
            _singleHeightArray = new float[1];
            _singlePointArray  = new Vector3[1];

            _triggerCollider = GetComponent<Collider>();
            if (_triggerCollider == null)
            {
                // Debug.LogWarning("WaterDataProvider requires a Collider with 'Is Trigger' ticked to be present " +
                //                  "on the same GameObject to act as a trigger volume. Creating one.");
                _triggerCollider                          = gameObject.AddComponent<SphereCollider>();
                _triggerCollider.isTrigger                = true;
                ((SphereCollider)_triggerCollider).radius = 1000000f;
            }

            if (!_triggerCollider.isTrigger)
            {
                Debug.LogWarning("WaterDataProvider Collider has to have 'Is Trigger' ticked. Fixing.");
                _triggerCollider.isTrigger = true;
            }
        }


        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(transform.position, 0.1f);
        }


        private void OnTriggerEnter(Collider other)
        {
            // Assign the current water data provider
            Rigidbody targetRigidbody = other.attachedRigidbody;
            if (targetRigidbody != null)
            {
                WaterObject[] targetWaterObjects = targetRigidbody.GetComponentsInChildren<WaterObject>();
                for (int i = 0; i < targetWaterObjects.Length; i++)
                {
                    targetWaterObjects[i].OnEnterWaterDataProvider(this);
                }
            }
        }


        private void OnTriggerExit(Collider other)
        {
            // Assign the current water data provider
            Rigidbody targetRigidbody = other.attachedRigidbody;
            if (targetRigidbody != null)
            {
                WaterObject[] targetWaterObjects = targetRigidbody.GetComponentsInChildren<WaterObject>();
                for (int i = 0; i < targetWaterObjects.Length; i++)
                {
                    targetWaterObjects[i].OnExitWaterDataProvider(this);
                }
            }
        }


        /// <summary>
        /// Does this water system support water height queries?
        /// </summary>
        /// <returns> True if it does, false if it does not. </returns>
        public abstract bool SupportsWaterHeightQueries();


        /// <summary>
        /// Does this water system support water normal queries?
        /// </summary>
        /// <returns> True if it does, false if it does not. </returns>
        public abstract bool SupportsWaterNormalQueries();


        /// <summary>
        /// Does this water system support water velocity queries?
        /// </summary>
        /// <returns> True if it does, false if it does not. </returns>
        public abstract bool SupportsWaterFlowQueries();


        /// <summary>
        /// Returns water height at each given point.
        /// Override this method to provide water height data from your water system.
        /// </summary>
        /// <param name="waterObject">WaterObject requesting the data.</param>
        /// <param name="points">Position array in world coordinates.</param>
        /// <param name="waterHeights">Water height array in world coordinates. Corresponds to positions.</param>
        public virtual void GetWaterHeights(WaterObject waterObject, ref Vector3[] points, ref float[] waterHeights)
        {
            // Do nothing. This will use the initial values of water heights (0).
        }


        /// <summary>
        /// Returns water flow velocity at each given point.
        /// Override this method to provide water flow data from your water system.
        /// Flow should be in world space and relative to the world, not the WaterObject.
        /// </summary>
        /// <param name="waterObject">WaterObject requesting the data.</param>
        /// <param name="points">Position array in world coordinates.</param>
        /// <param name="waterFlows">Water flow velocity array in world coordinates. Corresponds to positions.</param>
        public virtual void GetWaterFlows(WaterObject waterObject, ref Vector3[] points, ref Vector3[] waterFlows)
        {
            // Do nothing. This will use the initial values of water velocities (0,0,0).
        }


        /// <summary>
        /// Returns water surface normals at each given point.
        /// Override this method to provide water normal data from your water system.
        /// </summary>
        /// <param name="waterObject">WaterObject requesting the data.</param>
        /// <param name="points">Position array in world coordinates.</param>
        /// <param name="waterNormals">Water surface normal array in world coordinates. Corresponds to positions.</param>
        public virtual void GetWaterNormals(WaterObject waterObject, ref Vector3[] points, ref Vector3[] waterNormals)
        {
            // Do nothing. This will use the initial values of water normals (0,0,0).
        }


        /// <summary>
        /// Queries water data based on enabled flags.
        /// Calls the appropriate getter methods based on which data types are requested.
        /// </summary>
        /// <param name="waterObject">WaterObject requesting the data.</param>
        /// <param name="points">Position array in world coordinates.</param>
        /// <param name="waterHeights">Output water heights array.</param>
        /// <param name="waterFlows">Output water flows array.</param>
        /// <param name="waterNormals">Output water normals array.</param>
        /// <param name="useWaterHeight">Should water heights be queried.</param>
        /// <param name="useWaterNormals">Should water normals be queried.</param>
        /// <param name="useWaterFlow">Should water flows be queried.</param>
        public void GetWaterHeightsFlowsNormals(WaterObject waterObject, ref Vector3[] points, ref float[] waterHeights,
            ref Vector3[] waterFlows, ref Vector3[] waterNormals, bool useWaterHeight, bool useWaterNormals,
            bool useWaterFlow)
        {
            if (useWaterHeight)
            {
                GetWaterHeights(waterObject, ref points, ref waterHeights);
            }

            if (useWaterFlow && SupportsWaterFlowQueries())
            {
                GetWaterFlows(waterObject, ref points, ref waterFlows);
            }

            if (useWaterNormals && SupportsWaterNormalQueries())
            {
                GetWaterNormals(waterObject, ref points, ref waterNormals);
            }
        }


        /// <summary>
        /// Returns water height at a single point.
        /// Less efficient than batch queries but useful for one-off checks.
        /// </summary>
        /// <param name="waterObject">WaterObject requesting the data.</param>
        /// <param name="point">Position in world coordinates.</param>
        /// <returns>Water height at the given point.</returns>
        public virtual float GetWaterHeightSingle(WaterObject waterObject, Vector3 point)
        {
            _singlePointArray[0] = point;
            GetWaterHeights(waterObject, ref _singlePointArray, ref _singleHeightArray);
            return _singleHeightArray[0];
        }


        /// <summary>
        /// Checks if a point is underwater.
        /// Accounts for wave height when water normals are supported.
        /// </summary>
        /// <param name="waterObject">WaterObject making the query.</param>
        /// <param name="worldPoint">Position to check in world coordinates.</param>
        /// <returns>True if the point is below the water surface.</returns>
        public bool PointInWater(WaterObject waterObject, Vector3 worldPoint)
        {
            return GetWaterHeight(waterObject, worldPoint) > worldPoint.y;
        }


        /// <summary>
        /// Returns water height at the given world position.
        /// </summary>
        /// <param name="waterObject">WaterObject making the query.</param>
        /// <param name="worldPoint">Position in world coordinates.</param>
        /// <returns>Water surface height at the given point.</returns>
        public float GetWaterHeight(WaterObject waterObject, Vector3 worldPoint)
        {
            return GetWaterHeightSingle(waterObject, worldPoint);
        }
    }
}