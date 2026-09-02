using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using AddressableLoader = ResourceLoader.AddressableResourceLoader.AddressableResourceLoader;

namespace ResourceLoader.Tests
{
    public sealed class AddressableResourceLoaderTests
    {
        private static readonly FieldInfo LoadedResourcesField = typeof(AddressableLoader)
            .GetField("_loadedResources", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo WaitOrCancelAsyncMethod = typeof(AddressableLoader)
            .GetMethod("WaitOrCancelAsync", BindingFlags.Static | BindingFlags.NonPublic);

        private AddressableLoader _loader;
        private GameObject _resource;

        [SetUp]
        public void SetUp()
        {
            _loader = new AddressableLoader();
            _resource = new GameObject("ResourceLoaderTestResource");
        }

        [TearDown]
        public void TearDown()
        {
            _loader.Dispose();
            UnityEngine.Object.DestroyImmediate(_resource);
        }

        [Test]
        public void ReleaseResource_ReleasesTrackedHandle()
        {
            var handle = Track(_resource);

            _loader.ReleaseResource(_resource);

            Assert.That(handle.IsValid(), Is.False);
        }

        [Test]
        public void ReleaseResource_WhenResourceWasLoadedTwice_ReleasesOneHandlePerCall()
        {
            var firstHandle = Track(_resource);
            var secondHandle = Track(_resource);

            _loader.ReleaseResource(_resource);

            Assert.That(firstHandle.IsValid(), Is.False);
            Assert.That(secondHandle.IsValid(), Is.True);

            _loader.ReleaseResource(_resource);

            Assert.That(secondHandle.IsValid(), Is.False);
        }

        [Test]
        public void ReleaseResource_WithComponent_ReleasesPrefabHandle()
        {
            var component = _resource.AddComponent<TestComponent>();
            var handle = Track(_resource);

            _loader.ReleaseResource(component);

            Assert.That(handle.IsValid(), Is.False);
        }

        [Test]
        public void ReleaseAllResources_ReleasesEveryTrackedHandle()
        {
            var firstHandle = Track(_resource);
            var secondHandle = Track(_resource);

            _loader.ReleaseAllResources();

            Assert.That(firstHandle.IsValid(), Is.False);
            Assert.That(secondHandle.IsValid(), Is.False);
        }

        [Test]
        public void ReleaseAllResources_WhenCalledTwice_DoesNotReleaseHandlesTwice()
        {
            var handle = Track(_resource);

            _loader.ReleaseAllResources();
            Assert.DoesNotThrow(_loader.ReleaseAllResources);

            Assert.That(handle.IsValid(), Is.False);
        }

        [Test]
        public async Task WaitOrCancelAsync_WhenTokenCancelledBeforeTaskCompletes_ReturnsTrueWithoutWaitingForTask()
        {
            var neverCompletingSource = new TaskCompletionSource<int>();
            using var cts = new CancellationTokenSource();

            var waitTask = InvokeWaitOrCancelAsync(neverCompletingSource.Task, cts.Token);
            cts.Cancel();

            var raced = await Task.WhenAny(waitTask, Task.Delay(TimeSpan.FromSeconds(5)));

            Assert.That(raced, Is.SameAs(waitTask), "Cancellation must interrupt the wait instead of blocking until the task completes.");
            Assert.That(await waitTask, Is.True);
            Assert.That(neverCompletingSource.Task.IsCompleted, Is.False);
        }

        [Test]
        public async Task WaitOrCancelAsync_WhenTaskCompletesBeforeCancellation_ReturnsFalse()
        {
            var source = new TaskCompletionSource<int>();
            using var cts = new CancellationTokenSource();

            var waitTask = InvokeWaitOrCancelAsync(source.Task, cts.Token);
            source.SetResult(42);

            Assert.That(await waitTask, Is.False);
        }

        [Test]
        public void LoadResource_AfterDispose_ThrowsObjectDisposedException()
        {
            _loader.Dispose();

            Assert.Throws<ObjectDisposedException>(() => _loader.LoadResource<GameObject>("any"));
        }

        [Test]
        public void LoadResource_WithCallback_AfterDispose_ThrowsObjectDisposedException()
        {
            _loader.Dispose();

            Assert.Throws<ObjectDisposedException>(() => _loader.LoadResource<GameObject>("any", _ => { }));
        }

        private static Task<bool> InvokeWaitOrCancelAsync(Task task, CancellationToken token)
        {
            return (Task<bool>)WaitOrCancelAsyncMethod.Invoke(null, new object[] { task, token });
        }

        private AsyncOperationHandle Track(UnityEngine.Object resource)
        {
            Assert.That(LoadedResourcesField, Is.Not.Null);

            var handle = Addressables.ResourceManager.CreateCompletedOperation(resource, null);
            var loadedResources = (List<AsyncOperationHandle>)LoadedResourcesField.GetValue(_loader);
            loadedResources.Add(handle);
            return handle;
        }

        private sealed class TestComponent : MonoBehaviour
        {
        }
    }
}
