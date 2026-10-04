using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ResourceLoader.Examples
{
/// <summary>A plain C# owner; no GameObject or Disposable package is required.</summary>
public sealed class PlainOwnerExample : IDisposable
{
    private readonly IResourceLifetimeLoader _loader;
    private readonly ResourceLifetime _lifetime = new ResourceLifetime();

    public PlainOwnerExample(IResourceLifetimeLoader loader) => _loader = loader;

    public async Task UseTextureAsync(string textureAddress, CancellationToken cancellationToken)
    {
        var texture = await _loader.LoadResourceForLifetimeAsync<Texture2D>(
            textureAddress, _lifetime, cancellationToken);
        Debug.Log($"Plain owner uses: {texture.name}");
    }

    public async Task UseExistingLeaseAsync(string textureAddress, CancellationToken cancellationToken)
    {
        // A lease acquired separately can also be attached to this lifetime.
        var lease = _lifetime.Add(await _loader.LoadResourceLeaseAsync<Texture2D>(
            textureAddress, cancellationToken));
        Debug.Log($"Registered lease: {lease.Resource.name}");
        lease.Dispose(); // Optional early release; Dispose() would release it otherwise.
    }

    public void Dispose()
    {
        // This releases only the acquisitions made for this owner. The injected loader
        // may still be used by other owners, so its creator disposes it separately.
        _lifetime.Dispose();
    }
}
}
