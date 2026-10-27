using System;
using UnityEngine;

[Serializable]
public sealed class GameGridTerrainLayer
{
    public GameGridTileSet tileSet;
    public bool visible = true;
    public string sortingLayerName = "Default";
    public int sortingOrder;

    [SerializeField] private bool[] cells = Array.Empty<bool>();

    public GameGridTileSet TileSet => tileSet;

    public bool[] Cells => cells;

    public bool GetCell(int index)
    {
        return cells != null && index >= 0 && index < cells.Length && cells[index];
    }

    public void SetCell(int index, bool occupied)
    {
        if (cells == null || index < 0 || index >= cells.Length) return;
        cells[index] = occupied;
    }

    public void ReplaceCells(bool[] value)
    {
        cells = value ?? Array.Empty<bool>();
    }

    public void FillCells(bool occupied)
    {
        if (cells == null) return;
        for (var i = 0; i < cells.Length; i++) cells[i] = occupied;
    }
}
