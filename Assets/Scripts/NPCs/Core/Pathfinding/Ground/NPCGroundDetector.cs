using UnityEngine;

public class NPCGroundDetector : MonoBehaviour
{


    [Header("Ground Detection")]
    [SerializeField, Tooltip("LayerMask for ground detection")]
    private LayerMask groundLayerMask = 1; // Default layer

    [SerializeField, Tooltip("Distance to raycast down for ground")]
    private float groundCheckDistance = 2f;

    public bool IsGrounded => isGrounded;

    private bool isGrounded;

    [SerializeField] private bool enableDebugLogs = false;

    // Update is called once per frame
    void Update()
    {
        CheckGroundState();
    }

    private void CheckGroundState()
    {
        // Simple ground check using raycast
        Vector3 rayStart = transform.position + Vector3.up * 0.1f;
        isGrounded = Physics.Raycast(rayStart, Vector3.down, groundCheckDistance, groundLayerMask);

        if (!isGrounded && enableDebugLogs)
        {
            Debug.LogWarning($"{gameObject.name}: Not grounded! May need to switch to water movement.");
        }
    }
}
