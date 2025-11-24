using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Text;
using Sirenix.OdinInspector;

/// <summary>
/// Comprehensive debug system for the inventory to help diagnose rotation and placement issues
/// </summary>
public class InventoryDebugSystem : MonoBehaviour
{
    [Header("Debug UI References")]
    [SerializeField] private GameObject debugPanel;
    [SerializeField] private TextMeshProUGUI debugText;
    [SerializeField] private TextMeshProUGUI gridVisualizationText;
    [SerializeField] private Toggle enableDebugToggle;
    [SerializeField] private Button refreshDebugButton;

    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugMode = true;
    [SerializeField] private bool showGridVisualization = true;
    [SerializeField] private bool showDetailedItemInfo = true;
    [SerializeField] private bool logRotationAttempts = true;
    [SerializeField] private bool logPlacementAttempts = true;
    [SerializeField] private KeyCode toggleDebugKey = KeyCode.F12;

    [Header("Visual Debug")]
    [SerializeField] private GameObject cellDebugPrefab;
    [SerializeField] private Color occupiedCellColor = Color.red;
    [SerializeField] private Color freeCellColor = Color.green;
    [SerializeField] private Color invalidCellColor = Color.magenta;

    private PlayerInventoryManager persistentInventory;
    private PlayerInventoryGridVisual gridVisual;
    private List<GameObject> debugCells = new List<GameObject>();
    private StringBuilder debugStringBuilder = new StringBuilder();

    // Track debug events
    private List<string> recentEvents = new List<string>();
    private const int maxEvents = 20;

    private void Awake()
    {
        CreateDebugUI();
        persistentInventory = PlayerInventoryManager.Instance;
    }

