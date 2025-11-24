using System.Collections.Generic;
using UnityEngine;
using NWH.VehiclePhysics2.Damage;
using System.Linq;

/// <summary>
/// Enhanced VehicleSaveData that includes damage and mesh deformation data
/// </summary>
[System.Serializable]
public class VehicleDamageData
{
    [Header("Damage State")]
    public float overallDamage = 0f;
    public float engineDamage = 0f;
    public float transmissionDamage = 0f;
    public List<float> wheelDamages = new List<float>();

    [Header("Mesh Deformation")]
    public List<MeshDeformationData> meshDeformations = new List<MeshDeformationData>();

    [Header("Collision Settings")]
    public float lastCollisionTime = -1f;

    public VehicleDamageData()
    {
        // Default constructor
    }

    /// <summary>
    /// Update damage data from a DamageHandler component
    /// </summary>
    public void UpdateFromDamageHandler(DamageHandler damageHandler, NWH.VehiclePhysics2.VehicleController vehicle)
    {
        if (damageHandler == null || vehicle == null) return;

        // Capture overall damage
        overallDamage = damageHandler.Damage;
        lastCollisionTime = damageHandler.lastCollisionTime;

        // Capture powertrain damage
        engineDamage = vehicle.powertrain.engine.Damage;
        transmissionDamage = vehicle.powertrain.transmission.Damage;

        // Capture wheel damage
        wheelDamages.Clear();
        for (int i = 0; i < vehicle.powertrain.wheelCount; i++)
        {
            wheelDamages.Add(vehicle.powertrain.wheels[i].wheelUAPI.Damage);
        }

        // Capture mesh deformation data
        CaptureDeformedMeshes(damageHandler);

    }

    /// <summary>
    /// Apply damage data to a DamageHandler component
    /// </summary>
    public void ApplyToDamageHandler(DamageHandler damageHandler, VehicleController vehicle)
    {
        if (damageHandler == null || vehicle == null) return;

        // Apply mesh deformation
        ApplyDeformedMeshes(damageHandler);

    }

