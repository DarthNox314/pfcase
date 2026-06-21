using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// PixelFlow Level Editor
/// Open via:  PixelFlow > Level Editor
///
/// Left-click        — paint selected colour (or erase in erase mode)
/// Right-click       — erase cell
/// HP Mode           — left-click sets HP on already-painted cells to Paint HP value
/// Key Mode          — left-click stamps a 2×2 Key onto the grid; right-click removes
/// Lock toggle       — each lane entry can be toggled Pig ↔ Lock
/// </summary>
public class LevelEditorWindow : EditorWindow
{
    // ------------------------------------------------------------------ refs
    private GameConfig _config;
    private LevelData  _target;

    // ------------------------------------------------------------------ grid state
    private int    _cols      = 8;
    private int    _rows      = 6;
    private int    _defaultHp = 1;
    private int[,] _colorGrid;   // -1 = empty
    private int[,] _hpGrid;

    // ------------------------------------------------------------------ paint state
    private int  _selectedColor = 0;
    private bool _eraseMode     = false;
    private bool _hpMode        = false;
    private bool _keyMode       = false;
    private int  _paintHp       = 1;

    // ------------------------------------------------------------------ key placement state
    // Each entry is the bottom-left corner of a 2x2 key footprint (col, row)
    private readonly List<Vector2Int>  _keyPlacements = new List<Vector2Int>();
    private readonly HashSet<Vector2Int> _keyCells    = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> _keyOrigins  = new HashSet<Vector2Int>();
    private bool _keyCacheDirty = true;

    // ------------------------------------------------------------------ pig lanes state
    private const int LANE_COUNT = 4;
    private List<LaneEntry>[] _pigLanes;

    private struct LaneEntry
    {
        public int  colorIndex;
        public int  ammo;
        public bool isHidden;
        public bool isLock;   // true = LockController, false = PigController
    }

    // ------------------------------------------------------------------ scroll
    private Vector2 _sidebarScroll;
    private Vector2 _gridScroll;
    private Vector2 _lanesScroll;

    // ------------------------------------------------------------------ layout
    private const float GAP       = 2f;
    private const float MIN_CELL  = 4f;   // smallest a cell will ever be drawn
    private const float SIDEBAR_W = 170f;
    private const float LANES_W   = 220f;

    // ------------------------------------------------------------------ cached textures / styles
    private Texture2D[] _colorTextures;
    private Texture2D   _emptyTex;
    private Texture2D   _keyTex;
    private GUIStyle    _cellLabelStyle;
    private Color[]     _cachedColors;

    // ------------------------------------------------------------------ colors
    private static readonly Color KeyColor      = new Color(1f, 0.85f, 0.1f, 0.85f);
    private static readonly Color KeyBorderColor = new Color(1f, 0.95f, 0.3f);

    private static readonly Color[] FallbackColors =
    {
        new Color(1f,    0.33f, 0.33f),
        new Color(0.33f, 0.78f, 0.33f),
        new Color(0.33f, 0.55f, 1f),
        new Color(1f,    0.85f, 0.2f),
        new Color(0.75f, 0.33f, 1f),
    };

    private Color[] ActiveColors => _config != null && _config.pigColors != null
                                        && _config.pigColors.Length > 0
                                    ? _config.pigColors : FallbackColors;

    // ================================================================== open
    [MenuItem("PixelFlow/Level Editor")]
    public static void Open() => GetWindow<LevelEditorWindow>("PixelFlow Level Editor");

    // ================================================================== lifecycle
    private void OnEnable()
    {
        ResetGrid();
        ResetPigLanes();
        TryAutoFindConfig();
        RebuildTextures();
    }

    private void OnDisable() => DestroyTextures();

    // ================================================================== GUI
    private void OnGUI()
    {
        if (_cachedColors != ActiveColors) RebuildTextures();

        DrawToolbar();

        EditorGUILayout.BeginHorizontal();
        DrawSidebar();
        GUILayout.Space(4);
        DrawGrid();
        GUILayout.Space(4);
        DrawPigLanes();
        EditorGUILayout.EndHorizontal();

        if (Event.current.type == EventType.MouseDrag) Repaint();
    }

