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
/// - Lease-based <see cref="LoadResourceLeaseAsync{TResource}"/>/<see cref="LoadInstanceLeaseAsync{TComponent,TParent}"/>
///   return an <see cref="IDisposable"/> that owns exactly one handle (and, for instances, the
///   spawned object); prefer them over <see cref="ReleaseResource{TResource}"/>'s value-matching.
/// </summary>
public class AddressableResourceLoader : IResourceLoader, IDisposable
{
	private readonly List<AsyncOperationHandle> _loadedResources;
	private readonly object _lock = new();
	private volatile bool _disposed;

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
		_disposed = true;
		ReleaseAllResources();
	}

	#region Public Load Methods

	/// <summary>
	/// Preloads a resource into the cache asynchronously.
	/// If TResource is a Component, it loads the prefab (GameObject), obtains the component,
	/// and caches the prefab handle.
	/// </summary>
	/// <exception cref="OperationCanceledException"><paramref name="token"/> was cancelled before the load finished.</exception>
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
	/// <exception cref="OperationCanceledException">
	/// <paramref name="token"/> was cancelled before the load finished. The awaiting caller regains
	/// control as soon as cancellation is requested, without waiting for the underlying Addressables
	/// operation to actually finish; the handle is released once it does.
	/// </exception>
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
	/// <exception cref="OperationCanceledException"><paramref name="token"/> was cancelled before the load finished.</exception>
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

	/// <summary>
	/// Loads a resource asynchronously and returns an exclusive lease over it. Disposing the
	/// lease releases the underlying Addressables handle.
	/// </summary>
	/// <exception cref="OperationCanceledException"><paramref name="token"/> was cancelled before the load finished.</exception>
	public async Task<IResourceLease<TResource>> LoadResourceLeaseAsync<TResource>(
		string resourceId,
		CancellationToken token = default)
	{
		var (handle, resource) = await LoadAssetHandleAsync<TResource>(resourceId, token);
		return new ResourceLeaseImpl<TResource>(this, handle, resource);
	}

	/// <summary>
	/// Asynchronously loads a prefab, instantiates it under the specified parent, and returns an
	/// exclusive lease over the instantiated component. Disposing the lease destroys the
	/// instantiated object and releases the underlying Addressables handle.
	/// </summary>
	/// <exception cref="ArgumentException">
	/// <paramref name="parent"/> is neither <c>null</c>, a <see cref="Transform"/>, nor a <see cref="Component"/>.
	/// </exception>
	/// <exception cref="OperationCanceledException"><paramref name="token"/> was cancelled before the load finished.</exception>
	public async Task<IInstanceLease<TComponent>> LoadInstanceLeaseAsync<TComponent, TParent>(
		string resourceId,
		TParent parent,
		CancellationToken token = default)
		where TComponent : class
	{
		var parentTransform = ResolveParentTransform(parent);

		var (handle, prefab) = await LoadAssetHandleAsync<GameObject>(resourceId, token);

		if (prefab == null)
		{
			if (Untrack(handle))
			{
				Addressables.Release(handle);
			}

			throw new Exception($"Prefab {resourceId} loaded as null.");
		}

		var instance = Object.Instantiate(prefab, parentTransform);

		if (!instance.TryGetComponent(out TComponent component))
		{
			Object.Destroy(instance);

			if (Untrack(handle))
			{
				Addressables.Release(handle);
			}

			throw new Exception($"Instantiated prefab {resourceId} does not have component {typeof(TComponent).Name}.");
		}

		return new InstanceLeaseImpl<TComponent>(this, handle, instance, component);
	}

	#endregion

	#region Private Helper Methods (Sync)

	/// <summary>
	/// Synchronously loads a non-Component resource (e.g. ScriptableObject, AudioClip, Material).
	/// </summary>
	private TResource LoadAssetSync<TResource>(string resourceId)
	{
		ThrowIfDisposed();

		var handle = Addressables.LoadAssetAsync<TResource>(resourceId);
		handle.WaitForCompletion();

		if (handle.Status != AsyncOperationStatus.Succeeded)
		{
			Addressables.Release(handle);
			throw new Exception($"Failed to load resource synchronously: {resourceId}");
		}

		if (!TryTrack(handle))
		{
			Addressables.Release(handle);
			throw new ObjectDisposedException(nameof(AddressableResourceLoader));
		}

		return handle.Result;
	}

	/// <summary>
	/// Synchronously loads a prefab as a GameObject and returns the requested component from it.
	/// </summary>
	private TResource LoadComponentSync<TResource>(string resourceId)
	{
		ThrowIfDisposed();

		var prefabHandle = Addressables.LoadAssetAsync<GameObject>(resourceId);
		prefabHandle.WaitForCompletion();

		if (prefabHandle.Status != AsyncOperationStatus.Succeeded)
		{
			Addressables.Release(prefabHandle);
			throw new Exception($"Failed to load prefab synchronously for component: {resourceId}");
		}

		if (!TryTrack(prefabHandle))
		{
			Addressables.Release(prefabHandle);
			throw new ObjectDisposedException(nameof(AddressableResourceLoader));
		}

		var prefab = prefabHandle.Result;
		return prefab != null ? prefab.GetComponent<TResource>() : default;
	}

	#endregion

	#region Private Helper Methods (Async)

	/// <summary>
	/// Awaits <paramref name="task"/>, but returns as soon as <paramref name="token"/> is cancelled
	/// instead of blocking until the underlying operation actually finishes.
	/// </summary>
	/// <returns><c>true</c> if <paramref name="token"/> was cancelled before <paramref name="task"/> completed.</returns>
	private static async Task<bool> WaitOrCancelAsync(Task task, CancellationToken token)
	{
		if (!token.CanBeCanceled)
		{
			await task;
			return false;
		}

		token.ThrowIfCancellationRequested();

		var cancellationCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		using var registration = token.Register(
			static state => ((TaskCompletionSource<bool>)state).TrySetResult(true),
			cancellationCompletionSource);

		var completedTask = await Task.WhenAny(task, cancellationCompletionSource.Task);
		return completedTask == cancellationCompletionSource.Task;
	}

	/// <summary>
	/// Loads a resource asynchronously and, on success, tracks its handle in <see cref="_loadedResources"/>.
	/// Shared by every async load path (typed result, component, callback, lease).
	/// </summary>
	/// <exception cref="OperationCanceledException">
	/// <paramref name="token"/> was cancelled before the load finished. The handle is released.
	/// </exception>
	/// <exception cref="ObjectDisposedException">
	/// The loader was disposed while this load was in flight. The handle is released.
	/// </exception>
	private async Task<(AsyncOperationHandle Handle, TResource Resource)> LoadAssetHandleAsync<TResource>(
		string resourceId,
		CancellationToken token)
	{
		ThrowIfDisposed();

		var handle = Addressables.LoadAssetAsync<TResource>(resourceId);
		bool cancelled;
		try
		{
			cancelled = await WaitOrCancelAsync(handle.Task, token);
		}
		catch
		{
			Addressables.Release(handle);
			throw;
		}

		if (cancelled)
		{
			Addressables.Release(handle);
			throw new OperationCanceledException(token);
		}

		if (handle.Status != AsyncOperationStatus.Succeeded)
		{
			Addressables.Release(handle);
			throw new Exception($"Failed to load resource asynchronously: {resourceId}");
		}

		if (!TryTrack(handle))
		{
			Addressables.Release(handle);
			throw new ObjectDisposedException(nameof(AddressableResourceLoader));
		}

		return (handle, handle.Result);
	}

	/// <summary>
	/// Asynchronously loads a non-Component resource (e.g. ScriptableObject, AudioClip).
	/// </summary>
	/// <exception cref="OperationCanceledException">
	/// <paramref name="token"/> was cancelled before the load finished. The handle is released.
	/// </exception>
	private async Task<TResource> LoadAssetAsync<TResource>(string resourceId, CancellationToken token)
	{
		var (_, resource) = await LoadAssetHandleAsync<TResource>(resourceId, token);
		return resource;
	}

	/// <summary>
	/// Asynchronously loads a prefab as a GameObject, then returns the requested component.
	/// </summary>
	/// <exception cref="OperationCanceledException">
	/// <paramref name="token"/> was cancelled before the load finished. The handle is released.
	/// </exception>
	private async Task<TResource> LoadComponentAsync<TResource>(string resourceId, CancellationToken token)
	{
		var (_, prefab) = await LoadAssetHandleAsync<GameObject>(resourceId, token);
		return prefab != null ? prefab.GetComponent<TResource>() : default;
	}

	/// <summary>
	/// Asynchronous load of a non-Component resource with a callback.
	/// </summary>
	private void LoadAssetAsyncWithCallback<TResource>(
		string resourceId,
		Action<TResource> onResourceLoaded,
		CancellationToken token)
	{
		ThrowIfDisposed();

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

			if (completedHandle.Status != AsyncOperationStatus.Succeeded)
			{
				// Thrown from Addressables' Completed event, not an await context - a caller-visible
				// exception can't propagate from here, so this is a log rather than a throw.
				Debug.LogError($"Failed to load resource asynchronously: {resourceId}");
				Addressables.Release(handle);
				return;
			}

			if (!TryTrack(handle))
			{
				Addressables.Release(handle);
				return;
			}

			onResourceLoaded?.Invoke(completedHandle.Result);
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
		ThrowIfDisposed();

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

			if (completedHandle.Status != AsyncOperationStatus.Succeeded)
			{
				// Thrown from Addressables' Completed event, not an await context - a caller-visible
				// exception can't propagate from here, so this is a log rather than a throw.
				Debug.LogError($"Failed to load prefab asynchronously (component): {resourceId}");
				Addressables.Release(prefabHandle);
				return;
			}

			if (!TryTrack(prefabHandle))
			{
				Addressables.Release(prefabHandle);
				return;
			}

			var prefab = completedHandle.Result;
			var component = prefab != null ? prefab.GetComponent<TResource>() : default;
			onResourceLoaded?.Invoke(component);
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
		if (!TryTakeLoadedResource(resource, out var handle))
		{
			return;
		}

		Addressables.Release(handle);
	}

	/// <summary>
	/// Releases all loaded resources tracked by this loader.
	/// </summary>
	public void ReleaseAllResources()
	{
		AsyncOperationHandle[] handles;

		lock (_lock)
		{
			handles = _loadedResources.ToArray();
			_loadedResources.Clear();
		}

		foreach (var handle in handles)
		{
			if (handle.IsValid())
			{
				Addressables.Release(handle);
			}
		}
	}

	private bool TryTakeLoadedResource<TResource>(TResource resource, out AsyncOperationHandle loadedHandle)
	{
		lock (_lock)
		{
			for (var index = 0; index < _loadedResources.Count; index++)
			{
				var handle = _loadedResources[index];
				if (!handle.IsValid())
				{
					_loadedResources.RemoveAt(index--);
					continue;
				}

				if (!MatchesResource(handle.Result, resource))
				{
					continue;
				}

				_loadedResources.RemoveAt(index);
				loadedHandle = handle;
				return true;
			}
		}

		loadedHandle = default;
		return false;
	}

	private void ThrowIfDisposed()
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(AddressableResourceLoader));
		}
	}

	/// <summary>
	/// Tracks <paramref name="handle"/> in <see cref="_loadedResources"/> so <see cref="ReleaseAllResources"/>
	/// and <see cref="Dispose"/> can release it. Returns <c>false</c> without tracking it if the loader
	/// has already been disposed - callers must release the handle themselves in that case.
	/// </summary>
	private bool TryTrack(AsyncOperationHandle handle)
	{
		lock (_lock)
		{
			if (_disposed)
			{
				return false;
			}

			_loadedResources.Add(handle);
			return true;
		}
	}

	/// <summary>
	/// Removes <paramref name="handle"/> from <see cref="_loadedResources"/> if it is still tracked there.
	/// <see cref="_loadedResources"/> membership is the single ownership token for a tracked handle:
	/// whichever caller actually removes it - a lease's own <c>Dispose()</c>, or <see cref="ReleaseAllResources"/>
	/// racing it - is the one responsible for calling <c>Addressables.Release</c>. Returns <c>false</c>
	/// (meaning "someone else already claimed it, do not release it again") if it was already removed.
	/// </summary>
	private bool Untrack(AsyncOperationHandle handle)
	{
		lock (_lock)
		{
			for (var index = 0; index < _loadedResources.Count; index++)
			{
				if (_loadedResources[index].Equals(handle))
				{
					_loadedResources.RemoveAt(index);
					return true;
				}
			}
		}

		return false;
	}

	/// <summary>
	/// Resolves the <see cref="Transform"/> to instantiate under from a <see cref="Transform"/>,
	/// a <see cref="Component"/> that has one, or <c>null</c> for no parent.
	/// </summary>
	private static Transform ResolveParentTransform<TParent>(TParent parent)
	{
		if (parent is Transform transform)
		{
			return transform;
		}

		if (parent is Component parentComponent)
		{
			return parentComponent.transform;
		}

		if (parent is null)
		{
			return null;
		}

		throw new ArgumentException(
			$"Parent of type {typeof(TParent).Name} does not provide a Transform.",
			nameof(parent));
	}

	private static bool MatchesResource<TResource>(object loadedResource, TResource resource)
	{
		if (Equals(loadedResource, resource))
		{
			return true;
		}

		if (resource is Component component && loadedResource is GameObject gameObject)
		{
			return Equals(gameObject.GetComponent<TResource>(), component);
		}

		return false;
	}

	#endregion

	#region Leases

	/// <summary>
	/// Disposing releases <see cref="_handle"/> at most once. <c>_disposed</c> guards against this
	/// lease's own <c>Dispose()</c> running twice; <see cref="Untrack"/>'s return value guards against
	/// <see cref="ReleaseAllResources"/>/<see cref="AddressableResourceLoader.Dispose"/> having already
	/// claimed and released the same handle first - only whichever side actually removes it from
	/// <see cref="_loadedResources"/> is allowed to release it.
	/// </summary>
	private sealed class ResourceLeaseImpl<TResource> : IResourceLease<TResource>
	{
		private readonly AddressableResourceLoader _owner;
		private readonly AsyncOperationHandle _handle;
		private int _disposed;

		public TResource Resource { get; }

		public ResourceLeaseImpl(AddressableResourceLoader owner, AsyncOperationHandle handle, TResource resource)
		{
			_owner = owner;
			_handle = handle;
			Resource = resource;
		}

		public void Dispose()
		{
			if (Interlocked.Exchange(ref _disposed, 1) != 0)
			{
				return;
			}

			if (_owner.Untrack(_handle))
			{
				Addressables.Release(_handle);
			}
		}
	}

	/// <summary>
	/// Disposing always destroys the instantiated object, and releases <see cref="_handle"/> at most
	/// once. <c>_disposed</c> guards against this lease's own <c>Dispose()</c> running twice;
	/// <see cref="Untrack"/>'s return value guards against <see cref="ReleaseAllResources"/>/
	/// <see cref="AddressableResourceLoader.Dispose"/> having already claimed and released the same
	/// handle first - only whichever side actually removes it from <see cref="_loadedResources"/> is
	/// allowed to release it.
	/// </summary>
	private sealed class InstanceLeaseImpl<TComponent> : IInstanceLease<TComponent>
	{
		private readonly AddressableResourceLoader _owner;
		private readonly AsyncOperationHandle _handle;
		private readonly GameObject _instance;
		private int _disposed;

		public TComponent Instance { get; }

		public InstanceLeaseImpl(AddressableResourceLoader owner, AsyncOperationHandle handle, GameObject instance, TComponent instanceComponent)
		{
			_owner = owner;
			_handle = handle;
			_instance = instance;
			Instance = instanceComponent;
		}

		public void Dispose()
		{
			if (Interlocked.Exchange(ref _disposed, 1) != 0)
			{
				return;
			}

			if (_instance != null)
			{
				Object.Destroy(_instance);
			}

			if (_owner.Untrack(_handle))
			{
				Addressables.Release(_handle);
			}
		}
	}

	#endregion
}
}
