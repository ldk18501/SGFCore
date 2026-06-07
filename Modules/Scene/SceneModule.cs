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
    /// Addressables 场景管理模块，负责场景加载、切换、卸载和句柄追踪。
    /// </summary>
    public class SceneModule : IFrameworkModule
    {
        public int Priority => 44;

        private readonly Dictionary<string, AsyncOperationHandle<SceneInstance>> _loadedSceneHandles =
            new Dictionary<string, AsyncOperationHandle<SceneInstance>>();

        private AsyncOperationHandle<SceneInstance> _currentSceneHandle;
        private string _currentSceneAddress;
        private bool _isLoading;
        private bool _isDestroyed;

        public string CurrentSceneAddress => _currentSceneAddress;
        public string CurrentSceneName => _currentSceneAddress;
        public bool IsLoading => _isLoading;
        public int LoadedSceneCount => _loadedSceneHandles.Count;

        public void OnInit()
        {
            _isDestroyed = false;
            Log.Module("Scene", "场景模块初始化完成。");
        }

        public void OnUpdate(float deltaTime, float unscaledDeltaTime)
        {
        }

        public void OnDestroy()
        {
            _isDestroyed = true;
            foreach (var handle in _loadedSceneHandles.Values)
            {
                ReleaseSceneHandle(handle);
            }

            ReleaseSceneHandle(_currentSceneHandle);

            _loadedSceneHandles.Clear();
            _currentSceneHandle = default;
            _currentSceneAddress = null;
            _isLoading = false;
        }

        public async UniTask<SceneInstance> LoadSceneAsync(
            string address,
            LoadSceneMode mode = LoadSceneMode.Additive,
            bool setActive = true)
        {
            SceneLoadResult result = await TryLoadSceneAsync(address, mode, setActive);
            return result.SceneInstance;
        }

        public async UniTask<SceneLoadResult> TryLoadSceneAsync(
            string address,
            LoadSceneMode mode = LoadSceneMode.Additive,
            bool setActive = true,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return SceneLoadResult.Failed(address, "场景地址为空。");
            }

            if (_isDestroyed)
            {
                return SceneLoadResult.Failed(address, "SceneModule 已销毁。");
            }

            if (!await GameApp.Res.EnsureInitializedAsync(cancellationToken))
            {
                return SceneLoadResult.Failed(address, "资源模块初始化失败。");
            }

            if (mode == LoadSceneMode.Additive && _loadedSceneHandles.TryGetValue(address, out var existingHandle))
            {
                if (existingHandle.IsValid() && existingHandle.IsDone)
                {
                    Log.Warning($"[Scene] Additive 场景已经加载，直接返回现有实例: {address}");
                    return SceneLoadResult.Succeeded(address, existingHandle.Result, mode, false);
                }
            }

            AsyncOperationHandle<SceneInstance> previousSingleHandle = _currentSceneHandle;
            AsyncOperationHandle<SceneInstance> handle = default;
            _isLoading = true;
            BroadcastStarted(address, mode);

            try
            {
                handle = Addressables.LoadSceneAsync(address, mode);
                SceneInstance sceneInstance = await handle.ToUniTask(cancellationToken: cancellationToken);

                if (_isDestroyed)
                {
                    ReleaseSceneHandle(handle);
                    return SceneLoadResult.Failed(address, "SceneModule 已销毁。");
                }

                if (handle.Status != AsyncOperationStatus.Succeeded || !sceneInstance.Scene.IsValid())
                {
                    string reason = GetOperationError(handle);
                    ReleaseHandle(handle);
                    BroadcastCompleted(address, mode, false, reason);
                    return SceneLoadResult.Failed(address, reason);
                }

                if (setActive && sceneInstance.Scene.isLoaded)
                {
                    SceneManager.SetActiveScene(sceneInstance.Scene);
                }

                if (mode == LoadSceneMode.Single)
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

                BroadcastCompleted(address, mode, true, null);
                return SceneLoadResult.Succeeded(address, sceneInstance, mode, true);
            }
            catch (OperationCanceledException)
            {
                ReleaseSceneHandle(handle);
                BroadcastCompleted(address, mode, false, "加载被取消。");
                return SceneLoadResult.Failed(address, "加载被取消。");
            }
            catch (Exception e)
            {
                ReleaseSceneHandle(handle);
                BroadcastCompleted(address, mode, false, e.Message);
                return SceneLoadResult.Failed(address, e.Message);
            }
            finally
            {
                _isLoading = false;
            }
        }

        public async UniTask SwitchSceneAsync(string address)
        {
            await TrySwitchSceneAsync(address);
        }

        public async UniTask<bool> TrySwitchSceneAsync(
            string address,
            bool setActive = true,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SceneLoadResult result = await TryLoadSceneAsync(address, LoadSceneMode.Single, setActive, cancellationToken);
            return result.Success;
        }

        public async UniTask UnloadSceneAsync(SceneInstance sceneInstance)
        {
            await TryUnloadSceneAsync(sceneInstance);
        }

        public async UniTask<bool> TryUnloadSceneAsync(
            SceneInstance sceneInstance,
            CancellationToken cancellationToken = default(CancellationToken))
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
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return false;
            }

            if (!_loadedSceneHandles.TryGetValue(address, out var handle))
            {
                if (address == _currentSceneAddress && _currentSceneHandle.IsValid())
                {
                    handle = _currentSceneHandle;
                }
                else
                {
                    Log.Warning($"[Scene] 未找到已加载场景: {address}");
                    return false;
                }
            }

            try
            {
                await Addressables.UnloadSceneAsync(handle).ToUniTask(cancellationToken: cancellationToken);
                _loadedSceneHandles.Remove(address);

                if (address == _currentSceneAddress)
                {
                    _currentSceneHandle = default;
                    _currentSceneAddress = null;
                }

                GameApp.Event?.Broadcast(new SceneUnloadedEvent(address));
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception e)
            {
                Log.Error($"[Scene] 卸载场景失败: {address}, 原因: {e.Message}");
                return false;
            }
        }

        public SceneUsageSnapshot GetUsageSnapshot()
        {
            return new SceneUsageSnapshot(_currentSceneAddress, _isLoading, _loadedSceneHandles.Count);
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
            foreach (var handle in _loadedSceneHandles.Values)
            {
                ReleaseHandle(handle);
            }
        }

        private string FindAddress(SceneInstance sceneInstance)
        {
            foreach (var pair in _loadedSceneHandles)
            {
                if (pair.Value.IsValid() && pair.Value.IsDone && pair.Value.Result.Scene == sceneInstance.Scene)
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
                Addressables.UnloadSceneAsync(handle);
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
    }

    public readonly struct SceneLoadResult
    {
        public readonly bool Success;
        public readonly string Address;
        public readonly SceneInstance SceneInstance;
        public readonly LoadSceneMode Mode;
        public readonly bool IsNewLoad;
        public readonly string Error;

        private SceneLoadResult(
            bool success,
            string address,
            SceneInstance sceneInstance,
            LoadSceneMode mode,
            bool isNewLoad,
            string error)
        {
            Success = success;
            Address = address;
            SceneInstance = sceneInstance;
            Mode = mode;
            IsNewLoad = isNewLoad;
            Error = error;
        }

        public static SceneLoadResult Succeeded(
            string address,
            SceneInstance sceneInstance,
            LoadSceneMode mode,
            bool isNewLoad)
        {
            return new SceneLoadResult(true, address, sceneInstance, mode, isNewLoad, null);
        }

        public static SceneLoadResult Failed(string address, string error)
        {
            return new SceneLoadResult(false, address, default, default, false, error);
        }
    }

    public readonly struct SceneUsageSnapshot
    {
        public readonly string CurrentSceneAddress;
        public readonly bool IsLoading;
        public readonly int LoadedSceneCount;

        public SceneUsageSnapshot(string currentSceneAddress, bool isLoading, int loadedSceneCount)
        {
            CurrentSceneAddress = currentSceneAddress;
            IsLoading = isLoading;
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
