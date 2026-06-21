using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// A 2x2 board object. Becomes Unlocked when ANY adjacent pixel cube is
/// destroyed. Once unlocked it pairs with the first LockController that
/// reaches lane-front via KeyLockManager; if no lock is waiting it stays
/// on the board until one arrives.
///
/// NOT IHittable — projectiles pass through it completely.
/// </summary>
public class KeyController : MonoBehaviour, IBoardObject
{
    // ------------------------------------------------------------------ IBoardObject
    public bool    IsCleared { get; private set; }
    public Vector3 Position  => transform.position;

    // ------------------------------------------------------------------ state
    public bool IsUnlocked { get; private set; }

    private GameEventRegistry _events;
    private KeyLockManager    _keyLockManager;
    private AnimationConfig   _anim;

    private readonly HashSet<IHittable> _adjacentCubes = new HashSet<IHittable>();

    // ------------------------------------------------------------------ init
    /// <param name="col">Bottom-left column of the 2x2 footprint.</param>
    /// <param name="row">Bottom-left row of the 2x2 footprint.</param>
    public void Initialize(int col, int row,
                           BoardManager board,
                           GameEventRegistry events,
                           KeyLockManager keyLockManager,
                           AnimationConfig anim)
    {
        _events         = events;
        _keyLockManager = keyLockManager;
        _anim           = anim;

        // Only the 8 edge-adjacent cells (no diagonals / corner cells).
        // Top and bottom rows:
        for (int dc = 0; dc < 2; dc++)
        {
            TryAdd(board, new Vector2Int(col + dc, row - 1));   // bottom edge
            TryAdd(board, new Vector2Int(col + dc, row + 2));   // top edge
        }
        // Left and right columns:
        for (int dr = 0; dr < 2; dr++)
        {
            TryAdd(board, new Vector2Int(col - 1, row + dr));   // left edge
            TryAdd(board, new Vector2Int(col + 2, row + dr));   // right edge
        }

        _events.onCubeCleared.Subscribe(OnCubeCleared);

        // If spawned with no adjacent cubes it's immediately unlocked.
        if (_adjacentCubes.Count == 0)
            BecomeUnlocked();
    }

    private void TryAdd(BoardManager board, Vector2Int cell)
    {
        var cube = board.GetCubeAt(cell);
        if (cube != null) _adjacentCubes.Add(cube);
    }

    private void OnDisable() => _events?.onCubeCleared.Unsubscribe(OnCubeCleared);

    // ------------------------------------------------------------------ cube tracking
    private void OnCubeCleared(IHittable cube)
    {
        if (!_adjacentCubes.Remove(cube)) return;  // not our neighbour
        if (!IsUnlocked)
            BecomeUnlocked();
    }

    private void BecomeUnlocked()
    {
        if (IsUnlocked) return;
        IsUnlocked = true;
        Debug.Log("[KeyController] Key unlocked — notifying KeyLockManager.");

        // Visual cue: gentle pulse to signal the key is ready
        transform.DOKill();
        transform.DOPunchScale(Vector3.one * _anim.keyUnlockPunchStrength, _anim.keyUnlockPunchDuration, 5, 0.5f);

        _keyLockManager.OnKeyUnlocked(this);
    }

    // ------------------------------------------------------------------ activation
    /// <summary>
    /// Called by KeyLockManager — marks the key consumed and stops cube tracking.
    /// The actual fly + destroy animation is driven by KeyLockManager.
    /// </summary>
    public void PrepareActivate()
    {
        IsCleared = true;
        _events.onCubeCleared.Unsubscribe(OnCubeCleared);
    }

    /// <summary>
    /// Pop-and-destroy animation, called by KeyLockManager after the key has
    /// flown to the lock position.
    /// </summary>
    public void PlayDestroyAnimation()
    {
        transform.DOKill();
        DOTween.Sequence()
            .Append(transform.DOScale(transform.localScale * _anim.keyDestroyScalePeak, _anim.keyDestroyScaleUpDur).SetEase(Ease.OutBack))
            .AppendInterval(_anim.keyDestroyHoldDur)
            .Append(transform.DOScale(Vector3.zero, _anim.keyDestroyScaleDownDur).SetEase(Ease.InQuad))
            .OnComplete(() => Destroy(gameObject));
    }
}
