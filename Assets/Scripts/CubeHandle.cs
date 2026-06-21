using UnityEngine;

/// <summary>
/// Thin IHittable handle for a single cube in BoardManager's data array.
/// Plain C# class — no MonoBehaviour, no GameObject.
/// </summary>
public class CubeHandle : IHittable
{
    public readonly int Index;
    private readonly BoardManager _board;

    public CubeHandle(int index, BoardManager board)
    {
        Index  = index;
        _board = board;
    }

    public Vector3 Position  => _board.GetCubePosition(Index);
    public int  ColorIndex   => _board.GetCubeColorIndex(Index);
    public bool IsCleared    => _board.IsCubeCleared(Index);
    public int  HitPoints    => _board.GetCubeHitPoints(Index);
    public int  PendingHits  => _board.GetCubePendingHits(Index);

    public void ReservePendingHit() => _board.ReserveCubePendingHit(Index);
    public void ReleasePendingHit() => _board.ReleaseCubePendingHit(Index);
    public void ReceiveHit()        => _board.ReceiveCubeHit(Index);
}
