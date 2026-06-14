using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameFramework.Core
{
    /// <summary>
    /// 可被内存池回收的引用接口
    /// </summary>
    public interface IReference
    {
        void Clear();
    }

    /// <summary>
    /// 池配置
    /// </summary>
    public class PoolConfig
    {
        public int MaxCapacity = 100;
        public int PrewarmCount = 0;
    }

    /// <summary>
    /// 全局池管理模块
    /// </summary>
    public class PoolModule : IFrameworkModule
    {
        public int Priority => 15; // 优先级较高，在 Timer 之前初始化

        // --- C# 类内存池 ---
        private readonly Dictionary<Type, Queue<IReference>> _classPools = new Dictionary<Type, Queue<IReference>>();

        // --- GameObject 对象池 ---
        private readonly Dictionary<string, Queue<GameObject>> _gameObjectPools = new Dictionary<string, Queue<GameObject>>();
        private readonly Dictionary<string, PoolConfig> _poolConfigs = new Dictionary<string, PoolConfig>();
        private Transform _poolRoot; // 场景中存放回收对象的根节点

        public void OnInit()
        {
            _poolRoot = new GameObject("[Framework_GameObjectPool]").transform;
            UnityEngine.Object.DontDestroyOnLoad(_poolRoot.gameObject);
            Log.Module("Pool", "池管理模块初始化完成。");
        }

        public void OnUpdate(float deltaTime, float unscaledDeltaTime) { }
        public void OnDestroy()
        {
            ClearAllGameObjectPools();
            _classPools.Clear();

            if (_poolRoot != null)
            {
                UnityEngine.Object.Destroy(_poolRoot.gameObject);
                _poolRoot = null;
            }
        }

        // ==========================================
        // C# 内存池 API
        // ==========================================

        public T AllocateClass<T>() where T : class, IReference, new()
        {
            Type type = typeof(T);
            if (_classPools.TryGetValue(type, out var pool) && pool.Count > 0)
            {
                return pool.Dequeue() as T;
            }
            return new T();
        }

        public void ReleaseClass(IReference refObj)
        {
            if (refObj == null) return;
            Type type = refObj.GetType();
            
            refObj.Clear(); // 强制清理数据，防止脏数据残留

            if (!_classPools.TryGetValue(type, out var pool))
            {
                pool = new Queue<IReference>();
                _classPools[type] = pool;
            }

            if (pool.Contains(refObj))
            {
                Log.Warning($"[Pool] 重复回收类对象已忽略: {type.Name}");
                return;
            }

            pool.Enqueue(refObj);
        }

        // ==========================================
        // GameObject 对象池 API
        // ==========================================

        public GameObject SpawnGameObject(string poolName, GameObject prefab, Transform parent = null)
        {
            if (_gameObjectPools.TryGetValue(poolName, out var pool))
            {
                while (pool.Count > 0)
                {
                    GameObject go = pool.Dequeue();
                    if (go != null)
                    {
                        go.transform.SetParent(parent);
                        go.transform.localPosition = Vector3.zero;
                        go.transform.localRotation = Quaternion.identity;
                        go.transform.localScale = Vector3.one;
                        go.SetActive(true);
                        return go;
                    }
                }
            }

            // 池中没有，实例化一个新的
            GameObject newObj = UnityEngine.Object.Instantiate(prefab, parent);
            newObj.name = prefab.name;
            return newObj;
        }

        public void RecycleGameObject(string poolName, GameObject go)
        {
            if (go == null) return;

            if (!_gameObjectPools.TryGetValue(poolName, out var pool))
            {
                pool = new Queue<GameObject>();
                _gameObjectPools[poolName] = pool;
            }

            if (ContainsGameObject(pool, go))
            {
                Log.Warning($"[Pool] 重复回收 GameObject 已忽略: pool={poolName}, object={go.name}");
                return;
            }

            if (_poolConfigs.TryGetValue(poolName, out var config) && pool.Count >= config.MaxCapacity)
            {
                UnityEngine.Object.Destroy(go);
                return;
            }

            go.SetActive(false);
            go.transform.SetParent(_poolRoot);
            pool.Enqueue(go);
        }

        public void SetPoolConfig(string poolName, PoolConfig config)
        {
            _poolConfigs[poolName] = config;
        }

        public void PrewarmGameObject(string poolName, GameObject prefab, int count)
        {
            for (int i = 0; i < count; i++)
            {
                GameObject go = UnityEngine.Object.Instantiate(prefab, _poolRoot);
                go.name = prefab.name;
                go.SetActive(false);
                RecycleGameObject(poolName, go);
            }
        }

        public int GetPoolCount(string poolName)
        {
            return _gameObjectPools.TryGetValue(poolName, out var pool) ? pool.Count : 0;
        }

        public void ClearPool(string poolName)
        {
            if (_gameObjectPools.TryGetValue(poolName, out var pool))
            {
                while (pool.Count > 0)
                {
                    var go = pool.Dequeue();
                    if (go != null) UnityEngine.Object.Destroy(go);
                }
                _gameObjectPools.Remove(poolName);
            }
        }

        public void ClearAllGameObjectPools()
        {
            foreach (string poolName in new List<string>(_gameObjectPools.Keys))
            {
                ClearPool(poolName);
            }
        }

        private static bool ContainsGameObject(Queue<GameObject> pool, GameObject target)
        {
            foreach (GameObject item in pool)
            {
                if (item == target)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
