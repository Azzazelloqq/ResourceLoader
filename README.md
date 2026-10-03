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
- `ResourceLifetime` groups ownership for a plain C# owner or a `GameObject`.
- An optional Editor generator produces grouped string address constants.

## Installation

```bash
git submodule add https://github.com/Azzazelloqq/ResourceLoader.git Assets/ResourceLoader
```

Or add to `Packages/manifest.json`:

```json
"com.azzazello.resourceloader": "https://github.com/Azzazelloqq/ResourceLoader.git"
```

To try the preview in one project, pin that project's Git dependency to the prerelease tag:

```json
"com.azzazello.resourceloader": "https://github.com/Azzazelloqq/ResourceLoader.git#v1.3.0-beta.1"
```

The tag does not change projects that track another tag or an existing submodule commit.

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
release its handle as a safety net. Lifetime-owned instance leases also destroy their
instances and keep each prefab handle until that instance's `OnDestroy` callback.

## Owner lifetimes

Create one lifetime per owner period. Every acquisition belongs to the specified lifetime;
closing one lifetime never releases another consumer's acquisition.

```csharp
using var lifetime = new ResourceLifetime();
var config = await loader.LoadResourceForLifetimeAsync<ScriptableObject>(
    "GameConfig", lifetime, cancellationToken);
// lifetime.Dispose() releases this owner's resource hold.
```

For early release, request a lease owned by the lifetime:

```csharp
var lease = await loader.LoadResourceLeaseForLifetimeAsync<ScriptableObject>(
    "GameConfig", lifetime, cancellationToken);
lease.Dispose();
```

Use `ResourceLifetime.For(gameObject)` to get a lifetime that closes when that object is
destroyed. A plain C# owner using the separate `Disposable` module can register one lifetime
with `CompositeDisposable.AddDisposable(lifetime)`; ResourceLoader does not require that module.

Lifetime calls live in additive `IResourceLifetimeLoader`; existing `IResourceLoader`
implementations remain valid. Existing bare loads and `ReleaseResource` remain available for
source compatibility. New lifetime/lease acquisitions are not matched by the legacy
`ReleaseResource(resource)` method.

## Address code generation

Create **ResourceLoader/Address Generator Settings** in the Unity asset menu, then open
**Tools/ResourceLoader/Address Generator**. Configure groups, separators, identifier casing,
namespace, and output file. Preview checks collisions before Generate writes a `.cs` file.
The generated members are `const string` values containing the original Addressables
addresses; the generator does not change Addressables settings.

## Tests

The Editor integration tests create a temporary texture and prefab, load them through
Addressables, then remove the assets. They require built Addressables content in the test
project; without it, those tests are skipped. The other EditMode tests run without a
catalog. The temporary assets are never added to the project's Addressables groups.

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
