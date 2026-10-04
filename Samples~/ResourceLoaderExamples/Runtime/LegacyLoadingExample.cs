using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ResourceLoader.Examples
{
/// <summary>Examples for the original IResourceLoader API. The caller owns the loader.</summary>
public static class LegacyLoadingExample
{
    public static async Task LoadAndReleaseAsync(IResourceLoader loader, string textureAddress,
        CancellationToken cancellationToken)
    {
        // Preload keeps a handle in this loader until ReleaseResource or ReleaseAllResources.
        await loader.PreloadInCacheAsync<Texture2D>(textureAddress, cancellationToken);
        loader.ReleaseAllResources();

        // This load obtains a new handle after the preload has been released.
        var texture = await loader.LoadResourceAsync<Texture2D>(textureAddress, cancellationToken);
        try { Debug.Log($"Loaded texture: {texture.name}"); }
        finally { loader.ReleaseResource(texture); }
    }

    public static void LoadSynchronously(IResourceLoader loader, string textureAddress)
    {
        // Synchronous loading blocks the calling thread; prefer async loading in gameplay.
        var texture = loader.LoadResource<Texture2D>(textureAddress);
        try { Debug.Log($"Loaded texture: {texture.name}"); }
        finally { loader.ReleaseResource(texture); }
    }

    public static void LoadWithCallback(IResourceLoader loader, string textureAddress,
        CancellationToken cancellationToken)
    {
        // Keep the loader alive until the callback completes or the token is cancelled.
        loader.LoadResource<Texture2D>(textureAddress, texture =>
        {
            try { Debug.Log($"Loaded texture: {texture.name}"); }
            finally { loader.ReleaseResource(texture); }
        }, cancellationToken);
    }

    public static async Task LoadPrefabComponentAsync(IResourceLoader loader, string prefabAddress,
        CancellationToken cancellationToken)
    {
        // This reads a component from the prefab asset; it does not instantiate the prefab.
        var component = await loader.LoadResourceAsync<Transform>(prefabAddress, cancellationToken);
        try { Debug.Log($"Prefab component: {component.name}"); }
        finally { loader.ReleaseResource(component); }
    }

    public static async Task<Transform> InstantiateLegacyAsync(IResourceLoader loader,
        string prefabAddress, Transform parent, CancellationToken cancellationToken)
    {
        // The legacy method returns only the component. The caller must destroy its GameObject.
        // Keep the loader alive until after that destruction and then call ReleaseAllResources.
        return await loader.LoadAndCreateAsync<Transform, Transform>(
            prefabAddress, parent, cancellationToken);
    }
}
}
