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

namespace NWH.DWP2.DemoContent
{
    /// <summary>
    /// Demo script that spawns a 3D grid of cubes with WaterObjects attached.
    /// Used for stress-testing and demonstrating the buoyancy system with multiple objects.
    /// </summary>
    public class CubeGridSpawner : MonoBehaviour
    {
        /// <summary>
        /// Spacing between cubes on the Z axis.
        /// </summary>
        public float depth       = 1.1f;

        /// <summary>
        /// Spacing between cubes on the Y axis.
        /// </summary>
        public float height      = 1.1f;

        /// <summary>
        /// Spacing between cubes on the X axis.
        /// </summary>
        public float width       = 1.1f;

        /// <summary>
        /// Number of cubes to spawn along the X axis.
        /// </summary>
        public int   xResolution = 10;

        /// <summary>
        /// Number of cubes to spawn along the Y axis.
        /// </summary>
        public int   yResolution = 10;

        /// <summary>
        /// Number of cubes to spawn along the Z axis.
        /// </summary>
        public int   zResolution = 10;


        private void Start()
        {
            for (int x = 0; x < xResolution; x++)
            {
                for (int y = 0; y < yResolution; y++)
                {
                    for (int z = 0; z < zResolution; z++)
                    {
                        GameObject spawnedObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        spawnedObject.transform.position = new Vector3(x * width, y * height, z * depth);
                        Rigidbody rb = spawnedObject.AddComponent<Rigidbody>();
                        rb.mass = 200f;
                        spawnedObject.AddComponent<WaterObject>();
                    }
                }
            }
        }
    }
}