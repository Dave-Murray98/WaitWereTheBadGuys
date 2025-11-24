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
    /// Attribute that marks a field to be displayed in runtime settings UI.
    /// Allows players to adjust vehicle parameters during gameplay.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public partial class ShowInSettings : Attribute
    {
        /// <summary>
        /// Maximum value for the setting slider.
        /// </summary>
        public float  max = 1f;

        /// <summary>
        /// Minimum value for the setting slider.
        /// </summary>
        public float  min;

        /// <summary>
        /// Display name for the setting in the UI.
        /// </summary>
        public string name;

        /// <summary>
        /// Increment step for the slider. Smaller values allow finer adjustment.
        /// </summary>
        public float  step = 0.1f;


        /// <summary>
        /// Creates a settings attribute with a custom display name.
        /// </summary>
        public ShowInSettings(string name)
        {
            this.name = name;
        }


        /// <summary>
        /// Creates a settings attribute with specified min, max, and step values.
        /// </summary>
        public ShowInSettings(float min, float max, float step = 0.1f)
        {
            this.min  = min;
            this.max  = max;
            this.step = step;
        }


        /// <summary>
        /// Creates a settings attribute with custom name and value constraints.
        /// </summary>
        public ShowInSettings(string name, float min, float max, float step = 0.1f)
        {
            this.name = name;
            this.min  = min;
            this.max  = max;
            this.step = step;
        }


        public ShowInSettings()
        {
        }
    }
}