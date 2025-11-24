using UnityEngine;

/// <summary>
/// Temporary debug script to help diagnose the aim sync issue
/// Attach this to your player and it will log detailed information
/// </summary>
public class DebugAimSync : MonoBehaviour
{
    [Header("References - Assign These")]
    public Transform aimTarget;
    public Transform cameraTransform; // Your virtual camera transform
    public Transform headBone;
    public AimController aimController;

    [Header("Debug Settings")]
    public bool enableLogging = true;
    public KeyCode debugKey = KeyCode.F1;

    private void Update()
    {
        if (Input.GetKeyDown(debugKey))
        {
            LogCurrentState();
        }

        // Continuous logging every 2 seconds if enabled
        if (enableLogging && Time.frameCount % 120 == 0)
        {
            LogCurrentState();
        }
    }

    private void LogCurrentState()
    {
        Debug.Log("=== AIM SYNC DEBUG ===");

        if (aimTarget != null)
        {
            Debug.Log($"Aim Target Position: {aimTarget.position}");
            Debug.Log($"Aim Target Local Position: {aimTarget.localPosition}");
            Debug.Log($"Aim Target Parent: {aimTarget.parent?.name}");
            Debug.Log($"Aim Target Parent Scale: {aimTarget.parent?.localScale}");
        }

        if (cameraTransform != null)
        {
            Debug.Log($"Camera Position: {cameraTransform.position}");
            Debug.Log($"Camera Rotation: {cameraTransform.eulerAngles}");
            Debug.Log($"Camera Forward: {cameraTransform.forward}");
        }

        if (headBone != null)
        {
            Debug.Log($"Head Bone Position: {headBone.position}");
            Debug.Log($"Head Bone Rotation: {headBone.eulerAngles}");
            Debug.Log($"Head Bone Forward: {headBone.forward}");
        }

        if (aimController != null)
        {
            Debug.Log($"Current Vertical Angle: {aimController.CurrentVerticalAngle}");
        }

        // Calculate the angle between camera forward and aim direction
        if (cameraTransform != null && aimTarget != null)
        {
            Vector3 aimDirection = (aimTarget.position - cameraTransform.position).normalized;
            Vector3 cameraForward = cameraTransform.forward;

            float angleBetween = Vector3.Angle(cameraForward, aimDirection);
            Debug.Log($"Angle between camera forward and aim direction: {angleBetween:F2}°");

            // Calculate vertical angles separately
            float cameraVerticalAngle = Mathf.Asin(cameraForward.y) * Mathf.Rad2Deg;
            float aimVerticalAngle = Mathf.Asin(aimDirection.y) * Mathf.Rad2Deg;

            Debug.Log($"Camera vertical angle: {cameraVerticalAngle:F2}°");
            Debug.Log($"Aim direction vertical angle: {aimVerticalAngle:F2}°");
            Debug.Log($"Vertical angle difference: {(aimVerticalAngle - cameraVerticalAngle):F2}°");
        }

        Debug.Log("=== END DEBUG ===");
    }

    private void OnDrawGizmos()
    {
        if (cameraTransform != null && aimTarget != null)
        {
            // Draw camera forward (blue)
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(cameraTransform.position, cameraTransform.forward * 5f);

            // Draw aim direction (red)
            Gizmos.color = Color.red;
            Vector3 aimDirection = (aimTarget.position - cameraTransform.position).normalized;
            Gizmos.DrawRay(cameraTransform.position, aimDirection * 5f);

            // Draw the difference
            if (Vector3.Angle(cameraTransform.forward, aimDirection) > 0.1f)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(cameraTransform.position + cameraTransform.forward * 5f,
                               cameraTransform.position + aimDirection * 5f);
            }
        }
    }
}