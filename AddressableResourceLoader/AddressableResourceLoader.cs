using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Component = UnityEngine.Component;
using Object = UnityEngine.Object;

namespace ResourceLoader.AddressableResourceLoader
{
/// <summary>
/// A resource loader that uses Unity's Addressable Assets system to load and manage resources.
/// 
/// Features:
/// - If TResource is a Component, loads the prefab as a GameObject, then calls GetComponent<TResource>().
/// - If TResource is not a Component, loads the asset directly as TResource.
/// - Provides synchronous and asynchronous methods for loading, plus a method for load & instantiate.
/// - Uses refactored private methods to reduce duplication of logic.
/// </summary>
public class AddressableResourceLoader : IResourceLoader, IDisposable
{
	private readonly List<AsyncOperationHandle> _loadedResources;
	private readonly object _lock = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="AddressableResourceLoader"/> class.
	/// </summary>
	/// <param name="cacheCount">Initial capacity of the list tracking loaded resources.</param>
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

	#region Public Load Methods

	/// <summary>
	/// Preloads a resource into the cache asynchronously.
	/// If TResource is a Component, it loads the prefab (GameObject), obtains the component, 
	/// and caches the prefab handle.
	/// </summary>
	public async Task PreloadInCacheAsync<TResource>(string resourceId, CancellationToken token)
	{
		// Just load it asynchronously and discard the result.
		await LoadResourceAsync<TResource>(resourceId, token);
	}

	/// <summary>
	/// Loads a resource synchronously (blocking).
	/// - If TResource is a Component, it loads a GameObject then returns TComponent from GetComponent.
	/// - Otherwise, it loads TResource directly.
	/// </summary>
	public TResource LoadResource<TResource>(string resourceId)
	{
		if (typeof(Component).IsAssignableFrom(typeof(TResource)))
		{
			return LoadComponentSync<TResource>(resourceId);
		}
		else
		{
			return LoadAssetSync<TResource>(resourceId);
		}
	}

	/// <summary>
	/// Loads a resource asynchronously.
	/// - If TResource is a Component, it loads a GameObject then returns TComponent from GetComponent.
	/// - Otherwise, it loads TResource directly.
	/// </summary>
	public async Task<TResource> LoadResourceAsync<TResource>(string resourceId, CancellationToken token = default)
	{
		if (typeof(Component).IsAssignableFrom(typeof(TResource)))
		{
			return await LoadComponentAsync<TResource>(resourceId, token);
		}
		else
		{
			return await LoadAssetAsync<TResource>(resourceId, token);
		}
	}

	/// <summary>
	/// Loads a resource asynchronously and invokes a callback upon completion.
	/// - If TResource is a Component, loads a GameObject and returns TComponent via callback.
	/// - Otherwise, loads TResource directly.
	/// </summary>
	public void LoadResource<TResource>(
		string resourceId,
		Action<TResource> onResourceLoaded,
		CancellationToken token = default)
	{
		if (typeof(Component).IsAssignableFrom(typeof(TResource)))
		{
			LoadComponentAsyncWithCallback(resourceId, onResourceLoaded, token);
		}
		else
		{
			LoadAssetAsyncWithCallback(resourceId, onResourceLoaded, token);
		}
	}

