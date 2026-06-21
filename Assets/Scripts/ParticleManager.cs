using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays pooled particle effects in response to game events.
/// Assign prefabs in the Inspector; pool size controls max simultaneous instances per effect.
/// </summary>
public class ParticleManager : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private GameEventRegistry _events;

    [Header("Effect Prefabs")]
    [SerializeField] private ParticleSystem _pigRevealFx;
    [SerializeField] private ParticleSystem _lockUnlockFx;
    [SerializeField] private ParticleSystem _pigExpiredFx;

    [Header("Pool Sizes")]
    [SerializeField] [Range(1, 8)] private int _poolSize = 4;

    // ------------------------------------------------------------------ pools
    private Queue<ParticleSystem> _revealPool;
    private Queue<ParticleSystem> _lockPool;
    private Queue<ParticleSystem> _expiredPool;

    // ------------------------------------------------------------------ lifecycle
    private void Awake()
    {
        _revealPool  = BuildPool(_pigRevealFx);
        _lockPool    = BuildPool(_lockUnlockFx);
        _expiredPool = BuildPool(_pigExpiredFx);
    }

    private void OnEnable()
    {
        _events.onPigRevealed.Subscribe(OnPigRevealed);
        _events.onLockUnlocked.Subscribe(OnLockUnlocked);
        _events.onPigExpired.Subscribe(OnPigExpired);
    }

    private void OnDisable()
    {
        _events.onPigRevealed.Unsubscribe(OnPigRevealed);
        _events.onLockUnlocked.Unsubscribe(OnLockUnlocked);
        _events.onPigExpired.Unsubscribe(OnPigExpired);
    }

    // ------------------------------------------------------------------ handlers
    private void OnPigRevealed(PigController pig)
        => Spawn(_revealPool, pig.transform.position);

    private void OnLockUnlocked(IStackable lock_)
        => Spawn(_lockPool, lock_.transform.position);

    private void OnPigExpired(PigController pig)
        => Spawn(_expiredPool, pig.transform.position);

    // ------------------------------------------------------------------ pool
    private Queue<ParticleSystem> BuildPool(ParticleSystem prefab)
    {
        var pool = new Queue<ParticleSystem>();
        if (prefab == null) return pool;

        for (int i = 0; i < _poolSize; i++)
        {
            var ps = Instantiate(prefab, transform);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.gameObject.SetActive(false);
            pool.Enqueue(ps);
        }

        return pool;
    }

    private void Spawn(Queue<ParticleSystem> pool, Vector3 position)
    {
        if (pool == null || pool.Count == 0) return;

        // Find a free instance (not currently playing).
        int checked_ = 0;
        ParticleSystem ps;
        do
        {
            ps = pool.Dequeue();
            pool.Enqueue(ps);
            checked_++;
        }
        while (ps.isPlaying && checked_ < pool.Count);

        if (ps.isPlaying) return; // all busy — skip

        ps.transform.position = position;
        ps.gameObject.SetActive(true);
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.Play();

        StartCoroutine(ReturnWhenDone(ps, pool));
    }

    private IEnumerator ReturnWhenDone(ParticleSystem ps, Queue<ParticleSystem> pool)
    {
        yield return new WaitUntil(() => !ps.IsAlive(true));
        ps.gameObject.SetActive(false);
    }
}
