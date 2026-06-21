using UnityEngine;

/// <summary>
/// Runs before all other MonoBehaviours (Script Execution Order = -100).
/// Resets all ScriptableObject events so stale delegates from a previous
/// Play session or scene reload cannot leak into the new session.
///
/// SETUP: Assign _events in the Inspector. Script Execution Order is set
/// via Edit > Project Settings > Script Execution Order to -100.
/// </summary>
[DefaultExecutionOrder(-100)]
public class GameBootstrapper : MonoBehaviour
{
    [SerializeField] private GameEventRegistry _events;

    private void Awake()
    {
        if (_events == null)
        {
            Debug.LogError("[GameBootstrapper] GameEventRegistry not assigned!");
            return;
        }
        _events.ResetAll();
    }
}