	/// <summary>
	/// Asynchronously loads a prefab from Addressables, instantiates it under the specified parent, 
	/// and retrieves the requested component from the instantiated object.
	/// </summary>
	/// <typeparam name="TComponent">The type of the component to retrieve from the instantiated prefab.</typeparam>
	/// <typeparam name="TParent">The type of the parent to associate with the instantiated prefab. Must be either <see cref="Transform"/> or a type containing a <see cref="Transform"/> component.</typeparam>
	/// <param name="resourceId">The identifier of the prefab to load from Addressables.</param>
	/// <param name="parent">
	/// The parent object under which the prefab will be instantiated. 
	/// Can be a <see cref="Transform"/> or a <see cref="Component"/> that contains a Transform.
	/// </param>
	/// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
	/// <returns>
	/// The requested component of type <typeparamref name="TComponent"/> from the instantiated prefab.
	/// If the prefab does not contain the component, returns <c>default</c> and logs an error.
	/// </returns>
	/// <exception cref="ArgumentException">
	/// Thrown if the provided <paramref name="parent"/> is not a valid type (neither <see cref="Transform"/> nor a <see cref="Component"/>).
	/// </exception>
	/// <remarks>
	/// This method ensures that the instantiated prefab is placed under the specified parent transform if a valid parent is provided. 
	/// If the prefab does not contain the requested component, an error is logged, and <c>default</c> is returned.
	/// </remarks>
	public async Task<TComponent> LoadAndCreateAsync<TComponent, TParent>(
		string resourceId,
		TParent parent,
		CancellationToken token = default)
	{
		Transform parentTransform = null;
		if (parent is Transform transform)
		{
			parentTransform = transform;
		}
		else if (parent is Component parentComponent)
		{
			parentTransform = parentComponent.transform;
		}
		else
		{
			Debug.LogError("Parent object is not have Transform component");
		}
		
		var assetObject = await LoadAssetAsync<GameObject>(resourceId, token);
		
		var instantiateObject = Object.Instantiate(assetObject, parentTransform);

		if (instantiateObject.TryGetComponent(out TComponent component))
		{
			return component;
		}
		
		Debug.LogError($"Prefab {resourceId} does not have component {typeof(TComponent).Name}");
		return default;
	}

	#endregion

	#region Private Helper Methods (Sync)

	/// <summary>
	/// Synchronously loads a non-Component resource (e.g. ScriptableObject, AudioClip, Material).
	/// </summary>
	private TResource LoadAssetSync<TResource>(string resourceId)
	{
		var handle = Addressables.LoadAssetAsync<TResource>(resourceId);
		handle.WaitForCompletion();

		if (handle.Status == AsyncOperationStatus.Succeeded)
		{
			var loadedResource = handle.Result;
			lock (_lock)
			{
				_loadedResources.Add(handle);
			}

			return loadedResource;
		}
		else
		{
			Addressables.Release(handle);
			throw new Exception($"Failed to load resource synchronously: {resourceId}");
		}
	}

	/// <summary>
	/// Synchronously loads a prefab as a GameObject and returns the requested component from it.
	/// </summary>
	private TResource LoadComponentSync<TResource>(string resourceId)
	{
		var prefabHandle = Addressables.LoadAssetAsync<GameObject>(resourceId);
		prefabHandle.WaitForCompletion();

		if (prefabHandle.Status == AsyncOperationStatus.Succeeded)
		{
			var prefab = prefabHandle.Result;
			lock (_lock)
			{
				_loadedResources.Add(prefabHandle);
			}

			var component = prefab != null ? prefab.GetComponent<TResource>() : default;
			return component;
		}
		else
		{
			Addressables.Release(prefabHandle);
			throw new Exception($"Failed to load prefab synchronously for component: {resourceId}");
		}
	}

	#endregion

	#region Private Helper Methods (Async)

	/// <summary>
	/// Asynchronously loads a non-Component resource (e.g. ScriptableObject, AudioClip).
	/// </summary>
	private async Task<TResource> LoadAssetAsync<TResource>(string resourceId, CancellationToken token)
	{
		var handle = Addressables.LoadAssetAsync<TResource>(resourceId);
		try
		{
			await handle.Task;

			if (token.IsCancellationRequested)
			{
				Addressables.Release(handle);
				return default;
			}

			if (handle.Status == AsyncOperationStatus.Succeeded)
			{
				var loadedResource = handle.Result;
				lock (_lock)
				{
					_loadedResources.Add(handle);
				}

				return loadedResource;
			}

			Addressables.Release(handle);
			throw new Exception($"Failed to load resource asynchronously: {resourceId}");
		}
		catch
		{
			Addressables.Release(handle);
			throw;
		}
	}

