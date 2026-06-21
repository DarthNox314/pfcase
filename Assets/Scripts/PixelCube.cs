using UnityEngine;

// Replaced by BoardManager's data-driven cube system (CubeData + CubeHandle).
// Kept as a stub so stale prefab/scene references don't break compilation.
public class PixelCube : MonoBehaviour, IHittable
{
    public Vector3 Position  => transform.position;
    public bool    IsCleared => false;
    public int     ColorIndex  => 0;
    public int     HitPoints   => 1;
    public int     PendingHits => 0;
    public void ReservePendingHit() { }
    public void ReleasePendingHit() { }
    public void ReceiveHit()        { }
}
