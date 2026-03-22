using UnityEngine;

namespace GameFramework.Core
{
    /// <summary>
    /// 系统支持的语言枚举
    /// </summary>
    public enum SystemLanguageType
    {
        Default = 0, // 默认语言（通常是中文或英文基准）
        EN,          // 英文
        ZH,          // 简体中文
        JP,          // 日文
        KR,          // 韩文
        DE,          // 德文
        FR,          // 法文
        ES,          // 西班牙文
        RU,          // 俄文
        IT,          // 意大利文
        TR,          // 土耳其文
        NL,          // 荷兰文
        SV,          // 瑞典文
        // 可以根据发行地区随意扩展...
    }

    /// <summary>
    /// 语言切换全局事件
    /// </summary>
    public struct LanguageChangedEvent
    {
        public SystemLanguageType NewLanguage;
    }
}