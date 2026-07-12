using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameFramework.Core
{
    /// <summary>
    /// 全局事件中心模块
    /// 基于类型的事件发布/订阅系统，强制使用 Struct 以避免 GC
    /// </summary>
    public class EventModule : IFrameworkModule
    {
        // 存储所有事件委托的字典（使用List避免Delegate.Combine的GC）
        private readonly Dictionary<Type, List<Delegate>> _delegates = new Dictionary<Type, List<Delegate>>();

        public void OnInit()
        {
            Debug.Log("[Framework] EventModule 初始化完成.");
        }

        public void OnUpdate(float deltaTime, float unscaledDeltaTime)
        {
            // 事件系统通常是即时响应的，不需要在 Update 中轮询
        }

        public void OnDestroy()
        {
            _delegates.Clear();
            Debug.Log("[Framework] EventModule 已销毁.");
        }

        /// <summary>
        /// 注册事件监听
        /// </summary>
        /// <typeparam name="T">事件类型（必须是 struct）</typeparam>
        /// <param name="handler">事件处理方法</param>
        public void AddListener<T>(Action<T> handler) where T : struct
        {
            Type type = typeof(T);
            if (!_delegates.TryGetValue(type, out var list))
            {
                list = new List<Delegate>();
                _delegates[type] = list;
            }
            if (!list.Contains(handler))
            {
                list.Add(handler);
            }
        }

        /// <summary>
        /// 移除事件监听
        /// </summary>
        /// <typeparam name="T">事件类型（必须是 struct）</typeparam>
        /// <param name="handler">事件处理方法</param>
        public void RemoveListener<T>(Action<T> handler) where T : struct
        {
            Type type = typeof(T);
            if (_delegates.TryGetValue(type, out var list))
            {
                list.Remove(handler);
                if (list.Count == 0)
                {
                    _delegates.Remove(type);
                }
            }
        }
        
        public void RemoveListener(Type type, Delegate handler)
        {
            if (_delegates.TryGetValue(type, out var list))
            {
                list.Remove(handler);
                if (list.Count == 0)
                {
                    _delegates.Remove(type);
                }
            }
        }



        /// <summary>
        /// 广播/派发事件
        /// </summary>
        /// <typeparam name="T">事件类型（必须是 struct）</typeparam>
        /// <param name="eventData">事件数据</param>
        public void Broadcast<T>(T eventData) where T : struct
        {
            Type type = typeof(T);
            if (_delegates.TryGetValue(type, out var list))
            {
                int count = list.Count;
                Delegate[] snapshot = System.Buffers.ArrayPool<Delegate>.Shared.Rent(count);
                list.CopyTo(snapshot, 0);

                try
                {
                    for (int i = 0; i < count; i++)
                    {
                        try
                        {
                            ((Action<T>)snapshot[i]).Invoke(eventData);
                        }
                        catch (Exception exception)
                        {
                            Log.Error(
                                $"[Event] 监听器执行失败: event={type.Name}, " +
                                $"listener={snapshot[i]?.Method?.DeclaringType?.Name}.{snapshot[i]?.Method?.Name}, " +
                                $"error={exception}");
                        }
                    }
                }
                finally
                {
                    Array.Clear(snapshot, 0, count);
                    System.Buffers.ArrayPool<Delegate>.Shared.Return(snapshot);
                }
            }
        }

        public int GetListenerCount<T>() where T : struct
        {
            Type type = typeof(T);
            return _delegates.TryGetValue(type, out var list) ? list.Count : 0;
        }
    }
}
