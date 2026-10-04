# ResourceLoader examples

Import **ResourceLoader Examples** from the package's **Samples** section in Package
Manager. If ResourceLoader is checked out as `Assets/ResourceLoader`, copy this
directory into a normal `Assets` folder to compile the examples. The sample uses
two independent assemblies: `ResourceLoader.Examples` (runtime) and
`ResourceLoader.Examples.Editor` (Editor only). Neither is required by the library.

The examples use literal Addressables addresses. There is no generated ID class
or UUID in the sample. Set up a texture with address `GameConfig` and a prefab
with address `InventoryView`, or change the strings to
your own Addressables addresses. Build Addressables content, or use a Play Mode
Script that loads directly from the Asset Database while trying the examples.

## Runtime

- `LegacyLoadingExample` shows preload, sync/async/callback loads, loading a
  component from a prefab without instantiation, manual release, and the original
  `LoadAndCreateAsync` API. The caller owns the loader. For the original
  instantiation API, the caller also destroys the returned component's GameObject
  and keeps the loader alive until it has been destroyed.
- `LeaseLoadingExample` shows individual resource and instance leases. Dispose
  each lease when that particular use ends.
- Attach `GameObjectLifetimeExample` to a GameObject, enter Play Mode, then use
  its **Load for this GameObject** context menu. Destroying the GameObject closes
  its lifetime. The example shows both lifetime-held resources and early release.
- `PlainOwnerExample` shows how an ordinary `IDisposable` object owns a lifetime.
  It also registers a separately acquired lease with `ResourceLifetime.Add`.
  Its caller provides a loader and disposes that loader separately. Several
  owners can use the same loader without closing each other's lifetimes.

All async methods accept `CancellationToken` where the API supports it. A cancelled
load throws `OperationCanceledException`; closing a lifetime also cancels its
in-flight loads.

## Address generation

Create **ResourceLoader/Address Generator Settings** in the Assets menu. Configure
the output path, namespace, root class, separators (for example `/` or `-`), and
group selection. Open **Tools/ResourceLoader/Address Generator**, click **Preview**,
then **Generate** if the preview is correct. This writes a normal `.cs` file with
`const string` addresses into your project. It is an Editor command, not a Roslyn
source generator. No generated output is included in this sample.

The separate Editor example adds **Tools/ResourceLoader/Examples/Preview Selected
Address Settings**. Select a settings asset and run it to print the generated
code preview to the Console without writing a file.
