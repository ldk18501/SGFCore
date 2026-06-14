using System;
using System.Collections.Generic;
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

        internal ResourceScope(ResourceModule resourceModule, string owner)
        {
            _resourceModule = resourceModule;
            Owner = string.IsNullOrWhiteSpace(owner) ? "Unknown" : owner;
        }

        public T TrackAsset<T>(T asset) where T : UnityEngine.Object
        {
            if (asset != null)
            {
                _assets.Add(asset);
            }

            return asset;
        }

        public GameObject TrackInstance(GameObject instance)
        {
            if (instance != null)
            {
                _instances.Add(instance);
            }

            return instance;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            for (int i = 0; i < _assets.Count; i++)
            {
                _resourceModule.ReleaseAsset(_assets[i]);
            }

            for (int i = 0; i < _instances.Count; i++)
            {
                _resourceModule.ReleaseInstance(_instances[i]);
            }

            _assets.Clear();
            _instances.Clear();
        }
    }
}
