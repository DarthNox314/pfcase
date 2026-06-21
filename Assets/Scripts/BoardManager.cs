using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the pixel cube grid.
/// Cubes are plain data structs (no MonoBehaviours). Rendering is done each frame
/// via Graphics.DrawMeshInstanced — one draw call per color.
/// </summary>
public class BoardManager : MonoBehaviour
{
    // ------------------------------------------------------------------ inspector
    [Header("Dependencies")]
    [SerializeField] private GameConfig        _config;
    [SerializeField] private AnimationConfig   _anim;
    [SerializeField] private GameEventRegistry _events;

    [Header("Prefabs")]
    [SerializeField] private GameObject _keyPrefab;

    [Header("Key-Lock")]
    [SerializeField] private KeyLockManager _keyLockManager;

    [Header("Layout")]
    [Tooltip("World-space centre of the board.")]
    [SerializeField] private Transform _boardCenter;
    [Tooltip("Bottom-left corner of the board area. " +
             "cubeSize is recalculated so col=0 row=0 sits here and the board is centred on _boardCenter.")]
    [SerializeField] private Transform _boardOrigin;

    [Header("Rendering")]
    [Tooltip("Material with 'Enable GPU Instancing' checked. Color is overridden per color index.")]
    [SerializeField] private Material _cubeMaterial;

    [Tooltip("Optional sprite per color index. If assigned, its texture replaces the solid color. " +
             "Array length must match GameConfig.pigColors. Leave empty to use solid colors only.")]
    [SerializeField] private Sprite[] _cubeSprites;

    // ------------------------------------------------------------------ public state
    public Vector3 BoardCenter { get; private set; }

    // ------------------------------------------------------------------ cube data
    private struct CubeData
    {
        public int       col, row;
        public int       colorIndex;
        public int       hitPoints;
        public int       pendingHits;
        public bool      isCleared;
        public Vector3   worldPos;
        public float     animScale;
        public AnimPhase animPhase;
        public float     animTimer;
    }

    private enum AnimPhase { Idle, HitPunchUp, HitPunchDown, DyingPop, DyingShrink }

    private CubeData[]   _cubes;
    private CubeHandle[] _handles;
    private int          _cubeCount;
    private readonly List<GameObject> _spawnedKeys = new();
    private readonly Dictionary<Vector2Int, int>   _cubeIndex = new();

    // Spatial buckets for FindTargetsInLine — keyed by grid column (X) or grid row (Y).
    // Firing vertically (up/down) → look up by grid column (same world-X).
    // Firing horizontally (left/right) → look up by grid row (same world-Y).
    private readonly Dictionary<int, List<int>> _byCol = new();  // col → cube indices
    private readonly Dictionary<int, List<int>> _byRow = new();  // row → cube indices

    // ------------------------------------------------------------------ board state
    private int     _remainingCubes;
    private float   _step;
    private float   _runtimeCubeSize;
    public  float   RuntimeStep => _step;
    private float   _runtimeCubeGap;
    private Vector3 _centerOffset;

    // ------------------------------------------------------------------ rendering
    private Mesh          _quad;
    private Material[]    _colorMaterials;
    private Matrix4x4[][] _matrices;
    private int[]         _matrixCounts;

    // ------------------------------------------------------------------ FindTargetsInLine caches
    private readonly List<IHittable>          _resultsCache    = new();
    private readonly List<(float, IHittable)> _candidatesCache = new();
    private static readonly System.Comparison<(float dist, IHittable)> _distCompare =
        (a, b) => a.dist.CompareTo(b.dist);

