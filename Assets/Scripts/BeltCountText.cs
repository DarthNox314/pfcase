using TMPro;
using UnityEngine;

/// <summary>
/// Displays active pigs on the conveyor belt as "current/capacity" (e.g. 2/3).
/// Attach to a TextMeshPro or TextMeshProUGUI GameObject.
/// </summary>
public class BeltCountText : MonoBehaviour
{
    [SerializeField] private LaneManager _laneManager;
    [SerializeField] private TMP_Text    _text;

    private int _lastActive   = -1;
    private int _lastCapacity = -1;

    private void Awake()
    {
        if (_text == null) _text = GetComponent<TMP_Text>();
    }

    private void Update()
    {
        if (_laneManager == null || _text == null) return;

        int active   = _laneManager.ActiveOnBelt;
        int capacity = _laneManager.BeltCapacity;

        if (active == _lastActive && capacity == _lastCapacity) return;

        _lastActive   = active;
        _lastCapacity = capacity;
        _text.text    = $"{active}/{capacity}";
    }
}
