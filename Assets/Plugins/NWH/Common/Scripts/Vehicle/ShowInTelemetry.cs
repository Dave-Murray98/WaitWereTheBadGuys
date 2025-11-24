// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using System;

#endregion

namespace NWH.Common.Vehicles
{
    /// <summary>
    /// Attribute that marks a field or property to be displayed in the runtime telemetry UI.
    /// Used for monitoring vehicle parameters during gameplay and debugging.
    /// </summary>
    [AttributeUsage(AttributeTargets.All)]
    public partial class ShowInTelemetry : Attribute
    {
        /// <summary>
        /// Minimum value for the field (used for progress bar visualization).
        /// </summary>
        public float Min { get; set; } = float.NaN;

        /// <summary>
        /// Maximum value for the field (used for progress bar visualization).
        /// </summary>
        public float Max { get; set; } = float.NaN;

        /// <summary>
        /// Format string for displaying the value (e.g., "0.00", "0.0").
        /// </summary>
        public string Format { get; set; } = null;

        /// <summary>
        /// Unit of measurement (e.g., "km/h", "RPM", "N", "°").
        /// </summary>
        public string Unit { get; set; } = null;

        /// <summary>
        /// Display priority. 0 = highest (always visible), 3 = lowest (detailed info).
        /// </summary>
        public int Priority { get; set; } = 1;

        /// <summary>
        /// Creates a ShowInTelemetry attribute with optional parameters.
        /// </summary>
        public ShowInTelemetry(float min = float.NaN, float max = float.NaN, string format = null, string unit = null, int priority = 1)
        {
            Min = min;
            Max = max;
            Format = format;
            Unit = unit;
            Priority = priority;
        }
    }
}