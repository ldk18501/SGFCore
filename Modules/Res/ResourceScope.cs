using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameFramework.Core
{
    /// <summary>
    /// 资源作用域。适合 UI、流程或玩法系统集中托管一组动态加载资源。
    /// </summary>
    public sealed class ResourceScope : IDisposable
    {
        private readonly ResourceModule _resourceModule;
        private readonly List<UnityEngine.Object> _assets = new List<UnityEngine.Object>();
        private readonly List<GameObject> _instances = new List<GameObject>();
        private bool _disposed;

        public string Owner { get; }
        public int TrackedAssetCount => _assets.Count;
        public int TrackedInstanceCount => _instances.Count;
        public bool IsDisposed => _disposed;

        internal ResourceScope(ResourceModule resourceModule, string owner)
        {
            _resourceModule = resourceModule;
            Owner = string.IsNullOrWhiteSpace(owner) ? "Unknown" : owner;
        }

        public T TrackAsset<T>(T asset) where T : UnityEngine.Object
        {
            ThrowIfDisposed();
            if (asset != null)
            {
                _assets.Add(asset);
            }

            return asset;
        }

        public GameObject TrackInstance(GameObject instance)
        {
            ThrowIfDisposed();
            if (instance != null)
            {
                _instances.Add(instance);
            }

            return instance;
        }

        public async UniTask<T> LoadAssetAsync<T>(
            string address,
            CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            ThrowIfDisposed();
            T asset = await _resourceModule.LoadAssetAsync<T>(address, cancellationToken);
            if (_disposed)
            {
                _resourceModule.ReleaseAsset(asset);
                throw new ObjectDisposedException($"ResourceScope({Owner})");
            }

            return TrackAsset(asset);
        }

        public async UniTask<GameObject> InstantiateAsync(
            string address,
            Transform parent = null,
            bool instantiateInWorldSpace = false,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            GameObject instance = await _resourceModule.InstantiateAsync(
                address,
                parent,
                instantiateInWorldSpace,
                cancellationToken);
            if (_disposed)
            {
                _resourceModule.ReleaseInstance(instance);
                throw new ObjectDisposedException($"ResourceScope({Owner})");
            }

            return TrackInstance(instance);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            for (int i = 0; i < _instances.Count; i++)
            {
                _resourceModule.ReleaseInstance(_instances[i]);
            }

            for (int i = 0; i < _assets.Count; i++)
            {
                _resourceModule.ReleaseAsset(_assets[i]);
            }

            _assets.Clear();
            _instances.Clear();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException($"ResourceScope({Owner})");
            }
        }
    }
}
