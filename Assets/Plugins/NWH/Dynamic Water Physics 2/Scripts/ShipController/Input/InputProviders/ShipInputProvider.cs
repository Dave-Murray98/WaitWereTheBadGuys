// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using NWH.Common.Input;
using UnityEngine;

#endregion

namespace NWH.DWP2.ShipController
{
    /// <summary>
    /// Base class for all ship input providers.
    /// Inherit from this to create custom input systems for ship controls.
    /// Provides virtual methods for all ship control inputs including throttle, steering, thrusters, and submarine depth control.
    /// </summary>
    public abstract class ShipInputProvider : InputProvider
    {
        /// <summary>
        /// Horizontal steering input from -1 (port/left) to 1 (starboard/right).
        /// </summary>
        /// <returns>Steering input value.</returns>
        public virtual float Steering()
        {
            return 0f;
        }


        /// <summary>
        /// Primary throttle input from -1 (full reverse) to 1 (full forward).
        /// </summary>
        /// <returns>Throttle input value.</returns>
        public virtual float Throttle()
        {
            return 0f;
        }

        /// <summary>
        /// Secondary throttle input for independent engine control.
        /// </summary>
        /// <returns>Throttle2 input value.</returns>
        public virtual float Throttle2()
        {
            return 0f;
        }

        /// <summary>
        /// Tertiary throttle input for independent engine control.
        /// </summary>
        /// <returns>Throttle3 input value.</returns>
        public virtual float Throttle3()
        {
            return 0f;
        }

        /// <summary>
        /// Quaternary throttle input for independent engine control.
        /// </summary>
        /// <returns>Throttle4 input value.</returns>
        public virtual float Throttle4()
        {
            return 0f;
        }

        /// <summary>
        /// Stern thruster input from -1 to 1 for lateral movement at the rear.
        /// </summary>
        /// <returns>Stern thruster input value.</returns>
        public virtual float SternThruster()
        {
            return 0f;
        }

        /// <summary>
        /// Bow thruster input from -1 to 1 for lateral movement at the front.
        /// </summary>
        /// <returns>Bow thruster input value.</returns>
        public virtual float BowThruster()
        {
            return 0f;
        }

        /// <summary>
        /// Submarine depth control from -1 (surface) to 1 (dive).
        /// </summary>
        /// <returns>Submarine depth input value.</returns>
        public virtual float SubmarineDepth()
        {
            return 0f;
        }

        /// <summary>
        /// Toggle engine start/stop state.
        /// </summary>
        /// <returns>True if engine start/stop was triggered this frame.</returns>
        public virtual bool EngineStartStop()
        {
            return false;
        }

        /// <summary>
        /// Toggle anchor drop/weigh state.
        /// </summary>
        /// <returns>True if anchor toggle was triggered this frame.</returns>
        public virtual bool Anchor()
        {
            return false;
        }

        /// <summary>
        /// Sail rotation input for sailing vessels.
        /// </summary>
        /// <returns>Sail rotation input value.</returns>
        public virtual float RotateSail()
        {
            return 0f;
        }

        /// <summary>
        /// Mouse or touch position for object dragging in demo scenes.
        /// </summary>
        /// <returns>Drag position delta.</returns>
        public virtual Vector2 DragObjectPosition()
        {
            return Vector2.zero;
        }

        /// <summary>
        /// Modifier key for object dragging.
        /// </summary>
        /// <returns>True if drag modifier is held.</returns>
        public virtual bool DragObjectModifier()
        {
            return false;
        }
    }
}