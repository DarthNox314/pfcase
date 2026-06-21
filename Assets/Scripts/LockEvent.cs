using System;
using UnityEngine;

[CreateAssetMenu(fileName = "LockEvent", menuName = "PixelFlow/Events/LockEvent")]
public class LockEvent : ScriptableObject
{
    private event Action<IStackable> _listeners;

    public void Raise(IStackable l)               => _listeners?.Invoke(l);
    public void Subscribe(Action<IStackable> cb)   => _listeners += cb;
    public void Unsubscribe(Action<IStackable> cb) => _listeners -= cb;
    public void ClearAll()                         => _listeners = null;
}
