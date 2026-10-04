using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ResourceLoader.Examples
{
/// <summary>Each lease represents one acquisition and has one explicit release point.</summary>
public static class LeaseLoadingExample
{
    public static async Task LoadAssetAsync(IResourceLoader loader, string textureAddress,
        CancellationToken cancellationToken)
    {
        using (var lease = await loader.LoadResourceLeaseAsync<Texture2D>(
                   textureAddress, cancellationToken))
        {
            Debug.Log($"Lease owns: {lease.Resource.name}");
        } // Releases this acquisition only.
    }

    public static async Task InstantiateAsync(IResourceLoader loader, string prefabAddress,
        Transform parent, CancellationToken cancellationToken)
    {
        using (var lease = await loader.LoadInstanceLeaseAsync<Transform, Transform>(
                   prefabAddress, parent, cancellationToken))
        {
            Debug.Log($"Instance: {lease.Instance.name}");
        } // Destroys the instance and disposes its lease.
    }
}
}
