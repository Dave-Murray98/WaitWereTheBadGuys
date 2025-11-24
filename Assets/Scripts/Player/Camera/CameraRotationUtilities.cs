using UnityEngine;

/// <summary>
/// Utility class for camera rotation calculations and angle handling.
/// Provides helper methods for component-wise rotation interpolation and angle normalization.
/// </summary>
public static class CameraRotationUtilities
{
    /// <summary>
    /// Normalize angle to -180 to 180 range to prevent interpolation issues
    /// </summary>
    /// <param name="angle">Input angle in degrees</param>
    /// <returns>Normalized angle between -180 and 180 degrees</returns>
    public static float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    /// <summary>
    /// Normalize a Vector3 of angles to -180 to 180 range
    /// </summary>
    /// <param name="angles">Input angles (x=pitch, y=yaw, z=roll)</param>
    /// <returns>Normalized angles</returns>
    public static Vector3 NormalizeAngles(Vector3 angles)
    {
        return new Vector3(
            NormalizeAngle(angles.x),
            NormalizeAngle(angles.y),
            NormalizeAngle(angles.z)
        );
    }

    /// <summary>
    /// Calculate the shortest angular distance between two angles
    /// </summary>
    /// <param name="from">Starting angle</param>
    /// <param name="to">Target angle</param>
    /// <returns>Shortest angular distance (can be negative)</returns>
    public static float AngularDistance(float from, float to)
    {
        float diff = to - from;
        while (diff > 180f) diff -= 360f;
        while (diff < -180f) diff += 360f;
        return diff;
    }

    /// <summary>
    /// Check if an angular transition would be considered "large" (potentially causing somersaults)
    /// </summary>
    /// <param name="from">Starting angle</param>
    /// <param name="to">Target angle</param>
    /// <param name="threshold">Threshold in degrees (default 90°)</param>
    /// <returns>True if the angular distance exceeds the threshold</returns>
    public static bool IsLargeAngularTransition(float from, float to, float threshold = 90f)
    {
        return Mathf.Abs(AngularDistance(from, to)) > threshold;
    }

    /// <summary>
    /// Check if any component of a rotation transition is large
    /// </summary>
    /// <param name="from">Starting rotation</param>
    /// <param name="to">Target rotation</param>
    /// <param name="threshold">Threshold in degrees for each axis</param>
    /// <returns>True if any axis exceeds the threshold</returns>
    public static bool IsLargeRotationTransition(Vector3 from, Vector3 to, float threshold = 90f)
    {
        return IsLargeAngularTransition(from.x, to.x, threshold) ||
               IsLargeAngularTransition(from.y, to.y, threshold) ||
               IsLargeAngularTransition(from.z, to.z, threshold);
    }

    /// <summary>
    /// Smoothly interpolate between angles using SmoothDampAngle
    /// </summary>
    /// <param name="current">Current angle</param>
    /// <param name="target">Target angle</param>
    /// <param name="velocity">Reference to velocity (modified by function)</param>
    /// <param name="smoothTime">Smoothing time</param>
    /// <returns>Smoothly interpolated angle</returns>
    public static float SmoothAngle(float current, float target, ref float velocity, float smoothTime)
    {
        return Mathf.SmoothDampAngle(current, target, ref velocity, smoothTime);
    }

    /// <summary>
    /// Smoothly interpolate between two rotations using component-wise angle interpolation
    /// </summary>
    /// <param name="current">Current rotation</param>
    /// <param name="target">Target rotation</param>
    /// <param name="velocities">Reference to velocity vector (modified by function)</param>
    /// <param name="smoothTime">Smoothing time</param>
    /// <returns>Smoothly interpolated rotation</returns>
    public static Vector3 SmoothAngles(Vector3 current, Vector3 target, ref Vector3 velocities, float smoothTime)
    {
        return new Vector3(
            Mathf.SmoothDampAngle(current.x, target.x, ref velocities.x, smoothTime),
            Mathf.SmoothDampAngle(current.y, target.y, ref velocities.y, smoothTime),
            Mathf.SmoothDampAngle(current.z, target.z, ref velocities.z, smoothTime)
        );
    }

