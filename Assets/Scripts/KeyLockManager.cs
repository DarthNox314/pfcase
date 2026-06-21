using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Coordinates key-lock pairing.
///
/// A key becomes "unlocked" when ANY adjacent pixel cube is destroyed.
/// A lock blocks its lane until it reaches lane-front (slot 0).
///
/// Whichever event happens second (lock reaching front OR key becoming ready)
/// triggers the pair: the key activates (destroys itself) and the lock unlocks
/// (raises onLockUnlocked so LaneManager slides the lane, then destroys itself).
/// </summary>
public class KeyLockManager : MonoBehaviour
{
    [SerializeField] private GameEventRegistry _events;
    [SerializeField] private AnimationConfig   _anim;

    private readonly List<LockController> _waitingLocks = new List<LockController>();
    private readonly List<KeyController>  _unlockedKeys = new List<KeyController>();

    // ------------------------------------------------------------------ called by LockController
    public void OnLockAtFront(LockController lock_)
    {
        if (_unlockedKeys.Count > 0)
        {
            var key = _unlockedKeys[0];
            _unlockedKeys.RemoveAt(0);
            Pair(key, lock_);
        }
        else
        {
            if (!_waitingLocks.Contains(lock_))
                _waitingLocks.Add(lock_);
        }
    }

    // ------------------------------------------------------------------ called by KeyController
    public void OnKeyUnlocked(KeyController key)
    {
        if (_waitingLocks.Count > 0)
        {
            // Always unlock the lock in the lowest lane index (leftmost lane first).
            _waitingLocks.Sort((a, b) => a.LaneIndex.CompareTo(b.LaneIndex));
            var lock_ = _waitingLocks[0];
            _waitingLocks.RemoveAt(0);
            Pair(key, lock_);
        }
        else
        {
            if (!_unlockedKeys.Contains(key))
                _unlockedKeys.Add(key);
        }
    }

    // ------------------------------------------------------------------ called when a lock is destroyed externally
    public void OnLockRemoved(LockController lock_)
    {
        _waitingLocks.Remove(lock_);
    }

    // ------------------------------------------------------------------ private
    private void Pair(KeyController key, LockController lock_)
    {
        // Clean up KLM list immediately; lane slide is deferred to after destroy animation.
        lock_.PrepareUnlock();
        key.PrepareActivate();

        Debug.Log("[KeyLockManager] Key-lock pair — starting fly animation.");
        StartCoroutine(PairSequence(key, lock_));
    }

    private IEnumerator PairSequence(KeyController key, LockController lock_)
    {
        float delay      = _anim.keyFlyDelay;
        float travelTime = _anim.keyFlyDuration;

        yield return new WaitForSeconds(delay);

        if (key == null || lock_ == null) yield break;

        Vector3 lockPos = lock_.transform.position;
        key.transform.DOKill();
        key.transform.DOMove(lockPos, travelTime).SetEase(Ease.InCubic);

        yield return new WaitForSeconds(travelTime);

        // Lane slides only after lock's destroy animation completes (inside PlayDestroyAnimation).
        if (key   != null) key.PlayDestroyAnimation();
        if (lock_  != null) lock_.PlayDestroyAnimation(_events);
    }
}
