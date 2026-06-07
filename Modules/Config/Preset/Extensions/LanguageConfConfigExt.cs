using UnityEngine;

public partial class LanguageConf
{
    // 每一行数据解析完之后都会调用这里
    partial void OnPostLoad()
    {
    }

    // 整张表读取完毕后调用
    static partial void OnAllLoadDone()
    {
        // Debug.Log("LanguageConf_defaultConfig 加载完成");
    }
}
