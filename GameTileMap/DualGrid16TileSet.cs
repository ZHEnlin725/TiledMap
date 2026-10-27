using System;
using UnityEngine;

/// <summary>
/// Dual-grid layout with 16 sprites mapped to the four data cells around a corner.
/// Bit 0 = bottom-left, bit 1 = bottom-right, bit 2 = top-left, bit 3 = top-right.
/// </summary>
[CreateAssetMenu(
    fileName = "New Dual Grid 16 Tile Set",
    menuName = "Game Grid Tile Set/Dual Grid 16",
    order = 0)]
public sealed class DualGrid16TileSet : GameGridTileSet
{
    public const int TileCount = 16;

    public Sprite[] tiles = new Sprite[TileCount];
    [SerializeField] private Texture2D sourceTileset;

    public Texture2D SourceTileset => sourceTileset;

    public override GameGridTileSetMode Mode => GameGridTileSetMode.DualGrid16;
    public override int SpriteCount => TileCount;
    public override bool UsesCornerLayout => true;

    public Sprite GetTile(int mask)
    {
        if (tiles == null || mask < 0 || mask >= tiles.Length) return null;
        return tiles[mask];
    }

    public override Sprite GetTileForMask(int mask)
    {
        return GetTile(mask);
    }

    public override Sprite GetSpriteAt(int index)
    {
        return GetTile(index);
    }

    public override void EnsureSlots()
    {
        base.EnsureSlots();

        if (tiles == null)
            tiles = new Sprite[TileCount];
        else if (tiles.Length != TileCount)
        {
            var resized = new Sprite[TileCount];
            Array.Copy(tiles, resized, Mathf.Min(tiles.Length, TileCount));
            tiles = resized;
        }
    }
}
