using System.Collections.Generic;
using System.Reflection;
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
            Object.DestroyImmediate(_resource);
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

        private AsyncOperationHandle Track(Object resource)
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
