using UnityEngine;
using Sirenix.OdinInspector;
using UnityEditor;

/// <summary>
/// Advanced diagnostic tool for analyzing forces affecting the player.
/// This will help identify what's causing slow falling when physics settings appear correct.
/// </summary>
public class ForceAnalysisDiagnostic : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody playerRigidbody;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private SwimmingMovementController swimmingController;
    [SerializeField] private GroundMovementController groundController;
    [SerializeField] private PlayerWaterDetector waterDetector;

    [Header("Force Analysis")]
    [ShowInInspector, ReadOnly] private Vector3 currentVelocity;
    [ShowInInspector, ReadOnly] private Vector3 expectedGravityVelocity;
    [ShowInInspector, ReadOnly] private float gravityAcceleration;
    [ShowInInspector, ReadOnly] private float actualFallAcceleration;
    [ShowInInspector, ReadOnly] private bool isUnderwater;
    [ShowInInspector, ReadOnly] private bool isInWater;
    [ShowInInspector, ReadOnly] private string movementMode;
    [ShowInInspector, ReadOnly] private bool swimmingControllerActive;

    [Header("Issue Detection")]
    [ShowInInspector, ReadOnly] private bool possibleBuoyancyIssue;
    [ShowInInspector, ReadOnly] private bool possibleSwimmingForces;
    [ShowInInspector, ReadOnly] private bool possibleWaterDetectionIssue;
    [ShowInInspector, ReadOnly] private string diagnosedIssue;

    [Header("Fall Test")]
    [SerializeField] private bool runningFallTest = false;
    [SerializeField] private float fallTestStartTime;
    [SerializeField] private Vector3 fallTestStartVelocity;
    [SerializeField] private Vector3 fallTestStartPosition;

    private Vector3 lastVelocity;
    private float lastVelocityTime;

    private void Start()
    {
        FindReferences();
        gravityAcceleration = Mathf.Abs(Physics.gravity.y);
        lastVelocityTime = Time.time;
    }

    private void Update()
    {
        UpdateForceAnalysis();
        AnalyzeForIssues();

        if (runningFallTest)
        {
            UpdateFallTest();
        }
    }

    [Button("Find References")]
    private void FindReferences()
    {
        if (playerRigidbody == null)
            playerRigidbody = GetComponent<Rigidbody>() ?? FindFirstObjectByType<Rigidbody>();

        if (playerController == null)
            playerController = GetComponent<PlayerController>() ?? FindFirstObjectByType<PlayerController>();

        if (swimmingController == null)
            swimmingController = GetComponent<SwimmingMovementController>() ?? FindFirstObjectByType<SwimmingMovementController>();

        if (groundController == null)
            groundController = GetComponent<GroundMovementController>() ?? FindFirstObjectByType<GroundMovementController>();

        if (waterDetector == null)
            waterDetector = GetComponent<PlayerWaterDetector>() ?? FindFirstObjectByType<PlayerWaterDetector>();

        Debug.Log($"[ForceAnalysis] References found - RB: {playerRigidbody != null}, Controller: {playerController != null}, " +
                  $"Swimming: {swimmingController != null}, Ground: {groundController != null}, Water: {waterDetector != null}");
    }

    private void UpdateForceAnalysis()
    {
        if (playerRigidbody == null) return;

        currentVelocity = playerRigidbody.linearVelocity;

        // Calculate expected gravity velocity (what it should be with just gravity)
        expectedGravityVelocity = Vector3.down * gravityAcceleration;

        // Calculate actual fall acceleration
        if (Time.time > lastVelocityTime + 0.1f) // Update every 0.1 seconds
        {
            Vector3 velocityChange = currentVelocity - lastVelocity;
            float timeChange = Time.time - lastVelocityTime;

            if (timeChange > 0)
            {
                actualFallAcceleration = velocityChange.y / timeChange;
            }

            lastVelocity = currentVelocity;
            lastVelocityTime = Time.time;
        }

        // Update component states
        if (playerController != null)
        {
            movementMode = playerController.CurrentMovementMode.ToString();
            swimmingControllerActive = playerController.CurrentMovementMode == MovementMode.Swimming;
        }

        if (waterDetector != null)
        {
            isInWater = waterDetector.IsInWaterState;
            isUnderwater = waterDetector.IsHeadUnderwater;
        }
    }

    private void AnalyzeForIssues()
    {
        var issues = new System.Collections.Generic.List<string>();

        // Check if swimming forces are being applied when they shouldn't be
        possibleSwimmingForces = swimmingControllerActive && !isInWater;
        if (possibleSwimmingForces)
        {
            issues.Add("Swimming controller active but not in water");
        }

        // Check if buoyancy might still be applied
        possibleBuoyancyIssue = isInWater && movementMode == "Ground";
        if (possibleBuoyancyIssue)
        {
            issues.Add("In water but ground movement mode active");
        }

        // Check if water detection is stuck
        possibleWaterDetectionIssue = isInWater && actualFallAcceleration > -5f; // Much slower than gravity
        if (possibleWaterDetectionIssue)
        {
            issues.Add("Water detection may be incorrectly active");
        }

        // Check if falling is significantly slower than expected
        if (!playerRigidbody.isKinematic && !groundController?.IsGrounded == true)
        {
            float expectedFallAccel = -gravityAcceleration;
            if (actualFallAcceleration > expectedFallAccel + 5f) // Much slower than gravity
            {
                issues.Add($"Fall acceleration too slow: {actualFallAcceleration:F1} (expected ~{expectedFallAccel:F1})");
            }
        }

        diagnosedIssue = issues.Count > 0 ? string.Join(", ", issues) : "No issues detected";
    }

    [Button("Start Fall Test")]
    private void StartFallTest()
    {
        if (playerRigidbody == null) return;

        Debug.Log("[ForceAnalysis] Starting fall test...");

        runningFallTest = true;
        fallTestStartTime = Time.time;
        fallTestStartVelocity = playerRigidbody.linearVelocity;
        fallTestStartPosition = transform.position;

        // Clear any existing velocity
        playerRigidbody.linearVelocity = Vector3.zero;

        Debug.Log($"Fall test started - Position: {fallTestStartPosition}, Gravity: {Physics.gravity.y}");
    }

    [Button("Stop Fall Test")]
    private void StopFallTest()
    {
        if (!runningFallTest) return;

        float testDuration = Time.time - fallTestStartTime;
        Vector3 totalDisplacement = transform.position - fallTestStartPosition;

        // Calculate expected displacement: d = 0.5 * g * t^2
        float expectedFallDistance = 0.5f * gravityAcceleration * testDuration * testDuration;
        float actualFallDistance = Mathf.Abs(totalDisplacement.y);

        Debug.Log("=== FALL TEST RESULTS ===");
        Debug.Log($"Test Duration: {testDuration:F2}s");
        Debug.Log($"Expected Fall Distance: {expectedFallDistance:F2}m");
        Debug.Log($"Actual Fall Distance: {actualFallDistance:F2}m");
        Debug.Log($"Fall Efficiency: {(actualFallDistance / expectedFallDistance * 100f):F1}%");
        Debug.Log($"Final Velocity: {playerRigidbody.linearVelocity.y:F2} m/s");
        Debug.Log($"Expected Final Velocity: {-gravityAcceleration * testDuration:F2} m/s");

        if (actualFallDistance < expectedFallDistance * 0.8f)
        {
            Debug.LogWarning("SLOW FALLING DETECTED - Something is counteracting gravity!");
        }

        runningFallTest = false;
    }

    private void UpdateFallTest()
    {
        float testDuration = Time.time - fallTestStartTime;

        // Auto-stop after 3 seconds
        if (testDuration > 3f)
        {
            StopFallTest();
        }
    }

    [Button("Force Disable Swimming Controller")]
    private void ForceDisableSwimmingController()
    {
        if (swimmingController != null)
        {
            // Use reflection to call OnControllerDeactivated
            var method = swimmingController.GetType().GetMethod("OnControllerDeactivated");
            method?.Invoke(swimmingController, null);

            Debug.Log("[ForceAnalysis] Manually deactivated swimming controller");
        }
    }

    [Button("Check for Continuous Forces")]
    private void CheckForContinuousForces()
    {
        Debug.Log("=== CHECKING FOR CONTINUOUS FORCES ===");

        if (playerRigidbody == null) return;

        // Check if any MonoBehaviour is applying forces in FixedUpdate
        var monoBehaviours = GetComponents<MonoBehaviour>();

        Debug.Log($"Checking {monoBehaviours.Length} MonoBehaviour components for force application:");

        foreach (var component in monoBehaviours)
        {
            if (component == null) continue;

            var type = component.GetType();
            var fixedUpdateMethod = type.GetMethod("FixedUpdate",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (fixedUpdateMethod != null)
            {
                Debug.Log($"  {type.Name} has FixedUpdate - may be applying forces");

                // Check specific known force-applying components
                if (type.Name.Contains("Swimming") || type.Name.Contains("Buoyancy") || type.Name.Contains("Water"))
                {
                    Debug.LogWarning($"    ^ {type.Name} is water-related and may be applying buoyancy forces!");
                }
            }
        }

        // Check current physics state
        Debug.Log($"Current Rigidbody state:");
        Debug.Log($"  Position: {transform.position}");
        Debug.Log($"  Velocity: {playerRigidbody.linearVelocity}");
        Debug.Log($"  Mass: {playerRigidbody.mass}");
        Debug.Log($"  Drag: {playerRigidbody.linearDamping}");
        Debug.Log($"  UseGravity: {playerRigidbody.useGravity}");
        Debug.Log($"  IsKinematic: {playerRigidbody.isKinematic}");

        Debug.Log("================================");
    }

    [Button("Test Pure Gravity")]
    private void TestPureGravity()
    {
        Debug.Log("[ForceAnalysis] Testing pure gravity - disabling all player scripts temporarily");

        // Disable all player movement components temporarily
        var components = GetComponents<MonoBehaviour>();
        foreach (var comp in components)
        {
            if (comp != this && comp != null &&
                (comp.GetType().Name.Contains("Movement") ||
                 comp.GetType().Name.Contains("Swimming") ||
                 comp.GetType().Name.Contains("Controller")))
            {
                comp.enabled = false;
                Debug.Log($"Disabled {comp.GetType().Name}");
            }
        }

        // Clear velocity and let pure gravity work
        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
        }

        Debug.Log("Pure gravity test started - watch for 3 seconds, then manually re-enable components");

        // Auto re-enable after 3 seconds
        Invoke(nameof(ReEnableComponents), 3f);
    }

    private void ReEnableComponents()
    {
        var components = GetComponents<MonoBehaviour>();
        foreach (var comp in components)
        {
            if (comp != this && comp != null)
            {
                comp.enabled = true;
            }
        }
        Debug.Log("[ForceAnalysis] Re-enabled all components");
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(780, 10, 350, 600));
        GUILayout.BeginVertical("box");

        GUILayout.Label("Force Analysis", EditorStyles.boldLabel);
        GUILayout.Space(5);

        GUILayout.Label($"Movement Mode: {movementMode}");
        GUILayout.Label($"Swimming Active: {swimmingControllerActive}");
        GUILayout.Label($"In Water: {isInWater}");
        GUILayout.Label($"Underwater: {isUnderwater}");
        GUILayout.Space(5);

        GUILayout.Label("Physics Analysis:", EditorStyles.boldLabel);
        GUILayout.Label($"Current Velocity Y: {currentVelocity.y:F2}");
        GUILayout.Label($"Fall Acceleration: {actualFallAcceleration:F2}");
        GUILayout.Label($"Expected Gravity: {-gravityAcceleration:F2}");
        GUILayout.Space(5);

        // Color code the diagnosis
        GUI.color = diagnosedIssue == "No issues detected" ? Color.green : Color.red;
        GUILayout.Label($"Diagnosis: {diagnosedIssue}");
        GUI.color = Color.white;
        GUILayout.Space(5);

        if (GUILayout.Button("Start Fall Test"))
        {
            StartFallTest();
        }

        if (runningFallTest)
        {
            GUILayout.Label($"Fall Test Running: {Time.time - fallTestStartTime:F1}s");
            if (GUILayout.Button("Stop Fall Test"))
            {
                StopFallTest();
            }
        }

        if (GUILayout.Button("Force Disable Swimming"))
        {
            ForceDisableSwimmingController();
        }

        if (GUILayout.Button("Check Forces"))
        {
            CheckForContinuousForces();
        }

        if (GUILayout.Button("Test Pure Gravity"))
        {
            TestPureGravity();
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}