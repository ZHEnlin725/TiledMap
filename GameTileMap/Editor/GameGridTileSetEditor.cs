using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;


[CustomEditor(typeof(GameGridTileSet),true)]
public sealed class GameGridTileSetEditor : Editor
{
    private const float SlotSize = 52f;
    private const float SlotGap = 4f;
    private const int Columns = 4;
    private const int BlobColumns = 8;

    private SerializedProperty displayNameProperty;
    private SerializedProperty tilesProperty;
    private SerializedProperty blobTilesProperty;
    private SerializedProperty tintProperty;
    private SerializedProperty materialProperty;
    private SerializedProperty sourceTilesetProperty;
    private SerializedProperty maskVariantsProperty;
    private SerializedProperty customRulesProperty;
    private ReorderableList customRulesList;

    private static readonly int[] SliceToTileSlot =
    {
        1, 10, 7, 3,
        6, 11, 15, 13,
        8, 12, 14, 5,
        0, 2, 9, 4
    };

    private static readonly int[] Blob47SliceToTileSlot =
    {
        10, 31, 26, 5, 7, 28, 23, 18, 20, 40,
        12, 46, 42, 6, 9, 44, 41, 37, 39, 30,
        4, 36, 34, 1, 11, 45, 33, 27, 32, 24,
        29, 2, 15, 13, 0, 3, 35, 17, 14, 16,
        22, 38, 8, 43, 25, 19, 21
    };

