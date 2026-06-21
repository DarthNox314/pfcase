using System;
using UnityEngine;

/// <summary>
/// ScriptableObject event that carries an IHittable reference (e.g. cube hit, cube cleared).
/// Create via Assets > Create > PixelFlow > Events > CubeEvent.
/// </summary>
[CreateAssetMenu(fileName = "CubeEvent", menuName = "PixelFlow/Events/CubeEvent")]
public class CubeEvent : ScriptableObject
{
    private event Action<IHittable> _listeners;

    public void Raise(IHittable cube)              => _listeners?.Invoke(cube);
    public void Subscribe(Action<IHittable> cb)    => _listeners += cb;
    public void Unsubscribe(Action<IHittable> cb)  => _listeners -= cb;
    public void ClearAll()                         => _listeners = null;
}
