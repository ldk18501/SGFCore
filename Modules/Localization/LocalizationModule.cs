using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameFramework.Core
{
    public class LocalizationModule : IFrameworkModule
    {
        public SystemLanguageType CurrentLanguage { get; private set; } = SystemLanguageType.Default;
        public bool IsLoaded { get; private set; }
        public CultureInfo CurrentCulture { get; private set; } = CultureInfo.InvariantCulture;
        public bool PersistLanguageSelection { get; set; } = true;
        public string LanguagePreferenceKey { get; set; } = "SGFCore.Localization.Language";

        private const string DEFAULT_LANGUAGE_TABLE_PREFIX = "LanguageTableConf";
        private const string DEFAULT_LANGUAGE_SUFFIX = "Default";
        private const string LANGUAGE_CONFIG_NAME = "LanguageConf";

        private readonly Dictionary<string, string> _textMap = new Dictionary<string, string>();
        private readonly Dictionary<SystemLanguageType, string> _languageSuffixMap =
            new Dictionary<SystemLanguageType, string>();
        private readonly Dictionary<SystemLanguageType, string> _cultureNameMap =
            new Dictionary<SystemLanguageType, string>();

        private string _languageTablePrefix = DEFAULT_LANGUAGE_TABLE_PREFIX;
        private int _loadVersion;
        private CancellationTokenSource _activeChangeCts;

        public void OnInit()
        {
            RegisterDefaultCultureNames();
            Log.Module("Localization", "多语言本地化模块初始化完成。");
        }

        public void OnUpdate(float deltaTime, float unscaledDeltaTime)
        {
        }

        public void OnDestroy()
        {
            _loadVersion++;
            _activeChangeCts?.Cancel();
            _activeChangeCts = null;
            _textMap.Clear();
            _languageSuffixMap.Clear();
            _cultureNameMap.Clear();
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
            await TryChangeLanguageAsync(targetLanguage, default);
        }

        /// <summary>
        /// 异步切换语言，返回是否成功加载。目标语言缺失时会回退到 Default。
        /// </summary>
        public async UniTask<bool> TryChangeLanguageAsync(
            SystemLanguageType targetLanguage,
            CancellationToken cancellationToken = default)
        {
            _activeChangeCts?.Cancel();
            var changeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _activeChangeCts = changeCts;
            int version = ++_loadVersion;
            try
            {
                SystemLanguageType loadedLanguage = targetLanguage;
                string targetAddress = BuildTableAddress(targetLanguage);
                EnsureLanguageConfigRegistered();
                ConfigLoadResult loadResult = await GameApp.Config.TryLoadConfigAsync(
                    targetAddress,
                    LANGUAGE_CONFIG_NAME,
                    changeCts.Token);

                if (!loadResult.Success &&
                    !changeCts.IsCancellationRequested &&
                    targetLanguage != SystemLanguageType.Default)
                {
                    Log.Warning($"[Localization] 找不到语言分表 {targetAddress}，正在回退到 Default...");
                    loadedLanguage = SystemLanguageType.Default;
                    targetAddress = BuildTableAddress(SystemLanguageType.Default);
                    loadResult = await GameApp.Config.TryLoadConfigAsync(
                        targetAddress,
                        LANGUAGE_CONFIG_NAME,
                        changeCts.Token);
                }

                if (changeCts.IsCancellationRequested || version != _loadVersion)
                {
                    return false;
                }

                if (!loadResult.Success)
                {
                    Log.Error("[Localization] 语言切换失败，保留当前已加载语言。");
                    return false;
                }

                RebuildTextMap();

                CurrentLanguage = loadedLanguage;
                CurrentCulture = ResolveCulture(loadedLanguage);
                IsLoaded = true;
                SavePreferredLanguage(loadedLanguage);
                Log.Info($"[Localization] 语言切换成功，当前语言: {CurrentLanguage}，文本数量: {_textMap.Count}");

                GameApp.Event?.Broadcast(new LanguageChangedEvent
                {
                    RequestedLanguage = targetLanguage,
                    NewLanguage = CurrentLanguage,
                    IsFallback = loadedLanguage != targetLanguage,
                    CultureName = CurrentCulture.Name
                });

                return true;
            }
            finally
            {
                if (_activeChangeCts == changeCts)
                {
                    _activeChangeCts = null;
                }

                changeCts.Dispose();
            }
        }

        public UniTask<bool> LoadPreferredLanguageAsync(CancellationToken cancellationToken = default)
        {
            return TryChangeLanguageAsync(GetPreferredLanguage(), cancellationToken);
        }

        public SystemLanguageType GetPreferredLanguage()
        {
            if (PersistLanguageSelection &&
                !string.IsNullOrWhiteSpace(LanguagePreferenceKey) &&
                PlayerPrefs.HasKey(LanguagePreferenceKey))
            {
                string value = PlayerPrefs.GetString(LanguagePreferenceKey);
                if (Enum.TryParse(value, true, out SystemLanguageType savedLanguage))
                {
                    return savedLanguage;
                }
            }

            return DetectSystemLanguage(Application.systemLanguage);
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
                return string.Format(CurrentCulture, template, args);
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

        public string GetLanguageSuffix(SystemLanguageType language)
        {
            if (_languageSuffixMap.TryGetValue(language, out string suffix))
            {
                return suffix;
            }

            return language == SystemLanguageType.Default ? DEFAULT_LANGUAGE_SUFFIX : language.ToString();
        }

        public void SetCultureName(SystemLanguageType language, string cultureName)
        {
            if (string.IsNullOrWhiteSpace(cultureName))
            {
                _cultureNameMap.Remove(language);
                return;
            }

            try
            {
                CultureInfo.GetCultureInfo(cultureName);
                _cultureNameMap[language] = cultureName;
            }
            catch (CultureNotFoundException exception)
            {
                Log.Warning($"[Localization] 无效 CultureInfo: {cultureName}, {exception.Message}");
            }
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

        private void SavePreferredLanguage(SystemLanguageType language)
        {
            if (!PersistLanguageSelection || string.IsNullOrWhiteSpace(LanguagePreferenceKey))
            {
                return;
            }

            PlayerPrefs.SetString(LanguagePreferenceKey, language.ToString());
            PlayerPrefs.Save();
        }

        private CultureInfo ResolveCulture(SystemLanguageType language)
        {
            if (_cultureNameMap.TryGetValue(language, out string cultureName))
            {
                try
                {
                    return CultureInfo.GetCultureInfo(cultureName);
                }
                catch (CultureNotFoundException)
                {
                }
            }

            return CultureInfo.InvariantCulture;
        }

        private void RegisterDefaultCultureNames()
        {
            _cultureNameMap[SystemLanguageType.EN] = "en-US";
            _cultureNameMap[SystemLanguageType.ZH] = "zh-CN";
            _cultureNameMap[SystemLanguageType.CN] = "zh-CN";
            _cultureNameMap[SystemLanguageType.JP] = "ja-JP";
            _cultureNameMap[SystemLanguageType.KR] = "ko-KR";
            _cultureNameMap[SystemLanguageType.DE] = "de-DE";
            _cultureNameMap[SystemLanguageType.GE] = "de-DE";
            _cultureNameMap[SystemLanguageType.FR] = "fr-FR";
            _cultureNameMap[SystemLanguageType.ES] = "es-ES";
            _cultureNameMap[SystemLanguageType.RU] = "ru-RU";
            _cultureNameMap[SystemLanguageType.IT] = "it-IT";
            _cultureNameMap[SystemLanguageType.TR] = "tr-TR";
            _cultureNameMap[SystemLanguageType.NL] = "nl-NL";
            _cultureNameMap[SystemLanguageType.SV] = "sv-SE";
        }

        private static SystemLanguageType DetectSystemLanguage(SystemLanguage language)
        {
            switch (language)
            {
                case SystemLanguage.English: return SystemLanguageType.EN;
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified: return SystemLanguageType.ZH;
                case SystemLanguage.Japanese: return SystemLanguageType.JP;
                case SystemLanguage.Korean: return SystemLanguageType.KR;
                case SystemLanguage.German: return SystemLanguageType.DE;
                case SystemLanguage.French: return SystemLanguageType.FR;
                case SystemLanguage.Spanish: return SystemLanguageType.ES;
                case SystemLanguage.Russian: return SystemLanguageType.RU;
                case SystemLanguage.Italian: return SystemLanguageType.IT;
                case SystemLanguage.Turkish: return SystemLanguageType.TR;
                case SystemLanguage.Dutch: return SystemLanguageType.NL;
                case SystemLanguage.Swedish: return SystemLanguageType.SV;
                default: return SystemLanguageType.Default;
            }
        }
    }
}
