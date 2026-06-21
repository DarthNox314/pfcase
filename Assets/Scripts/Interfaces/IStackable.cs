using UnityEngine;

/// <summary>
/// Anything that can occupy a slot in a pig lane and be dispatched onto the conveyor.
/// LaneManager works exclusively against this interface — adding a new lane item
/// (power-up, decoy, obstacle) only requires implementing IStackable.
/// </summary>
public interface IStackable
{
    int       LaneIndex    { get; }
    bool      IsDispatched { get; }
    Transform transform    { get; }

    /// <summary>
    /// Called by LaneManager the moment this item slides into slot 0 (front of lane).
    /// Implementations use this to reveal hidden state, play a fanfare, etc.
    /// </summary>
    void OnMovedToFront();
}