    // ------------------------------------------------------------------ public API
    public void BuildBoard(LevelData level)
    {
        ClearBoard();

        if (_boardCenter == null || _boardOrigin == null)
        {
            Debug.LogError("[BoardManager] _boardCenter or _boardOrigin not assigned!");
            return;
        }

        int maxCol = 0, maxRow = 0;
        foreach (var entry in level.cubes)
        {
            if (entry.col > maxCol) maxCol = entry.col;
            if (entry.row > maxRow) maxRow = entry.row;
        }

        RecalcCubeSize(maxCol + 1, maxRow + 1);
        _step         = _runtimeCubeSize + _runtimeCubeGap;
        _centerOffset = new Vector3(maxCol * _step * 0.5f, maxRow * _step * 0.5f, 0f);

        _cubeCount = level.cubes.Length;
        _cubes     = new CubeData[_cubeCount];
        _handles   = new CubeHandle[_cubeCount];

        for (int i = 0; i < _cubeCount; i++)
        {
            var e   = level.cubes[i];
            var pos = _boardCenter.position + new Vector3(e.col * _step, e.row * _step, 0f) - _centerOffset;

            _cubes[i] = new CubeData
            {
                col        = e.col,
                row        = e.row,
                colorIndex = Mathf.Clamp(e.colorIndex, 0, _config.pigColors.Length - 1),
                hitPoints  = Mathf.Max(1, e.hitPoints),
                worldPos   = pos,
                animScale  = 1f,
                animPhase  = AnimPhase.Idle,
            };
            _handles[i] = new CubeHandle(i, this);
            _cubeIndex[new Vector2Int(e.col, e.row)] = i;

            if (!_byCol.TryGetValue(e.col, out var colList)) { colList = new List<int>(); _byCol[e.col] = colList; }
            colList.Add(i);
            if (!_byRow.TryGetValue(e.row, out var rowList)) { rowList = new List<int>(); _byRow[e.row] = rowList; }
            rowList.Add(i);
        }

        _remainingCubes = _cubeCount;
        BoardCenter     = _boardCenter.position;

        InitRendering();
        SpawnKeys(level);
        RebuildMatrices();

        Debug.Log($"[BoardManager] {_cubeCount} cubes built. Center={BoardCenter} Step={_step:F3}");
    }

    /// <summary>
    /// Raycast along fireDirection from pigPosition. Returns same-color cubes
    /// within one cube-width of the ray, nearest first. Stops at the first
    /// wrong-color cube — it blocks everything behind it.
    /// Uses spatial buckets: fires vertically → look up by grid column,
    /// fires horizontally → look up by grid row. O(cubes in line) not O(all cubes).
    /// </summary>
    public List<IHittable> FindTargetsInLine(int colorIndex, Vector3 pigPosition, Vector2 fireDirection)
    {
        _resultsCache.Clear();
        _candidatesCache.Clear();

        float halfCube = _runtimeCubeSize * 0.5f;

        // Pick the bucket that matches this fire direction.
        // Vertical fire (up/down) → all candidates share the pig's grid column (same world X).
        // Horizontal fire (left/right) → all candidates share the pig's grid row (same world Y).
        bool firesVertically = Mathf.Abs(fireDirection.y) > 0.5f;
        List<int> bucket;
        if (firesVertically)
        {
            int col = Mathf.RoundToInt((pigPosition.x - BoardCenter.x + _centerOffset.x) / _step);
            if (!_byCol.TryGetValue(col, out bucket)) return _resultsCache;
        }
        else
        {
            int row = Mathf.RoundToInt((pigPosition.y - BoardCenter.y + _centerOffset.y) / _step);
            if (!_byRow.TryGetValue(row, out bucket)) return _resultsCache;
        }

        Vector2 perp   = new Vector2(-fireDirection.y, fireDirection.x);
        float   pigLat = Vector2.Dot((Vector2)pigPosition, perp);

        foreach (int i in bucket)
        {
            ref var d = ref _cubes[i];
            if (d.isCleared) continue;
            if (d.pendingHits >= d.hitPoints) continue;

            Vector2 toC  = (Vector2)(d.worldPos - pigPosition);
            float   dist = Vector2.Dot(toC, fireDirection);
            if (dist <= 0f) continue;

            // Lateral tolerance still checked — handles floating point drift.
            float lateral = Mathf.Abs(Vector2.Dot((Vector2)d.worldPos, perp) - pigLat);
            if (lateral > halfCube) continue;

            _candidatesCache.Add((dist, _handles[i]));
        }

        _candidatesCache.Sort(_distCompare);

        foreach (var (_, cube) in _candidatesCache)
        {
            if (cube.ColorIndex == colorIndex)
                _resultsCache.Add(cube);
            else
                break;
        }

        return _resultsCache;
    }

