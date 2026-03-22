using System;

namespace GameFramework.Core.UI
{
    /// <summary>
    /// UI 配置数据结构
    /// 框架不再关心具体的枚举，只认 int 类型的 FormId
    /// </summary>
    public class UIFormConfig
    {
        public int FormId;
        public string PrefabAddress;
        public Type ScriptType;
        public UILayer Layer;

        // --- 新增配置项 ---
        public bool IsSingleton; // 是否全局唯一（比如主界面、设置面板）
        public bool IsCached; // 是否开启隐藏缓存（关闭时不销毁，极速秒开）
    }
}