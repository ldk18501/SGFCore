using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameFramework.Core
{
    /// <summary>
    /// 全局配置表管理模块，负责加载二进制配置并分发给生成代码的 Load(byte[])。
    /// </summary>
    public class ConfigModule : IFrameworkModule
    {
        private readonly Dictionary<string, ConfigRegistration> _loadMap =
            new Dictionary<string, ConfigRegistration>();

        private readonly HashSet<string> _loadedConfigs = new HashSet<string>();

        public int RegisteredCount => _loadMap.Count;
        public int LoadedCount => _loadedConfigs.Count;

        public void OnInit()
        {
            Log.Module("Config", "配置表模块初始化完成。");
        }

        public void OnUpdate(float deltaTime, float unscaledDeltaTime)
        {
        }

        public void OnDestroy()
        {
            _loadMap.Clear();
            _loadedConfigs.Clear();
        }

        public void RegisterConfig(string configName, Action<byte[]> loadMethod)
        {
            TryRegisterConfig(configName, loadMethod, replaceExisting: true);
        }

        public bool TryRegisterConfig(string configName, Action<byte[]> loadMethod, bool replaceExisting = false)
        {
            if (string.IsNullOrWhiteSpace(configName))
            {
                Log.Error("[Config] 注册失败：配置名为空。");
                return false;
            }

            if (loadMethod == null)
            {
                Log.Error($"[Config] 注册失败：{configName} 的解析方法为空。");
                return false;
            }

            if (_loadMap.ContainsKey(configName) && !replaceExisting)
            {
                Log.Warning($"[Config] 配置表已经注册，已忽略: {configName}");
                return false;
            }

            _loadMap[configName] = new ConfigRegistration(configName, loadMethod);
            return true;
        }

        public bool IsRegistered(string configName)
        {
            return !string.IsNullOrEmpty(configName) && _loadMap.ContainsKey(configName);
        }

        public bool IsLoaded(string configName)
        {
            return !string.IsNullOrEmpty(configName) && _loadedConfigs.Contains(configName);
        }

        public async UniTask LoadConfigAsync(string address, string configName)
        {
            await TryLoadConfigAsync(address, configName);
        }

        public async UniTask<ConfigLoadResult> TryLoadConfigAsync(
            string address,
            string configName,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return ConfigLoadResult.Failed(configName, address, "资源地址为空。");
            }

            if (string.IsNullOrWhiteSpace(configName))
            {
                return ConfigLoadResult.Failed(configName, address, "配置名为空。");
            }

            if (!_loadMap.TryGetValue(configName, out ConfigRegistration registration))
            {
                string error = $"未注册该表的解析方法: {configName}";
                Log.Error($"[Config] {error}");
                return ConfigLoadResult.Failed(configName, address, error);
            }

            TextAsset textAsset = null;
            try
            {
                textAsset = await GameApp.Res.LoadAssetAsync<TextAsset>(address, cancellationToken);
                if (textAsset == null)
                {
                    string error = $"无法加载配置表资源: {address}";
                    Log.Error($"[Config] {error}");
                    return ConfigLoadResult.Failed(configName, address, error);
                }

                registration.LoadMethod.Invoke(textAsset.bytes);
                _loadedConfigs.Add(configName);

                Log.Info($"[Config] 配置表加载并解析成功: {configName}");
                GameApp.Event?.Broadcast(new ConfigLoadedEvent(configName, address));
                return ConfigLoadResult.Succeeded(configName, address);
            }
            catch (OperationCanceledException)
            {
                return ConfigLoadResult.Failed(configName, address, "加载被取消。");
            }
            catch (Exception e)
            {
                Log.Error($"[Config] 配置表加载失败: {configName}, 原因: {e.Message}");
                return ConfigLoadResult.Failed(configName, address, e.Message);
            }
            finally
            {
                if (textAsset != null)
                {
                    GameApp.Res.ReleaseAsset(textAsset);
                }
            }
        }

        public async UniTask LoadConfigsBatchAsync(Dictionary<string, string> configAddressMap)
        {
            await TryLoadConfigsBatchAsync(configAddressMap);
        }

        public async UniTask<ConfigBatchLoadResult> TryLoadConfigsBatchAsync(
            Dictionary<string, string> configAddressMap,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (configAddressMap == null || configAddressMap.Count == 0)
            {
                return new ConfigBatchLoadResult(new ConfigLoadResult[0]);
            }

            List<UniTask<ConfigLoadResult>> tasks = new List<UniTask<ConfigLoadResult>>(configAddressMap.Count);
            foreach (var pair in configAddressMap)
            {
                tasks.Add(TryLoadConfigAsync(pair.Value, pair.Key, cancellationToken));
            }

            ConfigLoadResult[] results = await UniTask.WhenAll(tasks);
            ConfigBatchLoadResult batchResult = new ConfigBatchLoadResult(results);

            if (batchResult.Success)
            {
                Log.Module("Config", $"成功批量加载 {batchResult.TotalCount} 张配置表。");
            }
            else
            {
                Log.Error($"[Config] 批量加载完成，但有 {batchResult.FailedCount} 张配置表失败。");
            }

            return batchResult;
        }

        public async UniTask LoadConfigsAsync(params string[] configNames)
        {
            await TryLoadConfigsAsync(configNames);
        }

        public async UniTask<ConfigBatchLoadResult> TryLoadConfigsAsync(params string[] configNames)
        {
            if (configNames == null || configNames.Length == 0)
            {
                return new ConfigBatchLoadResult(new ConfigLoadResult[0]);
            }

            Dictionary<string, string> map = new Dictionary<string, string>(configNames.Length);
            for (int i = 0; i < configNames.Length; i++)
            {
                string name = configNames[i];
                if (!string.IsNullOrWhiteSpace(name))
                {
                    map[name] = name;
                }
            }

            return await TryLoadConfigsBatchAsync(map);
        }

        private readonly struct ConfigRegistration
        {
            public readonly string ConfigName;
            public readonly Action<byte[]> LoadMethod;

            public ConfigRegistration(string configName, Action<byte[]> loadMethod)
            {
                ConfigName = configName;
                LoadMethod = loadMethod;
            }
        }
    }

    public readonly struct ConfigLoadResult
    {
        public readonly bool Success;
        public readonly string ConfigName;
        public readonly string Address;
        public readonly string Error;

        private ConfigLoadResult(bool success, string configName, string address, string error)
        {
            Success = success;
            ConfigName = configName;
            Address = address;
            Error = error;
        }

        public static ConfigLoadResult Succeeded(string configName, string address)
        {
            return new ConfigLoadResult(true, configName, address, null);
        }

        public static ConfigLoadResult Failed(string configName, string address, string error)
        {
            return new ConfigLoadResult(false, configName, address, error);
        }
    }

    public readonly struct ConfigBatchLoadResult
    {
        public readonly ConfigLoadResult[] Results;
        public readonly int TotalCount;
        public readonly int SucceededCount;
        public readonly int FailedCount;
        public bool Success => FailedCount == 0;

        public ConfigBatchLoadResult(ConfigLoadResult[] results)
        {
            Results = results ?? new ConfigLoadResult[0];
            TotalCount = Results.Length;
            int succeededCount = 0;
            int failedCount = 0;

            for (int i = 0; i < Results.Length; i++)
            {
                if (Results[i].Success)
                {
                    succeededCount++;
                }
                else
                {
                    failedCount++;
                }
            }

            SucceededCount = succeededCount;
            FailedCount = failedCount;
        }
    }

    public readonly struct ConfigLoadedEvent
    {
        public readonly string ConfigName;
        public readonly string Address;

        public ConfigLoadedEvent(string configName, string address)
        {
            ConfigName = configName;
            Address = address;
        }
    }
}
