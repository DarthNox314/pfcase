using UnityEngine;

/// <summary>
/// Represents one visual slot in the pig stack or waiting row.
/// Holds a reference to the pig sitting in it and its world position.
/// </summary>
public class PigSlot : MonoBehaviour
{
    public PigController OccupiedBy { get; private set; }
    public bool          IsEmpty    => OccupiedBy == null;

    public void Place(PigController pig)
    {
        OccupiedBy = pig;
        if (pig != null)
            pig.transform.position = transform.position;
    }

    public PigController Take()
    {
        var pig = OccupiedBy;
        OccupiedBy = null;
        return pig;
    }
}
