using UnityEngine;

/// <summary>
/// Detects a tap / left-click and raises onTap on the event bus.
/// Uses the legacy Input system (Project Settings > Player > Active Input Handling = Input Manager).
/// </summary>
public class InputHandler : MonoBehaviour
{
    [SerializeField] private GameEventRegistry _events;

    private bool _valid;

    private void Start()
    {
        if (_events == null)
        {
            Debug.LogError("[InputHandler] GameEventRegistry not assigned!");
            return;
        }
        if (_events.onTap == null)
        {
            Debug.LogError("[InputHandler] onTap event asset is null in the registry!");
            return;
        }
        _valid = true;
    }

    private void Update()
    {
        if (!_valid || !DetectTap()) return;

        Debug.Log("[InputHandler] Tap detected — raising onTap.");
        _events.onTap.Raise();
    }

    private static bool DetectTap()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        return Input.GetMouseButtonDown(0);
#else
        return Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began;
#endif
    }
}
