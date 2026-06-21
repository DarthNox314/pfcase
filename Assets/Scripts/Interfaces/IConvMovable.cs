using UnityEngine;

/// <summary>
/// Anything that can travel the conveyor belt.
/// Provides lifecycle hooks so future conveyor-aware systems (obstacles, special tiles)
/// can respond to segment transitions without coupling to PigController.
/// </summary>
public interface IConvMovable
{
    bool IsOnConveyor { get; }

    /// <summary>Send this item onto the conveyor belt.</summary>
    void Dispatch();

    /// <summary>Called the moment this item enters a new conveyor segment.</summary>
    void OnConveyorSegmentEntered(int segmentIndex, Vector2 inwardDirection);

    /// <summary>Animate the item back to a waiting/lane slot position after completing a lap.</summary>
    void ReturnToSlot(Vector3 slotPosition);
}
