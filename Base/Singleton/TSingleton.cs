using System;
using System.Reflection;
using UnityEngine;

namespace GameFramework.Core
{
    /// <summary>
    /// 纯 C# 泛型单例基类
    /// </summary>
    public abstract class TSingleton<T> where T : TSingleton<T>
    {
        private static T _instance;
        // 用于线程安全的锁
        private static readonly object _lock = new object();

        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            // 使用反射获取私有无参构造函数，防止外部通过 new T() 创建实例破坏单例
                            var ctors = typeof(T).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic);
                            var ctor = Array.Find(ctors, c => c.GetParameters().Length == 0);
                            
                            if (ctor == null)
                            {
                                Debug.LogError($"[Singleton] {typeof(T).Name} 缺少私有无参构造函数！");
                            }
                            else
                            {
                                _instance = ctor.Invoke(null) as T;
                            }
                        }
                    }
                }
                return _instance;
            }
        }

        // 保护的构造函数，强制子类不能公开 new
        protected TSingleton() { }
    }
}