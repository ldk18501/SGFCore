using UnityEngine;

public partial class LanguageConf
{
    // 每一行数据解析完之后都会调用这里
    partial void OnPostLoad()
    {
        // TODO: 如果这第一列是 int 类型的 id，请取消下面这行的注释将其加入字典
        // Dict[this.id] = this; 
    }

    // 整张表读取完毕后调用
    static partial void OnAllLoadDone()
    {
        // Debug.Log("LanguageConf_defaultConfig 加载完成");
    }
}