    // ------------------------------------------------------------------ public helpers
    public IHittable GetCubeAt(Vector2Int cell)
        => _cubeIndex.TryGetValue(cell, out int i) ? _handles[i] : null;

    public Vector3 GridToWorld(int col, int row)
        => _boardCenter.position + new Vector3(col * _step, row * _step, 0f) - _centerOffset;

    // ------------------------------------------------------------------ CubeHandle accessors (called by CubeHandle)
    public Vector3 GetCubePosition(int i)    => _cubes[i].worldPos;
    public int     GetCubeColorIndex(int i)  => _cubes[i].colorIndex;
    public bool    IsCubeCleared(int i)      => _cubes[i].isCleared;
    public int     GetCubeHitPoints(int i)   => _cubes[i].hitPoints;
    public int     GetCubePendingHits(int i) => _cubes[i].pendingHits;

    public void ReserveCubePendingHit(int i) => _cubes[i].pendingHits++;
    public void ReleaseCubePendingHit(int i) => _cubes[i].pendingHits = Mathf.Max(0, _cubes[i].pendingHits - 1);

    public void ReceiveCubeHit(int i)
    {
        ref var cube = ref _cubes[i];
        if (cube.isCleared) return;

        cube.hitPoints--;
        _events.onCubeHit.Raise(_handles[i]);

        if (cube.hitPoints <= 0)
            StartClearAnim(i);
        else
            StartHitAnim(i);
    }

    // ------------------------------------------------------------------ animation
    private void StartHitAnim(int i)
    {
        ref var cube = ref _cubes[i];
        cube.animPhase = AnimPhase.HitPunchUp;
        cube.animTimer = 0f;
    }

    private void StartClearAnim(int i)
    {
        ref var cube = ref _cubes[i];
        cube.isCleared = true;
        cube.animPhase = AnimPhase.DyingPop;
        cube.animTimer = 0f;

        _cubeIndex.Remove(new Vector2Int(cube.col, cube.row));
        if (_byCol.TryGetValue(cube.col, out var colList)) colList.Remove(i);
        if (_byRow.TryGetValue(cube.row, out var rowList)) rowList.Remove(i);
        _events.onCubeCleared.Raise(_handles[i]);

        _remainingCubes--;
        if (_remainingCubes <= 0) _events.onBoardCleared.Raise();
    }

    private void Update()
    {
        if (_cubes == null) return;

        bool changed   = false;
        float halfHit  = _anim.cubeHitDuration * 0.5f;

        for (int i = 0; i < _cubeCount; i++)
        {
            ref var c = ref _cubes[i];
            if (c.animPhase == AnimPhase.Idle) continue;
            changed      = true;
            c.animTimer += Time.deltaTime;

            switch (c.animPhase)
            {
                case AnimPhase.HitPunchUp:
                {
                    float t = Mathf.Clamp01(c.animTimer / halfHit);
                    c.animScale = Mathf.Lerp(1f, _anim.cubeHitScalePeak, t);
                    if (t >= 1f) { c.animPhase = AnimPhase.HitPunchDown; c.animTimer = 0f; }
                    break;
                }
                case AnimPhase.HitPunchDown:
                {
                    float t = Mathf.Clamp01(c.animTimer / halfHit);
                    c.animScale = Mathf.Lerp(_anim.cubeHitScalePeak, 1f, t);
                    if (t >= 1f) { c.animScale = 1f; c.animPhase = AnimPhase.Idle; }
                    break;
                }
                case AnimPhase.DyingPop:
                {
                    float t = Mathf.Clamp01(c.animTimer / _anim.cubeDestroyScaleUpDur);
                    c.animScale = Mathf.Lerp(1f, _anim.cubeDestroyScalePeak, t);
                    if (t >= 1f) { c.animPhase = AnimPhase.DyingShrink; c.animTimer = 0f; }
                    break;
                }
                case AnimPhase.DyingShrink:
                {
                    float t = Mathf.Clamp01(c.animTimer / _anim.cubeDestroyScaleDownDur);
                    c.animScale = Mathf.Lerp(_anim.cubeDestroyScalePeak, 0f, t);
                    if (t >= 1f) { c.animScale = 0f; c.animPhase = AnimPhase.Idle; }
                    break;
                }
            }
        }

        if (changed) RebuildMatrices();
    }

