using System;
using System.Collections.Generic;
using UnityEngine;

public enum GameGridTileSetMode
{
    DualGrid16,
    Blob47,
    CustomMaskRules
}

/// <summary>
/// Base class for one terrain layer rendered by <see cref="GameGridRenderer"/>.
/// Concrete subclasses own the sprite data for their layout, and the mode is
/// derived from the subclass type instead of being stored on the asset.
/// </summary>
public abstract class GameGridTileSet : ScriptableObject
{
    public string displayName = "Terrain";
    public Color tint = Color.white;
    public Material materialTemplate;
    [SerializeField] private List<MaskTileVariant> maskVariants = new List<MaskTileVariant>();

    [Serializable]
    public sealed class MaskTileVariant
    {
        public int mask;
        public int randomSeed;
        public Sprite[] sprites = Array.Empty<Sprite>();

        public Sprite GetSprite(Vector2Int position)
        {
            if (sprites == null || sprites.Length == 0) return null;

            // Keep the editor-configured seed as the root, then salt it by cell
            // position so identical masks still vary across the map.
            var seed = (int)((long)randomSeed ^ ((long)mask * 2654435761L));
            seed ^= position.x * 73856093;
            seed ^= position.y * 19349663;
            var prng = new System.Random(seed);
            return sprites[prng.Next(0, sprites.Length)];
        }
    }

    /// <summary>How this asset maps masks to sprites; defined by the concrete type.</summary>
    public abstract GameGridTileSetMode Mode { get; }
    /// <summary>Number of sprite slots exposed through <see cref="GetSpriteAt"/>.</summary>
    public abstract int SpriteCount { get; }
    /// <summary>Resolve a raw terrain mask to its sprite.</summary>
    public abstract Sprite GetTileForMask(int mask);
    /// <summary>Resolve a slot index; used by the inspector's grid view.</summary>
    public abstract Sprite GetSpriteAt(int index);
    /// <summary>
    /// True when a sprite is placed on every cell corner from the four surround
    /// cells; false when one sprite is placed per filled cell from a cell mask.
    /// </summary>
    public abstract bool UsesCornerLayout { get; }

    public string SafeName
    {
        get
        {
            return string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        }
    }

    public bool IsBlob47 => Mode == GameGridTileSetMode.Blob47;
    public bool IsCustomMaskRules => Mode == GameGridTileSetMode.CustomMaskRules;

    public Sprite GetTileForMask(int mask, Vector2Int position)
    {
        if (maskVariants != null)
        {
            for (var i = 0; i < maskVariants.Count; i++)
            {
                var variant = maskVariants[i];
                if (variant == null || variant.mask != mask) continue;
                var variantSprite = variant.GetSprite(position);
                if (variantSprite != null) return variantSprite;
            }
        }

        return GetTileForMask(mask);
    }

    public bool AddMaskVariant(int mask)
    {
        if (mask < 0) return false;
        if (maskVariants == null) maskVariants = new List<MaskTileVariant>();

        for (var i = 0; i < maskVariants.Count; i++)
        {
            var variant = maskVariants[i];
            if (variant != null && variant.mask == mask) return false;
        }

        maskVariants.Add(new MaskTileVariant
        {
            mask = mask,
            randomSeed = 0,
            sprites = Array.Empty<Sprite>()
        });
        return true;
    }

    public bool HasTile(int mask)
    {
        return GetTileForMask(mask) != null;
    }

    public int CountAssignedTiles()
    {
        var count = 0;
        for (var i = 0; i < SpriteCount; i++)
            if (GetSpriteAt(i) != null) count++;
        return count;
    }

    /// <summary>
    /// Guarantees shared collections exist. Subclasses override to resize their
    /// own sprite arrays and must call the base implementation.
    /// </summary>
    public virtual void EnsureSlots()
    {
        if (maskVariants == null) maskVariants = new List<MaskTileVariant>();
    }

    private void OnEnable()
    {
        EnsureSlots();
    }

    private void OnValidate()
    {
        EnsureSlots();
    }
}
