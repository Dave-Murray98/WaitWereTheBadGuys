using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Visual renderer for inventory items - pure visual component
/// </summary>
public class InventoryItemVisualRenderer : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private float imagePadding = 4f;
    [SerializeField] private Color borderColor = Color.black;
    [SerializeField] private float borderWidth = 1f;

    private InventoryItemData itemData;
    private BaseInventoryGridVisual gridVisual;
    private List<GameObject> cellObjects = new List<GameObject>();
    private GameObject imageOverlay;
    private RectTransform rectTransform;
    private InventoryHotkeyIndicator hotkeyIndicator;

    public InventoryItemData ItemData => itemData;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    public void Initialize(InventoryItemData item, BaseInventoryGridVisual visual)
    {
        itemData = item;
        gridVisual = visual;
        RefreshVisual();

        hotkeyIndicator = GetComponent<InventoryHotkeyIndicator>();
        if (hotkeyIndicator != null)
        {
            hotkeyIndicator.Initialize(itemData);
        }
    }

    public void RefreshVisual()
    {
        if (itemData?.ItemData == null)
        {
            Debug.LogWarning("Cannot refresh visual - no item data");
            return;
        }

        ClearVisuals();
        CreateCells();
        UpdateLayout();
        RefreshImageAndHotkeyIndicatorVisuals();
        UpdatePosition();
    }

    private void UpdatePosition()
    {
        if (itemData != null && gridVisual != null)
        {
            Vector2 gridPos = gridVisual.GetCellWorldPosition(itemData.GridPosition.x, itemData.GridPosition.y);
            rectTransform.localPosition = gridPos;
        }
    }

    private void ClearVisuals()
    {
        foreach (var cell in cellObjects)
        {
            if (cell != null)
                Destroy(cell);
        }
        cellObjects.Clear();

        if (imageOverlay != null)
        {
            Destroy(imageOverlay);
            imageOverlay = null;
        }
    }

    private void CreateCells()
    {
        var shapeData = itemData.CurrentShapeData;
        var itemDataSO = itemData.ItemData;

        foreach (var cellPos in shapeData.cells)
        {
            GameObject cell = CreateCell();
            cell.name = $"Cell_{cellPos.x}_{cellPos.y}";

            RectTransform cellRect = cell.GetComponent<RectTransform>();
            cellRect.sizeDelta = new Vector2(gridVisual.CellSize, gridVisual.CellSize);
            cellRect.anchorMin = new Vector2(0, 1);
            cellRect.anchorMax = new Vector2(0, 1);
            cellRect.pivot = new Vector2(0, 1);

            Image cellImage = cell.GetComponent<Image>();
            if (cellImage != null && itemDataSO != null)
            {
                cellImage.color = itemDataSO.CellColor;
            }

            Vector2 position = new Vector2(
                cellPos.x * (gridVisual.CellSize + gridVisual.CellSpacing),
                -cellPos.y * (gridVisual.CellSize + gridVisual.CellSpacing)
            );
            cellRect.anchoredPosition = position;

            cellObjects.Add(cell);
        }
    }

    private GameObject CreateCell()
    {
        GameObject cell = new GameObject("Cell");
        cell.transform.SetParent(transform, false);

        RectTransform cellRect = cell.AddComponent<RectTransform>();
        Image cellImage = cell.AddComponent<Image>();
        cellImage.color = Color.white;

        if (borderWidth > 0)
        {
            Outline outline = cell.AddComponent<Outline>();
            outline.effectColor = borderColor;
            outline.effectDistance = new Vector2(borderWidth, borderWidth);
        }

        return cell;
    }

    private void UpdateLayout()
    {
        if (itemData == null) return;

        var shapeData = itemData.CurrentShapeData;
        if (shapeData.cells.Length == 0) return;

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        foreach (var cellPos in shapeData.cells)
        {
            float cellX = cellPos.x * (gridVisual.CellSize + gridVisual.CellSpacing);
            float cellY = -cellPos.y * (gridVisual.CellSize + gridVisual.CellSpacing);

            minX = Mathf.Min(minX, cellX);
            maxX = Mathf.Max(maxX, cellX + gridVisual.CellSize);
            minY = Mathf.Min(minY, cellY);
            maxY = Mathf.Max(maxY, cellY + gridVisual.CellSize);
        }

        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

        Vector2 totalSize = new Vector2(maxX - minX, maxY - minY);
        rectTransform.sizeDelta = totalSize;
        rectTransform.pivot = new Vector2(0, 1);
    }

    private void CreateImageOverlay()
    {
        var itemDataSO = itemData?.ItemData;
        if (itemDataSO?.itemSprite == null) return;

        imageOverlay = new GameObject("ImageOverlay");
        imageOverlay.transform.SetParent(transform, false);

        RectTransform imageRect = imageOverlay.AddComponent<RectTransform>();
        imageRect.anchorMin = new Vector2(0, 1);
        imageRect.anchorMax = new Vector2(0, 1);
        imageRect.pivot = new Vector2(0.5f, 0.5f);

        Image overlayImage = imageOverlay.AddComponent<Image>();
        overlayImage.sprite = itemDataSO.itemSprite;
        overlayImage.raycastTarget = false;

        CalculateImageSizeAndPosition(imageRect, overlayImage);
        ApplyImageRotation(imageRect);
        imageOverlay.transform.SetAsLastSibling();
    }

    public void RefreshHotkeyIndicatorVisuals()
    {
        if (hotkeyIndicator != null)
            hotkeyIndicator.RefreshDisplay();
        // else
        // {
        //     Debug.Log("InventoryItemVisualRenderer.RefreshHotkeyIndicatorVisuals: No hotkey indicator found");
        // }
    }

    private void RefreshImageAndHotkeyIndicatorVisuals()
    {
        CreateImageOverlay();
        RefreshHotkeyIndicatorVisuals();
    }


    private void CalculateImageSizeAndPosition(RectTransform imageRect, Image overlayImage)
    {
        var itemDataSO = itemData?.ItemData;
        if (itemDataSO?.itemSprite == null) return;

        var shapeData = itemData.CurrentShapeData;
        if (shapeData.cells.Length == 0) return;

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        foreach (var cellPos in shapeData.cells)
        {
            float cellX = cellPos.x * (gridVisual.CellSize + gridVisual.CellSpacing);
            float cellY = -cellPos.y * (gridVisual.CellSize + gridVisual.CellSpacing);

            minX = Mathf.Min(minX, cellX);
            maxX = Mathf.Max(maxX, cellX + gridVisual.CellSize);
            minY = Mathf.Min(minY, cellY);
            maxY = Mathf.Max(maxY, cellY + gridVisual.CellSize);
        }

        float availableWidth = (maxX - minX) - (imagePadding * 2);
        float availableHeight = (maxY - minY) - (imagePadding * 2);

        Sprite sprite = itemDataSO.itemSprite;
        float spriteAspect = sprite.rect.width / sprite.rect.height;

        float imageWidth, imageHeight;

        if (availableWidth / availableHeight > spriteAspect)
        {
            imageHeight = availableHeight;
            imageWidth = imageHeight * spriteAspect;
        }
        else
        {
            imageWidth = availableWidth;
            imageHeight = imageWidth / spriteAspect;
        }

        imageWidth *= itemDataSO.spriteScaleX;
        imageHeight *= itemDataSO.spriteScaleY;

        imageRect.sizeDelta = new Vector2(imageWidth, imageHeight);

        float centerX = (minX + maxX) / 2f;
        float centerY = (minY + maxY) / 2f;

        Vector2 offset = itemDataSO.spritePositionOffset;
        offset.x *= gridVisual.CellSize;
        offset.y *= gridVisual.CellSize;

        imageRect.anchoredPosition = new Vector2(centerX + offset.x, centerY + offset.y);
    }

    private void ApplyImageRotation(RectTransform imageRect)
    {
        if (itemData == null) return;

        float rotationAngle = itemData.currentRotation * -90f;
        imageRect.localRotation = Quaternion.Euler(0, 0, rotationAngle);
    }

    public void SetAlpha(float alpha)
    {
        foreach (var cell in cellObjects)
        {
            if (cell != null)
            {
                Image cellImage = cell.GetComponent<Image>();
                if (cellImage != null)
                {
                    Color color = cellImage.color;
                    color.a = alpha;
                    cellImage.color = color;
                }
            }
        }

        if (imageOverlay != null)
        {
            Image overlayImage = imageOverlay.GetComponent<Image>();
            if (overlayImage != null)
            {
                Color color = overlayImage.color;
                color.a = alpha;
                overlayImage.color = color;
            }
        }
    }
}