    // ------------------------------------------------------------------ rendering
    private void InitRendering()
    {
        if (_cubeMaterial == null)
        {
            Debug.LogError("[BoardManager] _cubeMaterial not assigned — cubes will be invisible!");
            return;
        }

        int n        = _config.pigColors.Length;
        bool hasSprites = _cubeSprites != null && _cubeSprites.Length == n;
        // Pick the first valid sprite to extract UVs; all sprites should use the same atlas layout.
        Sprite refSprite = hasSprites ? System.Array.Find(_cubeSprites, s => s != null) : null;
        _quad = CreateQuad(refSprite);
        _colorMaterials = new Material[n];
        _matrices       = new Matrix4x4[n][];
        _matrixCounts   = new int[n];

        for (int c = 0; c < n; c++)
        {
            _colorMaterials[c] = new Material(_cubeMaterial);

            if (hasSprites && _cubeSprites[c] != null)
            {
                // Sprite texture drives appearance; tint from GameConfig.pigColors.
                _colorMaterials[c].mainTexture = _cubeSprites[c].texture;
                _colorMaterials[c].color       = _config.pigColors[c];
            }
            else
            {
                _colorMaterials[c].color = _config.pigColors[c];
            }

            _matrices[c] = new Matrix4x4[_cubeCount];  // worst case all cubes same color
        }
    }

    private void RebuildMatrices()
    {
        if (_colorMaterials == null) return;
        for (int c = 0; c < _matrixCounts.Length; c++) _matrixCounts[c] = 0;

        for (int i = 0; i < _cubeCount; i++)
        {
            ref var d = ref _cubes[i];
            if (d.animScale <= 0f) continue;

            float s    = _runtimeCubeSize * d.animScale;
            int   slot = _matrixCounts[d.colorIndex]++;
            _matrices[d.colorIndex][slot] = Matrix4x4.TRS(d.worldPos, Quaternion.identity, new Vector3(s, s, s));
        }
    }

    private const int DrawBatchMax = 1023;  // DrawMeshInstanced hard limit

    private void LateUpdate()
    {
        if (_colorMaterials == null || _quad == null) return;
        for (int c = 0; c < _colorMaterials.Length; c++)
        {
            int total = _matrixCounts[c];
            if (total == 0) continue;

            // Submit in chunks of 1023 when a color has more instances than the API limit.
            for (int offset = 0; offset < total; offset += DrawBatchMax)
            {
                int count = Mathf.Min(DrawBatchMax, total - offset);
                if (offset == 0)
                {
                    Graphics.DrawMeshInstanced(_quad, 0, _colorMaterials[c], _matrices[c], count);
                }
                else
                {
                    // Slice into a temp array for the overflow batch.
                    var slice = new Matrix4x4[count];
                    System.Array.Copy(_matrices[c], offset, slice, 0, count);
                    Graphics.DrawMeshInstanced(_quad, 0, _colorMaterials[c], slice, count);
                }
            }
        }
    }

