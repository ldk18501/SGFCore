using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameFramework.Core
{
    /// <summary>
    /// 全局配置表管理模块
    /// 负责统一下载、读取、解密二进制配置表，并分发给对应的解析类
    /// </summary>
    public class ConfigModule : IFrameworkModule
    {
        // 优先级排在 ResourceModule (40) 之后
        public int Priority => 45; 

        // 注册表：配置文件名 -> 对应的 Load 方法
        private readonly Dictionary<string, Action<byte[]>> _loadMap = new Dictionary<string, Action<byte[]>>();

        public void OnInit()
        {
            Log.Module("Config", "配置表模块初始化完成。");
        }

        public void OnUpdate(float deltaTime, float unscaledDeltaTime) { }
        
        public void OnDestroy() 
        {
            _loadMap.Clear();
        }

        /// <summary>
        /// 注册配置表的解析委托（通常在游戏启动时调用）
        /// </summary>
        public void RegisterConfig(string configName, Action<byte[]> loadMethod)
        {
            if (!_loadMap.ContainsKey(configName))
            {
                _loadMap.Add(configName, loadMethod);
            }
        }

        /// <summary>
        /// 异步加载单个配置文件
        /// </summary>
        /// <param name="address">Addressables 资源路径 (例如 "Assets/Configs/ItemValueConf.bytes")</param>
        /// <param name="configName">注册时的表名 (例如 "ItemValueConf")</param>
        public async UniTask LoadConfigAsync(string address, string configName)
        {
            if (!_loadMap.TryGetValue(configName, out var loadAction))
            {
                Log.Error($"[Config] 未注册该表的解析方法: {configName}");
                return;
            }

            // 1. 通过资源模块加载 TextAsset (二进制文件在 Unity 中通常作为 TextAsset 加载)
            TextAsset textAsset = await GameApp.Res.LoadAssetAsync<TextAsset>(address);
            
            if (textAsset != null)
            {
                // 2. 将字节数组交给具体的生成的 Config 类去解析
                loadAction.Invoke(textAsset.bytes);
                
                // 3. 【极其重要】解析完成后，立刻释放 TextAsset，否则这些 byte[] 会永远残留在内存中！
                GameApp.Res.ReleaseAsset(textAsset);
                
                Log.Info($"[Config] 配置表加载并解析成功: {configName}");
            }
            else
            {
                Log.Error($"[Config] 无法加载配置表资源: {address}");
            }
        }

        /// <summary>
        /// 批量异步加载配置表 (并行加载，速度极快)
        /// </summary>
        public async UniTask LoadConfigsBatchAsync(Dictionary<string, string> configAddressMap)
        {
            List<UniTask> tasks = new List<UniTask>();
            
            foreach (var kvp in configAddressMap)
            {
                // kvp.Key = configName, kvp.Value = address
                tasks.Add(LoadConfigAsync(kvp.Value, kvp.Key));
            }

            // 等待所有表全部加载解析完成
            await UniTask.WhenAll(tasks);
            Log.Module("Config", "<color=#00FF00>所有配置表批量加载完成！</color>");
        }
        
        /// <summary>
        /// 极简批量异步加载配置表
        /// 前提约定：Addressables 里的资源 Address 必须和注册的 configName 一致
        /// </summary>
        /// <param name="configNames">需要加载的表名数组</param>
        public async UniTask LoadConfigsAsync(params string[] configNames)
        {
            List<UniTask> tasks = new List<UniTask>();
            foreach (var name in configNames)
            {
                // 参数1是 Address，参数2是配置表名，这里约定它们同名
                tasks.Add(LoadConfigAsync(name, name));
            }

            await UniTask.WhenAll(tasks);
            Log.Module("Config", $"<color=#00FF00>成功批量加载 {configNames.Length} 张配置表！</color>");
        }
    }
}