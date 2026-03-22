using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameFramework.Core
{
    public class LocalizationModule : IFrameworkModule
    {
        public int Priority => 48; // 优先级在 Config(45) 和 UI(50) 之间

        public SystemLanguageType CurrentLanguage { get; private set; } = SystemLanguageType.Default;

        // 统一的语言表前缀
        private const string LANG_TABLE_PREFIX = "LanguageTableConf";

        public void OnInit()
        {
            Log.Module("Localization", "多语言本地化模块初始化完成。");
        }

        public void OnUpdate(float deltaTime, float unscaledDeltaTime) { }
        public void OnDestroy() { }

        /// <summary>
        /// 异步切换并加载特定语言
        /// </summary>
        public async UniTask ChangeLanguageAsync(SystemLanguageType targetLanguage)
        {
            // 1. 拼接目标语言的 Addressables 资源名 (例如 "LanguageTableConf_EN")
            string targetAddress = targetLanguage == SystemLanguageType.Default 
                ? $"{LANG_TABLE_PREFIX}_Default" 
                : $"{LANG_TABLE_PREFIX}_{targetLanguage.ToString()}";

            // 2. 尝试加载目标语言的二进制文件
            TextAsset textAsset = await GameApp.Res.LoadAssetAsync<TextAsset>(targetAddress);

            // 3. Fallback 容错机制：如果没有找到对应的分表，强行回退到 Default 表
            if (textAsset == null && targetLanguage != SystemLanguageType.Default)
            {
                Log.Warning($"[Localization] 找不到语言分表 {targetAddress}，正在回退到 Default...");
                targetAddress = $"{LANG_TABLE_PREFIX}_Default";
                textAsset = await GameApp.Res.LoadAssetAsync<TextAsset>(targetAddress);
            }

            if (textAsset == null)
            {
                Log.Fatal("[Localization] 致命错误：连 Default 语言表都找不到！请检查资源打包。");
                return;
            }

            // 4. 解析二进制数据
            // 这里直接复用你导表工具生成的静态 Load 方法！极其优雅！
            LanguageConf.Load(textAsset.bytes);
            
            // 解析完立刻释放，防内存泄漏
            GameApp.Res.ReleaseAsset(textAsset);

            CurrentLanguage = targetLanguage;
            Log.Info($"[Localization] 语言切换成功，当前语言: {CurrentLanguage}");

            // 5. 抛出全局事件，通知所有 UI 组件刷新！
            GameApp.Broadcast(new LanguageChangedEvent { NewLanguage = CurrentLanguage });
        }

        /// <summary>
        /// 获取本地化文本的便捷 API
        /// </summary>
        public string GetString(int keyId)
        {
            if (LanguageConf.Dict.TryGetValue(keyId, out var conf))
            {
                return conf.value; // 假设你在 Excel 里配的字段名叫 value
            }
            return $"#MISSING_{keyId}#"; // 找不到时的明显提示，方便查错
        }
    }
}