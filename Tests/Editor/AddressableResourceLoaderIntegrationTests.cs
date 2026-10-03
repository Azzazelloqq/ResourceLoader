using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using AddressableLoader = ResourceLoader.AddressableResourceLoader.AddressableResourceLoader;

namespace ResourceLoader.Tests
{
    public sealed class AddressableResourceLoaderIntegrationTests
    {
        private const string TextureAddress = "ResourceLoader.Tests/Texture";
        private const string PrefabAddress = "ResourceLoader.Tests/Prefab";

        private string _folder;
        private ResourceLocationMap _locator;
        private AssetDatabaseProvider _provider;
        private AddressableLoader _loader;

        [OneTimeSetUp]
        public void CreateAssets()
        {
            var runtimeSettings = SessionState.GetString(Addressables.kAddressablesRuntimeDataPath,
                Addressables.RuntimePath + "/settings.json");
            if (!File.Exists(runtimeSettings))
                Assert.Ignore("Build Addressables content before running ResourceLoader integration tests.");

            _folder = "Assets/ResourceLoader/Tests/Fixture-" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(_folder);

            var texture = new Texture2D(2, 2);
            texture.SetPixel(0, 0, Color.red);
            texture.Apply();
            var texturePath = _folder + "/Texture.asset";
            AssetDatabase.CreateAsset(texture, texturePath);

            var prefab = new GameObject("ResourceLoaderFixture");
            prefab.AddComponent<BoxCollider>();
            var prefabPath = _folder + "/Prefab.prefab";
            PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
            UnityEngine.Object.DestroyImmediate(prefab);
            AssetDatabase.SaveAssets();

            var initialization = Addressables.InitializeAsync(false);
            initialization.WaitForCompletion();
            Assert.That(initialization.Status, Is.EqualTo(AsyncOperationStatus.Succeeded));
            Addressables.Release(initialization);

            _provider = new AssetDatabaseProvider(0);
            _provider.Initialize("ResourceLoader.Tests.Provider", "");
            Addressables.ResourceManager.ResourceProviders.Add(_provider);
            _locator = new ResourceLocationMap("ResourceLoader.Tests", 2);
            _locator.Add(TextureAddress, new ResourceLocationBase(TextureAddress, texturePath,
                _provider.ProviderId, typeof(Texture2D)));
            _locator.Add(PrefabAddress, new ResourceLocationBase(PrefabAddress, prefabPath,
                _provider.ProviderId, typeof(GameObject)));
            Addressables.AddResourceLocator(_locator);
        }

        [OneTimeTearDown]
        public void RemoveAssets()
        {
            if (_locator != null) Addressables.RemoveResourceLocator(_locator);
            if (_provider != null) Addressables.ResourceManager.ResourceProviders.Remove(_provider);
            if (!string.IsNullOrEmpty(_folder)) AssetDatabase.DeleteAsset(_folder);
        }

        [SetUp]
        public void SetUp() => _loader = new AddressableLoader();

        [TearDown]
        public void TearDown() => _loader?.Dispose();

        [Test]
        public async Task Texture_TwoLifetimes_ClosingOneLeavesOtherUsable()
        {
            using var first = new ResourceLifetime();
            using var second = new ResourceLifetime();
            var firstTexture = await _loader.LoadResourceForLifetimeAsync<Texture2D>(TextureAddress, first);
            var secondTexture = await _loader.LoadResourceForLifetimeAsync<Texture2D>(TextureAddress, second);

            Assert.That(firstTexture, Is.SameAs(secondTexture));
            first.Dispose();
            Assert.That(secondTexture.GetPixel(0, 0), Is.EqualTo(Color.red));
            second.Dispose();
        }

        [Test]
        public async Task Prefab_DestroyingOwner_DestroysOnlyItsInstance()
        {
            var owner = new GameObject("ResourceLoaderOwner");
            var otherOwner = new GameObject("ResourceLoaderOtherOwner");
            try
            {
                var first = await _loader.LoadInstanceLeaseForLifetimeAsync<BoxCollider, Transform>(
                    PrefabAddress, owner.transform, ResourceLifetime.For(owner));
                var second = await _loader.LoadInstanceLeaseForLifetimeAsync<BoxCollider, Transform>(
                    PrefabAddress, otherOwner.transform, ResourceLifetime.For(otherOwner));
                var firstInstance = first.Instance;
                var secondInstance = second.Instance;

                UnityEngine.Object.DestroyImmediate(owner);

                Assert.That(firstInstance == null, Is.True);
                Assert.That(secondInstance == null, Is.False);
            }
            finally
            {
                if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
                if (otherOwner != null) UnityEngine.Object.DestroyImmediate(otherOwner);
            }
        }

        [Test]
        public async Task LegacyRelease_DoesNotReleaseLifetimeAcquisitionOfSameAsset()
        {
            using var lifetime = new ResourceLifetime();
            var legacy = await _loader.LoadResourceAsync<Texture2D>(TextureAddress);
            var lease = await _loader.LoadResourceLeaseForLifetimeAsync<Texture2D>(TextureAddress, lifetime);

            _loader.ReleaseResource(legacy);

            Assert.That(lease.Resource.GetPixel(0, 0), Is.EqualTo(Color.red));
            lease.Dispose();
        }

        [Test]
        public async Task LoaderDispose_DestroysLifetimeOwnedInstance()
        {
            using var lifetime = new ResourceLifetime();
            var lease = await _loader.LoadInstanceLeaseForLifetimeAsync<BoxCollider, Transform>(
                PrefabAddress, null, lifetime);
            var instance = lease.Instance;

            _loader.Dispose();

            Assert.That(instance == null, Is.True);
            Assert.DoesNotThrow(lifetime.Dispose);
        }

    }
}
