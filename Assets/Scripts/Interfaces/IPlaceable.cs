/// <summary>
/// Deprecated — split into IBoardObject (board presence) and IHittable (projectile target).
/// Kept as a compile-time shim so any existing references still build.
/// Prefer IHittable for new code.
/// </summary>
public interface IPlaceable : IHittable { }