    // ------------------------------------------------------------------ toolbar
    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        var newConfig = (GameConfig)EditorGUILayout.ObjectField(
            _config, typeof(GameConfig), false, GUILayout.Width(140));
        if (newConfig != _config) { _config = newConfig; RebuildTextures(); }

        _target = (LevelData)EditorGUILayout.ObjectField(
            _target, typeof(LevelData), false, GUILayout.Width(160));

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("New LevelData", EditorStyles.toolbarButton))
            CreateNewLevelData();

        GUI.enabled = _target != null;
        if (GUILayout.Button("Load", EditorStyles.toolbarButton)) LoadFromTarget();
        if (GUILayout.Button("Save", EditorStyles.toolbarButton)) SaveToTarget();
        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();
    }

    // ------------------------------------------------------------------ sidebar
    private void DrawSidebar()
    {
        _sidebarScroll = EditorGUILayout.BeginScrollView(
            _sidebarScroll, GUILayout.Width(SIDEBAR_W));

        EditorGUILayout.LabelField("Board", EditorStyles.boldLabel);
        int newCols = Mathf.Max(1, EditorGUILayout.IntField("Columns", _cols));
        int newRows = Mathf.Max(1, EditorGUILayout.IntField("Rows",    _rows));
        if (newCols != _cols || newRows != _rows)
        {
            _cols = newCols;
            _rows = newRows;
            ResetGrid();
        }

        EditorGUILayout.Space(6);

        EditorGUILayout.LabelField("Hit Points", EditorStyles.boldLabel);
        _defaultHp = Mathf.Max(1, EditorGUILayout.IntField("Default HP", _defaultHp));
        _hpMode    = EditorGUILayout.ToggleLeft("HP Paint Mode", _hpMode);
        if (_hpMode)
        {
            _paintHp = Mathf.Max(1, EditorGUILayout.IntField("Paint HP", _paintHp));
            EditorGUILayout.HelpBox("Click painted cells to set HP.", MessageType.None);
        }

        EditorGUILayout.Space(6);

        EditorGUILayout.LabelField("Colours", EditorStyles.boldLabel);
        var colors = ActiveColors;
        for (int i = 0; i < colors.Length && i < _colorTextures?.Length; i++)
            DrawPaletteButton(i, colors[i]);

        GUILayout.Space(4);

        // Eraser button
        using (var ch = new EditorGUI.ChangeCheckScope())
        {
            bool newErase = GUILayout.Toggle(_eraseMode, "✕  Eraser", "Button", GUILayout.Height(24));
            if (ch.changed) { _eraseMode = newErase; if (newErase) _keyMode = false; }
        }

        GUILayout.Space(2);

        // Key mode button — styled gold when active
        var prevBg = GUI.backgroundColor;
        GUI.backgroundColor = _keyMode ? new Color(1f, 0.85f, 0.1f) : Color.white;
        using (var ch = new EditorGUI.ChangeCheckScope())
        {
            bool newKey = GUILayout.Toggle(_keyMode, "⬛  Key (2×2)", "Button", GUILayout.Height(24));
            if (ch.changed) { _keyMode = newKey; if (newKey) { _eraseMode = false; _hpMode = false; } }
        }
        GUI.backgroundColor = prevBg;

        if (_keyMode)
        {
            EditorGUILayout.HelpBox(
                "Left-click: place 2×2 key\nRight-click: remove key at cell",
                MessageType.None);
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField($"Keys placed: {_keyPlacements.Count}", EditorStyles.miniLabel);

        EditorGUILayout.Space(6);
        DrawColorCounts();
        EditorGUILayout.Space(6);

        EditorGUILayout.LabelField("Fill", EditorStyles.boldLabel);
        if (GUILayout.Button("Fill Empty Cells"))
            FillEmpty(_selectedColor);
        if (GUILayout.Button("Fill All Cells"))
            FillAll(_selectedColor);

        EditorGUILayout.Space(4);

        if (GUILayout.Button("Clear Board")
            && EditorUtility.DisplayDialog("Clear", "Clear the entire board?", "Clear", "Cancel"))
        {
            ResetGrid();
            _keyPlacements.Clear();
            _keyCacheDirty = true;
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawPaletteButton(int index, Color color)
    {
        bool selected = !_eraseMode && !_keyMode && _selectedColor == index;
        var style = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleLeft,
            fontStyle = selected ? FontStyle.Bold : FontStyle.Normal,
            normal    = { background = _colorTextures[index], textColor = Color.white },
            hover     = { background = _colorTextures[index], textColor = Color.white },
            active    = { background = _colorTextures[index], textColor = Color.white },
            focused   = { background = _colorTextures[index], textColor = Color.white },
        };

        if (GUILayout.Button((selected ? "► " : "   ") + $"Color {index}", style, GUILayout.Height(24)))
        {
            _selectedColor = index;
            _eraseMode     = false;
            _keyMode       = false;
        }
    }

    private void DrawColorCounts()
    {
        EditorGUILayout.LabelField("Pixel Counts", EditorStyles.boldLabel);
        var colors   = ActiveColors;
        int[] counts = new int[colors.Length];

        for (int c = 0; c < _cols; c++)
            for (int r = 0; r < _rows; r++)
            {
                int ci = _colorGrid[c, r];
                if (ci >= 0 && ci < counts.Length) counts[ci]++;
            }

        int total = 0;
        for (int i = 0; i < colors.Length; i++)
        {
            if (counts[i] == 0) continue;
            var prev = GUI.color;
            GUI.color = Color.Lerp(colors[i], Color.gray, 0.15f);
            EditorGUILayout.LabelField($"  Color {i}", $"{counts[i]} px", EditorStyles.boldLabel);
            GUI.color = prev;
            total += counts[i];
        }
        EditorGUILayout.LabelField("  Total", $"{total} px", EditorStyles.boldLabel);
    }

    // ------------------------------------------------------------------ grid
    private void DrawGrid()
    {
        EnsureCellLabelStyle();

        float availW = position.width  - SIDEBAR_W - LANES_W - 32f;
        float availH = position.height - 50f;

        // Scale cell size so the whole grid fits without scrolling.
        float stepW = availW / Mathf.Max(1, _cols);
        float stepH = availH / Mathf.Max(1, _rows);
        float step  = Mathf.Max(MIN_CELL + GAP, Mathf.Min(stepW, stepH));
        float cell  = step - GAP;

        float totalW = _cols * step;
        float totalH = _rows * step;

        // Scroll view kept as a safety net for extreme window sizes.
        _gridScroll = EditorGUILayout.BeginScrollView(
            _gridScroll,
            GUILayout.Width(availW),
            GUILayout.Height(availH));

        Rect    canvas = GUILayoutUtility.GetRect(totalW, totalH);
        Color[] colors = ActiveColors;
        bool    showLabels = cell >= 14f;

        if (_keyCacheDirty) RebuildKeyCache();

        for (int r = 0; r < _rows; r++)
        {
            for (int c = 0; c < _cols; c++)
            {
                float cx   = canvas.x + c * step;
                float cy   = canvas.y + (_rows - 1 - r) * step;
                Rect  rect  = new Rect(cx, cy, cell, cell);
                Rect  inner = new Rect(cx + 1, cy + 1, cell - 2, cell - 2);

                var coord = new Vector2Int(c, r);
                int ci    = _colorGrid[c, r];

                if (_keyCells.Contains(coord))
                {
                    EditorGUI.DrawRect(rect,  KeyBorderColor);
                    EditorGUI.DrawRect(inner, KeyColor);
                    if (showLabels && _keyOrigins.Contains(coord))
                        GUI.Label(rect, "KEY", _cellLabelStyle);
                }
                else
                {
                    Color fill   = ci >= 0 && ci < colors.Length ? colors[ci] : new Color(0.18f, 0.18f, 0.18f);
                    Color border = ci >= 0 ? new Color(1f, 1f, 1f, 0.45f) : new Color(1f, 1f, 1f, 0.12f);
                    EditorGUI.DrawRect(rect,  border);
                    EditorGUI.DrawRect(inner, fill);

                    if (showLabels && ci >= 0 && _hpGrid[c, r] > 1)
                        GUI.Label(rect, _hpGrid[c, r].ToString(), _cellLabelStyle);
                }

                HandleCellInput(rect, c, r);
            }
        }

        if (_keyMode)
            DrawKeyPreview(canvas, step, cell);

        EditorGUILayout.EndScrollView();
    }

    private void DrawKeyPreview(Rect canvas, float step, float cell)
    {
        var e = Event.current;
        if (e.type != EventType.MouseMove && e.type != EventType.MouseDrag
            && e.type != EventType.Repaint) return;

        Vector2Int hover = CanvasToCell(canvas, e.mousePosition, step);
        if (hover.x < 0) return;

        for (int dc = 0; dc < 2; dc++)
        {
            for (int dr = 0; dr < 2; dr++)
            {
                int pc = hover.x + dc;
                int pr = hover.y + dr;
                if (pc < 0 || pc >= _cols || pr < 0 || pr >= _rows) continue;
                float x = canvas.x + pc * step;
                float y = canvas.y + (_rows - 1 - pr) * step;
                EditorGUI.DrawRect(new Rect(x, y, cell, cell), new Color(1f, 0.85f, 0.1f, 0.35f));
            }
        }

        if (e.type == EventType.MouseMove) Repaint();
    }

    private void HandleCellInput(Rect cell, int col, int row)
    {
        var e = Event.current;
        if (!cell.Contains(e.mousePosition)) return;
        if (e.type != EventType.MouseDown && e.type != EventType.MouseDrag) return;

        if (_keyMode)
        {
            if (e.button == 1)
            {
                _keyPlacements.RemoveAll(origin =>
                    col >= origin.x && col < origin.x + 2 &&
                    row >= origin.y && row < origin.y + 2);
                _keyCacheDirty = true;
            }
            else if (e.button == 0 && e.type == EventType.MouseDown)
            {
                TryPlaceKey(col, row);
            }
            e.Use();
            Repaint();
            return;
        }

        if (e.button == 1 || _eraseMode)
        {
            _colorGrid[col, row] = -1;
            _hpGrid[col, row]    = _defaultHp;
        }
        else if (_hpMode && _colorGrid[col, row] >= 0)
        {
            _hpGrid[col, row] = _paintHp;
        }
        else
        {
            _colorGrid[col, row] = _selectedColor;
            if (_hpGrid[col, row] <= 0) _hpGrid[col, row] = _defaultHp;
        }

        e.Use();
        Repaint();
    }

    private void TryPlaceKey(int col, int row)
    {
        // Clamp so the 2×2 stays within board bounds
        int c = Mathf.Clamp(col, 0, _cols - 2);
        int r = Mathf.Clamp(row, 0, _rows - 2);

        // Don't stack two keys on the same origin
        if (_keyPlacements.Contains(new Vector2Int(c, r))) return;

        // Don't overlap existing keys
        for (int dc = 0; dc < 2; dc++)
            for (int dr = 0; dr < 2; dr++)
                foreach (var existing in _keyPlacements)
                    if (c + dc >= existing.x && c + dc < existing.x + 2 &&
                        r + dr >= existing.y && r + dr < existing.y + 2)
                        return;

        _keyPlacements.Add(new Vector2Int(c, r));
        _keyCacheDirty = true;
    }

    // Helper: convert a mouse position inside the canvas rect to a board cell
    private Vector2Int CanvasToCell(Rect canvas, Vector2 mousePos, float step)
    {
        if (!canvas.Contains(mousePos)) return new Vector2Int(-1, -1);
        int c        = Mathf.FloorToInt((mousePos.x - canvas.x) / step);
        int rFlipped = Mathf.FloorToInt((mousePos.y - canvas.y) / step);
        int r        = (_rows - 1) - rFlipped;
        if (c < 0 || c >= _cols || r < 0 || r >= _rows) return new Vector2Int(-1, -1);
        return new Vector2Int(c, r);
    }

    // ------------------------------------------------------------------ pig lanes panel
    private void DrawPigLanes()
    {
        _lanesScroll = EditorGUILayout.BeginScrollView(
            _lanesScroll, GUILayout.Width(LANES_W));

        EditorGUILayout.LabelField("Lanes", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);

        var   colors      = ActiveColors;
        int   totalPigs   = 0;
        int   totalLocks  = 0;
        int[] pigPerColor = new int[colors.Length];

        for (int lane = 0; lane < LANE_COUNT; lane++)
        {
            DrawLaneHeader(lane, colors);

            var list     = _pigLanes[lane];
            int toRemove = -1;

            for (int p = 0; p < list.Count; p++)
            {
                var entry = list[p];
                EditorGUILayout.BeginHorizontal();

                if (entry.isLock)
                {
                    // Lock row: grey padlock icon area + LOCK label
                    var prevCol = GUI.color;
                    GUI.color = new Color(0.6f, 0.6f, 0.65f);
                    EditorGUILayout.LabelField("🔒 LOCK", GUILayout.Width(60));
                    GUI.color = prevCol;
                }
                else
                {
                    // Color dot
                    Color dotColor = entry.isHidden ? Color.gray
                        : (entry.colorIndex < colors.Length ? colors[entry.colorIndex] : Color.white);
                    Rect dotRect = GUILayoutUtility.GetRect(14, 14, GUILayout.Width(14));
                    EditorGUI.DrawRect(dotRect, dotColor);

                    // Color index
                    entry.colorIndex = Mathf.Clamp(
                        EditorGUILayout.IntField(entry.colorIndex, GUILayout.Width(22)),
                        0, colors.Length - 1);

                    // Ammo
                    EditorGUILayout.LabelField("x", GUILayout.Width(10));
                    entry.ammo = Mathf.Max(1, EditorGUILayout.IntField(entry.ammo, GUILayout.Width(26)));

                    // Hidden toggle
                    entry.isHidden = EditorGUILayout.ToggleLeft("?", entry.isHidden, GUILayout.Width(28));
                }

                // Pig/Lock toggle
                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = entry.isLock ? new Color(0.6f, 0.6f, 0.8f) : Color.white;
                if (GUILayout.Button(entry.isLock ? "P" : "L", GUILayout.Width(20), GUILayout.Height(16)))
                    entry.isLock = !entry.isLock;
                GUI.backgroundColor = prevBg;

                list[p] = entry;

                if (GUILayout.Button("✕", GUILayout.Width(20), GUILayout.Height(16)))
                    toRemove = p;

                EditorGUILayout.EndHorizontal();

                if (entry.isLock) totalLocks++;
                else
                {
                    totalPigs++;
                    if (entry.colorIndex < pigPerColor.Length)
                        pigPerColor[entry.colorIndex]++;
                }
            }

            if (toRemove >= 0) list.RemoveAt(toRemove);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button($"+ Pig {lane}", GUILayout.Height(20)))
                list.Add(new LaneEntry { colorIndex = _selectedColor, ammo = 3 });
            if (GUILayout.Button($"+ Lock {lane}", GUILayout.Height(20)))
                list.Add(new LaneEntry { isLock = true });
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);
        }

        // Summary
        EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
        for (int i = 0; i < colors.Length; i++)
        {
            if (pigPerColor[i] == 0) continue;
            var prev = GUI.color;
            GUI.color = Color.Lerp(colors[i], Color.gray, 0.15f);
            EditorGUILayout.LabelField($"  Color {i}", $"{pigPerColor[i]} pig(s)", EditorStyles.boldLabel);
            GUI.color = prev;
        }
        EditorGUILayout.LabelField("  Pigs total",  $"{totalPigs}",  EditorStyles.boldLabel);
        EditorGUILayout.LabelField("  Locks total", $"{totalLocks}", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("  Keys placed", $"{_keyPlacements.Count}", EditorStyles.boldLabel);

        EditorGUILayout.Space(8);
        if (GUILayout.Button("Clear All Lanes")
            && EditorUtility.DisplayDialog("Clear Lanes", "Remove all lane items?", "Clear", "Cancel"))
            ResetPigLanes();

        EditorGUILayout.EndScrollView();
    }

    private void DrawLaneHeader(int lane, Color[] colors)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Lane {lane}", EditorStyles.boldLabel, GUILayout.Width(50));

        var seen = new HashSet<int>();
        foreach (var p in _pigLanes[lane])
            if (!p.isLock && p.colorIndex >= 0 && p.colorIndex < colors.Length)
                seen.Add(p.colorIndex);

        foreach (int ci in seen)
        {
            Rect dot = GUILayoutUtility.GetRect(12, 12, GUILayout.Width(12));
            EditorGUI.DrawRect(dot, colors[ci]);
        }

        int lockCount = 0;
        foreach (var p in _pigLanes[lane]) if (p.isLock) lockCount++;

        string label = $"({_pigLanes[lane].Count}";
        if (lockCount > 0) label += $" / {lockCount}🔒";
        label += ")";
        EditorGUILayout.LabelField(label, GUILayout.Width(60));
        EditorGUILayout.EndHorizontal();
    }

    // ================================================================== data
    private void ResetGrid()
    {
        _colorGrid = new int[_cols, _rows];
        _hpGrid    = new int[_cols, _rows];
        for (int c = 0; c < _cols; c++)
            for (int r = 0; r < _rows; r++)
            {
                _colorGrid[c, r] = -1;
                _hpGrid[c, r]    = _defaultHp;
            }
        Repaint();
    }

    private void ResetPigLanes()
    {
        _pigLanes = new List<LaneEntry>[LANE_COUNT];
        for (int i = 0; i < LANE_COUNT; i++)
            _pigLanes[i] = new List<LaneEntry>();
    }

    private void LoadFromTarget()
    {
        if (_target == null) return;

        _cols = Mathf.Max(1, _target.boardColumns);
        _rows = Mathf.Max(1, _target.boardRows);
        ResetGrid();

        if (_target.cubes != null)
            foreach (var entry in _target.cubes)
                if (entry.col >= 0 && entry.col < _cols && entry.row >= 0 && entry.row < _rows)
                {
                    _colorGrid[entry.col, entry.row] = entry.colorIndex;
                    _hpGrid[entry.col, entry.row]    = Mathf.Max(1, entry.hitPoints);
                }

        ResetPigLanes();
        if (_target.pigQueue != null)
            foreach (var p in _target.pigQueue)
            {
                int lane = Mathf.Clamp(p.laneIndex, 0, LANE_COUNT - 1);
                _pigLanes[lane].Add(new LaneEntry
                {
                    colorIndex = p.colorIndex,
                    ammo       = p.ammo,
                    isHidden   = p.isHidden,
                    isLock     = p.itemType == LevelData.LaneItemType.Lock,
                });
            }

        // Load keys
        _keyPlacements.Clear();
        if (_target.keys != null)
            foreach (var k in _target.keys)
                _keyPlacements.Add(new Vector2Int(k.col, k.row));
        _keyCacheDirty = true;

        Repaint();
        Debug.Log($"[LevelEditor] Loaded '{_target.name}': {_target.cubes?.Length ?? 0} cubes, " +
                  $"{_target.pigQueue?.Length ?? 0} lane items, {_keyPlacements.Count} keys.");
    }

    private void SaveToTarget()
    {
        if (_target == null) return;

        // Cubes
        var cubes = new List<LevelData.CubeEntry>();
        for (int c = 0; c < _cols; c++)
            for (int r = 0; r < _rows; r++)
            {
                int ci = _colorGrid[c, r];
                if (ci < 0) continue;
                cubes.Add(new LevelData.CubeEntry
                {
                    col        = c,
                    row        = r,
                    colorIndex = ci,
                    hitPoints  = _hpGrid[c, r],
                });
            }

        // Lane items (pigs + locks)
        var pigs = new List<LevelData.PigEntry>();
        for (int lane = 0; lane < LANE_COUNT; lane++)
            foreach (var p in _pigLanes[lane])
                pigs.Add(new LevelData.PigEntry
                {
                    laneIndex  = lane,
                    colorIndex = p.isLock ? 0 : p.colorIndex,
                    ammo       = p.isLock ? 0 : p.ammo,
                    isHidden   = p.isHidden,
                    itemType   = p.isLock
                                    ? LevelData.LaneItemType.Lock
                                    : LevelData.LaneItemType.Pig,
                });

        // Keys
        var keys = new List<LevelData.KeyEntry>();
        foreach (var k in _keyPlacements)
            keys.Add(new LevelData.KeyEntry { col = k.x, row = k.y });

        Undo.RecordObject(_target, "Save Level");
        _target.boardColumns = _cols;
        _target.boardRows    = _rows;
        _target.cubes        = cubes.ToArray();
        _target.pigQueue     = pigs.ToArray();
        _target.keys         = keys.ToArray();
        EditorUtility.SetDirty(_target);
        AssetDatabase.SaveAssets();

        Debug.Log($"[LevelEditor] Saved '{_target.name}': {cubes.Count} cubes, " +
                  $"{pigs.Count} lane items, {keys.Count} keys.");
    }

    private void CreateNewLevelData()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create LevelData", "Level_01", "asset", "Choose save location");
        if (string.IsNullOrEmpty(path)) return;

        var asset = CreateInstance<LevelData>();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        _target = asset;
        SaveToTarget();
        Debug.Log($"[LevelEditor] Created '{path}'.");
    }

    private void FillEmpty(int colorIndex)
    {
        for (int c = 0; c < _cols; c++)
            for (int r = 0; r < _rows; r++)
                if (_colorGrid[c, r] < 0)
                {
                    _colorGrid[c, r] = colorIndex;
                    if (_hpGrid[c, r] <= 0) _hpGrid[c, r] = _defaultHp;
                }
        Repaint();
    }

    private void FillAll(int colorIndex)
    {
        for (int c = 0; c < _cols; c++)
            for (int r = 0; r < _rows; r++)
            {
                _colorGrid[c, r] = colorIndex;
                if (_hpGrid[c, r] <= 0) _hpGrid[c, r] = _defaultHp;
            }
        Repaint();
    }

    private void RebuildKeyCache()
    {
        _keyCells.Clear();
        _keyOrigins.Clear();
        foreach (var origin in _keyPlacements)
        {
            _keyOrigins.Add(origin);
            for (int dc = 0; dc < 2; dc++)
                for (int dr = 0; dr < 2; dr++)
                    _keyCells.Add(new Vector2Int(origin.x + dc, origin.y + dr));
        }
        _keyCacheDirty = false;
    }

    // ================================================================== helpers
    private void TryAutoFindConfig()
    {
        if (_config != null) return;
        var guids = AssetDatabase.FindAssets("t:GameConfig");
        if (guids.Length > 0)
            _config = AssetDatabase.LoadAssetAtPath<GameConfig>(
                AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    private void RebuildTextures()
    {
        DestroyTextures();
        _cachedColors  = ActiveColors;
        _colorTextures = new Texture2D[_cachedColors.Length];
        for (int i = 0; i < _cachedColors.Length; i++)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, _cachedColors[i] * 0.8f);
            tex.Apply();
            _colorTextures[i] = tex;
        }
        _emptyTex = MakeSolidTex(new Color(0.18f, 0.18f, 0.18f));
        _keyTex   = MakeSolidTex(new Color(1f, 0.85f, 0.1f));
        Repaint();
    }

    private void DestroyTextures()
    {
        if (_colorTextures != null)
            foreach (var t in _colorTextures)
                if (t != null) DestroyImmediate(t);
        if (_emptyTex != null) DestroyImmediate(_emptyTex);
        if (_keyTex   != null) DestroyImmediate(_keyTex);
        _colorTextures = null;
    }

    private static Texture2D MakeSolidTex(Color c)
    {
        var t = new Texture2D(1, 1);
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }

    private void EnsureCellLabelStyle()
    {
        if (_cellLabelStyle != null) return;
        _cellLabelStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize  = 10,
            normal    = { textColor = Color.black },
        };
    }
}
