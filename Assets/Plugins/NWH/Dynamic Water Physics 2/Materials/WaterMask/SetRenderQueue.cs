// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

/*
    SetRenderQueue.cs

    Sets the RenderQueue of an object's materials on Awake. This will instance
    the materials, so the script won't interfere with other renderers that
    reference the same materials.
*/

#region

using UnityEngine;

#endregion

namespace NWH.DWP2
{
    [AddComponentMenu("Rendering/SetRenderQueue")]
    public class SetRenderQueue : MonoBehaviour
    {
        public int queue;


        protected void Awake()
        {
            Material[] materials = GetComponent<Renderer>().materials;
            for (int i = 0; i < materials.Length; ++i)
            {
                materials[i].renderQueue = queue;
            }
        }
    }
}