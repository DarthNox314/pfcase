using System;
using UnityEngine;

/// <summary>
/// ScriptableObject event that carries a single int payload (e.g. score, color index).
/// Create via Assets > Create > PixelFlow > Events > IntEvent.
/// </summary>
[CreateAssetMenu(fileName = "IntEvent", menuName = "PixelFlow/Events/IntEvent")]
public class IntEvent : ScriptableObject
{
    private event Action<int> _listeners;

    public void Raise(int value)            => _listeners?.Invoke(value);
    public void Subscribe(Action<int> cb)   => _listeners += cb;
    public void Unsubscribe(Action<int> cb) => _listeners -= cb;
}
