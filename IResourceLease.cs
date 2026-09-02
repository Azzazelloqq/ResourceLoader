using System;

namespace ResourceLoader
{
/// <summary>
/// Owns a single loaded resource. Disposing releases the underlying Addressables handle.
/// If the lease is never disposed, <see cref="IResourceLoader.ReleaseAllResources"/> and
/// <see cref="IDisposable.Dispose"/> on the owning loader release it as a safety net.
/// </summary>
/// <typeparam name="TResource">The type of the loaded resource.</typeparam>
public interface IResourceLease<out TResource> : IDisposable
{
	/// <summary>
	/// The loaded resource. Valid until the lease is disposed.
	/// </summary>
	TResource Resource { get; }
}

/// <summary>
/// Owns a single instantiated prefab and the component retrieved from it. Disposing destroys
/// the instantiated <see cref="UnityEngine.GameObject"/> and releases the underlying Addressables
/// handle. If the lease is never disposed, <see cref="IResourceLoader.ReleaseAllResources"/> and
/// <see cref="IDisposable.Dispose"/> on the owning loader release the handle (but not the
/// instantiated object) as a safety net.
/// </summary>
/// <typeparam name="TComponent">The type of the component retrieved from the instantiated prefab.</typeparam>
public interface IInstanceLease<out TComponent> : IDisposable
{
	/// <summary>
	/// The component retrieved from the instantiated prefab. Valid until the lease is disposed.
	/// </summary>
	TComponent Instance { get; }
}
}
