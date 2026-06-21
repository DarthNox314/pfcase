using UnityEngine;

/// <summary>
/// A ball fired by a pig.
/// Moves toward the target IHittable, calls ReceiveHit on arrival,
/// then returns itself to ProjectilePool instead of destroying.
/// </summary>
public class Projectile : MonoBehaviour
{
    private IHittable      _target;
    private float          _speed;
    private int            _colorIndex;
    private TrailRenderer  _trail;

    // ------------------------------------------------------------------ lifecycle
    private void Awake() => _trail = GetComponent<TrailRenderer>();

    // ------------------------------------------------------------------ init
    public void Initialize(IHittable target, float speed, int colorIndex)
    {
        _target     = target;
        _speed      = speed;
        _colorIndex = colorIndex;
        _target?.ReservePendingHit();
    }

    // ------------------------------------------------------------------ update
    private void Update()
    {
        if (_target == null || _target.IsCleared)
        {
            _target?.ReleasePendingHit();
            Recycle();
            return;
        }

        Vector3 targetPos = _target.Position;
        transform.position = Vector3.MoveTowards(transform.position, targetPos, _speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetPos) < 0.001f)
        {
            // Capture and null _target before calling ReceiveHit so a re-entrant
            // Update (or the IsCleared branch above) cannot double-release PendingHits.
            var hit = _target;
            Recycle();
            hit.ReleasePendingHit();
            hit.ReceiveHit();
        }
    }

    // ------------------------------------------------------------------ pool return
    private void Recycle()
    {
        _target     = null;
        _colorIndex = 0;
        _trail?.Clear();
        ProjectilePool.Instance.Return(this);
    }
}