    private void OnEnable()
    {
        displayNameProperty = serializedObject.FindProperty("displayName");
        tilesProperty = serializedObject.FindProperty("tiles");
        blobTilesProperty = serializedObject.FindProperty("blobTiles");
        tintProperty = serializedObject.FindProperty("tint");
        materialProperty = serializedObject.FindProperty("materialTemplate");
        sourceTilesetProperty = serializedObject.FindProperty("sourceTileset");
        maskVariantsProperty = serializedObject.FindProperty("maskVariants");
        customRulesProperty = serializedObject.FindProperty("customRules");
        ((GameGridTileSet)target).EnsureSlots();

        if (customRulesProperty == null) return;

        customRulesList = new ReorderableList(
            serializedObject,
            customRulesProperty,
            true,
            true,
            true,
            true);
        customRulesList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Custom Mask Rules");
        customRulesList.drawElementCallback = (rect, index, isActive, isFocused) =>
            DrawCustomRuleElement(rect, index);
        customRulesList.elementHeight = 64f;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var tileSet = (GameGridTileSet)target;
        var isBlob47 = tileSet is Blob47TileSet;
        var isCustomMaskRules = tileSet is CustomMaskRulesTileSet;

        EditorGUILayout.LabelField("Terrain Layer", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "Tile Layout",
            ObjectNames.NicifyVariableName(tileSet.Mode.ToString()));
        EditorGUILayout.PropertyField(displayNameProperty, new GUIContent("Display Name"));
        EditorGUILayout.PropertyField(tintProperty, new GUIContent("Tint"));
        EditorGUILayout.PropertyField(materialProperty, new GUIContent("Material"));

        if (!isCustomMaskRules)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Tile Sheet", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(sourceTilesetProperty, new GUIContent("Source Tileset"));
                using (new EditorGUI.DisabledScope(sourceTilesetProperty.objectReferenceValue == null))
                {
                    if (GUILayout.Button("Auto Assign", GUILayout.Width(90f)))
                        AutoAssignTiles(sourceTilesetProperty.objectReferenceValue as Texture2D, isBlob47);
                }
            }
        }

        var spriteCount = isBlob47 ? Blob47TileSet.TileCount : DualGrid16TileSet.TileCount;
        var columns = isBlob47 ? BlobColumns : Columns;
        var spriteProperty = isBlob47 ? blobTilesProperty : tilesProperty;

        EditorGUILayout.Space(6f);
        if (isCustomMaskRules)
        {
            EditorGUILayout.LabelField("Custom Mask Rules", EditorStyles.boldLabel);
            customRulesList.DoLayoutList();
        }
        else
        {
            EditorGUILayout.LabelField(
                isBlob47
                    ? "Blob 47 (N, NE, E, SE, S, SW, W, NW)"
                    : "Tiles 0-15 (bit 0 BL, 1 BR, 2 TL, 3 TR)",
                EditorStyles.miniLabel);

            if (spriteProperty == null || spriteProperty.arraySize < spriteCount)
            {
                EditorGUILayout.HelpBox($"Tile slots need {spriteCount} entries.", MessageType.Warning);
            }
            else
            {
                var rows = Mathf.CeilToInt(spriteCount / (float)columns);
                var gridRect = GUILayoutUtility.GetRect(
                    1f,
                    rows * SlotSize + (rows - 1) * SlotGap,
                    GUILayout.ExpandWidth(true));
                var slotWidth = Mathf.Min(
                    SlotSize,
                    (gridRect.width - (columns - 1) * SlotGap) / columns);
                for (var spriteIndex = 0; spriteIndex < spriteCount; spriteIndex++)
                {
                    var column = spriteIndex % columns;
                    var row = spriteIndex / columns;
                    var slot = new Rect(
                        gridRect.x + column * (slotWidth + SlotGap),
                        gridRect.y + row * (SlotSize + SlotGap),
                        slotWidth,
                        SlotSize);
                    var mask = isBlob47
                        ? Blob47TileSet.GetBlob47Mask(spriteIndex)
                        : spriteIndex;
                    DrawTileSlot(
                        slot,
                        spriteProperty.GetArrayElementAtIndex(spriteIndex),
                        mask.ToString(),
                        mask);
                }
            }
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Mask Variants", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Optional. Add one row per mask that needs multiple visual variants. " +
            "Each row uses its own random seed when selecting a tile.",
            MessageType.Info);
        EditorGUILayout.PropertyField(maskVariantsProperty, new GUIContent("Mask Tile Variants"), true);

        if (GUI.changed)
        {
            serializedObject.ApplyModifiedProperties();
            ((GameGridTileSet)target).EnsureSlots();
            EditorUtility.SetDirty(target);
            serializedObject.Update();
        }
    }

    private void AutoAssignTiles(Texture2D sourceTileset, bool isBlob47)
    {
        if (sourceTileset == null) return;

        var spriteCount = isBlob47 ? Blob47TileSet.TileCount : DualGrid16TileSet.TileCount;
        var sprites = LoadSpritesFromTexture(sourceTileset);
        if (sprites.Length != spriteCount)
        {
            Debug.LogWarning(
                $"Grid TileSet: expected {spriteCount} sprites on '{sourceTileset.name}', but found {sprites.Length}.",
                sourceTileset);
            return;
        }

        Undo.RecordObject(target, "Auto Assign Grid Tiles");
        var targetProperty = isBlob47 ? blobTilesProperty : tilesProperty;
        targetProperty.arraySize = spriteCount;
        for (var i = 0; i < spriteCount; i++)
            targetProperty.GetArrayElementAtIndex(i).objectReferenceValue = null;

        var sliceMap = isBlob47 ? Blob47SliceToTileSlot : SliceToTileSlot;
        for (var sliceIndex = 0; sliceIndex < spriteCount; sliceIndex++)
        {
            var targetSlot = sliceMap[sliceIndex];
            targetProperty.GetArrayElementAtIndex(targetSlot).objectReferenceValue = sprites[sliceIndex];
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
        GUI.changed = true;
    }

    private static Sprite[] LoadSpritesFromTexture(Texture2D texture)
    {
        var path = AssetDatabase.GetAssetPath(texture);
        var assets = AssetDatabase.LoadAllAssetsAtPath(path);
        var sprites = new List<Sprite>();
        foreach (var asset in assets)
            if (asset is Sprite sprite)
                sprites.Add(sprite);

        sprites.Sort(CompareSprites);
        return sprites.ToArray();
    }

    private static int CompareSprites(Sprite a, Sprite b)
    {
        var yComparison = b.rect.y.CompareTo(a.rect.y);
        return yComparison != 0 ? yComparison : a.rect.x.CompareTo(b.rect.x);
    }

    private void DrawCustomRuleElement(Rect position, int index)
    {
        var element = customRulesProperty.GetArrayElementAtIndex(index);
        var maskProperty = element.FindPropertyRelative("mask");
        var spriteProperty = element.FindPropertyRelative("sprite");

        var matrixSize = 46f;
        var matrixRect = new Rect(position.x + 6f, position.y + 6f, matrixSize, matrixSize);
        DrawCustomMaskMatrix(matrixRect, maskProperty);

        var line = EditorGUIUtility.singleLineHeight;
        var spacing = EditorGUIUtility.standardVerticalSpacing;
        var previewSize = 32f;
        var inspectorRect = new Rect(
            matrixRect.xMax + 8f,
            position.y + 6f,
            position.width - matrixSize - 20f,
            previewSize);
        var maskRect = new Rect(
            inspectorRect.x,
            inspectorRect.y,
            inspectorRect.width - previewSize - 6f,
            line);
        var spriteRect = new Rect(
            inspectorRect.x,
            maskRect.yMax + spacing,
            inspectorRect.width - previewSize - 6f,
            line);
        var previewRect = new Rect(
            inspectorRect.xMax - previewSize,
            inspectorRect.y,
            previewSize,
            previewSize);

        EditorGUI.LabelField(maskRect, "Mask", maskProperty.intValue.ToString());
        GUI.Label(new Rect(spriteRect.x, spriteRect.y, 42f, line), "Sprite");
        EditorGUI.PropertyField(
            new Rect(spriteRect.x + 42f, spriteRect.y, spriteRect.width - 42f, line),
            spriteProperty,
            GUIContent.none);
        DrawSpritePreview(previewRect, spriteProperty.objectReferenceValue as Sprite);
    }

    private void DrawCustomMaskMatrix(Rect rect, SerializedProperty maskProperty)
    {
        var e = Event.current;
        var cellSize = Mathf.Min(rect.width, rect.height) / 3f;
        var oldColor = GUI.color;

        for (var row = 0; row < 3; row++)
        {
            for (var column = 0; column < 3; column++)
            {
                var bit = GetCustomMaskBit(row, column);
                if (bit == 0)
                {
                    DrawCustomMatrixCell(new Rect(
                        rect.x + column * cellSize,
                        rect.y + row * cellSize,
                        cellSize,
                        cellSize), "C", Color.gray, false);
                    continue;
                }

                var position = new Vector2Int(column - 1, 1 - row);
                var isSet = (maskProperty.intValue & bit) != 0;
                var cellRect = new Rect(
                    rect.x + column * cellSize,
                    rect.y + row * cellSize,
                    cellSize,
                    cellSize);

                DrawCustomMatrixCell(
                    cellRect,
                    isSet ? GetCustomMaskArrowText(position) : string.Empty,
                    isSet ? Color.green : Color.white,
                    true);

                if (e.type == EventType.MouseDown && cellRect.Contains(e.mousePosition))
                {
                    if (e.button == 0)
                        maskProperty.intValue ^= bit;
                    else if (e.button == 1)
                        maskProperty.intValue &= ~bit;

                    GUI.changed = true;
                    e.Use();
                }
            }
        }

        GUI.color = oldColor;
    }

    private static void DrawCustomMatrixCell(Rect rect, string text, Color color, bool interactive)
    {
        var oldColor = GUI.color;
        GUI.color = interactive
            ? EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.06f)
                : new Color(0f, 0f, 0f, 0.06f)
            : Color.gray;
        EditorGUI.DrawRect(rect, GUI.color);
        GUI.color = color;

        var style = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 9
        };
        GUI.Label(rect, text, style);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), Color.black);
        EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), Color.black);
        GUI.color = oldColor;
    }

    private static void DrawSpritePreview(Rect rect, Sprite sprite)
    {
        var border = EditorGUIUtility.isProSkin
            ? new Color(0.4f, 0.4f, 0.4f, 1f)
            : new Color(0.55f, 0.55f, 0.55f, 1f);
        var background = EditorGUIUtility.isProSkin
            ? new Color(0.12f, 0.12f, 0.12f, 1f)
            : new Color(0.9f, 0.9f, 0.9f, 1f);

        EditorGUI.DrawRect(rect, border);
        var inner = new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f);
        EditorGUI.DrawRect(inner, background);

        if (sprite == null || sprite.texture == null)
        {
            GUI.Label(inner, "None", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        var sourceSize = sprite.rect.size;
        var scale = Mathf.Min(
            (inner.width - 4f) / Mathf.Max(1f, sourceSize.x),
            (inner.height - 4f) / Mathf.Max(1f, sourceSize.y));
        var previewSize = sourceSize * scale;
        var previewRect = new Rect(
            inner.center.x - previewSize.x * 0.5f,
            inner.center.y - previewSize.y * 0.5f,
            previewSize.x,
            previewSize.y);
        var texture = sprite.texture;
        var spriteRect = sprite.textureRect;
        var uvRect = new Rect(
            spriteRect.x / texture.width,
            spriteRect.yMin / texture.height,
            spriteRect.width / texture.width,
            spriteRect.height / texture.height);
        GUI.DrawTextureWithTexCoords(previewRect, texture, uvRect, true);
    }

    private static int GetCustomMaskBit(int row, int column)
    {
        var x = column - 1;
        var y = 1 - row;

        if (x == 0 && y == 0) return 0;
        if (x == 0) return y > 0 ? 1 : 16;
        if (y == 0) return x > 0 ? 4 : 64;
        if (x > 0 && y > 0) return 2;
        if (x > 0 && y < 0) return 8;
        if (x < 0 && y < 0) return 32;
        return 128;
    }

    private static string GetCustomMaskArrowText(Vector2Int position)
    {
        if (position.x == 1 && position.y == 0) return "→";
        if (position.x == 1 && position.y == 1) return "↗";
        if (position.x == 0 && position.y == 1) return "↑";
        if (position.x == -1 && position.y == 1) return "↖";
        if (position.x == -1 && position.y == 0) return "←";
        if (position.x == -1 && position.y == -1) return "↙";
        if (position.x == 0 && position.y == -1) return "↓";
        return "↘";
    }

    private void DrawTileSlot(Rect slot, SerializedProperty tileProperty, string label, int mask)
    {
        var current = tileProperty.objectReferenceValue as Sprite;
        HandleTileSlotDrag(slot, tileProperty);

        var controlId = EditorGUIUtility.GetControlID(FocusType.Passive);
        HandleObjectPickerCommand(controlId, tileProperty);

        var e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0 && slot.Contains(e.mousePosition))
        {
            EditorGUIUtility.ShowObjectPicker<Sprite>(current, false, string.Empty, controlId);
            e.Use();
            return;
        }

        if (e.type == EventType.ContextClick && slot.Contains(e.mousePosition))
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("添加MaskTileVariant"), false, () => AddMaskVariant(mask));
            menu.ShowAsContext();
            e.Use();
            return;
        }

        DrawTileSlotPreview(slot, current, label);
    }

    private void AddMaskVariant(int mask)
    {
        if (!((GameGridTileSet)target).AddMaskVariant(mask)) return;
        serializedObject.Update();
        EditorUtility.SetDirty(target);
        GUI.changed = true;
    }

    private static void HandleObjectPickerCommand(int controlId, SerializedProperty tileProperty)
    {
        var e = Event.current;
        if (e.type != EventType.ExecuteCommand || e.commandName != "ObjectSelectorUpdated") return;
        if (EditorGUIUtility.GetObjectPickerControlID() != controlId) return;

        tileProperty.objectReferenceValue = EditorGUIUtility.GetObjectPickerObject() as Sprite;
        GUI.changed = true;
        e.Use();
    }

    private static void DrawTileSlotPreview(Rect slot, Sprite sprite, string label)
    {
        var background = EditorGUIUtility.isProSkin
            ? new Color(0.13f, 0.13f, 0.13f, 1f)
            : new Color(0.88f, 0.88f, 0.88f, 1f);
        var border = EditorGUIUtility.isProSkin
            ? new Color(0.42f, 0.42f, 0.42f, 1f)
            : new Color(0.55f, 0.55f, 0.55f, 1f);

        EditorGUI.DrawRect(slot, border);
        EditorGUI.DrawRect(new Rect(slot.x + 1f, slot.y + 1f, slot.width - 2f, slot.height - 2f), background);

        if (sprite != null && sprite.texture != null)
        {
            var previewArea = new Rect(
                slot.x + 3f,
                slot.y + 3f,
                slot.width - 6f,
                slot.height - 18f);
            var sourceSize = sprite.rect.size;
            var scale = Mathf.Min(
                previewArea.width / Mathf.Max(1f, sourceSize.x),
                previewArea.height / Mathf.Max(1f, sourceSize.y));
            var previewSize = sourceSize * scale;
            var previewRect = new Rect(
                previewArea.center.x - previewSize.x * 0.5f,
                previewArea.center.y - previewSize.y * 0.5f,
                previewSize.x,
                previewSize.y);

            var texture = sprite.texture;
            var spriteRect = sprite.textureRect;
            var uvRect = new Rect(
                spriteRect.x / texture.width,
                spriteRect.yMin / texture.height,
                spriteRect.width / texture.width,
                spriteRect.height / texture.height);
            GUI.DrawTextureWithTexCoords(previewRect, texture, uvRect, true);
        }
        else
        {
            var style = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                alignment = TextAnchor.MiddleCenter
            };
            GUI.Label(new Rect(slot.x, slot.y + 5f, slot.width, slot.height - 25f), "None", style);
        }

        var labelStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };
        GUI.Label(new Rect(slot.x + 2f, slot.yMax - 15f, slot.width - 4f, 13f), label, labelStyle);
    }

    private static void HandleTileSlotDrag(Rect slot, SerializedProperty tileProperty)
    {
        var e = Event.current;
        if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform) return;
        if (!slot.Contains(e.mousePosition)) return;

        var sprite = GetDraggedSprite();
        if (sprite == null) return;

        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        if (e.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            tileProperty.objectReferenceValue = sprite;
            GUI.changed = true;
        }

        e.Use();
    }

    private static Sprite GetDraggedSprite()
    {
        foreach (var reference in DragAndDrop.objectReferences)
        {
            if (reference is Sprite sprite) return sprite;

            if (reference is Texture2D texture)
            {
                var path = AssetDatabase.GetAssetPath(texture);
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (asset is Sprite candidate) return candidate;
            }
        }

        return null;
    }

}
