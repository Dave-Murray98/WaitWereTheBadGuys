// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using System;
using System.Collections.Generic;
using UnityEngine;

#endregion

namespace NWH.Common.Input
{
    /// <summary>
    /// Base class from which all input providers inherit.
    /// </summary>
    public abstract class InputProvider : MonoBehaviour
    {
        /// <summary>
        /// List of all InputProviders in the scene.
        /// </summary>
        public static List<InputProvider> Instances = new();


        public virtual void Awake()
        {
            Instances.Add(this);
        }


        public virtual void OnDestroy()
        {
            Instances.Remove(this);
        }


        /// <summary>
        /// Returns combined input of all InputProviders present in the scene.
        /// Result will be a sum of all inputs of the selected type.
        /// T is a type of InputProvider that the input will be retrieved from.
        /// </summary>
        public static int CombinedInput<T>(Func<T, int> selector) where T : InputProvider
        {
            int sum = 0;
            int count = Instances.Count;
            for (int i = 0; i < count; i++)
            {
                if (i >= Instances.Count)
                {
                    break;
                }

                InputProvider ip = Instances[i];
                if (ip != null && ip is T provider)
                {
                    sum += selector(provider);
                }
            }

            return sum;
        }


        /// <summary>
        /// Returns combined input of all InputProviders present in the scene.
        /// Result will be a sum of all inputs of the selected type.
        /// T is a type of InputProvider that the input will be retrieved from.
        /// </summary>
        public static float CombinedInput<T>(Func<T, float> selector) where T : InputProvider
        {
            float sum = 0;
            int count = Instances.Count;
            for (int i = 0; i < count; i++)
            {
                if (i >= Instances.Count)
                {
                    break;
                }

                InputProvider ip = Instances[i];
                if (ip != null && ip is T provider)
                {
                    sum += selector(provider);
                }
            }

            return sum;
        }


        /// <summary>
        /// Returns combined input of all InputProviders present in the scene.
        /// Result will be positive if any InputProvider has the selected input set to true.
        /// T is a type of InputProvider that the input will be retrieved from.
        /// </summary>
        public static bool CombinedInput<T>(Func<T, bool> selector) where T : InputProvider
        {
            int count = Instances.Count;
            for (int i = 0; i < count; i++)
            {
                if (i >= Instances.Count)
                {
                    break;
                }

                InputProvider ip = Instances[i];
                if (ip != null && ip is T provider && selector(provider))
                {
                    return true;
                }
            }

            return false;
        }


        /// <summary>
        /// Returns combined input of all InputProviders present in the scene.
        /// Result will be a sum of all inputs of the selected type.
        /// T is a type of InputProvider that the input will be retrieved from.
        /// </summary>
        public static Vector2 CombinedInput<T>(Func<T, Vector2> selector) where T : InputProvider
        {
            Vector2 sum = Vector2.zero;
            int count = Instances.Count;
            for (int i = 0; i < count; i++)
            {
                if (i >= Instances.Count)
                {
                    break;
                }

                InputProvider ip = Instances[i];
                if (ip != null && ip is T provider)
                {
                    sum += selector(provider);
                }
            }

            return sum;
        }
    }
}