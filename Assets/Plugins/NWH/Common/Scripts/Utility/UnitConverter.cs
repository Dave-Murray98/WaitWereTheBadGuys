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
    /// Static utility class for converting between various units of measurement.
    /// Includes conversions for distance, speed, fuel efficiency, and angular velocity.
    /// </summary>
    public static class UnitConverter
    {
        /// <summary>
        /// Converts inches to meters.
        /// </summary>
        /// <param name="inch">Value in inches.</param>
        /// <returns>Value in meters.</returns>
        public static float Inch_To_Meter(float inch)
        {
            return inch * 0.0254f;
        }


        /// <summary>
        /// Converts meters to inches.
        /// </summary>
        /// <param name="meters">Value in meters.</param>
        /// <returns>Value in inches.</returns>
        public static float Meter_To_Inch(float meters)
        {
            return meters * 39.3701f;
        }


        /// <summary>
        /// km/l to l/100km
        /// </summary>
        /// <param name="kml">Fuel efficiency in km/l.</param>
        /// <returns>Fuel efficiency in l/100km.</returns>
        public static float KmlToL100km(float kml)
        {
            return kml == 0 ? Mathf.Infinity : 100f / kml;
        }


        /// <summary>
        /// km/l to mpg
        /// </summary>
        /// <param name="kml">Fuel efficiency in km/l.</param>
        /// <returns>Fuel efficiency in mpg.</returns>
        public static float KmlToMpg(float kml)
        {
            return kml * 2.825f;
        }


        /// <summary>
        /// l/100km to km/l
        /// </summary>
        /// <param name="l100km">Fuel efficiency in l/100km.</param>
        /// <returns>Fuel efficiency in km/l.</returns>
        public static float L100kmToKml(float l100km)
        {
            return l100km == 0 ? 0 : 100f / l100km;
        }


        /// <summary>
        /// l/100km to mpg
        /// </summary>
        /// <param name="l100km">Fuel efficiency in l/100km.</param>
        /// <returns>Fuel efficiency in mpg.</returns>
        public static float L100kmToMpg(float l100km)
        {
            return l100km == 0 ? 0 : 282.5f / l100km;
        }


        /// <summary>
        /// Converts angular velocity (rad/s) to rotations per minute.
        /// </summary>
        /// <param name="angularVelocity">Angular velocity in rad/s.</param>
        /// <returns>Rotations per minute (RPM).</returns>
        public static float AngularVelocityToRPM(float angularVelocity)
        {
            return angularVelocity * 9.5492965855137f;
        }


        /// <summary>
        /// Converts rotations per minute to angular velocity (rad/s).
        /// </summary>
        /// <param name="RPM">Rotations per minute.</param>
        /// <returns>Angular velocity in rad/s.</returns>
        public static float RPMToAngularVelocity(float RPM)
        {
            return RPM * 0.10471975511966f;
        }


        /// <summary>
        /// mpg to km/l
        /// </summary>
        /// <param name="mpg">Fuel efficiency in mpg.</param>
        /// <returns>Fuel efficiency in km/l.</returns>
        public static float MpgToKml(float mpg)
        {
            return mpg * 0.354f;
        }


        /// <summary>
        /// mpg to l/100km
        /// </summary>
        /// <param name="mpg">Fuel efficiency in mpg.</param>
        /// <returns>Fuel efficiency in l/100km.</returns>
        public static float MpgToL100km(float mpg)
        {
            return mpg == 0 ? Mathf.Infinity : 282.5f / mpg;
        }


        /// <summary>
        /// miles/h to km/h
        /// </summary>
        /// <param name="value">Speed in mph.</param>
        /// <returns>Speed in km/h.</returns>
        public static float MphToKph(float value)
        {
            return value * 1.60934f;
        }


        /// <summary>
        /// m/s to km/h
        /// </summary>
        /// <param name="value">Speed in m/s.</param>
        /// <returns>Speed in km/h.</returns>
        public static float MpsToKph(float value)
        {
            return value * 3.6f;
        }


        /// <summary>
        /// m/s to miles/h
        /// </summary>
        /// <param name="value">Speed in m/s.</param>
        /// <returns>Speed in mph.</returns>
        public static float MpsToMph(float value)
        {
            return value * 2.23694f;
        }


        /// <summary>
        /// Converts km/h to mph.
        /// </summary>
        /// <param name="kmh">Speed in km/h.</param>
        /// <returns>Speed in mph.</returns>
        public static float Speed_kmhToMph(float kmh)
        {
            return kmh * 0.621371f;
        }


        /// <summary>
        /// Converts km/h to m/s.
        /// </summary>
        /// <param name="kmh">Speed in km/h.</param>
        /// <returns>Speed in m/s.</returns>
        public static float Speed_kmhToMs(float kmh)
        {
            return kmh * 0.277778f;
        }


        /// <summary>
        /// Converts mph to km/h.
        /// </summary>
        /// <param name="mph">Speed in mph.</param>
        /// <returns>Speed in km/h.</returns>
        public static float Speed_mphToKmh(float mph)
        {
            return mph * 1.60934f;
        }


        /// <summary>
        /// Converts mph to m/s.
        /// </summary>
        /// <param name="mph">Speed in mph.</param>
        /// <returns>Speed in m/s.</returns>
        public static float Speed_mphToMs(float mph)
        {
            return mph * 0.44704f;
        }


        /// <summary>
        /// Converts m/s to km/h.
        /// </summary>
        /// <param name="ms">Speed in m/s.</param>
        /// <returns>Speed in km/h.</returns>
        public static float Speed_msToKph(float ms)
        {
            return ms * 3.6f;
        }


        /// <summary>
        /// Converts m/s to mph.
        /// </summary>
        /// <param name="ms">Speed in m/s.</param>
        /// <returns>Speed in mph.</returns>
        public static float Speed_msToMph(float ms)
        {
            return ms * 2.23694f;
        }
    }
}