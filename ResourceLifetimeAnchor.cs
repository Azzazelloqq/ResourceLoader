using UnityEngine;

namespace ResourceLoader
{
/// <summary>Unity adapter for a ResourceLifetime. Use ResourceLifetime.For(owner) to obtain it.</summary>
[DisallowMultipleComponent]
[ExecuteAlways]
public sealed class ResourceLifetimeAnchor : MonoBehaviour
{
    private readonly ResourceLifetime _lifetime = new ResourceLifetime();
    public ResourceLifetime Lifetime => _lifetime;
    private void OnDestroy() => _lifetime.Dispose();
}
}
