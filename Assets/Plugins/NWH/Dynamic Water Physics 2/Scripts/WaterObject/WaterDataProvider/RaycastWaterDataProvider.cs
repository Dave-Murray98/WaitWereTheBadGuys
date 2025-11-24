// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using NWH.DWP2.WaterObjects;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

#endregion

namespace NWH.DWP2.WaterData
{
    /// <summary>
    /// Water data provider that uses raycasting to detect water surface.
    /// Works with any water system that has a collider.
    /// Supports water height and normal queries through raycasting.
    /// Uses Unity's job system for parallel raycasts.
    /// </summary>
    public class RaycastWaterDataProvider : WaterDataProvider
    {
        /// <summary>
        /// Number of raycast commands to process per job batch.
        /// Higher values may improve performance on systems with more CPU cores.
        /// </summary>
        [Tooltip("Minimum number of RaycastCommands per job.")]
        public int commandsPerJob = 16;

        /// <summary>
        /// Layer that floating objects are on.
        /// Used to prevent physics collisions between water and objects.
        /// </summary>
        [Tooltip(
            "    Layer the water object(s) are on. Required to be able to disable physics collisions between water and object.")]
        public int objectLayer = 12;

        /// <summary>
        /// Maximum distance raycasts can travel in each direction.
        /// Raycasts extend this distance above and below each point.
        /// Lower values improve performance but may miss water surfaces that are far from the object.
        /// </summary>
        [Tooltip(
            "    Raycasts will start at this distance above the point and extend this distance below the point. This means\r\n    that if the water surface is raycastDistance below or above the point, it will not be detected.\r\n    Using lower value will slightly improve performance of Raycasts.")]
        public float raycastDistance = 100f;

        /// <summary>
        /// Layer that water surface colliders are on.
        /// Used to filter raycasts and prevent physics collisions.
        /// </summary>
        [Tooltip(
            "    Layer the water is on. Required to be able to disable physics collisions between water and object.")]
        public int waterLayer = 4;

        protected Vector3    _flow;
        protected RaycastHit _hit;
        protected LayerMask  _layerMask;
        protected Mesh       _mesh;

        protected Vector3[]       _normals;
        protected int             _prevDataSize;
        protected QueryParameters _queryParameters;
        protected Ray             _ray;

        protected NativeArray<RaycastCommand> _raycastCommands;
        protected NativeArray<RaycastHit>     _raycastHits;
        protected JobHandle                   _raycastJobHandle;
        protected Vector3                     _rayDirection;
        protected Vector3                     _rayStartOffset;
        protected Vector3                     _tmp;
        protected RaycastCommand              _tmpCommand;
        protected Vector3                     _upVector;
        protected Vector2                     _uv4;
        protected Vector3                     _vertDir;
        protected int                         _vertIndex;
        protected Vector3                     _zeroVector;


        public override void Awake()
        {
            base.Awake();
            Physics.IgnoreLayerCollision(waterLayer, objectLayer);
            _rayDirection   = -Vector3.up;
            _rayStartOffset = -_rayDirection * raycastDistance * 0.5f;
            _prevDataSize   = -1;
            _zeroVector     = Vector3.zero;
            _upVector       = Vector3.up;

            _queryParameters = new QueryParameters();
        }


        public override bool SupportsWaterHeightQueries()
        {
            return true;
        }


        public override bool SupportsWaterNormalQueries()
        {
            return true;
        }


        public override bool SupportsWaterFlowQueries()
        {
            return false;
        }


        public override void GetWaterHeights(WaterObject waterObject, ref Vector3[] points, ref float[] waterHeights)
        {
            int n = points.Length;

            bool queriesHitBackfaces = Physics.queriesHitBackfaces;
            bool queriesHitTriggers  = Physics.queriesHitTriggers;

            try
            {
                Physics.queriesHitBackfaces = false;
                Physics.queriesHitTriggers  = false;

                if (n != _prevDataSize)
                {
                    _normals = new Vector3[n];
                    Deallocate();
                    _raycastCommands = new NativeArray<RaycastCommand>(n, Allocator.Persistent);
                    _raycastHits     = new NativeArray<RaycastHit>(n, Allocator.Persistent);
                }

                _queryParameters.layerMask = 1 << waterLayer;

                for (int i = 0; i < n; i++)
                {
                    _tmpCommand.from            = points[i] + _rayStartOffset;
                    _tmpCommand.direction       = _rayDirection;
                    _tmpCommand.distance        = raycastDistance;
                    _tmpCommand.queryParameters = _queryParameters;
                    _raycastCommands[i]         = _tmpCommand;
                }

                // Schedule raycast batch (removed duplicate scheduling that wasted 50% CPU time)
                _raycastJobHandle = RaycastCommand.ScheduleBatch(_raycastCommands, _raycastHits, 16);
                _raycastJobHandle.Complete();

                Vector3 hitNormal;
                for (int i = 0; i < n; i++)
                {
                    hitNormal       = _raycastHits[i].normal;
                    waterHeights[i] = _raycastHits[i].point.y;
                    _normals[i]     = hitNormal == _zeroVector ? _upVector : hitNormal;
                }
            }
            finally
            {
                // Always restore global physics settings, even if an exception occurs
                Physics.queriesHitBackfaces = queriesHitBackfaces;
                Physics.queriesHitTriggers  = queriesHitTriggers;
            }

            _prevDataSize = n;
        }


        public override void GetWaterNormals(WaterObject waterObject, ref Vector3[] points, ref Vector3[] waterNormals)
        {
            waterNormals = _normals;
        }


        public virtual void OnDisable()
        {
            Deallocate();
        }


        public virtual void OnDestroy()
        {
            Deallocate();
        }


        /// <summary>
        /// Deallocates native arrays used for raycasting.
        /// Called automatically on disable or destroy.
        /// </summary>
        public virtual void Deallocate()
        {
            _raycastJobHandle.Complete();
            if (_raycastCommands.IsCreated)
            {
                _raycastCommands.Dispose();
            }

            if (_raycastHits.IsCreated)
            {
                _raycastHits.Dispose();
            }
        }
    }
}