    private void Start()
    {
        if (persistentInventory == null)
            persistentInventory = FindFirstObjectByType<PlayerInventoryManager>();

        gridVisual = FindFirstObjectByType<PlayerInventoryGridVisual>();

        if (enableDebugToggle != null)
            enableDebugToggle.onValueChanged.AddListener(OnDebugToggleChanged);

        if (refreshDebugButton != null)
            refreshDebugButton.onClick.AddListener(RefreshDebugInfo);

        // Subscribe to inventory events for debugging
        SubscribeToDebugEvents();

        SetDebugPanelActive(enableDebugMode);
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleDebugKey))
        {
            ToggleDebugMode();
        }

        if (enableDebugMode)
        {
            UpdateDebugInfo();
        }
    }

    private void CreateDebugUI()
    {
        if (debugPanel == null)
        {
            // Create debug panel if it doesn't exist
            GameObject canvas = GameObject.Find("Canvas");
            if (canvas == null)
            {
                Debug.LogWarning("No Canvas found for debug UI");
                return;
            }

            debugPanel = new GameObject("InventoryDebugPanel");
            debugPanel.transform.SetParent(canvas.transform, false);

            // Setup panel
            RectTransform panelRect = debugPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 0);
            panelRect.anchorMax = new Vector2(0.4f, 1);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            Image panelImage = debugPanel.AddComponent<Image>();
            panelImage.color = new Color(0, 0, 0, 0.8f);

            // Create debug text
            GameObject textObj = new GameObject("DebugText");
            textObj.transform.SetParent(debugPanel.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 10);
            textRect.offsetMax = new Vector2(-10, -10);

            debugText = textObj.AddComponent<TextMeshProUGUI>();
            debugText.text = "Inventory Debug Info";
            debugText.fontSize = 12;
            debugText.color = Color.white;
            debugText.alignment = TextAlignmentOptions.TopLeft;
        }
    }

    private void SubscribeToDebugEvents()
    {
        if (persistentInventory != null)
        {
            persistentInventory.OnItemAdded += OnItemAddedDebug;
            persistentInventory.OnItemRemoved += OnItemRemovedDebug;
            persistentInventory.OnInventoryDataChanged += OnInventoryDataChangedDebug;
        }
    }

    #region Debug Event Handlers

    private void OnItemAddedDebug(InventoryItemData item)
    {
        LogDebugEvent($"ITEM ADDED: {item.ItemData?.itemName} at {item.GridPosition} (Rotation: {item.currentRotation})");
        RefreshGridVisualization();
    }

    private void OnItemRemovedDebug(string itemId)
    {
        LogDebugEvent($"ITEM REMOVED: {itemId}");
        RefreshGridVisualization();
    }

    private void OnInventoryDataChangedDebug(InventoryGridData gridData)
    {
        LogDebugEvent($"INVENTORY DATA CHANGED: {gridData.ItemCount} items");
        RefreshGridVisualization();
    }

    #endregion

    private void LogDebugEvent(string eventText)
    {
        string timestampedEvent = $"[{Time.time:F2}] {eventText}";
        recentEvents.Add(timestampedEvent);

        if (recentEvents.Count > maxEvents)
        {
            recentEvents.RemoveAt(0);
        }

        if (enableDebugMode)
        {
            Debug.Log($"[InventoryDebug] {timestampedEvent}");
        }
    }

    private void UpdateDebugInfo()
    {
        if (debugText == null || persistentInventory == null) return;

        debugStringBuilder.Clear();
        debugStringBuilder.AppendLine("=== INVENTORY DEBUG INFO ===");
        debugStringBuilder.AppendLine($"Debug Mode: {enableDebugMode}");
        debugStringBuilder.AppendLine($"Time: {Time.time:F2}");
        debugStringBuilder.AppendLine();

        // Grid stats
        var stats = persistentInventory.GetInventoryStats();
        debugStringBuilder.AppendLine("=== GRID STATS ===");
        debugStringBuilder.AppendLine($"Grid Size: {persistentInventory.GridWidth}x{persistentInventory.GridHeight}");
        debugStringBuilder.AppendLine($"Total Items: {stats.itemCount}");
        debugStringBuilder.AppendLine($"Occupied Cells: {stats.occupiedCells}/{stats.totalCells}");
        debugStringBuilder.AppendLine($"Grid Utilization: {(float)stats.occupiedCells / stats.totalCells * 100:F1}%");
        debugStringBuilder.AppendLine();

        // Detailed item info
        if (showDetailedItemInfo)
        {
            debugStringBuilder.AppendLine("=== ITEMS DETAIL ===");
            var allItems = persistentInventory.InventoryGridData.GetAllItems();
            foreach (var item in allItems)
            {
                debugStringBuilder.AppendLine($"ID: {item.ID}");
                debugStringBuilder.AppendLine($"  Name: {item.ItemData?.itemName ?? "NULL"}");
                debugStringBuilder.AppendLine($"  Position: {item.GridPosition}");
                debugStringBuilder.AppendLine($"  Rotation: {item.currentRotation}/{TetrominoDefinitions.GetRotationCount(item.shapeType)}");
                debugStringBuilder.AppendLine($"  Shape: {item.shapeType}");
                debugStringBuilder.AppendLine($"  Can Rotate: {item.CanRotate}");

                // Show occupied cells
                var occupiedCells = item.GetOccupiedPositions();
                debugStringBuilder.Append($"  Occupied Cells: ");
                foreach (var cell in occupiedCells)
                {
                    debugStringBuilder.Append($"({cell.x},{cell.y}) ");
                }
                debugStringBuilder.AppendLine();
                debugStringBuilder.AppendLine();
            }
        }

        // Recent events
        debugStringBuilder.AppendLine("=== RECENT EVENTS ===");
        for (int i = recentEvents.Count - 1; i >= 0; i--)
        {
            debugStringBuilder.AppendLine(recentEvents[i]);
        }

        // Grid visualization
        if (showGridVisualization)
        {
            debugStringBuilder.AppendLine();
            debugStringBuilder.AppendLine("=== GRID VISUALIZATION ===");
            debugStringBuilder.AppendLine(GetGridVisualizationString());
        }

        debugText.text = debugStringBuilder.ToString();
    }

    private string GetGridVisualizationString()
    {
        if (persistentInventory?.InventoryGridData == null) return "Grid data not available";

        var gridData = persistentInventory.InventoryGridData;
        StringBuilder gridBuilder = new StringBuilder();

        // Header with column numbers
        gridBuilder.Append("   ");
        for (int x = 0; x < gridData.Width; x++)
        {
            gridBuilder.Append($"{x % 10} ");
        }
        gridBuilder.AppendLine();

        // Grid content
        for (int y = 0; y < gridData.Height; y++)
        {
            gridBuilder.Append($"{y:D2} ");
            for (int x = 0; x < gridData.Width; x++)
            {
                var item = gridData.GetItemAt(x, y);
                if (item != null)
                {
                    gridBuilder.Append("X ");
                }
                else
                {
                    gridBuilder.Append(". ");
                }
            }
            gridBuilder.AppendLine();
        }

        return gridBuilder.ToString();
    }

    #region Public Debug Methods

    [Button("Test Item Placement")]
    public void TestItemPlacement()
    {
        if (persistentInventory == null) return;

        LogDebugEvent("=== TESTING ITEM PLACEMENT ===");

        // Test each position for a simple 1x1 item
        var testItemData = ScriptableObject.CreateInstance<ItemData>();
        testItemData.shapeType = TetrominoType.Single;
        testItemData.itemName = "TestItem";

        var testItem = new InventoryItemData("test", testItemData, Vector2Int.zero);

        for (int y = 0; y < persistentInventory.GridHeight; y++)
        {
            for (int x = 0; x < persistentInventory.GridWidth; x++)
            {
                Vector2Int testPos = new Vector2Int(x, y);
                bool isValid = persistentInventory.InventoryGridData.IsValidPosition(testPos, testItem);
                LogDebugEvent($"Position ({x},{y}): {(isValid ? "VALID" : "INVALID")}");
            }
        }
    }

    [Button("Test Item Rotation")]
    public void TestItemRotation()
    {
        if (persistentInventory?.InventoryGridData == null) return;

        LogDebugEvent("=== TESTING ITEM ROTATION ===");

        var allItems = persistentInventory.InventoryGridData.GetAllItems();
        foreach (var item in allItems)
        {
            LogDebugEvent($"Testing rotation for item {item.ID} ({item.ItemData?.itemName})");
            LogDebugEvent($"  Current rotation: {item.currentRotation}");
            LogDebugEvent($"  Can rotate: {item.CanRotate}");
            LogDebugEvent($"  Max rotations: {TetrominoDefinitions.GetRotationCount(item.shapeType)}");

            // Test each rotation state
            for (int rot = 0; rot < TetrominoDefinitions.GetRotationCount(item.shapeType); rot++)
            {
                bool isValid = persistentInventory.InventoryGridData.IsValidPosition(item.GridPosition, item, rot);
                LogDebugEvent($"  Rotation {rot}: {(isValid ? "VALID" : "INVALID")} at current position");
            }
        }
    }

    [Button("Highlight Invalid Placements")]
    public void HighlightInvalidPlacements()
    {
        ClearDebugCells();

        if (persistentInventory?.InventoryGridData == null || gridVisual == null) return;

        // Test every grid position with a 1x1 item
        var testItemData = ScriptableObject.CreateInstance<ItemData>();
        testItemData.shapeType = TetrominoType.Single;
        var testItem = new InventoryItemData("debug_test", testItemData, Vector2Int.zero);

        for (int y = 0; y < persistentInventory.GridHeight; y++)
        {
            for (int x = 0; x < persistentInventory.GridWidth; x++)
            {
                Vector2Int testPos = new Vector2Int(x, y);
                bool isValid = persistentInventory.InventoryGridData.IsValidPosition(testPos, testItem);
                bool isOccupied = persistentInventory.InventoryGridData.IsOccupied(x, y);

                Color cellColor;
                if (isOccupied)
                    cellColor = occupiedCellColor;
                else if (isValid)
                    cellColor = freeCellColor;
                else
                    cellColor = invalidCellColor;

                CreateDebugCell(x, y, cellColor);
            }
        }
    }

    [Button("Clear Debug Visuals")]
    public void ClearDebugVisuals()
    {
        ClearDebugCells();
    }

    #endregion

    private void CreateDebugCell(int x, int y, Color color)
    {
        if (gridVisual == null) return;

        GameObject debugCell;
        if (cellDebugPrefab != null)
        {
            debugCell = Instantiate(cellDebugPrefab, gridVisual.transform);
        }
        else
        {
            debugCell = new GameObject($"DebugCell_{x}_{y}");
            debugCell.transform.SetParent(gridVisual.transform, false);

            RectTransform rect = debugCell.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(gridVisual.CellSize * 0.8f, gridVisual.CellSize * 0.8f);
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0.5f, 0.5f);

            Image image = debugCell.AddComponent<Image>();
            image.raycastTarget = false;
        }

        // Position the debug cell
        RectTransform debugRect = debugCell.GetComponent<RectTransform>();
        Vector2 cellPos = gridVisual.GetCellWorldPosition(x, y);
        cellPos += new Vector2(gridVisual.CellSize * 0.5f, -gridVisual.CellSize * 0.5f); // Center it
        debugRect.localPosition = cellPos;

        // Set color
        Image debugImage = debugCell.GetComponent<Image>();
        color.a = 0.5f; // Make it semi-transparent
        debugImage.color = color;

        debugCells.Add(debugCell);
    }

    private void ClearDebugCells()
    {
        foreach (var cell in debugCells)
        {
            if (cell != null)
                DestroyImmediate(cell);
        }
        debugCells.Clear();
    }

    private void RefreshGridVisualization()
    {
        if (showGridVisualization)
        {
            // Could trigger visual refresh here if needed
        }
    }

    #region UI Event Handlers

    private void OnDebugToggleChanged(bool enabled)
    {
        SetDebugPanelActive(enabled);
    }

    private void RefreshDebugInfo()
    {
        UpdateDebugInfo();
        LogDebugEvent("Debug info manually refreshed");
    }

    private void ToggleDebugMode()
    {
        enableDebugMode = !enableDebugMode;
        SetDebugPanelActive(enableDebugMode);

        if (enableDebugToggle != null)
            enableDebugToggle.isOn = enableDebugMode;
    }

    private void SetDebugPanelActive(bool active)
    {
        enableDebugMode = active;
        if (debugPanel != null)
            debugPanel.SetActive(active);

        if (active)
            UpdateDebugInfo();
    }

    #endregion

    #region Static Debug Methods for External Use

    /// <summary>
    /// Log a debug message from external systems
    /// </summary>
    public static void LogItemRotationAttempt(string itemId, int fromRotation, int toRotation, bool success, string reason = "")
    {
        var debugSystem = FindFirstObjectByType<InventoryDebugSystem>();
        if (debugSystem != null && debugSystem.logRotationAttempts)
        {
            string message = $"ROTATION: {itemId} {fromRotation}→{toRotation} {(success ? "SUCCESS" : "FAILED")}";
            if (!string.IsNullOrEmpty(reason))
                message += $" ({reason})";
            debugSystem.LogDebugEvent(message);
        }
    }

    /// <summary>
    /// Log a placement attempt from external systems
    /// </summary>
    public static void LogItemPlacementAttempt(string itemId, Vector2Int position, bool success, string reason = "")
    {
        var debugSystem = FindFirstObjectByType<InventoryDebugSystem>();
        if (debugSystem != null && debugSystem.logPlacementAttempts)
        {
            string message = $"PLACEMENT: {itemId} at {position} {(success ? "SUCCESS" : "FAILED")}";
            if (!string.IsNullOrEmpty(reason))
                message += $" ({reason})";
            debugSystem.LogDebugEvent(message);
        }
    }

    #endregion

    private void OnDestroy()
    {
        ClearDebugCells();

        if (persistentInventory != null)
        {
            persistentInventory.OnItemAdded -= OnItemAddedDebug;
            persistentInventory.OnItemRemoved -= OnItemRemovedDebug;
            persistentInventory.OnInventoryDataChanged -= OnInventoryDataChangedDebug;
        }
    }
}