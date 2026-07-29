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
- Cancellation-aware async APIs.

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

## Lifetime

Use `ReleaseResource(resource)` for an individual loaded resource, or call
`ReleaseAllResources()`. `Dispose()` releases all handles tracked by the loader.

## API

`IResourceLoader` provides `PreloadInCacheAsync`, `LoadResource`,
`LoadResourceAsync`, `LoadAndCreateAsync`, `ReleaseResource` and
`ReleaseAllResources`.
