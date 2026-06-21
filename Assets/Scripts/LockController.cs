using UnityEngine;
using DG.Tweening;

/// <summary>
/// A lane obstacle. Sits in a pig lane and blocks all pigs behind it from
/// being dispatched until it is unlocked by a ready KeyController.
///
/// LockController only implements IStackable — it never rides the conveyor,
/// so it does NOT implement IConvMovable. Clicking it does nothing because
/// it has no OnMouseDown and LaneManager.DispatchItem rejects non-IConvMovable items.
/// </summary>
public class LockController : MonoBehaviour, IStackable
{
    // ------------------------------------------------------------------ IStackable
    public int  LaneIndex    { get; private set; }
    public bool IsDispatched => false;  // locks never dispatch

    // ------------------------------------------------------------------ visuals
    [SerializeField] private SpriteRenderer _renderer;

    private KeyLockManager  _keyLockManager;
    private AnimationConfig _anim;

    // ------------------------------------------------------------------ init
    public void Initialize(int laneIndex, KeyLockManager keyLockManager, AnimationConfig anim)
    {
        LaneIndex       = laneIndex;
        _keyLockManager = keyLockManager;
        _anim           = anim;
    }

    // ------------------------------------------------------------------ IStackable
    public void OnMovedToFront()
    {
        _keyLockManager.OnLockAtFront(this);
        Debug.Log($"[LockController] Lock in lane {LaneIndex} reached front — waiting for key.");
    }

    // ------------------------------------------------------------------ unlock
    /// <summary>
    /// Called immediately on pairing — removes this lock from KeyLockManager's
    /// waiting list. Does NOT slide the lane yet; that happens after the animation.
    /// </summary>
    public void PrepareUnlock()
    {
        _keyLockManager.OnLockRemoved(this);
    }

    /// <summary>
    /// Pop-and-destroy animation. Raises onLockUnlocked in OnComplete so
    /// LaneManager slides the lane only after the lock has visually disappeared.
    /// </summary>
    public void PlayDestroyAnimation(GameEventRegistry events)
    {
        transform.DOKill();
        DOTween.Sequence()
            .Append(transform.DOScale(transform.localScale * _anim.lockDestroyScalePeak, _anim.lockDestroyScaleUpDur).SetEase(Ease.OutBack))
            .AppendInterval(_anim.lockDestroyHoldDur)
            .Append(transform.DOScale(Vector3.zero, _anim.lockDestroyScaleDownDur).SetEase(Ease.InQuad))
            .OnComplete(() =>
            {
                events.onLockUnlocked.Raise(this);
                Destroy(gameObject);
            });
    }

    private void OnDestroy()
    {
        _keyLockManager?.OnLockRemoved(this);
    }
}
