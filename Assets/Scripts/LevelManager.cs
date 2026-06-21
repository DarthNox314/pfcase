using UnityEngine;

/// <summary>
/// Orchestrates level start. Passes BoardCenter from BoardManager to LaneManager
/// after the board is built, so pigs know which way to face.
/// </summary>
public class LevelManager : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private GameEventRegistry _events;
    [SerializeField] private BoardManager      _board;
    [SerializeField] private LaneManager        _pigManager;

    [Header("Level Data")]
    [SerializeField] private LevelData _currentLevel;

    private void Awake()
    {
        if (_events == null) { Debug.LogError("[LevelManager] GameEventRegistry not assigned!"); return; }
        _events.onBoardCleared.Subscribe(OnBoardCleared);
    }

    private void OnDestroy()
    {
        if (_events == null) return;
        _events.onBoardCleared.Unsubscribe(OnBoardCleared);
    }

    private void Start()
    {
        if (_currentLevel == null) { Debug.LogError("[LevelManager] No LevelData assigned!"); return; }
        StartLevel(_currentLevel);
    }

    public void StartLevel(LevelData level)
    {
        // Build board first — this calculates BoardCenter
        _board.BuildBoard(level);

        // Then load pigs — LaneManager reads BoardCenter via the _boardCenter Transform
        _pigManager.LoadLevel(level);

        _events.onLevelStart.Raise();
        Debug.Log($"[LevelManager] Level '{level.name}' started. BoardCenter={_board.BoardCenter}");
    }

    private void OnBoardCleared() => _events.onLevelComplete.Raise();
}
