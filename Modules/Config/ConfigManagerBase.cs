using System.Collections.Generic;

public abstract class ConfigManagerBase<T> : ConfigManagerBase<int, T> where T : class
{
}

public abstract class ConfigManagerBase<TKey, T> where T : class
{
    public static readonly List<T> List = new List<T>();
    public static readonly Dictionary<TKey, T> Dict = new Dictionary<TKey, T>();

    public static int Count => List.Count;

    public static void Clear()
    {
        List.Clear();
        Dict.Clear();
    }

    public static bool TryGet(TKey key, out T value)
    {
        return Dict.TryGetValue(key, out value);
    }

    public static T Get(TKey key)
    {
        return Dict.TryGetValue(key, out T value) ? value : null;
    }

    public static bool Contains(TKey key)
    {
        return Dict.ContainsKey(key);
    }

    protected static void AddItem(T item)
    {
        if (item != null)
        {
            List.Add(item);
        }
    }

    protected static void AddIndex(TKey key, T item)
    {
        Dict[key] = item;
    }
}
