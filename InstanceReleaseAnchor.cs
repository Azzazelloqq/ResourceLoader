using System;
using UnityEngine;

namespace ResourceLoader
{
/// <summary>Releases a prefab hold after its instance is actually destroyed.</summary>
[DisallowMultipleComponent]
[ExecuteAlways]
public sealed class InstanceReleaseAnchor : MonoBehaviour
{
    private Action _onDestroyed;
    public void Initialize(Action onDestroyed)
    {
        _onDestroyed = onDestroyed ?? throw new ArgumentNullException(nameof(onDestroyed));
    }
    private void OnDestroy()
    {
        var callback = _onDestroyed;
        _onDestroyed = null;
        callback?.Invoke();
    }
}
}
