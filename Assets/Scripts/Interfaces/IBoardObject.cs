using UnityEngine;

/// <summary>
/// Anything that physically occupies one or more cells on the board.
/// Provides world position and cleared state.
///
/// Does NOT imply the object can be hit by projectiles.
/// Use IHittable for targetable board objects.
/// </summary>
public interface IBoardObject
{
    bool    IsCleared { get; }
    Vector3 Position  { get; }
}
