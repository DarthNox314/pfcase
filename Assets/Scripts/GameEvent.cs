using System;
using UnityEngine;

/// <summary>
/// Zero-argument ScriptableObject event.
/// Create via Assets > Create > PixelFlow > Events > GameEvent.
/// </summary>
[CreateAssetMenu(fileName = "GameEvent", menuName = "PixelFlow/Events/GameEvent")]
public class GameEvent : ScriptableObject
{
    private event Action _listeners;

    public void Raise()                => _listeners?.Invoke();
    public void Subscribe(Action cb)   => _listeners += cb;
    public void Unsubscribe(Action cb) => _listeners -= cb;
    public void ClearAll()             => _listeners = null;
}
