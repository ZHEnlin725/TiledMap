#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEditorInternal;

[CustomEditor(typeof(GameGridRenderer))]
public sealed class GameGridRendererEditor : Editor
{
    private SerializedProperty widthProperty;
    private SerializedProperty heightProperty;
    private SerializedProperty cellSizeProperty;
    private SerializedProperty terrainLayersProperty;
    private ReorderableList terrainLayersList;

    private readonly List<Vector2Int> brushCells = new List<Vector2Int>(64);
    private Vector2Int lastPaintCell;
    private bool painting;
    private bool eraseStroke;
    private const int TileSetPickerControlId = 710210;

    private void OnEnable()
    {
        widthProperty = serializedObject.FindProperty("width");
        heightProperty = serializedObject.FindProperty("height");
        cellSizeProperty = serializedObject.FindProperty("cellSize");
        terrainLayersProperty = serializedObject.FindProperty("terrainLayers");
        terrainLayersList = new ReorderableList(
            serializedObject,
            terrainLayersProperty,
            true,
            true,
            true,
            true);
        terrainLayersList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Terrain Layers");
        terrainLayersList.drawElementCallback = (rect, index, isActive, isFocused) =>
            DrawTerrainLayerElement(rect, index);
        terrainLayersList.elementHeight = EditorGUIUtility.singleLineHeight * 4f +
            EditorGUIUtility.standardVerticalSpacing * 5f + 8f;
        terrainLayersList.onAddCallback = list => AddTerrainLayerElement();
        terrainLayersList.onRemoveCallback = list =>
        {
            ReorderableList.defaultBehaviours.DoRemoveButton(list);
            GUI.changed = true;
        };
    }

