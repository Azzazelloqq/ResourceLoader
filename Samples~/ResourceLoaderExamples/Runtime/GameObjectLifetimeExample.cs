using System;
using System.Threading.Tasks;
using Loader = ResourceLoader.AddressableResourceLoader.AddressableResourceLoader;
using UnityEngine;

namespace ResourceLoader.Examples
{
/// <summary>Attach to a GameObject; assign the Addressables addresses in the Inspector.</summary>
public sealed class GameObjectLifetimeExample : MonoBehaviour
{
    [SerializeField] private string textureAddress = "GameConfig";
    [SerializeField] private string prefabAddress = "InventoryView";

    private IResourceLifetimeLoader _loader;
    private ResourceLifetime _lifetime;

    private void Awake()
    {
        _loader = new Loader();
        _lifetime = ResourceLifetime.For(gameObject);
    }

    [ContextMenu("Load for this GameObject")]
    private async void LoadForGameObject()
    {
        if (!Application.isPlaying) return;
        try
        {
            // The texture remains owned until this GameObject is destroyed.
            var texture = await _loader.LoadResourceForLifetimeAsync<Texture2D>(
                textureAddress, _lifetime);
            Debug.Log($"Lifetime owns: {texture.name}", this);

            // Request a lease when this owner may release an acquisition early.
            var temporary = await _loader.LoadResourceLeaseForLifetimeAsync<Texture2D>(
                textureAddress, _lifetime);
            temporary.Dispose();

            // Closing the lifetime destroys this instance; its prefab handle stays alive
            // until Unity calls OnDestroy on the instance.
            var instance = await _loader.LoadInstanceLeaseForLifetimeAsync<Transform, Transform>(
                prefabAddress, transform, _lifetime);
            Debug.Log($"Lifetime instance: {instance.Instance.name}", this);
        }
        catch (OperationCanceledException)
        {
            // Destroying the owner cancels any load still in flight.
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
    }

    private void OnDestroy()
    {
        // Close this owner before disposing the loader. The GameObject anchor
        // also closes the same lifetime; a second Dispose call is harmless.
        _lifetime?.Dispose();
        _loader?.Dispose();
    }
}
}
