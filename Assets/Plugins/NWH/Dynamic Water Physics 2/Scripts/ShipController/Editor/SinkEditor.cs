// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#if UNITY_EDITOR

#region

using NWH.NUI;
using NWH.DWP2.ShipController;
using UnityEditor;

#endregion

namespace NWH.DWP2.WaterObjects
{
    /// <summary>
    /// Custom inspector for Sink.
    /// </summary>
    [CustomEditor(typeof(Sink))]
    [CanEditMultipleObjects]
    public class SinkEditor : DWP2NUIEditor
    {
        /// <summary>
        /// Draws custom inspector GUI for Sink.
        /// </summary>
        public override bool OnInspectorNUI()
        {
            if (!base.OnInspectorNUI())
            {
                return false;
            }

            Sink sink = (Sink)target;

            drawer.Info("Position of the Transform to which this component is attached to " +
                        "roughly represents the sink direction / water ingress point.");
            drawer.Field("addedMassPercentPerSecond");
            drawer.Field("maxAdditionalMass");

            if (drawer.Button("Start Sinking"))
            {
                sink.StartSinking();
            }

            if (drawer.Button("Stop Sinking"))
            {
                sink.StopSinking();
            }

            if (drawer.Button("Reset"))
            {
                sink.ResetMass();
            }

            drawer.EndEditor(this);
            return true;
        }
    }
}

#endif