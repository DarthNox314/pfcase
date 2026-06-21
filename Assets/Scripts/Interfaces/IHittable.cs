/// <summary>
/// A board object that can be targeted and destroyed by projectiles.
/// Implemented by CubeHandle (data-driven) rather than a MonoBehaviour.
/// BoardManager and Projectile require nothing beyond this interface.
/// </summary>
public interface IHittable : IBoardObject
{
    int  ColorIndex  { get; }
    int  HitPoints   { get; }
    int  PendingHits { get; }

    void ReceiveHit();
    void ReservePendingHit();
    void ReleasePendingHit();
}
