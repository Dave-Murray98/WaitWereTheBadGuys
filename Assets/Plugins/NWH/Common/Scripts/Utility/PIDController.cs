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
    /// Proportional-Integral-Derivative controller for smooth value regulation.
    /// Used for automated control systems like cruise control, stability systems, and steering assistance.
    /// </summary>
    /// <remarks>
    /// PID controllers combine three control strategies:
    /// - Proportional: Reacts to current error
    /// - Integral: Eliminates accumulated error over time
    /// - Derivative: Anticipates future error based on rate of change
    /// Tune the three gain values to achieve desired response characteristics.
    /// </remarks>
    public class PIDController
    {
        /// <summary>
        /// Maximum output value. Output will be clamped to this value.
        /// </summary>
        public float maxValue;

        /// <summary>
        /// Minimum output value. Output will be clamped to this value.
        /// </summary>
        public float minValue;

        private float _processVariable;


        /// <summary>
        /// Creates a new PID controller with specified gains and output limits.
        /// </summary>
        /// <param name="gainProportional">Proportional gain (Kp). Higher values increase response to current error.</param>
        /// <param name="gainIntegral">Integral gain (Ki). Higher values eliminate steady-state error faster.</param>
        /// <param name="gainDerivative">Derivative gain (Kd). Higher values dampen oscillations.</param>
        /// <param name="outputMin">Minimum output value.</param>
        /// <param name="outputMax">Maximum output value.</param>
        public PIDController(float gainProportional, float gainIntegral, float gainDerivative, float outputMin,
            float                  outputMax)
        {
            GainDerivative   = gainDerivative;
            GainIntegral     = gainIntegral;
            GainProportional = gainProportional;
            maxValue         = outputMax;
            minValue         = outputMin;
        }


        /// <summary>
        /// The derivative term is proportional to the rate of
        /// change of the error
        /// </summary>
        public float GainDerivative { get; set; }

        /// <summary>
        /// The integral term is proportional to both the magnitude
        /// of the error and the duration of the error
        /// </summary>
        public float GainIntegral { get; set; }

        /// <summary>
        /// The proportional term produces an output value that
        /// is proportional to the current error value
        /// </summary>
        /// <remarks>
        /// Tuning theory and industrial practice indicate that the
        /// proportional term should contribute the bulk of the output change.
        /// </remarks>
        public float GainProportional { get; set; }

        /// <summary>
        /// Adjustment made by considering the accumulated error over time
        /// </summary>
        /// <remarks>
        /// An alternative formulation of the integral action, is the
        /// proportional-summation-difference used in discrete-time systems
        /// </remarks>
        public float IntegralTerm { get; private set; }

        /// <summary>
        /// The current value
        /// </summary>
        public float ProcessVariable
        {
            get { return _processVariable; }
            set
            {
                ProcessVariableLast = _processVariable;
                _processVariable    = value;
            }
        }

        /// <summary>
        /// The last reported value (used to calculate the rate of change)
        /// </summary>
        public float ProcessVariableLast { get; private set; }

        /// <summary>
        /// The desired value
        /// </summary>
        public float SetPoint { get; set; } = 0;


        /// <summary>
        /// The controller output
        /// </summary>
        /// <param name="timeSinceLastUpdate">
        /// timespan of the elapsed time
        /// since the previous time that ControlVariable was called
        /// </param>
        /// <returns> Value of the variable that needs to be controlled </returns>
        public float ControlVariable(float timeSinceLastUpdate)
        {
            // Guard against zero or very small deltaTime to prevent NaN/Infinity
            // This can happen during game pause or time manipulation
            const float EPSILON = 0.0001f;
            if (timeSinceLastUpdate < EPSILON)
            {
                // Return proportional-only control when time is too small
                return Mathf.Clamp(GainProportional * (SetPoint - ProcessVariable), minValue, maxValue);
            }

            float error = SetPoint - ProcessVariable;

            // integral term calculation
            IntegralTerm += GainIntegral * error * timeSinceLastUpdate;
            IntegralTerm =  Mathf.Clamp(IntegralTerm, minValue, maxValue);

            // derivative term calculation
            float dInput         = _processVariable - ProcessVariableLast;
            float derivativeTerm = GainDerivative * (dInput / timeSinceLastUpdate);

            // proportional term calcullation
            float proportionalTerm = GainProportional * error;

            float output = proportionalTerm + IntegralTerm - derivativeTerm;

            output = Mathf.Clamp(output, minValue, maxValue);

            return output;
        }
    }
}