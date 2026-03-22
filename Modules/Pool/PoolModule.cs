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
    /// 全局池管理模块
    /// </summary>
    public class PoolModule : IFrameworkModule
    {
        public int Priority => 15; // 优先级较高，在 Timer 之前初始化

        // --- C# 类内存池 ---
        private readonly Dictionary<Type, Queue<IReference>> _classPools = new Dictionary<Type, Queue<IReference>>();

        // --- GameObject 对象池 ---
        private readonly Dictionary<string, Queue<GameObject>> _gameObjectPools = new Dictionary<string, Queue<GameObject>>();
        private Transform _poolRoot; // 场景中存放回收对象的根节点

        public void OnInit()
        {
            _poolRoot = new GameObject("[Framework_GameObjectPool]").transform;
            UnityEngine.Object.DontDestroyOnLoad(_poolRoot.gameObject);
            Log.Module("Pool", "池管理模块初始化完成。");
        }

        public void OnUpdate(float deltaTime, float unscaledDeltaTime) { }
        public void OnDestroy() { }

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
            pool.Enqueue(refObj);
        }

        // ==========================================
        // GameObject 对象池 API
        // ==========================================

        public GameObject SpawnGameObject(string poolName, GameObject prefab, Transform parent = null)
        {
            if (_gameObjectPools.TryGetValue(poolName, out var pool) && pool.Count > 0)
            {
                GameObject go = pool.Dequeue();
                if (go != null)
                {
                    go.transform.SetParent(parent);
                    go.SetActive(true);
                    return go;
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

            go.SetActive(false);
            go.transform.SetParent(_poolRoot);

            if (!_gameObjectPools.TryGetValue(poolName, out var pool))
            {
                pool = new Queue<GameObject>();
                _gameObjectPools[poolName] = pool;
            }
            pool.Enqueue(go);
        }
    }
}