using UnityEngine;

/// <summary>
/// Defines the conveyor path around the board.
///
/// Waypoints must be assigned in order:
///   [0] Start  (entry point — pigs snap here on dispatch)
///   [1] Corner 1
///   [2] Corner 2
///   [3] Corner 3
///   [4] End    (exit point — pigs call FinishPass here)
///
/// Segment i goes from waypoint[i] → waypoint[i+1].  4 segments total.
///
/// Inward facing per segment:
///   Segment 0  (bottom, moving right) → faces UP    ( 90°)
///   Segment 1  (right,  moving up)    → faces LEFT  (180°)
///   Segment 2  (top,    moving left)  → faces DOWN  (270°)
///   Segment 3  (left,   moving down)  → faces RIGHT (  0°)
/// </summary>
public class ConveyorPath : MonoBehaviour
{
    [SerializeField] private Transform[] _waypoints;   // exactly 5

    public Transform[] Waypoints => _waypoints;
    public bool IsValid => _waypoints != null && _waypoints.Length == 5;

    public int SegmentCount => IsValid ? _waypoints.Length - 1 : 0;  // 4 segments

    // ------------------------------------------------------------------ segment helpers

    /// <summary>
    /// Z-rotation (degrees) that makes a pig face inward on segment i
    /// (the segment beginning at waypoint[i]).
    /// </summary>
    public float SegmentFacingAngle(int waypointIndex)
    {
        switch (waypointIndex % 4)
        {
            case 0: return  90f;   // bottom  → face up
            case 1: return 180f;   // right   → face left
            case 2: return 270f;   // top     → face down
            case 3: return   0f;   // left    → face right
            default: return 90f;
        }
    }

    /// <summary>
    /// World-space inward direction a pig fires while on segment i.
    /// </summary>
    public Vector2 InwardDirection(int waypointIndex)
    {
        switch (waypointIndex % 4)
        {
            case 0: return Vector2.up;
            case 1: return Vector2.left;
            case 2: return Vector2.down;
            case 3: return Vector2.right;
            default: return Vector2.up;
        }
    }

    // ------------------------------------------------------------------ gizmos
    private void OnDrawGizmos()
    {
        if (!IsValid) return;
        Gizmos.color = Color.cyan;
        for (int i = 0; i < _waypoints.Length - 1; i++)
        {
            if (_waypoints[i] == null || _waypoints[i + 1] == null) continue;
            Gizmos.DrawLine(_waypoints[i].position, _waypoints[i + 1].position);
            Gizmos.DrawSphere(_waypoints[i].position, 0.12f);
        }
        // Draw end waypoint
        if (_waypoints[_waypoints.Length - 1] != null)
            Gizmos.DrawSphere(_waypoints[_waypoints.Length - 1].position, 0.12f);
    }
}
