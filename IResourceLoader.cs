using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ResourceLoader
{
/// <summary>
/// Defines a resource loader interface for loading and releasing resources.
/// </summary>
public interface IResourceLoader : IDisposable
{
	/// <summary>
	/// Preloads a resource into the cache asynchronously.
	/// </summary>
	/// <typeparam name="TResource">The type of the resource to preload.</typeparam>
	/// <param name="resourceId">The identifier of the resource to preload.</param>
	/// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
	/// <exception cref="OperationCanceledException"><paramref name="token"/> was cancelled before the load finished.</exception>
	public Task PreloadInCacheAsync<TResource>(string resourceId, CancellationToken token);
	

	/// <summary>
	/// Loads a resource synchronously.
	/// </summary>
	/// <typeparam name="TResource">The type of the resource to load.</typeparam>
	/// <param name="resourceId">The identifier of the resource to load.</param>
	/// <returns>The loaded resource.</returns>
	public TResource LoadResource<TResource>(string resourceId);

	/// <summary>
	/// Loads a resource asynchronously and invokes a callback upon completion.
	/// </summary>
	/// <typeparam name="TResource">The type of the resource to load.</typeparam>
	/// <param name="resourceId">The identifier of the resource to load.</param>
	/// <param name="onResourceLoaded">The callback to invoke when the resource is loaded.</param>
	/// <param name="token">A cancellation token to observe while waiting for the resource to load.</param>
	public void LoadResource<TResource>(string resourceId, Action<TResource> onResourceLoaded, CancellationToken token);

	/// <summary>
	/// Loads a resource asynchronously.
	/// </summary>
	/// <typeparam name="TResource">The type of the resource to load.</typeparam>
	/// <param name="resourceId">The identifier of the resource to load.</param>
	/// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
	/// <returns>A task that represents the asynchronous load operation. The task result contains the loaded resource.</returns>
	/// <exception cref="OperationCanceledException">
	/// <paramref name="token"/> was cancelled before the load finished. Control returns to the caller
	/// as soon as cancellation is requested, without waiting for the underlying load to actually finish.
	/// </exception>
	public Task<TResource> LoadResourceAsync<TResource>(string resourceId, CancellationToken token);

	/// <summary>
	/// Asynchronously loads a resource and creates an instance of the specified component, 
	/// associating it with the provided parent.
	/// </summary>
	/// <typeparam name="TComponent">The type of the component to be created from the loaded resource.</typeparam>
	/// <typeparam name="TParent">The type of the parent object to which the created resource will be associated.</typeparam>
	/// <param name="resourceId">The identifier of the resource to load.</param>
	/// <param name="parent">The parent object with which the created resource will be associated.</param>
	/// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
	/// <returns>The requested component created from the loaded resource.</returns>
	/// <exception cref="OperationCanceledException"><paramref name="token"/> was cancelled before the load finished.</exception>
	Task<TComponent> LoadAndCreateAsync<TComponent, TParent>(
		string resourceId,
		TParent parent,
		CancellationToken token = default);

	/// <summary>
	/// Loads a resource asynchronously and returns an exclusive lease over it. Disposing the
	/// lease releases the underlying Addressables handle.
	/// </summary>
	/// <typeparam name="TResource">The type of the resource to load.</typeparam>
	/// <param name="resourceId">The identifier of the resource to load.</param>
	/// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
	/// <returns>A lease owning the loaded resource.</returns>
	/// <exception cref="OperationCanceledException">
	/// <paramref name="token"/> was cancelled before the load finished. Control returns to the caller
	/// as soon as cancellation is requested, without waiting for the underlying load to actually finish.
	/// </exception>
	Task<IResourceLease<TResource>> LoadResourceLeaseAsync<TResource>(string resourceId, CancellationToken token = default);

	/// <summary>
	/// Asynchronously loads a prefab, instantiates it under the specified parent, and returns an
	/// exclusive lease over the instantiated component. Disposing the lease destroys the
	/// instantiated object and releases the underlying Addressables handle.
	/// </summary>
	/// <typeparam name="TComponent">
	/// The type of the component to retrieve from the instantiated prefab. May be a
	/// <see cref="UnityEngine.Component"/> type or an interface it implements.
	/// </typeparam>
	/// <typeparam name="TParent">The type of the parent to associate with the instantiated prefab. Must be either <see cref="UnityEngine.Transform"/> or a type containing a <see cref="UnityEngine.Transform"/> component, or <c>null</c> for no parent.</typeparam>
	/// <param name="resourceId">The identifier of the prefab to load from Addressables.</param>
	/// <param name="parent">The parent object under which the prefab will be instantiated, or <c>null</c>.</param>
	/// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
	/// <returns>A lease owning the instantiated component.</returns>
	/// <exception cref="ArgumentException">
	/// <paramref name="parent"/> is neither <c>null</c>, a <see cref="UnityEngine.Transform"/>, nor a <see cref="UnityEngine.Component"/>.
	/// </exception>
	/// <exception cref="OperationCanceledException"><paramref name="token"/> was cancelled before the load finished.</exception>
	Task<IInstanceLease<TComponent>> LoadInstanceLeaseAsync<TComponent, TParent>(
		string resourceId,
		TParent parent,
		CancellationToken token = default)
		where TComponent : class;

	/// <summary>
	/// Releases a loaded resource.
	/// </summary>
	/// <typeparam name="TResource">The type of the resource to release.</typeparam>
	/// <param name="resource">The resource to release.</param>
	public void ReleaseResource<TResource>(TResource resource);

	/// <summary>
	/// Releases all loaded resources.
	/// </summary>
	public void ReleaseAllResources();
}
}