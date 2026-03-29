using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;

namespace GameFramework.Core
{
    /// <summary>
    /// 场景管理模块 (基于 Addressables)
    /// 提供场景加载、切换和卸载的封装
    /// </summary>
    public class SceneModule : IFrameworkModule
    {
        public int Priority => 45;

        private AsyncOperationHandle<SceneInstance> _currentSceneHandle;
        private string _currentSceneName;

        public void OnInit()
        {
            Log.Module("Scene", "场景模块初始化完成");
        }

        public void OnUpdate(float deltaTime, float unscaledDeltaTime) { }

        public void OnDestroy()
        {
            if (_currentSceneHandle.IsValid())
            {
                Addressables.Release(_currentSceneHandle);
            }
        }

        /// <summary>
        /// 异步加载场景
        /// </summary>
        public async UniTask<SceneInstance> LoadSceneAsync(string address, LoadSceneMode mode = LoadSceneMode.Additive, bool setActive = true)
        {
            var handle = Addressables.LoadSceneAsync(address, mode);
            var sceneInstance = await handle.ToUniTask();

            if (mode == LoadSceneMode.Single)
            {
                _currentSceneHandle = handle;
                _currentSceneName = address;
            }

            if (setActive && sceneInstance.Scene.isLoaded)
            {
                SceneManager.SetActiveScene(sceneInstance.Scene);
            }

            return sceneInstance;
        }

        /// <summary>
        /// 异步切换场景 (单场景模式)
        /// </summary>
        public async UniTask SwitchSceneAsync(string address)
        {
            await LoadSceneAsync(address, LoadSceneMode.Single);
        }

        /// <summary>
        /// 卸载场景
        /// </summary>
        public async UniTask UnloadSceneAsync(SceneInstance sceneInstance)
        {
            await Addressables.UnloadSceneAsync(sceneInstance).ToUniTask();
        }
        
        /// <summary>
        /// 卸载场景
        /// </summary>
        public async UniTask UnloadSceneAsync()
        {
            // 卸载当前场景
            if (_currentSceneHandle.IsValid())
            {
                await Addressables.UnloadSceneAsync(_currentSceneHandle).ToUniTask();
            }
        }

        /// <summary>
        /// 获取当前场景名称
        /// </summary>
        public string CurrentSceneName => _currentSceneName;
    }
}
