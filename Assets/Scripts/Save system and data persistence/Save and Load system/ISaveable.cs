using UnityEngine;

/// <summary>
/// Core interface for any object that can be saved and loaded.
/// Provides context-aware restoration to handle different loading scenarios.
/// </summary>
public interface ISaveable
{
    /// <summary>
    /// Unique identifier for this saveable object.
    /// Must be consistent between saves/loads for proper data restoration.
    /// </summary>
    string SaveID { get; }

    /// <summary>
    /// Determines whether this object's data persists across scenes or with the current scene.
    /// PlayerDependent: Follows player between scenes (stats, inventory, equipment)
    /// SceneDependent: Tied to specific scenes (enemy states, door locks, pickups)
    /// </summary>
    SaveDataCategory SaveCategory { get; }

    /// <summary>
    /// Extracts the current state data that should be saved for this object.
    /// Called when saving to file or preparing for scene transitions.
    /// </summary>
    object GetDataToSave();

    /// <summary>
    /// Filters relevant data from a larger save container.
    /// Used by persistence managers to extract only the data this component needs.
    /// </summary>
    object ExtractRelevantData(object saveContainer);

    /// <summary>
    /// Restores data to this object with context about why the restoration is happening.
    /// Context determines what gets restored (e.g., skip position during doorway transitions).
    /// </summary>
    /// <param name="data">The data to restore</param>
    /// <param name="context">Why this restoration is happening</param>
    void LoadSaveDataWithContext(object data, RestoreContext context);

    /// <summary>
    /// Called before data collection for saving. Use for cleanup or preparation.
    /// </summary>
    void OnBeforeSave() { }

    /// <summary>
    /// Called after data restoration. Use for triggering UI updates or validation.
    /// </summary>
    void OnAfterLoad() { }
}

/// <summary>
/// Enhanced interface for player-dependent components that need to integrate
/// with the unified save system. Enables true modularity by letting components
/// handle their own data mapping and default state creation.
/// </summary>
public interface IPlayerDependentSaveable : ISaveable
{
    /// <summary>
    /// Extracts this component's data from the unified player data structure.
    /// Each component knows how to find its data in PlayerPersistentData.
    /// </summary>
    object ExtractFromUnifiedSave(PlayerPersistentData unifiedData);

    /// <summary>
    /// Creates appropriate default data for new games.
    /// Each component defines its own starting state.
    /// </summary>
    object CreateDefaultData();

    /// <summary>
    /// Stores this component's data into the unified player data structure.
    /// Called when building save files or preparing scene transitions.
    /// </summary>
    void ContributeToUnifiedSave(object componentData, PlayerPersistentData unifiedData);
}

/// <summary>
/// Categorizes save data by persistence behavior.
/// </summary>
public enum SaveDataCategory
{
    /// <summary>
    /// Data tied to specific scenes. Saved when leaving a scene,
    /// restored when returning. Examples: enemy health, door states, collected items.
    /// </summary>
    SceneDependent,

    /// <summary>
    /// Data that follows the player across scenes. Persists during doorway transitions,
    /// only reset when loading saves. Examples: health, inventory, equipment, abilities.
    /// </summary>
    PlayerDependent
}

/// <summary>
/// Describes why data restoration is happening, allowing components
/// to make appropriate decisions about what to restore.
/// </summary>
public enum RestoreContext
{
    /// <summary>
    /// Player moving between scenes via doorway/portal.
    /// Restore: stats, inventory, equipment, abilities
    /// Skip: player position (doorway sets position)
    /// </summary>
    DoorwayTransition,

    /// <summary>
    /// Loading from a save file.
    /// Restore: everything including exact player position and scene state
    /// </summary>
    SaveFileLoad,

    /// <summary>
    /// Starting a new game.
    /// Set: default values, starting position, clear inventory/progress
    /// </summary>
    NewGame
}