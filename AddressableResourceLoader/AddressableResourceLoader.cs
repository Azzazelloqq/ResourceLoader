using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ResourceLoader.AddressableResourceLoader
{
/// <summary>
/// A resource loader that uses Unity's Addressable Assets system to load and manage resources.
/// </summary>
public class AddressableResourceLoader : IResourceLoader
{
	private readonly List<AsyncOperationHandle> _loadedResources;
	private readonly object _lock = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="AddressableResourceLoader"/> class.
	/// </summary>
	/// <param name="cacheCount">The initial capacity of the resource cache.</param>
	public AddressableResourceLoader(int cacheCount = 50)
	{
		_loadedResources = new List<AsyncOperationHandle>(cacheCount);
	}

	/// <summary>
	/// Releases all resources used by the <see cref="AddressableResourceLoader"/>.
	/// </summary>
	public void Dispose()
	{
		ReleaseAllResources();
	}

	/// <summary>
	/// Preloads a resource into the cache asynchronously.
	/// </summary>
	/// <typeparam name="TResource">The type of the resource to preload.</typeparam>
	/// <param name="resourceId">The identifier of the resource to preload.</param>
	/// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
	public async Task PreloadInCacheAsync<TResource>(string resourceId, CancellationToken token)
	{
		await LoadResourceAsync<TResource>(resourceId, token);
	}

	/// <summary>
	/// Loads a resource synchronously.
	/// </summary>
	/// <typeparam name="TResource">The type of the resource to load.</typeparam>
	/// <param name="resourceId">The identifier of the resource to load.</param>
	/// <returns>The loaded resource.</returns>
	public TResource LoadResource<TResource>(string resourceId)
	{
		var asyncOperationHandle = Addressables.LoadAssetAsync<TResource>(resourceId);
		asyncOperationHandle.WaitForCompletion();

		if (asyncOperationHandle.Status == AsyncOperationStatus.Succeeded)
		{
			var loadedResource = asyncOperationHandle.Result;

			lock (_lock)
			{
				_loadedResources.Add(asyncOperationHandle);
			}

			return loadedResource;
		}
		else
		{
			Addressables.Release(asyncOperationHandle);
			throw new Exception($"Failed to load resource synchronously: {resourceId}");
		}
	}

	/// <summary>
	/// Loads a resource asynchronously and invokes a callback upon completion.
	/// </summary>
	/// <typeparam name="TResource">The type of the resource to load.</typeparam>
	/// <param name="resourceId">The identifier of the resource to load.</param>
	/// <param name="onResourceLoaded">The callback to invoke when the resource is loaded.</param>
	/// <param name="token">A cancellation token to observe while waiting for the resource to load.</param>
	public void LoadResource<TResource>(string resourceId, Action<TResource> onResourceLoaded,
		CancellationToken token = default)
	{
		var asyncOperationHandle = Addressables.LoadAssetAsync<TResource>(resourceId);
		Action<AsyncOperationHandle<TResource>> onComplete = null;
		onComplete = handle =>
		{
			asyncOperationHandle.Completed -= onComplete;

			if (token.IsCancellationRequested)
			{
				Addressables.Release(asyncOperationHandle);
				return;
			}

			if (handle.Status == AsyncOperationStatus.Succeeded)
			{
				var loadedResource = handle.Result;
				onResourceLoaded?.Invoke(loadedResource);

				lock (_lock)
				{
					_loadedResources.Add(asyncOperationHandle);
				}
			}
			else
			{
				Addressables.Release(asyncOperationHandle);
				throw new Exception($"Failed to load resource asynchronously: {resourceId}");
			}
		};

		asyncOperationHandle.Completed += onComplete;
	}

	/// <summary>
	/// Loads a resource asynchronously.
	/// </summary>
	/// <typeparam name="TResource">The type of the resource to load.</typeparam>
	/// <param name="resourceId">The identifier of the resource to load.</param>
	/// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
	/// <returns>A task that represents the asynchronous load operation. The task result contains the loaded resource.</returns>
	public async Task<TResource> LoadResourceAsync<TResource>(string resourceId, CancellationToken token)
	{
		var operationHandle = Addressables.LoadAssetAsync<TResource>(resourceId);

		try
		{
			await operationHandle.Task;

			if (token.IsCancellationRequested)
			{
				Addressables.Release(operationHandle);
				return default;
			}

			if (operationHandle.Status == AsyncOperationStatus.Succeeded)
			{
				var loadedResource = operationHandle.Result;

				lock (_lock)
				{
					_loadedResources.Add(operationHandle);
				}

				return loadedResource;
			}

			Addressables.Release(operationHandle);
			throw new Exception($"Failed to load resource asynchronously: {resourceId}");
		}
		catch (Exception)
		{
			Addressables.Release(operationHandle);
			throw;
		}
	}

	/// <summary>
	/// Releases a loaded resource.
	/// </summary>
	/// <typeparam name="TResource">The type of the resource to release.</typeparam>
	/// <param name="resource">The resource to release.</param>
	public void ReleaseResource<TResource>(TResource resource)
	{
		Addressables.Release(resource);

		// Remove the corresponding handle from the loaded resources
		lock (_lock)
		{
			_loadedResources.RemoveAll(handle => handle.Result.Equals(resource));
		}
	}

	/// <summary>
	/// Releases all loaded resources.
	/// </summary>
	public void ReleaseAllResources()
	{
		lock (_lock)
		{
			foreach (var loadedResource in _loadedResources)
			{
				Addressables.Release(loadedResource);
			}

			_loadedResources.Clear();
		}
	}
}
}