    /// <summary>
    /// Convert a Vector3 of Euler angles to a quaternion with proper normalization
    /// </summary>
    /// <param name="eulerAngles">Euler angles (x=pitch, y=yaw, z=roll)</param>
    /// <returns>Normalized quaternion</returns>
    public static Quaternion EulerToQuaternion(Vector3 eulerAngles)
    {
        Vector3 normalized = NormalizeAngles(eulerAngles);
        return Quaternion.Euler(normalized);
    }

    /// <summary>
    /// Safely extract Euler angles from a quaternion and normalize them
    /// </summary>
    /// <param name="rotation">Input quaternion</param>
    /// <returns>Normalized Euler angles</returns>
    public static Vector3 QuaternionToNormalizedEuler(Quaternion rotation)
    {
        Vector3 euler = rotation.eulerAngles;
        return NormalizeAngles(euler);
    }

    /// <summary>
    /// Check if two angles are approximately equal within a tolerance
    /// </summary>
    /// <param name="a">First angle</param>
    /// <param name="b">Second angle</param>
    /// <param name="tolerance">Tolerance in degrees (default 0.1°)</param>
    /// <returns>True if angles are approximately equal</returns>
    public static bool AngleApproximately(float a, float b, float tolerance = 0.1f)
    {
        return Mathf.Abs(AngularDistance(a, b)) <= tolerance;
    }

    /// <summary>
    /// Check if two rotations are approximately equal within a tolerance
    /// </summary>
    /// <param name="a">First rotation</param>
    /// <param name="b">Second rotation</param>
    /// <param name="tolerance">Tolerance in degrees for each axis</param>
    /// <returns>True if rotations are approximately equal</returns>
    public static bool RotationApproximately(Vector3 a, Vector3 b, float tolerance = 0.1f)
    {
        return AngleApproximately(a.x, b.x, tolerance) &&
               AngleApproximately(a.y, b.y, tolerance) &&
               AngleApproximately(a.z, b.z, tolerance);
    }

    /// <summary>
    /// Clamp an angle to a specific range
    /// </summary>
    /// <param name="angle">Input angle</param>
    /// <param name="min">Minimum angle</param>
    /// <param name="max">Maximum angle</param>
    /// <returns>Clamped angle</returns>
    public static float ClampAngle(float angle, float min, float max)
    {
        angle = NormalizeAngle(angle);

        // Handle wrap-around cases
        if (min < -180f || max > 180f)
        {
            Debug.LogWarning($"ClampAngle: min ({min}) or max ({max}) is outside normalized range. Results may be unexpected.");
        }

        return Mathf.Clamp(angle, min, max);
    }

    /// <summary>
    /// Get debug information about a rotation transition
    /// </summary>
    /// <param name="from">Starting rotation</param>
    /// <param name="to">Target rotation</param>
    /// <returns>Debug string with transition information</returns>
    public static string GetRotationTransitionDebugInfo(Vector3 from, Vector3 to)
    {
        Vector3 distances = new Vector3(
            AngularDistance(from.x, to.x),
            AngularDistance(from.y, to.y),
            AngularDistance(from.z, to.z)
        );

        bool isLarge = IsLargeRotationTransition(from, to);

        return $"Rotation Transition - From: {from:F1} To: {to:F1} " +
               $"Distances: {distances:F1} Large: {isLarge}";
    }

    /// <summary>
    /// Perform a safe quaternion to euler conversion that avoids gimbal lock issues
    /// </summary>
    /// <param name="rotation">Input quaternion</param>
    /// <returns>Safe euler angles with gimbal lock handling</returns>
    public static Vector3 SafeQuaternionToEuler(Quaternion rotation)
    {
        // Normalize the quaternion first
        rotation = rotation.normalized;

        // Extract euler angles
        Vector3 euler = rotation.eulerAngles;

        // Normalize to -180 to 180 range
        euler = NormalizeAngles(euler);

        // Check for gimbal lock (when pitch is close to ±90°)
        if (Mathf.Abs(euler.x) > 89f)
        {
            // In gimbal lock, yaw and roll become interdependent
            // We can set roll to 0 and calculate yaw differently
            Debug.LogWarning($"Gimbal lock detected at pitch {euler.x:F1}°. Adjusting rotation calculation.");

            // Keep pitch, calculate yaw from forward vector, set roll to 0
            Vector3 forward = rotation * Vector3.forward;
            float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;

            euler.y = NormalizeAngle(yaw);
            euler.z = 0f; // Reset roll in gimbal lock
        }

        return euler;
    }
}