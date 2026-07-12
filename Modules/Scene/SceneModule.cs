using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace GameFramework.Core
{
    /// <summary>
    /// Addressables 场景管理模块。负责并发合并、激活、卸载和句柄所有权。
    /// </summary>
    public class SceneModule : IAsyncFrameworkModule
    {
        private readonly Dictionary<string, AsyncOperationHandle<SceneInstance>> _loadedSceneHandles =
            new Dictionary<string, AsyncOperationHandle<SceneInstance>>(StringComparer.Ordinal);

        private readonly Dictionary<SceneLoadRequestKey, UniTask<SceneLoadResult>> _inflightLoads =
            new Dictionary<SceneLoadRequestKey, UniTask<SceneLoadResult>>();

        private AsyncOperationHandle<SceneInstance> _currentSceneHandle;
        private CancellationTokenSource _lifecycleCts;
        private string _currentSceneAddress;
        private int _pendingLoadCount;
        private bool _isDestroyed;
        private bool _asyncShutdownCompleted;

        public string CurrentSceneAddress => _currentSceneAddress;
        public string CurrentSceneName =>
            _currentSceneHandle.IsValid() && _currentSceneHandle.IsDone
                ? _currentSceneHandle.Result.Scene.name
                : string.Empty;
        public bool IsLoading => _pendingLoadCount > 0;
        public int PendingLoadCount => _pendingLoadCount;
        public int LoadedSceneCount => _loadedSceneHandles.Count + (_currentSceneHandle.IsValid() ? 1 : 0);

        public void OnInit()
        {
            _isDestroyed = false;
            _asyncShutdownCompleted = false;
            _lifecycleCts = new CancellationTokenSource();
            Log.Module("Scene", "场景模块初始化完成。");
        }

        public UniTask OnInitAsync(CancellationToken cancellationToken)
        {
            return UniTask.CompletedTask;
        }

        public void OnUpdate(float deltaTime, float unscaledDeltaTime)
        {
        }

        public async UniTask OnDestroyAsync(CancellationToken cancellationToken)
        {
            _isDestroyed = true;
            _lifecycleCts?.Cancel();

            var handles = new List<AsyncOperationHandle<SceneInstance>>(_loadedSceneHandles.Values);
            if (_currentSceneHandle.IsValid())
            {
                handles.Add(_currentSceneHandle);
            }

            for (int i = handles.Count - 1; i >= 0; i--)
            {
                await UnloadHandleAsync(handles[i], cancellationToken);
            }

            ClearState();
            _asyncShutdownCompleted = true;
        }

        public void OnDestroy()
        {
            _isDestroyed = true;
            _lifecycleCts?.Cancel();

            if (!_asyncShutdownCompleted)
            {
                foreach (AsyncOperationHandle<SceneInstance> handle in _loadedSceneHandles.Values)
                {
                    ReleaseSceneHandle(handle);
                }

                ReleaseSceneHandle(_currentSceneHandle);
            }

            ClearState();
            _lifecycleCts?.Dispose();
            _lifecycleCts = null;
        }

        public async UniTask<SceneInstance> LoadSceneAsync(
            string address,
            LoadSceneMode mode = LoadSceneMode.Additive,
            bool setActive = true)
        {
            SceneLoadResult result = await TryLoadSceneAsync(address, mode, setActive);
            return result.SceneInstance;
        }

        /// <summary>
        /// 兼容旧接口：场景始终立即激活，setActive 只控制是否成为 SceneManager.activeScene。
        /// 需要延迟激活时使用 SceneLoadOptions 重载。
        /// </summary>
        public UniTask<SceneLoadResult> TryLoadSceneAsync(
            string address,
            LoadSceneMode mode = LoadSceneMode.Additive,
            bool setActive = true,
            CancellationToken cancellationToken = default)
        {
            return TryLoadSceneAsync(
                address,
                new SceneLoadOptions(mode, activateOnLoad: true, setAsActiveScene: setActive),
                cancellationToken);
        }

        public async UniTask<SceneLoadResult> TryLoadSceneAsync(
            string address,
            SceneLoadOptions options,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return SceneLoadResult.Failed(address, "场景地址为空。");
            }

            if (_isDestroyed || _lifecycleCts == null)
            {
                return SceneLoadResult.Failed(address, "SceneModule 未初始化或已经销毁。");
            }

            var requestKey = new SceneLoadRequestKey(address, options);
            if (_inflightLoads.TryGetValue(requestKey, out UniTask<SceneLoadResult> inflight))
            {
                return await inflight;
            }

            UniTask<SceneLoadResult> loadTask = LoadSceneInternalAsync(
                    address,
                    options,
                    cancellationToken)
                .Preserve();
            _inflightLoads[requestKey] = loadTask;

            try
            {
                return await loadTask;
            }
            finally
            {
                _inflightLoads.Remove(requestKey);
            }
        }

        public async UniTask SwitchSceneAsync(string address)
        {
            await TrySwitchSceneAsync(address);
        }

        public async UniTask<bool> TrySwitchSceneAsync(
            string address,
            bool setActive = true,
            CancellationToken cancellationToken = default)
        {
            SceneLoadResult result = await TryLoadSceneAsync(
                address,
                LoadSceneMode.Single,
                setActive,
                cancellationToken);
            return result.Success;
        }

        public async UniTask<bool> ActivateSceneAsync(
            string address,
            bool setAsActiveScene = true,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetSceneHandle(address, out AsyncOperationHandle<SceneInstance> handle) ||
                !handle.IsValid() ||
                handle.Status != AsyncOperationStatus.Succeeded)
            {
                Log.Warning($"[Scene] 找不到可激活的场景: {address}");
                return false;
            }

            try
            {
                if (!handle.Result.Scene.isLoaded)
                {
                    await handle.Result.ActivateAsync().ToUniTask(cancellationToken: cancellationToken);
                }

                if (setAsActiveScene && handle.Result.Scene.isLoaded)
                {
                    SceneManager.SetActiveScene(handle.Result.Scene);
                }

                return handle.Result.Scene.isLoaded;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception exception)
            {
                Log.Error($"[Scene] 激活场景失败: {address}, {exception.Message}");
                return false;
            }
        }

        public async UniTask UnloadSceneAsync(SceneInstance sceneInstance)
        {
            await TryUnloadSceneAsync(sceneInstance);
        }

        public async UniTask<bool> TryUnloadSceneAsync(
            SceneInstance sceneInstance,
            CancellationToken cancellationToken = default)
        {
            string address = FindAddress(sceneInstance);
            if (string.IsNullOrEmpty(address))
            {
                Log.Warning("[Scene] 找不到场景对应的句柄，无法通过 SceneModule 卸载。");
                return false;
            }

            return await TryUnloadSceneAsync(address, cancellationToken);
        }

        public async UniTask UnloadSceneAsync()
        {
            if (!string.IsNullOrEmpty(_currentSceneAddress))
            {
                await TryUnloadSceneAsync(_currentSceneAddress);
            }
        }

        public async UniTask<bool> TryUnloadSceneAsync(
            string address,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(address) ||
                !TryGetSceneHandle(address, out AsyncOperationHandle<SceneInstance> handle))
            {
                Log.Warning($"[Scene] 未找到已加载场景: {address}");
                return false;
            }

            try
            {
                await Addressables.UnloadSceneAsync(handle, autoReleaseHandle: true)
                    .ToUniTask(cancellationToken: cancellationToken);
                RemoveTrackedHandle(address);
                GameApp.Event?.Broadcast(new SceneUnloadedEvent(address));
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception exception)
            {
                Log.Error($"[Scene] 卸载场景失败: {address}, 原因: {exception.Message}");
                return false;
            }
        }

        public SceneUsageSnapshot GetUsageSnapshot()
        {
            return new SceneUsageSnapshot(
                _currentSceneAddress,
                _pendingLoadCount,
                LoadedSceneCount);
        }

        private async UniTask<SceneLoadResult> LoadSceneInternalAsync(
            string address,
            SceneLoadOptions options,
            CancellationToken cancellationToken)
        {
            using (CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                       cancellationToken,
                       _lifecycleCts.Token))
            {
                CancellationToken token = linkedCts.Token;
                if (!await GameApp.Res.EnsureInitializedAsync(token))
                {
                    token.ThrowIfCancellationRequested();
                    return SceneLoadResult.Failed(address, "资源模块初始化失败。");
                }

                if (options.Mode == LoadSceneMode.Additive &&
                    _loadedSceneHandles.TryGetValue(address, out AsyncOperationHandle<SceneInstance> existingHandle) &&
                    existingHandle.IsValid() &&
                    existingHandle.IsDone &&
                    existingHandle.Status == AsyncOperationStatus.Succeeded)
                {
                    Log.Warning($"[Scene] Additive 场景已经加载，直接返回现有实例: {address}");
                    return SceneLoadResult.Succeeded(
                        address,
                        existingHandle.Result,
                        options.Mode,
                        isNewLoad: false,
                        requiresActivation: !existingHandle.Result.Scene.isLoaded);
                }

                AsyncOperationHandle<SceneInstance> previousSingleHandle = _currentSceneHandle;
                AsyncOperationHandle<SceneInstance> handle = default;
                _pendingLoadCount++;
                BroadcastStarted(address, options.Mode);

                try
                {
                    handle = Addressables.LoadSceneAsync(
                        address,
                        options.Mode,
                        options.ActivateOnLoad,
                        options.Priority);
                    SceneInstance sceneInstance = await handle.ToUniTask(cancellationToken: token);

                    if (_isDestroyed)
                    {
                        ReleaseSceneHandle(handle);
                        return SceneLoadResult.Failed(address, "SceneModule 已销毁。");
                    }

                    if (handle.Status != AsyncOperationStatus.Succeeded || !sceneInstance.Scene.IsValid())
                    {
                        string reason = GetOperationError(handle);
                        ReleaseHandle(handle);
                        BroadcastCompleted(address, options.Mode, false, reason);
                        return SceneLoadResult.Failed(address, reason);
                    }

                    if (options.SetAsActiveScene && options.ActivateOnLoad && sceneInstance.Scene.isLoaded)
                    {
                        SceneManager.SetActiveScene(sceneInstance.Scene);
                    }

                    if (options.Mode == LoadSceneMode.Single)
                    {
                        ReleasePreviousSingleHandle(previousSingleHandle);
                        ReleaseLoadedSceneHandles();
                        _loadedSceneHandles.Clear();
                        _currentSceneHandle = handle;
                        _currentSceneAddress = address;
                    }
                    else
                    {
                        _loadedSceneHandles[address] = handle;
                    }

                    BroadcastCompleted(address, options.Mode, true, null);
                    return SceneLoadResult.Succeeded(
                        address,
                        sceneInstance,
                        options.Mode,
                        isNewLoad: true,
                        requiresActivation: !options.ActivateOnLoad);
                }
                catch (OperationCanceledException)
                {
                    ReleaseSceneHandle(handle);
                    BroadcastCompleted(address, options.Mode, false, "加载被取消。");
                    return SceneLoadResult.Failed(address, "加载被取消。");
                }
                catch (Exception exception)
                {
                    ReleaseSceneHandle(handle);
                    BroadcastCompleted(address, options.Mode, false, exception.Message);
                    return SceneLoadResult.Failed(address, exception.Message);
                }
                finally
                {
                    _pendingLoadCount = Math.Max(0, _pendingLoadCount - 1);
                }
            }
        }

        private async UniTask UnloadHandleAsync(
            AsyncOperationHandle<SceneInstance> handle,
            CancellationToken cancellationToken)
        {
            if (!handle.IsValid())
            {
                return;
            }

            try
            {
                if (handle.IsDone && handle.Status == AsyncOperationStatus.Succeeded)
                {
                    await Addressables.UnloadSceneAsync(handle, autoReleaseHandle: true)
                        .ToUniTask(cancellationToken: cancellationToken);
                }
                else
                {
                    Addressables.Release(handle);
                }
            }
            catch (OperationCanceledException)
            {
                ReleaseSceneHandle(handle);
            }
            catch (Exception exception)
            {
                Log.Warning($"[Scene] 关闭阶段卸载场景失败: {exception.Message}");
                ReleaseSceneHandle(handle);
            }
        }

        private bool TryGetSceneHandle(string address, out AsyncOperationHandle<SceneInstance> handle)
        {
            if (_loadedSceneHandles.TryGetValue(address, out handle))
            {
                return true;
            }

            if (string.Equals(address, _currentSceneAddress, StringComparison.Ordinal) &&
                _currentSceneHandle.IsValid())
            {
                handle = _currentSceneHandle;
                return true;
            }

            handle = default;
            return false;
        }

        private void RemoveTrackedHandle(string address)
        {
            _loadedSceneHandles.Remove(address);
            if (string.Equals(address, _currentSceneAddress, StringComparison.Ordinal))
            {
                _currentSceneHandle = default;
                _currentSceneAddress = null;
            }
        }

        private void ClearState()
        {
            _loadedSceneHandles.Clear();
            _inflightLoads.Clear();
            _currentSceneHandle = default;
            _currentSceneAddress = null;
            _pendingLoadCount = 0;
        }

        private void ReleasePreviousSingleHandle(AsyncOperationHandle<SceneInstance> previousHandle)
        {
            if (previousHandle.IsValid() && !previousHandle.Equals(_currentSceneHandle))
            {
                ReleaseHandle(previousHandle);
            }
        }

        private void ReleaseLoadedSceneHandles()
        {
            foreach (AsyncOperationHandle<SceneInstance> handle in _loadedSceneHandles.Values)
            {
                ReleaseHandle(handle);
            }
        }

        private string FindAddress(SceneInstance sceneInstance)
        {
            foreach (KeyValuePair<string, AsyncOperationHandle<SceneInstance>> pair in _loadedSceneHandles)
            {
                if (pair.Value.IsValid() &&
                    pair.Value.IsDone &&
                    pair.Value.Result.Scene == sceneInstance.Scene)
                {
                    return pair.Key;
                }
            }

            if (_currentSceneHandle.IsValid() &&
                _currentSceneHandle.IsDone &&
                _currentSceneHandle.Result.Scene == sceneInstance.Scene)
            {
                return _currentSceneAddress;
            }

            return null;
        }

        private void BroadcastStarted(string address, LoadSceneMode mode)
        {
            GameApp.Event?.Broadcast(new SceneLoadStartedEvent(address, mode));
        }

        private void BroadcastCompleted(string address, LoadSceneMode mode, bool success, string error)
        {
            GameApp.Event?.Broadcast(new SceneLoadCompletedEvent(address, mode, success, error));
        }

        private static void ReleaseHandle(AsyncOperationHandle<SceneInstance> handle)
        {
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
        }

        private static void ReleaseSceneHandle(AsyncOperationHandle<SceneInstance> handle)
        {
            if (!handle.IsValid())
            {
                return;
            }

            if (handle.IsDone && handle.Status == AsyncOperationStatus.Succeeded)
            {
                Addressables.UnloadSceneAsync(handle, autoReleaseHandle: true);
            }
            else
            {
                Addressables.Release(handle);
            }
        }

        private static string GetOperationError(AsyncOperationHandle<SceneInstance> handle)
        {
            return handle.OperationException != null ? handle.OperationException.Message : "未知错误";
        }

        private readonly struct SceneLoadRequestKey : IEquatable<SceneLoadRequestKey>
        {
            private readonly string _address;
            private readonly SceneLoadOptions _options;

            public SceneLoadRequestKey(string address, SceneLoadOptions options)
            {
                _address = address;
                _options = options;
            }

            public bool Equals(SceneLoadRequestKey other)
            {
                return string.Equals(_address, other._address, StringComparison.Ordinal) &&
                       _options.Equals(other._options);
            }

            public override bool Equals(object obj)
            {
                return obj is SceneLoadRequestKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((_address != null ? _address.GetHashCode() : 0) * 397) ^ _options.GetHashCode();
                }
            }
        }
    }

    public readonly struct SceneLoadOptions : IEquatable<SceneLoadOptions>
    {
        public SceneLoadOptions(
            LoadSceneMode mode,
            bool activateOnLoad = true,
            bool setAsActiveScene = true,
            int priority = 100)
        {
            Mode = mode;
            ActivateOnLoad = activateOnLoad;
            SetAsActiveScene = setAsActiveScene;
            Priority = priority;
        }

        public LoadSceneMode Mode { get; }
        public bool ActivateOnLoad { get; }
        public bool SetAsActiveScene { get; }
        public int Priority { get; }

        public bool Equals(SceneLoadOptions other)
        {
            return Mode == other.Mode &&
                   ActivateOnLoad == other.ActivateOnLoad &&
                   SetAsActiveScene == other.SetAsActiveScene &&
                   Priority == other.Priority;
        }

        public override bool Equals(object obj)
        {
            return obj is SceneLoadOptions other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (int)Mode;
                hashCode = (hashCode * 397) ^ ActivateOnLoad.GetHashCode();
                hashCode = (hashCode * 397) ^ SetAsActiveScene.GetHashCode();
                hashCode = (hashCode * 397) ^ Priority;
                return hashCode;
            }
        }
    }

    public readonly struct SceneLoadResult
    {
        public readonly bool Success;
        public readonly string Address;
        public readonly SceneInstance SceneInstance;
        public readonly LoadSceneMode Mode;
        public readonly bool IsNewLoad;
        public readonly bool RequiresActivation;
        public readonly string Error;

        private SceneLoadResult(
            bool success,
            string address,
            SceneInstance sceneInstance,
            LoadSceneMode mode,
            bool isNewLoad,
            bool requiresActivation,
            string error)
        {
            Success = success;
            Address = address;
            SceneInstance = sceneInstance;
            Mode = mode;
            IsNewLoad = isNewLoad;
            RequiresActivation = requiresActivation;
            Error = error;
        }

        public static SceneLoadResult Succeeded(
            string address,
            SceneInstance sceneInstance,
            LoadSceneMode mode,
            bool isNewLoad,
            bool requiresActivation)
        {
            return new SceneLoadResult(
                true,
                address,
                sceneInstance,
                mode,
                isNewLoad,
                requiresActivation,
                null);
        }

        public static SceneLoadResult Failed(string address, string error)
        {
            return new SceneLoadResult(false, address, default, default, false, false, error);
        }
    }

    public readonly struct SceneUsageSnapshot
    {
        public readonly string CurrentSceneAddress;
        public readonly int PendingLoadCount;
        public readonly int LoadedSceneCount;
        public bool IsLoading => PendingLoadCount > 0;

        public SceneUsageSnapshot(
            string currentSceneAddress,
            int pendingLoadCount,
            int loadedSceneCount)
        {
            CurrentSceneAddress = currentSceneAddress;
            PendingLoadCount = pendingLoadCount;
            LoadedSceneCount = loadedSceneCount;
        }
    }

    public readonly struct SceneLoadStartedEvent
    {
        public readonly string Address;
        public readonly LoadSceneMode Mode;

        public SceneLoadStartedEvent(string address, LoadSceneMode mode)
        {
            Address = address;
            Mode = mode;
        }
    }

    public readonly struct SceneLoadCompletedEvent
    {
        public readonly string Address;
        public readonly LoadSceneMode Mode;
        public readonly bool Success;
        public readonly string Error;

        public SceneLoadCompletedEvent(string address, LoadSceneMode mode, bool success, string error)
        {
            Address = address;
            Mode = mode;
            Success = success;
            Error = error;
        }
    }

    public readonly struct SceneUnloadedEvent
    {
        public readonly string Address;

        public SceneUnloadedEvent(string address)
        {
            Address = address;
        }
    }
}
