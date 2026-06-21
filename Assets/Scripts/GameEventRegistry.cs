using UnityEngine;

[CreateAssetMenu(fileName = "GameEventRegistry", menuName = "PixelFlow/GameEventRegistry")]
public class GameEventRegistry : ScriptableObject
{
    [Header("Level Lifecycle")]
    public GameEvent onLevelStart;
    public GameEvent onLevelComplete;
    public GameEvent onLevelFailed;

    [Header("Pig")]
    public PigEvent  onPigFinishedPass;   // pig done with lap, still has ammo
    public PigEvent  onPigExpired;        // pig out of ammo, leaves stage
    public PigEvent  onPigRevealed;       // hidden pig identity revealed
    public GameEvent onQueueEmpty;        // all pigs gone

    [Header("Board")]
    public CubeEvent onCubeHit;
    public CubeEvent onCubeCleared;
    public GameEvent onBoardCleared;

    [Header("Key-Lock")]
    public LockEvent onLockUnlocked;      // a lock was paired with a key and is being removed

    [Header("Input")]
    public GameEvent onTap;               // kept for UI buttons if needed; pigs use OnMouseDown

    /// <summary>
    /// Clears all listeners from every event in this registry.
    /// Must be called once at scene/game start (before any MonoBehaviour subscribes)
    /// to prevent stale delegates from a previous Play session leaking through the
    /// ScriptableObject, which persists across domain reloads in the editor.
    /// </summary>
    public void ResetAll()
    {
        onLevelStart.ClearAll();
        onLevelComplete.ClearAll();
        onLevelFailed.ClearAll();
        onPigFinishedPass.ClearAll();
        onPigExpired.ClearAll();
        onPigRevealed.ClearAll();
        onQueueEmpty.ClearAll();
        onCubeHit.ClearAll();
        onCubeCleared.ClearAll();
        onBoardCleared.ClearAll();
        onLockUnlocked.ClearAll();
        onTap.ClearAll();
    }
}
