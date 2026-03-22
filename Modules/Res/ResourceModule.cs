using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Cysharp.Threading.Tasks;

namespace GameFramework.Core
{
    /// <summary>
    /// 全局资源管理模块 (基于 Addressables)
    /// 提供极致简洁的 async/await 异步加载和安全的内存释放机制
    /// </summary>
    public class ResourceModule : IFrameworkModule
    {
        public int Priority => 40; // 优先级排在文件系统和日志之后

        private bool _isInitialized = false;

        public void OnInit()
        {
            // 异步初始化 Addressables
            InitializeAddressables();
        }

        private async void InitializeAddressables()
        {
            try
            {
                var handle = Addressables.InitializeAsync();
                await handle.Task;
                _isInitialized = true;
                Log.Module("Resource", "Addressables 系统初始化完成！");
            }
            catch (Exception e)
            {
                Log.Fatal($"[Resource] Addressables 初始化失败: {e.Message}");
            }
        }

        public void OnUpdate(float deltaTime, float unscaledDeltaTime) { }
        public void OnDestroy() { }

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
            // ...
            // 使用 ToUniTask() 无缝衔接
            return await Addressables.LoadAssetAsync<T>(address).ToUniTask(); 
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
            // ...
            var go = await Addressables.InstantiateAsync(address, parent, instantiateInWorldSpace).ToUniTask();
            go.name = go.name.Replace("(Clone)", "");
            return go;
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
            Addressables.Release(asset);
        }

        /// <summary>
        /// 销毁并释放通过 InstantiateAsync 实例化的 GameObject
        /// 极其重要：千万不要对 Addressables 实例化的对象直接调用 GameObject.Destroy()！
        /// </summary>
        public void ReleaseInstance(GameObject instance)
        {
            if (instance == null) return;
            
            // 底层会自动销毁 GameObject 并减少 Prefab 的引用计数
            Addressables.ReleaseInstance(instance); 
        }
    }
}