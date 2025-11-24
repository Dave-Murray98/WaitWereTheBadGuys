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
    /// Extension methods for AnimationCurve manipulation and processing.
    /// </summary>
    public static class AnimationCurveExtensions
    {
        /// <summary>
        /// Smooths out a scripting-generated AnimationCurve by calculating appropriate tangents.
        /// Creates smooth transitions between keyframes.
        /// </summary>
        /// <param name="inCurve">The curve to smooth.</param>
        /// <returns>A new smoothed AnimationCurve.</returns>
        public static AnimationCurve MakeSmooth(this AnimationCurve inCurve)
        {
            AnimationCurve outCurve = new();

            for (int i = 0; i < inCurve.keys.Length; i++)
            {
                float    inTangent     = 0;
                float    outTangent    = 0;
                bool     intangentSet  = false;
                bool     outtangentSet = false;
                Vector2  point1;
                Vector2  point2;
                Vector2  deltapoint;
                Keyframe key = inCurve[i];

                if (i == 0)
                {
                    inTangent    = 0;
                    intangentSet = true;
                }

                if (i == inCurve.keys.Length - 1)
                {
                    outTangent    = 0;
                    outtangentSet = true;
                }

                if (!intangentSet)
                {
                    point1.x = inCurve.keys[i - 1].time;
                    point1.y = inCurve.keys[i - 1].value;
                    point2.x = inCurve.keys[i].time;
                    point2.y = inCurve.keys[i].value;

                    deltapoint = point2 - point1;

                    inTangent = deltapoint.y / deltapoint.x;
                }

                if (!outtangentSet)
                {
                    point1.x = inCurve.keys[i].time;
                    point1.y = inCurve.keys[i].value;
                    point2.x = inCurve.keys[i + 1].time;
                    point2.y = inCurve.keys[i + 1].value;

                    deltapoint = point2 - point1;

                    outTangent = deltapoint.y / deltapoint.x;
                }

                key.inTangent  = inTangent;
                key.outTangent = outTangent;
                outCurve.AddKey(key);
            }

            return outCurve;
        }


        /// <summary>
        /// Samples an AnimationCurve at regular intervals and returns the values as an array.
        /// Useful for pre-calculating curve values for performance-critical code.
        /// </summary>
        /// <param name="self">The curve to sample.</param>
        /// <param name="resolution">Number of samples to take. Higher values provide more precision.</param>
        /// <returns>Array of sampled values from 0 to 1.</returns>
        public static float[] GenerateCurveArray(this AnimationCurve self, int resolution = 256)
        {
            float[] returnArray = new float[resolution];
            for (int j = 0; j < resolution; j++)
            {
                returnArray[j] = self.Evaluate(j / (float)resolution);
            }

            return returnArray;
        }
    }
}