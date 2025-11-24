// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#if UNITY_EDITOR

#region

using NWH.Common.AssetInfo;
using UnityEditor;

#endregion

namespace NWH.DWP2
{
    /// <summary>
    /// Editor initialization for Dynamic Water Physics 2 package.
    /// Automatically adds scripting defines and displays welcome window on first import.
    /// </summary>
    public class InitializationMethodsDWP2 : CommonInitializationMethods
    {
        /// <summary>
        /// Adds NWH_DWP2 scripting define symbol on editor load.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void AddDWP2Defines()
        {
            AddDefines("NWH_DWP2");
        }


        /// <summary>
        /// Shows welcome window for Dynamic Water Physics 2 on first import or version update.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void ShowDWP2WelcomeWindow()
        {
            ShowWelcomeWindow("Dynamic Water Physics 2");
        }
    }
}
#endif