	/// <summary>
	/// Asynchronously loads a prefab as a GameObject, then returns the requested component.
	/// </summary>
	private async Task<TResource> LoadComponentAsync<TResource>(string resourceId, CancellationToken token)
	{
		var prefabHandle = Addressables.LoadAssetAsync<GameObject>(resourceId);
		try
		{
			await prefabHandle.Task;

			if (token.IsCancellationRequested)
			{
				Addressables.Release(prefabHandle);
				return default;
			}

			if (prefabHandle.Status == AsyncOperationStatus.Succeeded)
			{
				var prefab = prefabHandle.Result;
				lock (_lock)
				{
					_loadedResources.Add(prefabHandle);
				}

				var component = prefab != null ? prefab.GetComponent<TResource>() : default;
				return component;
			}

			Addressables.Release(prefabHandle);
			throw new Exception($"Failed to load prefab asynchronously for component: {resourceId}");
		}
		catch
		{
			Addressables.Release(prefabHandle);
			throw;
		}
	}

	/// <summary>
	/// Asynchronous load of a non-Component resource with a callback.
	/// </summary>
	private void LoadAssetAsyncWithCallback<TResource>(
		string resourceId,
		Action<TResource> onResourceLoaded,
		CancellationToken token)
	{
		var handle = Addressables.LoadAssetAsync<TResource>(resourceId);
		Action<AsyncOperationHandle<TResource>> onComplete = null;

		onComplete = completedHandle =>
		{
			handle.Completed -= onComplete;

			if (token.IsCancellationRequested)
			{
				Addressables.Release(handle);
				return;
			}

			if (completedHandle.Status == AsyncOperationStatus.Succeeded)
			{
				var loadedResource = completedHandle.Result;
				onResourceLoaded?.Invoke(loadedResource);

				lock (_lock)
				{
					_loadedResources.Add(handle);
				}
			}
			else
			{
				Addressables.Release(handle);
				throw new Exception($"Failed to load resource asynchronously: {resourceId}");
			}
		};

		handle.Completed += onComplete;
	}

	/// <summary>
	/// Asynchronous load of a prefab + component with a callback.
	/// </summary>
	private void LoadComponentAsyncWithCallback<TResource>(
		string resourceId,
		Action<TResource> onResourceLoaded,
		CancellationToken token)
	{
		var prefabHandle = Addressables.LoadAssetAsync<GameObject>(resourceId);
		Action<AsyncOperationHandle<GameObject>> onComplete = null;

		onComplete = completedHandle =>
		{
			prefabHandle.Completed -= onComplete;

			if (token.IsCancellationRequested)
			{
				Addressables.Release(prefabHandle);
				return;
			}

			if (completedHandle.Status == AsyncOperationStatus.Succeeded)
			{
				var prefab = completedHandle.Result;
				var component = prefab != null ? prefab.GetComponent<TResource>() : default;

				onResourceLoaded?.Invoke(component);

				lock (_lock)
				{
					_loadedResources.Add(prefabHandle);
				}
			}
			else
			{
				Addressables.Release(prefabHandle);
				throw new Exception($"Failed to load prefab asynchronously (component): {resourceId}");
			}
		};

		prefabHandle.Completed += onComplete;
	}

	#endregion

	#region Release Methods

	/// <summary>
	/// Releases a loaded resource or component.
	/// If TResource is a Component, this tries to match the underlying prefab handle 
	/// by checking prefab.GetComponent<TResource>() for equality.
	/// </summary>
	public void ReleaseResource<TResource>(TResource resource)
	{
		Addressables.Release(resource);

		lock (_lock)
		{
			_loadedResources.RemoveAll(handle =>
			{
				if (handle.Result == null)
				{
					return false;
				}

				// If the loaded object *is* the resource, remove it
				if (handle.Result.Equals(resource))
				{
					return true;
				}

				// If resource is a Component, handle.Result might be a GameObject
				if (resource is Component cmp && handle.Result is GameObject go)
				{
					var c = go.GetComponent<TResource>();
					if (c != null && c.Equals(cmp))
					{
						return true;
					}
				}

				return false;
			});
		}
	}

	/// <summary>
	/// Releases all loaded resources tracked by this loader.
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

	#endregion
}
}