    private static Mesh CreateQuad(Sprite sprite = null)
    {
        // If a sprite is provided, use its UV rect so atlas-packed sprites display correctly.
        Vector2 uvMin = Vector2.zero, uvMax = Vector2.one;
        if (sprite != null)
        {
            var r  = sprite.textureRect;
            var tw = sprite.texture.width;
            var th = sprite.texture.height;
            uvMin = new Vector2(r.xMin / tw, r.yMin / th);
            uvMax = new Vector2(r.xMax / tw, r.yMax / th);
        }

        var mesh = new Mesh { name = "CubeQuad" };
        mesh.vertices  = new[] { new Vector3(-0.5f,-0.5f,0), new Vector3(0.5f,-0.5f,0),
                                  new Vector3(0.5f, 0.5f,0), new Vector3(-0.5f, 0.5f,0) };
        mesh.uv        = new[] { new Vector2(uvMin.x, uvMin.y), new Vector2(uvMax.x, uvMin.y),
                                  new Vector2(uvMax.x, uvMax.y), new Vector2(uvMin.x, uvMax.y) };
        mesh.triangles = new[] { 0,2,1, 0,3,2 };
        mesh.RecalculateNormals();
        return mesh;
    }

    // ------------------------------------------------------------------ key spawning
    private void SpawnKeys(LevelData level)
    {
        if (level.keys == null || level.keys.Length == 0) return;
        if (_keyPrefab == null)     { Debug.LogWarning("[BoardManager] _keyPrefab not assigned."); return; }
        if (_keyLockManager == null){ Debug.LogWarning("[BoardManager] _keyLockManager not assigned."); return; }

        foreach (var entry in level.keys)
        {
            Vector3 topLeft = GridToWorld(entry.col, entry.row);
            Vector3 keyPos  = topLeft + new Vector3(_step * 0.5f, _step * 0.5f, 0f);

            var go  = Instantiate(_keyPrefab, keyPos, Quaternion.identity, transform);
            _spawnedKeys.Add(go);
            var key = go.GetComponent<KeyController>();
            if (key == null) { Debug.LogError("[BoardManager] Key prefab missing KeyController!"); continue; }
            key.Initialize(entry.col, entry.row, this, _events, _keyLockManager, _anim);
        }
    }

    // ------------------------------------------------------------------ helpers
    private void ClearBoard()
    {
        foreach (var go in _spawnedKeys)
            if (go != null) Destroy(go);
        _spawnedKeys.Clear();

        _cubeIndex.Clear();
        _byCol.Clear();
        _byRow.Clear();
        _cubes          = null;
        _handles        = null;
        _cubeCount      = 0;
        _remainingCubes = 0;
        _colorMaterials = null;
        _matrices       = null;
        _matrixCounts   = null;
    }

    /// <summary>
    /// Derives _runtimeCubeSize/_runtimeCubeGap from the scene transforms.
    /// Available area = (_boardCenter - _boardOrigin) * 2.
    /// step = min(availableWidth/cols, availableHeight/rows).
    /// Gap scales proportionally so visual density stays consistent.
    /// </summary>
    private void RecalcCubeSize(int cols, int rows)
    {
        float availableW = (_boardCenter.position.x - _boardOrigin.position.x) * 2f;
        float availableH = (_boardCenter.position.y - _boardOrigin.position.y) * 2f;

        float step = Mathf.Min(availableW / cols, availableH / rows);

        float gapRatio   = _config.cubeGap / (_config.cubeSize + _config.cubeGap);
        _runtimeCubeGap  = step * gapRatio;
        _runtimeCubeSize = step - _runtimeCubeGap;

        Debug.Log($"[BoardManager] {cols}x{rows} board — available {availableW:F2}x{availableH:F2} " +
                  $"→ cubeSize={_runtimeCubeSize:F3} cubeGap={_runtimeCubeGap:F3}");
    }
}
