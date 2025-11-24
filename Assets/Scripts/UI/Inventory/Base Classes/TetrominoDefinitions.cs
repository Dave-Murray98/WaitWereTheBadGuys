using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public enum TetrominoType
{
    Single,    // Pink 1x1 square
    Line2,     // Green 1x2 line
    Square,    // Blue 2x2 square
    Line4,     // Purple 1x4 line
    LShape,    // Red L-shape
    Comb,      // Yellow comb/rake shape (was Cross)
    Corner     // Orange corner/truncated L shape (was ZShape)
}

[System.Serializable]
public struct TetrominoData
{
    public TetrominoType type;
    public Vector2Int[] cells;
    public Color color;

    public TetrominoData(TetrominoType type, Vector2Int[] cells, Color color)
    {
        this.type = type;
        this.cells = cells;
        this.color = color;
    }
}

public static class TetrominoDefinitions
{
    private static readonly Dictionary<TetrominoType, TetrominoData[]> _rotationStates =
        new Dictionary<TetrominoType, TetrominoData[]>();

    static TetrominoDefinitions()
    {
        InitializeRotationStates();
    }

    private static void InitializeRotationStates()
    {
        // Single (1x1) - Pink
        _rotationStates[TetrominoType.Single] = new TetrominoData[]
        {
            new TetrominoData(TetrominoType.Single, new Vector2Int[]
            {
                new Vector2Int(0, 0)
            }, new Color(1f, 0.75f, 0.8f)) // Light pink
        };

        // Line2 (1x2) - Green
        _rotationStates[TetrominoType.Line2] = new TetrominoData[]
        {          
            // 90° - Vertical (rotated clockwise)
            new TetrominoData(TetrominoType.Line2, new Vector2Int[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(0, 1)
            }, new Color(0.2f, 0.8f, 0.2f)),

            // 0° - Horizontal
            new TetrominoData(TetrominoType.Line2, new Vector2Int[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(1, 0)
            }, new Color(0.2f, 0.8f, 0.2f)) // Green
        };

        // Square (2x2) - Blue - No rotation needed
        _rotationStates[TetrominoType.Square] = new TetrominoData[]
        {
            new TetrominoData(TetrominoType.Square, new Vector2Int[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(1, 0),
                new Vector2Int(0, 1),
                new Vector2Int(1, 1)
            }, new Color(0.2f, 0.6f, 1f)) // Blue
        };

        // Line4 (1x4) - Purple
        _rotationStates[TetrominoType.Line4] = new TetrominoData[]
        {
            // 0° - Horizontal
            new TetrominoData(TetrominoType.Line4, new Vector2Int[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(1, 0),
                new Vector2Int(2, 0),
                new Vector2Int(3, 0)
            }, new Color(0.7f, 0.3f, 1f)), // Purple
            
            // 90° - Vertical (rotated clockwise)
            new TetrominoData(TetrominoType.Line4, new Vector2Int[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(0, 1),
                new Vector2Int(0, 2),
                new Vector2Int(0, 3)
            }, new Color(0.7f, 0.3f, 1f))
        };

        // LShape - Red (Your corrected rotations)
        _rotationStates[TetrominoType.LShape] = new TetrominoData[]
        {
            // 0° - Rotated clockwise
            new TetrominoData(TetrominoType.LShape, new Vector2Int[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(1, 0),
                new Vector2Int(2, 0),
                new Vector2Int(0, 1)
            }, new Color(1f, 0.2f, 0.2f)),

            // 90° - Rotated clockwise
            new TetrominoData(TetrominoType.LShape, new Vector2Int[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(1, 0),
                new Vector2Int(1, 1),
                new Vector2Int(1, 2)
            }, new Color(1f, 0.2f, 0.2f)),

            // 180° - Rotated clockwise
            new TetrominoData(TetrominoType.LShape, new Vector2Int[]
            {
                new Vector2Int(2, 0),
                new Vector2Int(0, 1),
                new Vector2Int(1, 1),
                new Vector2Int(2, 1)
            }, new Color(1f, 0.2f, 0.2f)),

            // 270° - Base L shape
            new TetrominoData(TetrominoType.LShape, new Vector2Int[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(0, 1),
                new Vector2Int(0, 2),
                new Vector2Int(1, 2)
            }, new Color(1f, 0.2f, 0.2f)) // Red
        };

        // Comb/Rake Shape - Yellow (Your corrected rotations)
        _rotationStates[TetrominoType.Comb] = new TetrominoData[]
        {
            // 0° - Horizontal comb with teeth pointing down
            new TetrominoData(TetrominoType.Comb, new Vector2Int[]
            {
                new Vector2Int(0, 0),     // Horizontal line
                new Vector2Int(1, 0),
                new Vector2Int(2, 0),
                new Vector2Int(3, 0),
                new Vector2Int(1, 1),     // Left tooth
                new Vector2Int(3, 1)      // Right tooth
            }, new Color(1f, 1f, 0.2f)), // Yellow

            // 90° - Vertical comb with teeth pointing left (rotated clockwise)
            new TetrominoData(TetrominoType.Comb, new Vector2Int[]
            {
                new Vector2Int(1, 1),     // Upper tooth
                new Vector2Int(1, 3),     // Lower tooth
                new Vector2Int(2, 0),     // Vertical line
                new Vector2Int(2, 1),
                new Vector2Int(2, 2),
                new Vector2Int(2, 3)
            }, new Color(1f, 1f, 0.2f)),

            // 180° - Horizontal comb with teeth pointing up (rotated clockwise)
            new TetrominoData(TetrominoType.Comb, new Vector2Int[]
            {
                new Vector2Int(0, 0),     // Left tooth
                new Vector2Int(2, 0),     // Right tooth
                new Vector2Int(0, 1),     // Horizontal line
                new Vector2Int(1, 1),
                new Vector2Int(2, 1),
                new Vector2Int(3, 1)
            }, new Color(1f, 1f, 0.2f)),

            // 270° - Vertical comb with teeth pointing right (rotated clockwise)
            new TetrominoData(TetrominoType.Comb, new Vector2Int[]
            {
                new Vector2Int(0, 0),     // Vertical line
                new Vector2Int(0, 1),
                new Vector2Int(0, 2),
                new Vector2Int(0, 3),
                new Vector2Int(1, 0),     // Upper tooth
                new Vector2Int(1, 2)      // Lower tooth
            }, new Color(1f, 1f, 0.2f))
        };

        // Corner/Truncated L Shape - Orange (Your corrected rotations)
        _rotationStates[TetrominoType.Corner] = new TetrominoData[]
        {
            // 0° - Base corner
            new TetrominoData(TetrominoType.Corner, new Vector2Int[]
            {
                new Vector2Int(0, 0),     // Corner base
                new Vector2Int(1, 0),     // Right extension
                new Vector2Int(0, 1)      // Down extension
            }, new Color(1f, 0.5f, 0.2f)), // Orange

            // 90°- Rotated clockwise
            new TetrominoData(TetrominoType.Corner, new Vector2Int[]
            {
                new Vector2Int(0, 0),     // Left extension
                new Vector2Int(1, 0),     // Corner base
                new Vector2Int(1, 1)      // Down extension
            }, new Color(1f, 0.5f, 0.2f)),

            // 180° - Rotated clockwise
            new TetrominoData(TetrominoType.Corner, new Vector2Int[]
            {
                new Vector2Int(1, 0),     // Up extension
                new Vector2Int(0, 1),     // Left extension
                new Vector2Int(1, 1)      // Corner base
            }, new Color(1f, 0.5f, 0.2f)),
            
            // 270° - Rotated clockwise
            new TetrominoData(TetrominoType.Corner, new Vector2Int[]
            {
                new Vector2Int(0, 0),     // Up extension
                new Vector2Int(0, 1),     // Corner base
                new Vector2Int(1, 1)      // Right extension
            }, new Color(1f, 0.5f, 0.2f))
        };
    }

    public static TetrominoData[] GetRotationStates(TetrominoType type)
    {
        return _rotationStates.TryGetValue(type, out var states) ? states : new TetrominoData[0];
    }

    public static TetrominoData GetRotationState(TetrominoType type, int rotation)
    {
        var states = GetRotationStates(type);
        if (states.Length == 0) return default;

        int clampedRotation = rotation % states.Length;
        return states[clampedRotation];
    }

    public static int GetRotationCount(TetrominoType type)
    {
        return GetRotationStates(type).Length;
    }
}