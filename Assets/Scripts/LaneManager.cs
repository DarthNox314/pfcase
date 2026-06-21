using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Manages 4 independent lanes and 5 waiting slots.
/// Lanes can contain any IStackable — pigs, locks, or future types.
///
/// INSPECTOR SETUP:
///   _laneOrigins  — 4 Transforms (front/top of each lane)
///   _waitingSlots — 5 Transforms
///   _pigPrefab, _lockPrefab — lane item prefabs
///   _keyLockManager, _config, _events, _board, _conveyorPath
/// </summary>
public class LaneManager : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private GameConfig        _config;
    [SerializeField] private AnimationConfig   _anim;
    [SerializeField] private GameEventRegistry _events;
    [SerializeField] private BoardManager      _board;
    [SerializeField] private ConveyorPath      _conveyorPath;
    [SerializeField] private KeyLockManager    _keyLockManager;

    [Header("Prefabs")]
    [SerializeField] private GameObject _pigPrefab;
    [SerializeField] private GameObject _lockPrefab;

    [Header("Lane Origins (4 — one per lane, front/top position)")]
    [SerializeField] private Transform[] _laneOrigins;

    [Header("Waiting Slots (5)")]
    [SerializeField] private Transform[] _waitingSlots;

    [Header("Layout")]
    [Tooltip("Vertical distance between stacked items in a lane (world units).")]
    [SerializeField] private float _pigSpacing = 0.8f;

    // ------------------------------------------------------------------ public state
    public int  ActiveOnBelt => _activeOnPath;
    public int  BeltCapacity => _config != null ? _config.beltCapacity : 0;
    public bool IsFrenzy     => _isFrenzy;

    // ------------------------------------------------------------------ runtime
    private Queue<LevelData.PigEntry>[] _incomingPerLane;
    private List<IStackable>[]          _laneSlots;
    private IStackable[]                _waitingPigs;
    private int                         _activeOnPath;
    private float                       _dispatchCooldown;
    private bool                        _isFrenzy;

    // ------------------------------------------------------------------ lifecycle
    private void Awake()
    {
        ValidateInspector();

        int laneCount    = _laneOrigins.Length;
        _incomingPerLane = new Queue<LevelData.PigEntry>[laneCount];
        _laneSlots       = new List<IStackable>[laneCount];

        for (int i = 0; i < laneCount; i++)
        {
            _incomingPerLane[i] = new Queue<LevelData.PigEntry>();
            _laneSlots[i]       = new List<IStackable>();
        }

        _waitingPigs = new IStackable[_waitingSlots.Length];
        Debug.Log($"[LaneManager] Awake OK — {laneCount} lanes, {_waitingSlots.Length} waiting slots.");
    }

    private void Update()
    {
        if (_dispatchCooldown > 0f)
            _dispatchCooldown -= Time.deltaTime;
    }

    private void OnEnable()
    {
        _events.onPigFinishedPass.Subscribe(OnPigFinishedPass);
        _events.onPigExpired.Subscribe(OnPigExpired);
        _events.onLockUnlocked.Subscribe(OnLockUnlocked);
    }

    private void OnDisable()
    {
        _events.onPigFinishedPass.Unsubscribe(OnPigFinishedPass);
        _events.onPigExpired.Unsubscribe(OnPigExpired);
        _events.onLockUnlocked.Unsubscribe(OnLockUnlocked);
    }

    // ------------------------------------------------------------------ public API
    public void LoadLevel(LevelData level)
    {
        ClearAll();

        if (level.pigQueue == null || level.pigQueue.Length == 0)
        {
            Debug.LogError($"[LaneManager] LevelData '{level.name}' has no lane items!");
            return;
        }

        foreach (var entry in level.pigQueue)
        {
            int lane = Mathf.Clamp(entry.laneIndex, 0, _laneOrigins.Length - 1);
            _incomingPerLane[lane].Enqueue(entry);
        }

        for (int i = 0; i < _laneOrigins.Length; i++)
            SpawnAllInLane(i);

        for (int i = 0; i < _laneOrigins.Length; i++)
            Debug.Log($"[LaneManager] Lane {i}: {_laneSlots[i].Count} items spawned.");
    }

    public void RequestDispatch(IStackable item)
    {
        if (_dispatchCooldown > 0f) return;

        if (_activeOnPath >= _config.beltCapacity)
        {
            Debug.Log($"[LaneManager] Belt full ({_activeOnPath}/{_config.beltCapacity}).");
            return;
        }

        for (int i = 0; i < _laneSlots.Length; i++)
        {
            if (_laneSlots[i].Count > 0 && ReferenceEquals(_laneSlots[i][0], item))
            {
                _laneSlots[i].RemoveAt(0);
                SlideLaneUp(i);
                DispatchItem(item);
                return;
            }
        }

        for (int i = 0; i < _waitingPigs.Length; i++)
        {
            if (ReferenceEquals(_waitingPigs[i], item))
            {
                _waitingPigs[i] = null;
                DispatchItem(item);
                return;
            }
        }

        Debug.Log("[LaneManager] Clicked item is not at the front of its lane.");
    }

    public void RemoveFromLane(IStackable item)
    {
        var lane = _laneSlots[item.LaneIndex];
        for (int i = 0; i < lane.Count; i++)
        {
            if (!ReferenceEquals(lane[i], item)) continue;
            lane.RemoveAt(i);
            SlideLaneUp(item.LaneIndex);
            return;
        }
    }

    // ------------------------------------------------------------------ dispatch
    private void DispatchItem(IStackable item)
    {
        if (item is not IConvMovable convMovable)
        {
            Debug.LogWarning($"[LaneManager] Item in lane {item.LaneIndex} does not implement IConvMovable — cannot dispatch.");
            return;
        }

        _activeOnPath++;
        _dispatchCooldown = _pigSpacing / _config.beltSpeed;
        convMovable.Dispatch();

        if (item is PigController pig)
            Debug.Log($"[LaneManager] → Dispatched pig lane={pig.LaneIndex} color={pig.ColorIndex} ammo={pig.Ammo}. Active={_activeOnPath}");
        else
            Debug.Log($"[LaneManager] → Dispatched lane={item.LaneIndex}. Active={_activeOnPath}");
    }

    // ------------------------------------------------------------------ callbacks
    private void OnPigFinishedPass(PigController pig)
    {
        _activeOnPath = Mathf.Max(0, _activeOnPath - 1);

        if (_isFrenzy)
        {
            // Loop immediately — no waiting slot, no cooldown
            _activeOnPath++;
            pig.Dispatch();
            Debug.Log($"[LaneManager] Frenzy: re-dispatching pig color={pig.ColorIndex} ammo={pig.Ammo}.");
            return;
        }

        for (int i = 0; i < _waitingPigs.Length; i++)
        {
            if (_waitingPigs[i] == null)
            {
                _waitingPigs[i]        = pig;
                pig.transform.rotation = Quaternion.identity;
                pig.ReturnToSlot(_waitingSlots[i].position);
                Debug.Log($"[LaneManager] Pig → waiting slot {i}. Ammo left={pig.Ammo}");
                return;
            }
        }

        Debug.Log("[LaneManager] Waiting slots full — level failed.");
        Destroy(pig.gameObject);
        _events.onLevelFailed.Raise();
    }

    private void OnPigExpired(PigController pig)
    {
        _activeOnPath = Mathf.Max(0, _activeOnPath - 1);
        Debug.Log($"[LaneManager] Pig expired. Active={_activeOnPath}");

        if (!_isFrenzy) CheckFrenzy();

        if (!HasAnyItemsLeft())
            _events.onQueueEmpty.Raise();
    }

    private void OnLockUnlocked(IStackable lock_)
    {
        RemoveFromLane(lock_);
        Debug.Log($"[LaneManager] Lock removed from lane {lock_.LaneIndex}.");
        if (!_isFrenzy) CheckFrenzy();
    }

    // ------------------------------------------------------------------ lane helpers
    private Vector3 LaneSlotPosition(int laneIndex, int slotIndex)
        => _laneOrigins[laneIndex].position + Vector3.down * slotIndex * _pigSpacing;

    private void SpawnAllInLane(int laneIndex)
    {
        var items    = _laneSlots[laneIndex];
        var incoming = _incomingPerLane[laneIndex];

        while (incoming.Count > 0)
        {
            var entry = incoming.Dequeue();
            var item  = SpawnLaneItem(entry, laneIndex, LaneSlotPosition(laneIndex, items.Count));
            if (item != null) items.Add(item);
        }

        if (items.Count > 0)
            items[0].OnMovedToFront();
    }

    private void SlideLaneUp(int laneIndex)
    {
        var items = _laneSlots[laneIndex];
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] == null) continue;
            items[i].transform.DOKill();
            items[i].transform.DOMove(LaneSlotPosition(laneIndex, i), _anim.laneSlideUpDuration)
                .SetEase(Ease.OutCubic);
        }

        if (items.Count > 0)
            items[0].OnMovedToFront();
    }

    // ------------------------------------------------------------------ spawn
    private IStackable SpawnLaneItem(LevelData.PigEntry entry, int laneIndex, Vector3 position)
    {
        return entry.itemType == LevelData.LaneItemType.Lock
            ? (IStackable)SpawnLock(laneIndex, position)
            : (IStackable)SpawnPig(entry, laneIndex, position);
    }

    private PigController SpawnPig(LevelData.PigEntry entry, int laneIndex, Vector3 position)
    {
        if (_pigPrefab == null) { Debug.LogError("[LaneManager] _pigPrefab not assigned!"); return null; }
        if (!_conveyorPath.IsValid) { Debug.LogError("[LaneManager] ConveyorPath invalid!"); return null; }

        var go  = Instantiate(_pigPrefab, position, Quaternion.identity);
        var pig = go.GetComponent<PigController>();
        if (pig == null) { Debug.LogError("[LaneManager] Pig prefab missing PigController!"); Destroy(go); return null; }

        pig.Initialize(entry.colorIndex, entry.ammo, laneIndex, entry.isHidden,
                       _config, _anim, _events, _board, _conveyorPath, this);
        return pig;
    }

    private LockController SpawnLock(int laneIndex, Vector3 position)
    {
        if (_lockPrefab == null) { Debug.LogError("[LaneManager] _lockPrefab not assigned!"); return null; }

        var go    = Instantiate(_lockPrefab, position, Quaternion.identity);
        var lock_ = go.GetComponent<LockController>();
        if (lock_ == null) { Debug.LogError("[LaneManager] Lock prefab missing LockController!"); Destroy(go); return null; }

        lock_.Initialize(laneIndex, _keyLockManager, _anim);
        return lock_;
    }

    // ------------------------------------------------------------------ frenzy
    private void CheckFrenzy()
    {
        // Any lock still in a lane blocks frenzy
        foreach (var lane in _laneSlots)
            foreach (var item in lane)
                if (item is LockController) return;

        // Count every surviving pig
        int total = _activeOnPath;
        foreach (var lane in _laneSlots)
            foreach (var item in lane)
                if (item is PigController) total++;
        foreach (var p in _waitingPigs)
            if (p != null) total++;

        if (total <= 5)
            EnterFrenzy();
    }

    private void EnterFrenzy()
    {
        if (_isFrenzy) return;
        _isFrenzy = true;
        Debug.Log("[LaneManager] *** FRENZY MODE ***");

        // Apply 2x speed to all pigs that exist right now
        foreach (var lane in _laneSlots)
            foreach (var item in lane)
                if (item is PigController pig) pig.SpeedMultiplier = 2f;

        foreach (var p in _waitingPigs)
            if (p is PigController pig) pig.SpeedMultiplier = 2f;

        // Pigs currently on the belt get the multiplier too — they are not in
        // _laneSlots or _waitingPigs, but SpeedMultiplier is read every frame
        // in WalkPerimeter so finding them requires a scene search.
        foreach (var pig in FindObjectsByType<PigController>(FindObjectsSortMode.None))
            pig.SpeedMultiplier = 2f;
    }

    // ------------------------------------------------------------------ helpers
    private void ClearAll()
    {
        if (_laneSlots != null)
            foreach (var lane in _laneSlots)
                foreach (var item in lane)
                    if (item != null) Destroy(item.transform.gameObject);

        if (_waitingPigs != null)
            foreach (var item in _waitingPigs)
                if (item != null) Destroy(item.transform.gameObject);

        if (_incomingPerLane != null)
            foreach (var q in _incomingPerLane) q?.Clear();

        if (_laneSlots != null)
            foreach (var l in _laneSlots) l?.Clear();

        if (_waitingPigs != null)
            System.Array.Clear(_waitingPigs, 0, _waitingPigs.Length);

        _activeOnPath = 0;
        _isFrenzy     = false;
    }

    private bool HasAnyItemsLeft()
    {
        if (_activeOnPath > 0) return true;
        foreach (var lane in _laneSlots)
            if (lane.Count > 0) return true;
        foreach (var p in _waitingPigs)
            if (p != null) return true;
        foreach (var q in _incomingPerLane)
            if (q.Count > 0) return true;
        return false;
    }

    private void ValidateInspector()
    {
        if (_laneOrigins == null || _laneOrigins.Length == 0)
            Debug.LogError("[LaneManager] _laneOrigins is empty!");
        if (_waitingSlots == null || _waitingSlots.Length == 0)
            Debug.LogError("[LaneManager] _waitingSlots is empty!");
        if (_pigPrefab == null)
            Debug.LogError("[LaneManager] _pigPrefab not assigned!");
        if (_conveyorPath == null)
            Debug.LogError("[LaneManager] _conveyorPath not assigned!");
        if (_board == null)
            Debug.LogError("[LaneManager] BoardManager not assigned!");
    }
}
