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

namespace NWH.Common.Utility
{
    /// <summary>
    /// Extension methods for GameObject and Transform operations.
    /// </summary>
    public static class GameObjectExtensions
    {
        /// <summary>
        /// Calculates the combined bounds of all MeshRenderers in a GameObject and its children.
        /// </summary>
        /// <param name="gameObject">GameObject to calculate bounds for.</param>
        /// <returns>Combined bounds encapsulating all child renderers.</returns>
        public static Bounds FindBoundsIncludeChildren(this GameObject gameObject)
        {
            Bounds bounds = new();
            foreach (MeshRenderer mr in gameObject.GetComponentsInChildren<MeshRenderer>())
            {
                bounds.Encapsulate(mr.bounds);
            }

            return bounds;
        }


        /// <summary>
        /// Searches for a component in parent GameObjects, with option to include inactive objects.
        /// More flexible than Unity's built-in GetComponentInParent.
        /// </summary>
        /// <typeparam name="T">Type of component to find.</typeparam>
        /// <param name="transform">Starting transform.</param>
        /// <param name="includeInactive">Include inactive GameObjects in search.</param>
        /// <returns>First component of type T found in parents, or null if none found.</returns>
        public static T GetComponentInParent<T>(this Transform transform, bool includeInactive = true)
            where T : Component
        {
            Transform here   = transform;
            T         result = null;
            while (here && !result)
            {
                if (includeInactive || here.gameObject.activeSelf)
                {
                    result = here.GetComponent<T>();
                }

                here = here.parent;
            }

            return result;
        }


        /// <summary>
        /// Searches for a component in parents first, then children if not found.
        /// Combines functionality of GetComponentInParent and GetComponentInChildren.
        /// </summary>
        /// <typeparam name="T">Type of component to find.</typeparam>
        /// <param name="transform">Starting transform.</param>
        /// <param name="includeInactive">Include inactive GameObjects in search.</param>
        /// <returns>First component of type T found, or null if none found.</returns>
        public static T GetComponentInParentsOrChildren<T>(this Transform transform, bool includeInactive = true)
            where T : Component
        {
            T result = transform.GetComponentInParent<T>(includeInactive);
            if (result == null)
            {
                result = transform.GetComponentInChildren<T>(includeInactive);
            }

            return result;
        }
    }
}