    public override void OnInspectorGUI()
    {
        var renderer = (GameGridRenderer)target;
        serializedObject.Update();
        if (Event.current.type == EventType.ExecuteCommand &&
            Event.current.commandName == "ObjectSelectorUpdated" &&
            EditorGUIUtility.GetObjectPickerControlID() == TileSetPickerControlId)
        {
            var pickedTileSet = EditorGUIUtility.GetObjectPickerObject() as GameGridTileSet;
            if (pickedTileSet != null) AddTerrainLayer(renderer, pickedTileSet);
            Event.current.Use();
        }

        EditorGUILayout.LabelField("Grid", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(widthProperty);
        EditorGUILayout.PropertyField(heightProperty);
        EditorGUILayout.PropertyField(cellSizeProperty);

        EditorGUILayout.Space(6f);
        terrainLayersList.DoLayoutList();
        if (GUILayout.Button("Add Terrain Layer"))
            EditorGUIUtility.ShowObjectPicker<GameGridTileSet>(
                null, false, string.Empty, TileSetPickerControlId);

        var propertiesChanged = GUI.changed;
        serializedObject.ApplyModifiedProperties();
        if (propertiesChanged)
        {
            renderer.EnsureLayerData(false);
            renderer.Rebuild();
        }

        DrawStatistics(renderer);
        DrawTools(renderer);
    }

    private void OnSceneGUI()
    {
        var renderer = (GameGridRenderer)target;
        if (renderer == null || !renderer.isActiveAndEnabled) return;
        if (!renderer.EditorPaintMode)
        {
            painting = false;
            return;
        }

        DrawSceneGrid(renderer);
        var controlId = GUIUtility.GetControlID(FocusType.Passive);
        HandleUtility.AddDefaultControl(controlId);
        var e = Event.current;
        if (e.type == EventType.KeyDown && !e.control && !e.alt) HandleHotkeys(renderer, e);
        if (!TryGetHoverCell(renderer, e.mousePosition, out var cell))
        {
            return;
        }

        DrawBrushPreview(renderer, cell, e.shift);
        switch (e.type)
        {
            case EventType.MouseDown when e.button == 0:
                painting = true;
                eraseStroke = e.shift;
                Undo.RecordObject(renderer, "Paint Grid");
                lastPaintCell = cell;
                PaintAt(renderer, cell, eraseStroke);
                e.Use();
                break;
            case EventType.MouseDrag when e.button == 0 && painting:
                PaintLine(renderer, lastPaintCell, cell, eraseStroke);
                lastPaintCell = cell;
                e.Use();
                break;
            case EventType.MouseUp when e.button == 0 && painting:
                painting = false;
                e.Use();
                break;
            case EventType.MouseLeaveWindow:
                painting = false;
                break;
        }
    }

    [MenuItem("GameObject/2D Object/Game Grid Renderer", false, 30)]
    private static void CreateDualGridRenderer(MenuCommand command)
    {
        var gameObject = new GameObject("Game Grid Renderer");
        GameObjectUtility.SetParentAndAlign(gameObject, command.context as GameObject);
        Undo.RegisterCreatedObjectUndo(gameObject, "Create " + gameObject.name);
        gameObject.AddComponent<GameGridRenderer>();
        Selection.activeObject = gameObject;
    }

    private void AddTerrainLayer(GameGridRenderer renderer, GameGridTileSet tileSet)
    {
        if (tileSet == null) return;

        Undo.RecordObject(renderer, "Add Grid Terrain");
        var index = terrainLayersProperty.arraySize;
        terrainLayersProperty.arraySize++;
        var element = terrainLayersProperty.GetArrayElementAtIndex(index);
        element.FindPropertyRelative("tileSet").objectReferenceValue = tileSet;
        element.FindPropertyRelative("visible").boolValue = true;
        element.FindPropertyRelative("sortingLayerName").stringValue = "Default";
        element.FindPropertyRelative("sortingOrder").intValue = 0;
        serializedObject.ApplyModifiedProperties();
        renderer.EditorBrushLayerIndex = index;
        EditorUtility.SetDirty(renderer);
        renderer.EnsureLayerData(false);
        renderer.Rebuild();
        GUI.changed = true;
    }

    private void AddTerrainLayerElement()
    {
        var renderer = (GameGridRenderer)target;
        Undo.RecordObject(renderer, "Add Grid Terrain");
        var index = terrainLayersProperty.arraySize;
        terrainLayersProperty.arraySize++;
        var element = terrainLayersProperty.GetArrayElementAtIndex(index);
        element.FindPropertyRelative("tileSet").objectReferenceValue = null;
        element.FindPropertyRelative("visible").boolValue = true;
        element.FindPropertyRelative("sortingLayerName").stringValue = "Default";
        element.FindPropertyRelative("sortingOrder").intValue = 0;
        serializedObject.ApplyModifiedProperties();
        renderer.EditorBrushLayerIndex = index;
        renderer.EnsureLayerData(false);
        EditorUtility.SetDirty(renderer);
        GUI.changed = true;
    }

    private void DrawTerrainLayerElement(Rect position, int index)
    {
        var element = terrainLayersProperty.GetArrayElementAtIndex(index);
        var tileSetProperty = element.FindPropertyRelative("tileSet");
        var visibleProperty = element.FindPropertyRelative("visible");
        var sortingLayerNameProperty = element.FindPropertyRelative("sortingLayerName");
        var sortingOrderProperty = element.FindPropertyRelative("sortingOrder");

        var line = EditorGUIUtility.singleLineHeight;
        var spacing = EditorGUIUtility.standardVerticalSpacing;
        var tileSetRect = new Rect(
            position.x + 4f,
            position.y + 4f,
            position.width - 8f,
            line);
        EditorGUI.PropertyField(tileSetRect, tileSetProperty, new GUIContent("Tile Set"));

        var visibleRect = new Rect(
            tileSetRect.x,
            tileSetRect.yMax + spacing,
            position.width - 8f,
            line);
        EditorGUI.PropertyField(visibleRect, visibleProperty, new GUIContent("Visible"));

        var sortingRect = new Rect(
            tileSetRect.x,
            visibleRect.yMax + spacing,
            position.width - 8f,
            line);
        DrawSortingLayerPopup(sortingRect, sortingLayerNameProperty);

        var orderRect = new Rect(
            tileSetRect.x,
            sortingRect.yMax + spacing,
            position.width - 8f,
            line);
        sortingOrderProperty.intValue = EditorGUI.IntField(orderRect, "SortingOrder", sortingOrderProperty.intValue);
    }

    private static void DrawSortingLayerPopup(Rect rect, SerializedProperty property)
    {
        var current = property.stringValue;
        if (string.IsNullOrEmpty(current)) current = "Default";

        var layers = SortingLayer.layers;
        var names = new string[layers.Length + 1];
        var currentIndex = -1;
        for (var i = 0; i < layers.Length; i++)
        {
            names[i] = layers[i].name;
            if (layers[i].name == current) currentIndex = i;
        }

        if (currentIndex < 0)
        {
            names[layers.Length] = current + " (Missing)";
            currentIndex = layers.Length;
        }

        const float labelWidth = 88f;
        var labelRect = new Rect(rect.x, rect.y, labelWidth, rect.height);
        var popupRect = new Rect(
            labelRect.xMax + 4f,
            rect.y,
            Mathf.Max(0f, rect.width - labelWidth - 4f),
            rect.height);
        EditorGUI.LabelField(labelRect, "Sorting Layer");
        var newIndex = EditorGUI.Popup(popupRect, currentIndex, names);
        if (newIndex != currentIndex) property.stringValue = names[newIndex];
    }

    private static void DrawStatistics(GameGridRenderer renderer)
    {
        var total = renderer.Width * renderer.Height;
        var layers = renderer.TerrainLayers;
        if (layers == null || layers.Count == 0)
        {
            EditorGUILayout.HelpBox("No terrain layers are configured yet.", MessageType.Info);
            return;
        }

        for (var i = 0; i < layers.Count; i++)
        {
            var layer = layers[i];
            if (layer == null)
            {
                EditorGUILayout.HelpBox($"Terrain layer {i} is null.", MessageType.Warning);
                continue;
            }

            var filled = renderer.CountCells(layer);

            var set = layer.TileSet;
            var layerName = set != null ? set.SafeName : $"Layer {i}";
            EditorGUILayout.HelpBox($"{layerName}: {filled}/{total} cells.", MessageType.Info);

            if (set == null)
            {
                EditorGUILayout.HelpBox($"Terrain layer {i} has no Tile Set.", MessageType.Warning);
                continue;
            }

            var spriteCount = set.SpriteCount;
            var missing = 0;
            for (var tileIndex = 0; tileIndex < spriteCount; tileIndex++)
                if (set.GetSpriteAt(tileIndex) == null) missing++;

            if (missing > 0)
            {
                EditorGUILayout.HelpBox(
                    $"{set.SafeName}: {missing}/{spriteCount} tile slots are empty.",
                    MessageType.Warning);
            }
        }
    }

    private static void DrawTools(GameGridRenderer renderer)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Scene Tools", EditorStyles.boldLabel);

        var showGrid = EditorGUILayout.ToggleLeft("Show Grid In Scene", renderer.EditorShowGrid);
        if (showGrid != renderer.EditorShowGrid)
        {
            renderer.EditorShowGrid = showGrid;
            EditorUtility.SetDirty(renderer);
            SceneView.RepaintAll();
        }

        var paintMode = EditorGUILayout.ToggleLeft("Paint Mode", renderer.EditorPaintMode);
        if (paintMode != renderer.EditorPaintMode)
        {
            renderer.EditorPaintMode = paintMode;
            EditorUtility.SetDirty(renderer);
            renderer.Rebuild();
            SceneView.RepaintAll();
        }

        var layers = renderer.TerrainLayers;
        if (layers == null || layers.Count == 0)
        {
            EditorGUILayout.HelpBox("Add a terrain layer to paint.", MessageType.Info);
            return;
        }

        var options = new string[layers.Count];
        var currentIndex = Mathf.Clamp(renderer.EditorBrushLayerIndex, 0, layers.Count - 1);
        for (var i = 0; i < layers.Count; i++)
        {
            var set = layers[i]?.TileSet;
            options[i] = set != null ? $"{i}: {set.SafeName}" : $"{i}: Missing";
        }

        var newIndex = EditorGUILayout.Popup("Brush Layer", currentIndex, options);
        if (newIndex != currentIndex)
        {
            renderer.EditorBrushLayerIndex = newIndex;
            EditorUtility.SetDirty(renderer);
            SceneView.RepaintAll();
        }

        var brushSize = EditorGUILayout.IntSlider("Brush Size", renderer.EditorBrushSize, 1, 32);
        if (brushSize != renderer.EditorBrushSize)
        {
            renderer.EditorBrushSize = brushSize;
            EditorUtility.SetDirty(renderer);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Fill Brush Terrain"))
        {
            Undo.RecordObject(renderer, "Fill Grid");
            renderer.Fill(GetBrushLayer(renderer), true);
            EditorUtility.SetDirty(renderer);
        }
        if (GUILayout.Button("Clear All Cells"))
        {
            Undo.RecordObject(renderer, "Clear Grid");
            renderer.ClearAll();
            EditorUtility.SetDirty(renderer);
        }
        EditorGUILayout.EndHorizontal();
        if (GUILayout.Button("Rebuild Preview")) renderer.Rebuild();

        EditorGUILayout.HelpBox(
            "Scene: left-drag paints, Shift+left-drag erases, [ and ] switch layer.",
            MessageType.None);
    }

    private static GameGridTerrainLayer GetBrushLayer(GameGridRenderer renderer)
    {
        var layers = renderer.TerrainLayers;
        var index = renderer.EditorBrushLayerIndex;
        if (layers == null || index < 0 || index >= layers.Count) return null;
        return layers[index];
    }

    private static void DrawSceneGrid(GameGridRenderer renderer)
    {
        if (!renderer.EditorShowGrid) return;

        var oldMatrix = Handles.matrix;
        Handles.matrix = renderer.transform.localToWorldMatrix;
        var cellSize = renderer.CellSize;
        var width = renderer.Width * cellSize;
        var height = renderer.Height * cellSize;
        var stepX = Mathf.Max(1, Mathf.CeilToInt(renderer.Width / 80f));
        var stepY = Mathf.Max(1, Mathf.CeilToInt(renderer.Height / 80f));

        Handles.color = new Color(1f, 1f, 1f, 0.12f);
        for (var x = 0; x <= renderer.Width; x += stepX)
            Handles.DrawLine(new Vector3(x * cellSize, 0f, 0f), new Vector3(x * cellSize, height, 0f));
        for (var y = 0; y <= renderer.Height; y += stepY)
            Handles.DrawLine(new Vector3(0f, y * cellSize, 0f), new Vector3(width, y * cellSize, 0f));

        Handles.matrix = oldMatrix;
    }

    private static bool TryGetHoverCell(GameGridRenderer renderer, Vector2 mousePosition, out Vector2Int cell)
    {
        var ray = HandleUtility.GUIPointToWorldRay(mousePosition);
        var plane = new Plane(renderer.transform.forward, renderer.transform.position);
        if (!plane.Raycast(ray, out var enter))
        {
            cell = default;
            return false;
        }

        cell = renderer.WorldToCell(ray.GetPoint(enter));
        return cell.x >= 0 && cell.y >= 0 && cell.x < renderer.Width && cell.y < renderer.Height;
    }

    private void DrawBrushPreview(GameGridRenderer renderer, Vector2Int center, bool erase)
    {
        var size = renderer.EditorBrushSize;
        var offset = (size - 1) / 2;
        var minX = center.x - offset;
        var minY = center.y - offset;
        var maxX = minX + size - 1;
        var maxY = minY + size - 1;
        var p0 = renderer.CellCornerToWorld(minX, minY);
        var p1 = renderer.CellCornerToWorld(maxX + 1, minY);
        var p2 = renderer.CellCornerToWorld(maxX + 1, maxY + 1);
        var p3 = renderer.CellCornerToWorld(minX, maxY + 1);

        Color face;
        Color outline;
        if (erase)
        {
            face = new Color(1f, 0.2f, 0.2f, 0.14f);
            outline = new Color(1f, 0.2f, 0.2f, 0.9f);
        }
        else
        {
            var set = GetBrushLayer(renderer)?.TileSet;
            var tint = set != null ? set.tint : new Color(0.3f, 0.7f, 1f);
            face = new Color(tint.r, tint.g, tint.b, 0.16f);
            outline = new Color(tint.r, tint.g, tint.b, 0.9f);
        }

        Handles.DrawSolidRectangleWithOutline(new[] { p0, p1, p2, p3 }, face, outline);
        if (size == 1) Handles.Label(p2 + Vector3.up * 0.2f, center.ToString());
    }

    private void PaintAt(GameGridRenderer renderer, Vector2Int center, bool erase)
    {
        brushCells.Clear();
        AppendBrushCells(renderer, center);
        ApplyBrushCells(renderer, erase);
    }

    private void PaintLine(GameGridRenderer renderer, Vector2Int from, Vector2Int to, bool erase)
    {
        brushCells.Clear();
        var dx = to.x - from.x;
        var dy = to.y - from.y;
        var steps = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
        for (var i = 1; i <= steps; i++)
        {
            var t = i / (float)steps;
            AppendBrushCells(renderer, Vector2Int.RoundToInt(new Vector2(from.x + dx * t, from.y + dy * t)));
        }
        ApplyBrushCells(renderer, erase);
    }

    private void AppendBrushCells(GameGridRenderer renderer, Vector2Int center)
    {
        var size = renderer.EditorBrushSize;
        var offset = (size - 1) / 2;
        var minX = center.x - offset;
        var minY = center.y - offset;
        for (var y = minY; y < minY + size; y++)
            for (var x = minX; x < minX + size; x++)
                brushCells.Add(new Vector2Int(x, y));
    }

    private void ApplyBrushCells(GameGridRenderer renderer, bool erase)
    {
        if (brushCells.Count == 0) return;
        var layer = GetBrushLayer(renderer);
        if (layer == null) return;
        if (renderer.SetCells(layer, brushCells, !erase, false) <= 0) return;

        renderer.Rebuild();
        EditorUtility.SetDirty(renderer);
        SceneView.RepaintAll();
    }

    private static void HandleHotkeys(GameGridRenderer renderer, Event e)
    {
        if (e.keyCode != KeyCode.LeftBracket && e.keyCode != KeyCode.RightBracket) return;
        CycleBrushTerrain(renderer, e.keyCode == KeyCode.LeftBracket ? -1 : 1);
        EditorUtility.SetDirty(renderer);
        e.Use();
        SceneView.RepaintAll();
    }

    private static void CycleBrushTerrain(GameGridRenderer renderer, int direction)
    {
        var layers = renderer.TerrainLayers;
        if (layers == null || layers.Count == 0) return;

        var currentIndex = Mathf.Clamp(renderer.EditorBrushLayerIndex, 0, layers.Count - 1);
        var nextIndex = Mathf.Clamp(currentIndex + direction, 0, layers.Count - 1);
        renderer.EditorBrushLayerIndex = nextIndex;
    }
}
#endif
