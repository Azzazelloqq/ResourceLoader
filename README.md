# Resource Loader for Unity

> Addressables resource loading, instantiation and release behind one interface.

`ResourceLoader` wraps Unity Addressables in `IResourceLoader`. It supports both
assets and components stored on prefabs, tracks loaded handles and releases them
when the loader is disposed.

## Features

- Synchronous and asynchronous loading.
- Preload assets into the loader cache.
- Load a component directly from an Addressable prefab.
- Instantiate a prefab under a `Transform` or `Component` parent.
- Release individual resources or all tracked resources.
- Cancellation-aware async APIs: a cancelled `CancellationToken` interrupts the `await`
  immediately (instead of waiting for the load to finish) and throws
  `OperationCanceledException`, so it can't be confused with a `null`/missing-asset result.
- Lease-based loading (`LoadResourceLeaseAsync`/`LoadInstanceLeaseAsync`) returns an
  `IDisposable` that owns exactly one handle, instead of matching resources back to a
  handle by value in `ReleaseResource`.

## Installation

```bash
git submodule add https://github.com/Azzazelloqq/ResourceLoader.git Assets/ResourceLoader
```

Or add to `Packages/manifest.json`:

```json
"com.azzazello.resourceloader": "https://github.com/Azzazelloqq/ResourceLoader.git"
```

The module requires `com.unity.addressables` and supports Unity `2020.3` and newer.

## Load an asset

```csharp
using ResourceLoader.AddressableResourceLoader;
using UnityEngine;

using var loader = new AddressableResourceLoader();

var config = await loader.LoadResourceAsync<ScriptableObject>(
    "GameConfig",
    cancellationToken);
```

## Load a component from a prefab

When the requested type inherits from `Component`, the loader loads the prefab
and returns that component from it:

```csharp
var presenter = await loader.LoadResourceAsync<MainMenuPresenter>(
    "MainMenuPrefab",
    cancellationToken);
```

## Instantiate an Addressable prefab

```csharp
var view = await loader.LoadAndCreateAsync<InventoryView, Transform>(
    resourceId: "InventoryView",
    parent: canvasTransform,
    token: cancellationToken);
```

## Lease-based loading

`LoadResourceLeaseAsync`/`LoadInstanceLeaseAsync` return a lease instead of a bare
resource. Disposing the lease is the release - no separate call, no value-matching:

```csharp
using var lease = await loader.LoadResourceLeaseAsync<ScriptableObject>(
    "GameConfig",
    cancellationToken);

var config = lease.Resource;
// lease.Dispose() (or the `using` above) releases the Addressables handle.
```

```csharp
using var view = await loader.LoadInstanceLeaseAsync<InventoryView, Transform>(
    resourceId: "InventoryView",
    parent: canvasTransform,
    token: cancellationToken);

view.Instance.Show();
// Disposing destroys the instantiated GameObject and releases the handle.
```

`TComponent` only needs to be a reference type (`where TComponent : class`), so it can be
an interface implemented by the prefab's component, not just the component type itself.

If a lease is never disposed, `ReleaseAllResources()`/`Dispose()` on the loader still
release its handle as a safety net - but for an instance lease, only the handle; the
instantiated `GameObject` is destroyed only by disposing the lease itself.

## Lifetime

Use `ReleaseResource(resource)` for an individual loaded resource, or call
`ReleaseAllResources()`. `Dispose()` releases all handles tracked by the loader.
Prefer the lease-based methods above when you can - `ReleaseResource` matches a
resource back to its handle by value, which is ambiguous for anything instantiated
more than once with the same content (e.g. `LoadAndCreateAsync`).

## API

`IResourceLoader` provides `PreloadInCacheAsync`, `LoadResource`,
`LoadResourceAsync`, `LoadAndCreateAsync`, `LoadResourceLeaseAsync`,
`LoadInstanceLeaseAsync`, `ReleaseResource` and `ReleaseAllResources`.
