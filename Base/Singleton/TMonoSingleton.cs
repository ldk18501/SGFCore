using UnityEngine;

namespace GameFramework.Core
{
    /// <summary>
    /// MonoBehaviour 泛型单例基类
    /// </summary>
    public abstract class TMonoSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;
        private static readonly object _lock = new object();
        private static bool _applicationIsQuitting = false;

        public static T Instance
        {
            get
            {
                // 如果程序正在退出，直接返回 null，防止在 OnDestroy 中调用生成幽灵节点
                if (_applicationIsQuitting)
                {
                    return null;
                }

                lock (_lock)
                {
                    if (_instance == null)
                    {
                        // 1. 先尝试在场景中找
                        _instance = FindObjectOfType<T>();

                        if (_instance == null)
                        {
                            // 2. 场景里没有，自动创建一个新的 GameObject 并挂载
                            GameObject singletonGO = new GameObject();
                            _instance = singletonGO.AddComponent<T>();
                            singletonGO.name = $"[Singleton] {typeof(T).Name}";
                            
                            // 设置为常驻节点
                            DontDestroyOnLoad(singletonGO);
                            
                            Debug.Log($"[Singleton] 自动创建了单例节点: {singletonGO.name}");
                        }
                    }
                    return _instance;
                }
            }
        }

        protected virtual void Awake()
        {
            if (_instance == null)
            {
                _instance = this as T;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Debug.LogWarning($"[Singleton] 场景中存在多个 {typeof(T).Name} 实例，正在销毁多余的实例。");
                Destroy(gameObject);
            }
        }

        protected virtual void OnApplicationQuit()
        {
            _applicationIsQuitting = true;
        }

        protected virtual void OnDestroy()
        {
            _applicationIsQuitting = true;
        }
    }
}