    /// <summary>
    /// Capture current mesh deformation states
    /// </summary>
    private void CaptureDeformedMeshes(DamageHandler damageHandler)
    {
        meshDeformations.Clear();

        // Get deformable mesh filters using reflection
        var deformableMeshFiltersField = typeof(DamageHandler).GetField("_deformableMeshFilters",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var originalMeshesField = typeof(DamageHandler).GetField("_originalMeshes",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (deformableMeshFiltersField?.GetValue(damageHandler) is List<MeshFilter> deformableMeshFilters &&
            originalMeshesField?.GetValue(damageHandler) is List<Mesh> originalMeshes)
        {
            for (int i = 0; i < deformableMeshFilters.Count && i < originalMeshes.Count; i++)
            {
                var meshFilter = deformableMeshFilters[i];
                var originalMesh = originalMeshes[i];

                if (meshFilter?.mesh != null && originalMesh != null)
                {
                    // Check if mesh has been deformed by comparing vertex counts and positions
                    Vector3[] currentVertices = meshFilter.mesh.vertices;
                    Vector3[] originalVertices = originalMesh.vertices;

                    bool isDeformed = false;
                    if (currentVertices.Length == originalVertices.Length)
                    {
                        // Sample a few vertices to check for deformation (more efficient than checking all)
                        int sampleCount = Mathf.Min(currentVertices.Length, 50);
                        for (int v = 0; v < sampleCount; v++)
                        {
                            int index = (v * currentVertices.Length) / sampleCount;
                            if (Vector3.Distance(currentVertices[index], originalVertices[index]) > 0.001f)
                            {
                                isDeformed = true;
                                break;
                            }
                        }
                    }

                    if (isDeformed)
                    {
                        var deformationData = new MeshDeformationData
                        {
                            meshName = meshFilter.name,
                            meshIndex = i,
                            deformedVertices = currentVertices.ToList(),
                            transformPath = GetTransformPath(meshFilter.transform, damageHandler.transform)
                        };

                        meshDeformations.Add(deformationData);
                    }
                }
            }
        }

    }

    /// <summary>
    /// Apply saved mesh deformation states
    /// </summary>
    private void ApplyDeformedMeshes(DamageHandler damageHandler)
    {
        if (meshDeformations.Count == 0) return;

        // Get deformable mesh filters using reflection
        var deformableMeshFiltersField = typeof(DamageHandler).GetField("_deformableMeshFilters",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (deformableMeshFiltersField?.GetValue(damageHandler) is List<MeshFilter> deformableMeshFilters)
        {
            foreach (var deformationData in meshDeformations)
            {
                // Find the corresponding mesh filter
                MeshFilter targetMeshFilter = null;

                // Try by index first
                if (deformationData.meshIndex >= 0 && deformationData.meshIndex < deformableMeshFilters.Count)
                {
                    targetMeshFilter = deformableMeshFilters[deformationData.meshIndex];
                }

                // Fall back to finding by name and transform path
                if (targetMeshFilter == null || targetMeshFilter.name != deformationData.meshName)
                {
                    targetMeshFilter = deformableMeshFilters.FirstOrDefault(mf =>
                        mf.name == deformationData.meshName &&
                        GetTransformPath(mf.transform, damageHandler.transform) == deformationData.transformPath);
                }

                if (targetMeshFilter != null && targetMeshFilter.mesh != null)
                {
                    // Create a new mesh instance and apply deformed vertices
                    Mesh deformedMesh = Object.Instantiate(targetMeshFilter.mesh);
                    deformedMesh.vertices = deformationData.deformedVertices.ToArray();
                    deformedMesh.RecalculateBounds();
                    deformedMesh.RecalculateNormals();
                    deformedMesh.RecalculateTangents();

                    targetMeshFilter.mesh = deformedMesh;

                    Debug.Log($"[VehicleDamageData] Applied deformation to mesh: {deformationData.meshName}");
                }
                else
                {
                    Debug.LogWarning($"[VehicleDamageData] Could not find mesh filter for: {deformationData.meshName}");
                }
            }
        }

        Debug.Log($"[VehicleDamageData] Applied {meshDeformations.Count} mesh deformations");
    }

    /// <summary>
    /// Get transform path relative to root for identification
    /// </summary>
    private string GetTransformPath(Transform transform, Transform root)
    {
        if (transform == root) return "";

        List<string> path = new List<string>();
        Transform current = transform;

        while (current != null && current != root)
        {
            path.Add(current.name);
            current = current.parent;
        }

        path.Reverse();
        return string.Join("/", path);
    }
}

/// <summary>
/// Data structure for individual mesh deformation
/// </summary>
[System.Serializable]
public class MeshDeformationData
{
    public string meshName;
    public int meshIndex = -1;
    public List<Vector3> deformedVertices = new List<Vector3>();
    public string transformPath;
}

/// <summary>
/// Enhanced VehicleSaveData that includes damage information
/// </summary>

/// <summary>
/// Enhanced VehicleController with damage restoration support
/// </summary>
public static class VehicleControllerDamageExtensions
{
    /// <summary>
    /// Enhanced ApplyVehicleState that includes damage restoration
    /// </summary>
    public static void ApplyVehicleStateWithDamage(this VehicleController vehicle, VehicleSaveData saveData)
    {
        if (saveData == null || vehicle == null)
        {
            Debug.LogWarning("[VehicleControllerDamageExtensions] Invalid data for damage restoration");
            return;
        }

        // Apply base vehicle state (position, rotation, etc.)
        vehicle.StopAllInputs();

        // Handle physics
        var rb = vehicle.GetComponent<Rigidbody>();
        bool wasKinematic = false;

        if (rb != null)
        {
            wasKinematic = rb.isKinematic;
            rb.isKinematic = true;
            // rb.linearVelocity = Vector3.zero;
            // rb.angularVelocity = Vector3.zero;

            // Use rb.Move for proper physics positioning
            rb.Move(saveData.position, saveData.rotation);
        }
        else
        {
            // Fallback to transform
            vehicle.transform.SetPositionAndRotation(saveData.position, saveData.rotation);
        }

        // Apply operational state
        vehicle.SetOperational(saveData.isOperational);

        // Apply damage data
        var damageHandler = vehicle.GetComponent<DamageHandler>();
        if (damageHandler != null && saveData.damageData != null)
        {
            saveData.damageData.ApplyToDamageHandler(damageHandler, vehicle);
        }

        // Restore physics state after a delay
        if (rb != null)
        {
            vehicle.StartCoroutine(RestorePhysicsAfterDelay(rb, wasKinematic));
        }

    }

    private static System.Collections.IEnumerator RestorePhysicsAfterDelay(Rigidbody rb, bool wasKinematic)
    {
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        if (rb != null)
        {
            rb.isKinematic = wasKinematic;
        }
    }
}