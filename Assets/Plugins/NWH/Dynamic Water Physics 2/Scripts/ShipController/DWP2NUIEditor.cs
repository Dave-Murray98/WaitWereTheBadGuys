// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#if UNITY_EDITOR

using NWH.NUI;

namespace NWH.DWP2
{
    /// <summary>
    /// Base NUI editor for Dynamic Water Physics 2 package.
    /// Sets correct documentation URL for DWP2 components.
    /// </summary>
    public class DWP2NUIEditor : NUIEditor
    {
        public override string GetDocumentationBaseURL()
        {
            return "http://dynamicwaterphysics.com";
        }
    }
}

#endif
