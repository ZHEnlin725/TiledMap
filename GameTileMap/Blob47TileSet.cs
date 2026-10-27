using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Blob layout with 47 canonical sprites for the eight-neighbour masks
/// (N, NE, E, SE, S, SW, W, NW).
/// </summary>
[CreateAssetMenu(
    fileName = "New Blob 47 Tile Set",
    menuName = "Game Grid Tile Set/Blob 47",
    order = 1)]
public sealed class Blob47TileSet : GameGridTileSet
{
    public const int TileCount = 47;

    public Sprite[] blobTiles = new Sprite[TileCount];
    [SerializeField] private Texture2D sourceTileset;

    public Texture2D SourceTileset => sourceTileset;

    public override GameGridTileSetMode Mode => GameGridTileSetMode.Blob47;
    public override int SpriteCount => TileCount;
    public override bool UsesCornerLayout => false;

    public override Sprite GetTileForMask(int mask)
    {
        if (blobTiles == null || mask < 0 || mask >= Blob47IndexForMask.Length) return null;

        var index = Blob47IndexForMask[mask];
        return index >= 0 && index < blobTiles.Length ? blobTiles[index] : null;
    }

    public override Sprite GetSpriteAt(int index)
    {
        if (blobTiles == null || index < 0 || index >= blobTiles.Length) return null;
        return blobTiles[index];
    }

    public override void EnsureSlots()
    {
        base.EnsureSlots();

        if (blobTiles == null)
            blobTiles = new Sprite[TileCount];
        else if (blobTiles.Length != TileCount)
        {
            var resized = new Sprite[TileCount];
            Array.Copy(blobTiles, resized, Mathf.Min(blobTiles.Length, TileCount));
            blobTiles = resized;
        }
    }

    public static int GetBlob47Mask(int index)
    {
        if (Blob47Masks == null || index < 0 || index >= Blob47Masks.Length) return -1;
        return Blob47Masks[index];
    }

    private static readonly int[] Blob47Masks;
    private static readonly int[] Blob47IndexForMask = new int[256];

    static Blob47TileSet()
    {
        var masks = new List<int>(TileCount);
        var seen = new bool[256];
        var indexForMask = new int[256];

        for (var raw = 0; raw < 256; raw++)
        {
            var canonical = CanonicalizeBlobMask(raw);
            if (seen[canonical]) continue;
            seen[canonical] = true;
            indexForMask[canonical] = masks.Count;
            masks.Add(canonical);
        }

        Blob47Masks = masks.ToArray();
        for (var raw = 0; raw < 256; raw++)
            Blob47IndexForMask[raw] = indexForMask[CanonicalizeBlobMask(raw)];
    }

    private static int CanonicalizeBlobMask(int mask)
    {
        const int n = 1;
        const int e = 4;
        const int s = 16;
        const int w = 64;
        const int ne = 2;
        const int se = 8;
        const int sw = 32;
        const int nw = 128;

        var hasN = (mask & n) != 0;
        var hasE = (mask & e) != 0;
        var hasS = (mask & s) != 0;
        var hasW = (mask & w) != 0;

        var canonicalNe = hasN && hasE ? mask & ne : 0;
        var canonicalSe = hasE && hasS ? mask & se : 0;
        var canonicalSw = hasS && hasW ? mask & sw : 0;
        var canonicalNw = hasW && hasN ? mask & nw : 0;

        return (mask & (n | e | s | w)) | canonicalNe | canonicalSe | canonicalSw | canonicalNw;
    }
}
