using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace GameFramework.Core
{
    /// <summary>
    /// 全局资源管理模块 (基于 Addressables)
    /// 统一负责 Addressables 初始化、加载句柄追踪、实例释放和泄漏审计。
    /// </summary>
    public class ResourceModule : IFrameworkModule
    {
        public int Priority => 40; // 优先级排在文件系统和日志之后

        private readonly Dictionary<UnityEngine.Object, Stack<AssetHandleRecord>> _assetHandles =
            new Dictionary<UnityEngine.Object, Stack<AssetHandleRecord>>();

        private readonly Dictionary<GameObject, InstanceHandleRecord> _instanceHandles =
            new Dictionary<GameObject, InstanceHandleRecord>();

        private Task _initializeTask;
        private Exception _initializeException;
        private bool _isInitialized;
        private bool _isDestroyed;
        private int _pendingOperationCount;

        public bool IsInitialized => _isInitialized;
        public int PendingOperationCount => _pendingOperationCount;
        public int TrackedAssetCount => _assetHandles.Count;
        public int TrackedInstanceCount => _instanceHandles.Count;

        public ResourceScope CreateScope(string owner)
        {
            return new ResourceScope(this, owner);
        }

        public void OnInit()
        {
            _isDestroyed = false;
            _initializeTask = InitializeAddressablesAsync();
        }

        private async Task InitializeAddressablesAsync()
        {
            AsyncOperationHandle handle = default;
            try
            {
                handle = Addressables.InitializeAsync(false);
                await handle.Task;

                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    _isInitialized = true;
                    _initializeException = null;
                    Log.Module("Resource", "Addressables 系统初始化完成！");
                }
                else
                {
                    _initializeException = handle.OperationException ??
                                           new Exception("Addressables 初始化返回 Failed 状态。");
                    Log.Fatal($"[Resource] Addressables 初始化失败: {_initializeException.Message}");
                }
            }
            catch (Exception e)
            {
                _initializeException = e;
                Log.Fatal($"[Resource] Addressables 初始化失败: {e.Message}");
            }
            finally
            {
                ReleaseHandle(handle);
            }
        }

        public void OnUpdate(float deltaTime, float unscaledDeltaTime) { }

        public void OnDestroy()
        {
            _isDestroyed = true;

            int assetHandleCount = CountTrackedAssetHandles();
            if (assetHandleCount > 0 || _instanceHandles.Count > 0)
            {
                Log.Warning(
                    $"[Resource] 模块销毁时仍有未释放资源，自动清理。AssetHandles: {assetHandleCount}, Instances: {_instanceHandles.Count}");
            }

            ReleaseAllInstances();
            ReleaseAllAssets();

            _isInitialized = false;
            _initializeTask = null;
            _initializeException = null;
            _pendingOperationCount = 0;
        }

        /// <summary>
        /// 等待 Addressables 初始化完成。建议在启动流程或热更流程正式加载资源前显式调用。
        /// </summary>
        public async UniTask<bool> EnsureInitializedAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            if (_isInitialized) return true;

            if (_isDestroyed)
            {
                Log.Warning("[Resource] 模块已经销毁，无法初始化 Addressables。");
                return false;
            }

            if (_initializeTask == null)
            {
                _initializeTask = InitializeAddressablesAsync();
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _initializeTask;
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception e)
            {
                _initializeException = e;
                Log.Fatal($"[Resource] 等待 Addressables 初始化时发生异常: {e.Message}");
                return false;
            }

            if (_initializeException != null)
            {
                Log.Fatal($"[Resource] Addressables 尚未初始化成功: {_initializeException.Message}");
                return false;
            }

            return _isInitialized;
        }

        // ==========================================
        // API: 加载与实例化 (基于 async/await)
        // ==========================================

        /// <summary>
        /// 异步加载资源 (例如 AudioClip, Sprite, ScriptableObject 等数据资源)
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="address">资源的可寻址路径/标签</param>
        /// <returns>加载完成的资源对象</returns>
        public async UniTask<T> LoadAssetAsync<T>(string address) where T : UnityEngine.Object
        {
            return await LoadAssetAsync<T>(address, default(CancellationToken));
        }

        public async UniTask<T> LoadAssetAsync<T>(string address, CancellationToken cancellationToken)
            where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(address))
            {
                Log.Error("[Resource] LoadAssetAsync 失败：address 为空。");
                return null;
            }

            if (!await EnsureInitializedAsync(cancellationToken))
            {
                return null;
            }

            AsyncOperationHandle<T> handle = default;
            _pendingOperationCount++;

            try
            {
                handle = Addressables.LoadAssetAsync<T>(address);
                T asset = await handle.ToUniTask(cancellationToken: cancellationToken);

                if (_isDestroyed)
                {
                    ReleaseHandle(handle);
                    return null;
                }

                if (handle.Status != AsyncOperationStatus.Succeeded || asset == null)
                {
                    LogLoadFailure("加载资源", address, handle.OperationException);
                    ReleaseHandle(handle);
                    return null;
                }

                TrackAssetHandle(asset, handle, address, typeof(T));
                return asset;
            }
            catch (OperationCanceledException)
            {
                ReleaseHandle(handle);
                return null;
            }
            catch (Exception e)
            {
                LogLoadFailure("加载资源", address, e);
                ReleaseHandle(handle);
                return null;
            }
            finally
            {
                if (_pendingOperationCount > 0)
                {
                    _pendingOperationCount--;
                }
            }
        }

     
        /// <summary>
        /// 异步实例化 GameObject (专用于 Prefab 预制体)
        /// Addressables.InstantiateAsync 性能优于 Load + 原生 Instantiate
        /// </summary>
        /// <param name="address">预制体可寻址路径</param>
        /// <param name="parent">父节点</param>
        /// <param name="instantiateInWorldSpace">是否保持世界坐标</param>
        /// <returns>实例化出的 GameObject</returns>
        public async UniTask<GameObject> InstantiateAsync(string address, Transform parent = null, bool instantiateInWorldSpace = false)
        {
            return await InstantiateAsync(address, parent, instantiateInWorldSpace, default(CancellationToken));
        }

        public async UniTask<GameObject> InstantiateAsync(string address, Transform parent, CancellationToken cancellationToken)
        {
            return await InstantiateAsync(address, parent, false, cancellationToken);
        }

        public async UniTask<GameObject> InstantiateAsync(
            string address,
            Transform parent,
            bool instantiateInWorldSpace,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(address))
            {
                Log.Error("[Resource] InstantiateAsync 失败：address 为空。");
                return null;
            }

            if (!await EnsureInitializedAsync(cancellationToken))
            {
                return null;
            }

            AsyncOperationHandle<GameObject> handle = default;
            _pendingOperationCount++;

            try
            {
                handle = Addressables.InstantiateAsync(address, parent, instantiateInWorldSpace);
                GameObject instance = await handle.ToUniTask(cancellationToken: cancellationToken);

                if (_isDestroyed)
                {
                    ReleaseInstanceHandle(handle);
                    return null;
                }

                if (handle.Status != AsyncOperationStatus.Succeeded || instance == null)
                {
                    LogLoadFailure("实例化资源", address, handle.OperationException);
                    ReleaseHandle(handle);
                    return null;
                }

                instance.name = instance.name.Replace("(Clone)", "");
                _instanceHandles[instance] = new InstanceHandleRecord(handle, address);
                return instance;
            }
            catch (OperationCanceledException)
            {
                ReleaseInstanceHandle(handle);
                return null;
            }
            catch (Exception e)
            {
                LogLoadFailure("实例化资源", address, e);
                ReleaseInstanceHandle(handle);
                return null;
            }
            finally
            {
                if (_pendingOperationCount > 0)
                {
                    _pendingOperationCount--;
                }
            }
        }


        // ==========================================
        // API: 内存释放 (解决内存泄漏的核心)
        // ==========================================

        /// <summary>
        /// 释放通过 LoadAssetAsync 加载的数据资源 (Sprite, AudioClip 等)
        /// </summary>
        public void ReleaseAsset(object asset)
        {
            if (asset == null) return;

            if (!(asset is UnityEngine.Object unityAsset))
            {
                Log.Warning($"[Resource] ReleaseAsset 忽略非 UnityEngine.Object 对象: {asset.GetType().Name}");
                return;
            }

            if (!_assetHandles.TryGetValue(unityAsset, out var handles) || handles.Count == 0)
            {
                Log.Warning($"[Resource] ReleaseAsset 找不到资源句柄，可能重复释放或资源不是由 ResourceModule 加载: {GetSafeObjectName(unityAsset)}");
                return;
            }

            AssetHandleRecord record = handles.Pop();
            ReleaseHandle(record.Handle);

            if (handles.Count == 0)
            {
                _assetHandles.Remove(unityAsset);
            }
        }

        /// <summary>
        /// 销毁并释放通过 InstantiateAsync 实例化的 GameObject
        /// 极其重要：千万不要对 Addressables 实例化的对象直接调用 GameObject.Destroy()！
        /// </summary>
        public void ReleaseInstance(GameObject instance)
        {
            if (ReferenceEquals(instance, null)) return;

            if (!_instanceHandles.TryGetValue(instance, out var record))
            {
                Log.Warning($"[Resource] ReleaseInstance 找不到实例句柄，改用 Destroy 清理对象: {GetSafeObjectName(instance)}");
                if (instance != null)
                {
                    UnityEngine.Object.Destroy(instance);
                }
                return;
            }

            _instanceHandles.Remove(instance);
            ReleaseInstanceHandle(record.Handle);
        }

        /// <summary>
        /// 主动释放所有已追踪资源。通常只在切换大流程或退出游戏时调用。
        /// </summary>
        public void ReleaseAll()
        {
            ReleaseAllInstances();
            ReleaseAllAssets();
        }

        public ResourceUsageSnapshot GetUsageSnapshot()
        {
            return new ResourceUsageSnapshot(
                _isInitialized,
                _pendingOperationCount,
                CountTrackedAssetHandles(),
                _assetHandles.Count,
                _instanceHandles.Count);
        }

        private void TrackAssetHandle(UnityEngine.Object asset, AsyncOperationHandle handle, string address, Type assetType)
        {
            if (!_assetHandles.TryGetValue(asset, out var handles))
            {
                handles = new Stack<AssetHandleRecord>();
                _assetHandles[asset] = handles;
            }

            handles.Push(new AssetHandleRecord(handle, address, assetType));
        }

        private void ReleaseAllAssets()
        {
            foreach (var handles in _assetHandles.Values)
            {
                while (handles.Count > 0)
                {
                    ReleaseHandle(handles.Pop().Handle);
                }
            }

            _assetHandles.Clear();
        }

        private void ReleaseAllInstances()
        {
            foreach (var record in _instanceHandles.Values)
            {
                ReleaseInstanceHandle(record.Handle);
            }

            _instanceHandles.Clear();
        }

        private int CountTrackedAssetHandles()
        {
            int count = 0;
            foreach (var handles in _assetHandles.Values)
            {
                count += handles.Count;
            }

            return count;
        }

        private static void ReleaseHandle(AsyncOperationHandle handle)
        {
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
        }

        private static void ReleaseInstanceHandle(AsyncOperationHandle<GameObject> handle)
        {
            if (handle.IsValid())
            {
                if (handle.IsDone && handle.Status == AsyncOperationStatus.Succeeded)
                {
                    Addressables.ReleaseInstance(handle);
                }
                else
                {
                    Addressables.Release(handle);
                }
            }
        }

        private static void LogLoadFailure(string action, string address, Exception exception)
        {
            string reason = exception != null ? exception.Message : "未知错误";
            Log.Error($"[Resource] {action}失败: {address}, 原因: {reason}");
        }

        private static string GetSafeObjectName(UnityEngine.Object obj)
        {
            return obj != null ? obj.name : "<destroyed>";
        }

        private readonly struct AssetHandleRecord
        {
            public readonly AsyncOperationHandle Handle;
            public readonly string Address;
            public readonly Type AssetType;

            public AssetHandleRecord(AsyncOperationHandle handle, string address, Type assetType)
            {
                Handle = handle;
                Address = address;
                AssetType = assetType;
            }
        }

        private readonly struct InstanceHandleRecord
        {
            public readonly AsyncOperationHandle<GameObject> Handle;
            public readonly string Address;

            public InstanceHandleRecord(AsyncOperationHandle<GameObject> handle, string address)
            {
                Handle = handle;
                Address = address;
            }
        }

        public readonly struct ResourceUsageSnapshot
        {
            public readonly bool IsInitialized;
            public readonly int PendingOperationCount;
            public readonly int AssetHandleCount;
            public readonly int UniqueAssetCount;
            public readonly int InstanceCount;

            public ResourceUsageSnapshot(
                bool isInitialized,
                int pendingOperationCount,
                int assetHandleCount,
                int uniqueAssetCount,
                int instanceCount)
            {
                IsInitialized = isInitialized;
                PendingOperationCount = pendingOperationCount;
                AssetHandleCount = assetHandleCount;
                UniqueAssetCount = uniqueAssetCount;
                InstanceCount = instanceCount;
            }
        }
    }
}
