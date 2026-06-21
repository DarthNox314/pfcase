using System;
using UnityEngine;

/// <summary>
/// ScriptableObject event that carries a PigController reference (e.g. pig deployed, pig expired).
/// Create via Assets > Create > PixelFlow > Events > PigEvent.
/// </summary>
[CreateAssetMenu(fileName = "PigEvent", menuName = "PixelFlow/Events/PigEvent")]
public class PigEvent : ScriptableObject
{
    private event Action<PigController> _listeners;

    public void Raise(PigController pig)              => _listeners?.Invoke(pig);
    public void Subscribe(Action<PigController> cb)   => _listeners += cb;
    public void Unsubscribe(Action<PigController> cb) => _listeners -= cb;
    public void ClearAll()                            => _listeners = null;
}
