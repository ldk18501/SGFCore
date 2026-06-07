using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameFramework.Core
{
    public class LocalizationModule : IFrameworkModule
    {
        public int Priority => 48;

        public SystemLanguageType CurrentLanguage { get; private set; } = SystemLanguageType.Default;
        public bool IsLoaded { get; private set; }

        private const string DEFAULT_LANGUAGE_TABLE_PREFIX = "LanguageTableConf";
        private const string DEFAULT_LANGUAGE_SUFFIX = "Default";
        private const string LANGUAGE_CONFIG_NAME = "LanguageConf";

        private readonly Dictionary<string, string> _textMap = new Dictionary<string, string>();
        private readonly Dictionary<SystemLanguageType, string> _languageSuffixMap =
            new Dictionary<SystemLanguageType, string>();

        private string _languageTablePrefix = DEFAULT_LANGUAGE_TABLE_PREFIX;
        private int _loadVersion;

        public void OnInit()
        {
            Log.Module("Localization", "多语言本地化模块初始化完成。");
        }

        public void OnUpdate(float deltaTime, float unscaledDeltaTime)
        {
        }

        public void OnDestroy()
        {
            _loadVersion++;
            _textMap.Clear();
            _languageSuffixMap.Clear();
            IsLoaded = false;
        }

        public void SetLanguageTablePrefix(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                Log.Warning("[Localization] 语言表前缀不能为空，已忽略。");
                return;
            }

            _languageTablePrefix = prefix.Trim();
        }

        public void SetLanguageSuffix(SystemLanguageType language, string suffix)
        {
            if (string.IsNullOrWhiteSpace(suffix))
            {
                _languageSuffixMap.Remove(language);
                return;
            }

            _languageSuffixMap[language] = suffix.Trim();
        }

        /// <summary>
        /// 异步切换并加载特定语言。兼容旧用法，失败时只记录日志。
        /// </summary>
        public async UniTask ChangeLanguageAsync(SystemLanguageType targetLanguage)
        {
            await TryChangeLanguageAsync(targetLanguage);
        }

        /// <summary>
        /// 异步切换语言，返回是否成功加载。目标语言缺失时会回退到 Default。
        /// </summary>
        public async UniTask<bool> TryChangeLanguageAsync(SystemLanguageType targetLanguage)
        {
            int version = ++_loadVersion;
            SystemLanguageType loadedLanguage = targetLanguage;
            string targetAddress = BuildTableAddress(targetLanguage);
            EnsureLanguageConfigRegistered();
            ConfigLoadResult loadResult = await GameApp.Config.TryLoadConfigAsync(targetAddress, LANGUAGE_CONFIG_NAME);

            if (!loadResult.Success && targetLanguage != SystemLanguageType.Default)
            {
                Log.Warning($"[Localization] 找不到语言分表 {targetAddress}，正在回退到 Default...");
                loadedLanguage = SystemLanguageType.Default;
                targetAddress = BuildTableAddress(SystemLanguageType.Default);
                loadResult = await GameApp.Config.TryLoadConfigAsync(targetAddress, LANGUAGE_CONFIG_NAME);
            }

            if (!loadResult.Success)
            {
                Log.Fatal("[Localization] 致命错误：连 Default 语言表都找不到！请检查资源打包。");
                IsLoaded = false;
                return false;
            }

            if (version != _loadVersion)
            {
                return false;
            }

            RebuildTextMap();

            CurrentLanguage = loadedLanguage;
            IsLoaded = true;
            Log.Info($"[Localization] 语言切换成功，当前语言: {CurrentLanguage}，文本数量: {_textMap.Count}");

            GameApp.Event?.Broadcast(new LanguageChangedEvent
            {
                RequestedLanguage = targetLanguage,
                NewLanguage = CurrentLanguage,
                IsFallback = loadedLanguage != targetLanguage
            });

            return true;
        }

        public string GetString(int keyId)
        {
            return GetString(keyId.ToString());
        }

        public string GetString(string key)
        {
            return TryGetString(key, out string value) ? value : $"#MISSING_{key}#";
        }

        public bool TryGetString(int keyId, out string value)
        {
            return TryGetString(keyId.ToString(), out value);
        }

        public bool TryGetString(string key, out string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                value = string.Empty;
                return false;
            }

            return _textMap.TryGetValue(key, out value);
        }

        public string Format(int keyId, params object[] args)
        {
            return Format(keyId.ToString(), args);
        }

        public string Format(string key, params object[] args)
        {
            string template = GetString(key);
            if (args == null || args.Length == 0)
            {
                return template;
            }

            try
            {
                return string.Format(template, args);
            }
            catch (FormatException e)
            {
                Log.Warning($"[Localization] 文本格式化失败: key={key}, error={e.Message}");
                return template;
            }
        }

        private string BuildTableAddress(SystemLanguageType language)
        {
            return $"{_languageTablePrefix}_{GetLanguageSuffix(language)}";
        }

        private string GetLanguageSuffix(SystemLanguageType language)
        {
            if (_languageSuffixMap.TryGetValue(language, out string suffix))
            {
                return suffix;
            }

            return language == SystemLanguageType.Default ? DEFAULT_LANGUAGE_SUFFIX : language.ToString();
        }

        private static void EnsureLanguageConfigRegistered()
        {
            ConfigModule configModule = GameApp.Config;
            if (configModule != null && !configModule.IsRegistered(LANGUAGE_CONFIG_NAME))
            {
                configModule.RegisterConfig(LANGUAGE_CONFIG_NAME, LanguageConf.Load);
            }
        }

        private void RebuildTextMap()
        {
            _textMap.Clear();

            for (int i = 0; i < LanguageConf.List.Count; i++)
            {
                LanguageConf conf = LanguageConf.List[i];
                if (conf == null || string.IsNullOrWhiteSpace(conf.id))
                {
                    continue;
                }

                if (_textMap.ContainsKey(conf.id))
                {
                    Log.Warning($"[Localization] 发现重复文本 key: {conf.id}，已使用后出现的值。");
                }

                _textMap[conf.id] = conf.value ?? string.Empty;
            }
        }
    }
}
