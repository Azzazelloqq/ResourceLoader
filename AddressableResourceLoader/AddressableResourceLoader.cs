using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ResourceLoader.AddressableResourceLoader
{
public class AddressableResourceLoader : IResourceLoader
{
    private readonly List<AsyncOperationHandle> _loadedResources;

    public AddressableResourceLoader(int cashCount = 50)
    {
        _loadedResources = new List<AsyncOperationHandle>(cashCount);
    }

    public void Dispose()
    {
        _loadedResources.Clear();
    }

    public async Task PreloadInCashAsync<TResource>(string resourceId, CancellationToken token)
    {
        await LoadResourceAsync<TResource>(resourceId, token);
    }

    public TResource LoadResource<TResource>(string resourceId)
    {
        var asyncOperationHandle = Addressables.LoadAssetAsync<TResource>(resourceId);
        asyncOperationHandle.WaitForCompletion();

        var loadedResource = asyncOperationHandle.Result;
        _loadedResources.Add(asyncOperationHandle);

        return loadedResource;
    }


    public void LoadResource<TResource>(string resourceId, Action<TResource> onResourceLoaded,
        CancellationToken token = default)
    {
        var asyncOperationHandle = Addressables.LoadAssetAsync<TResource>(resourceId);
        Action<AsyncOperationHandle<TResource>> onComplete = null;
        onComplete = handle =>
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            var loadedResource = handle.Result;
            onResourceLoaded?.Invoke(loadedResource);

            asyncOperationHandle.Completed -= onComplete;

            _loadedResources.Add(asyncOperationHandle);
        };

        asyncOperationHandle.Completed += onComplete;
    }

    public async Task<TResource> LoadResourceAsync<TResource>(string resourceId, CancellationToken token)
    {
        var operationHandle = Addressables.LoadAssetAsync<TResource>(resourceId);

        while (operationHandle.Status != AsyncOperationStatus.Succeeded)
        {
            await Task.Yield();

            if (token.IsCancellationRequested)
            {
                return default;
            }
        }

        var loadedResource = operationHandle.Result;
        _loadedResources.Add(operationHandle);

        return loadedResource;
    }

    public void ReleaseResource<TResource>(TResource resource)
    {
        Addressables.Release(resource);
    }

    public void ReleaseAllResources()
    {
        lock (_loadedResources)
        {
            foreach (var loadedResource in _loadedResources)
            {
                Addressables.Release(loadedResource);
            }
        }
    }
}
}