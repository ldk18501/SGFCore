using System.Collections.Generic;

// 这个类保留在全局或你的游戏业务命名空间下
public abstract class ConfigManagerBase<T> where T : class
{
    // 存储列表数据 (适合遍历)
    public static List<T> List = new List<T>();
    // 存储 ID 字典索引 (适合极速查找)
    public static Dictionary<int, T> Dict = new Dictionary<int, T>();

    public static void Clear()
    {
        List.Clear();
        Dict.Clear();
    }
}