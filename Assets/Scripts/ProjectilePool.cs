using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Object pool for Projectile instances.
/// Pre-warms _poolSize projectiles on Awake.
/// If every projectile is in flight and another is needed, the pool expands
/// automatically rather than dropping the shot.
///
/// SETUP:
///   Place on any scene GameObject (e.g. GameManager or its own GO).
///   Assign the projectile prefab to _prefab in the Inspector.
/// </summary>
public class ProjectilePool : MonoBehaviour
{
    public static ProjectilePool Instance { get; private set; }

    [SerializeField] private GameObject _prefab;
    [SerializeField] private int        _poolSize = 100;

    private readonly Stack<Projectile> _available = new Stack<Projectile>();

    // ------------------------------------------------------------------ lifecycle
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Prewarm();
    }

    // ------------------------------------------------------------------ public API
    public Projectile Get()
    {
        Projectile p = _available.Count > 0
            ? _available.Pop()
            : CreateOne();          // pool exhausted — expand silently

        p.gameObject.SetActive(true);
        return p;
    }

    public void Return(Projectile p)
    {
        p.gameObject.SetActive(false);
        p.transform.SetParent(transform);
        _available.Push(p);
    }

    // ------------------------------------------------------------------ private
    private void Prewarm()
    {
        if (_prefab == null)
        {
            Debug.LogError("[ProjectilePool] _prefab not assigned in Inspector!");
            return;
        }

        for (int i = 0; i < _poolSize; i++)
            _available.Push(CreateOne());

        Debug.Log($"[ProjectilePool] Pre-warmed {_poolSize} projectiles.");
    }

    private Projectile CreateOne()
    {
        var go = Instantiate(_prefab, transform);
        go.SetActive(false);
        var p = go.GetComponent<Projectile>();
        if (p == null)
            Debug.LogError("[ProjectilePool] Prefab is missing a Projectile component!");
        return p;
    }
}
