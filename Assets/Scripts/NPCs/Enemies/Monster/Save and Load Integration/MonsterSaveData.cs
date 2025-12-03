using UnityEngine;

/// <summary>
/// Save data structure for the underwater monster.
/// Stores the 5 essential data points: health, alive status, transform, and state.
/// Designed for single monster scenarios in the underwater survival horror game.
/// </summary>
[System.Serializable]
public class MonsterSaveData
{
    [Header("Health Data")]
    public float currentHealth = 100f;
    public bool isAlive = true;

    [Header("Transform Data")]
    public Vector3 position = Vector3.zero;
    public Quaternion rotation = Quaternion.identity;

    [Header("State Data")]
    public string currentStateName = "Patrolling";

    [Header("Metadata")]
    public string saveTimestamp;
    public string monsterID = "Monster_Main";

    #region Constructors

    public MonsterSaveData()
    {
        saveTimestamp = System.DateTime.Now.ToString();
    }

    public MonsterSaveData(float health, bool alive, Vector3 pos, Quaternion rot, string stateName, string id = "Monster_Main")
    {
        currentHealth = health;
        isAlive = alive;
        position = pos;
        rotation = rot;
        currentStateName = stateName;
        monsterID = id;
        saveTimestamp = System.DateTime.Now.ToString();
    }

    #endregion

    #region Data Management

    /// <summary>
    /// Updates save data from monster components
    /// </summary>
    public void UpdateFromMonster(UnderwaterMonsterController controller)
    {
        if (controller == null) return;

        // Extract health data
        if (controller.health != null)
        {
            currentHealth = controller.health.CurrentHealth;
            isAlive = controller.health.isAlive;
        }

        // Extract transform data
        if (controller.transform != null)
        {
            position = controller.transform.position;
            rotation = controller.transform.rotation;
        }

        // Extract state data
        if (controller.stateMachine?.currentState != null)
        {
            currentStateName = controller.stateMachine.currentState.name;
        }

        monsterID = "Monster_Main";
        saveTimestamp = System.DateTime.Now.ToString();
    }

    /// <summary>
    /// Applies this save data to monster components
    /// </summary>
    public void ApplyToMonster(UnderwaterMonsterController controller, RestoreContext context)
    {
        if (controller == null) return;

        // Always restore health and alive status
        if (controller.health != null)
        {
            controller.health.CurrentHealth = currentHealth;
            controller.health.isAlive = isAlive;

            // If monster is dead, make sure health reflects that
            if (!isAlive && controller.health.CurrentHealth > 0)
            {
                controller.health.CurrentHealth = 0;
            }
        }

        // Always restore position and rotation (simplified approach)
        ApplyTransformData(controller);

        // Always try to restore state (with validation)
        ApplyStateData(controller);
    }

    /// <summary>
    /// Applies transform data to monster
    /// </summary>
    private void ApplyTransformData(UnderwaterMonsterController controller)
    {
        if (controller.transform != null)
        {
            // Use rigidbody move for physics safety
            if (controller.rb != null)
            {
                // Clear velocity first
                controller.rb.linearVelocity = Vector3.zero;
                controller.rb.angularVelocity = Vector3.zero;

                // Move to saved position
                controller.rb.Move(position, rotation);
            }
            else
            {
                // Fallback: direct transform modification
                controller.transform.position = position;
                controller.transform.rotation = rotation;
            }
        }
    }

    /// <summary>
    /// Applies state data to monster with validation
    /// </summary>
    private void ApplyStateData(UnderwaterMonsterController controller)
    {
        if (controller.stateMachine == null || string.IsNullOrEmpty(currentStateName)) return;

        // Handle death state specially
        if (!isAlive)
        {
            // Force to death state if monster is dead
            if (controller.stateMachine.deathState != null)
            {
                controller.stateMachine.ForceChangeState(controller.stateMachine.deathState);
            }
            return;
        }

        // Try to restore saved state for living monsters
        EnemyState targetState = GetStateByName(controller.stateMachine, currentStateName);
        if (targetState != null)
        {
            controller.stateMachine.ForceChangeState(targetState);
        }
        else
        {
            // Fallback to patrol state if saved state is invalid
            if (controller.stateMachine.patrolState != null)
            {
                controller.stateMachine.ForceChangeState(controller.stateMachine.patrolState);
            }
        }
    }

    /// <summary>
    /// Gets a state by name from the state machine
    /// </summary>
    private EnemyState GetStateByName(EnemyStateMachine stateMachine, string stateName)
    {
        return stateName switch
        {
            "Patrolling" => stateMachine.patrolState,
            "Engaging" => stateMachine.engageState,
            "Pursuing" => stateMachine.pursueState,
            "Investigating Noise" => stateMachine.investigateState,
            "Death" => stateMachine.deathState,
            _ => null
        };
    }

    #endregion

    #region Validation

    /// <summary>
    /// Validates the integrity of the save data
    /// </summary>
    public bool IsValid()
    {
        // Health validation
        if (currentHealth < 0) return false;

        // Alive status validation
        if (!isAlive && currentHealth > 0) return false;

        // Position validation (basic NaN check)
        if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z)) return false;

        // State validation
        if (string.IsNullOrEmpty(currentStateName)) return false;

        return true;
    }

    /// <summary>
    /// Creates safe default values if current data is invalid
    /// </summary>
    public void SetDefaults()
    {
        currentHealth = 100f;
        isAlive = true;
        position = Vector3.zero;
        rotation = Quaternion.identity;
        currentStateName = "PatrollingState";
        monsterID = "Monster_Main";
        saveTimestamp = System.DateTime.Now.ToString();
    }

    #endregion

    #region Debug and Utility

    /// <summary>
    /// Returns detailed debug information about the save data
    /// </summary>
    public string GetDebugInfo()
    {
        return $"MonsterSaveData[ID: {monsterID}]\n" +
               $"  Health: {currentHealth} (Alive: {isAlive})\n" +
               $"  Position: {position}\n" +
               $"  Rotation: {rotation.eulerAngles}\n" +
               $"  State: {currentStateName}\n" +
               $"  Saved: {saveTimestamp}";
    }

    public override string ToString()
    {
        return $"Monster[{monsterID}] - HP:{currentHealth} Alive:{isAlive} State:{currentStateName} Pos:{position}";
    }

    #endregion
}