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
        // 优先级设置得非常高（值越小越早），因为其他模块初始化时可能就需要注册事件
        public int Priority => 10; 

        // 存储所有事件委托的字典
        private readonly Dictionary<Type, Delegate> _delegates = new Dictionary<Type, Delegate>();

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
            if (_delegates.TryGetValue(type, out Delegate currentDel))
            {
                // 合并委托
                _delegates[type] = Delegate.Combine(currentDel, handler);
            }
            else
            {
                _delegates[type] = handler;
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
            if (_delegates.TryGetValue(type, out Delegate currentDel))
            {
                // 移除委托
                Delegate newDel = Delegate.Remove(currentDel, handler);
                if (newDel == null)
                {
                    _delegates.Remove(type); // 如果该事件没有监听者了，从字典中移除键值对，节省内存
                }
                else
                {
                    _delegates[type] = newDel;
                }
            }
        }
        
        //  GameApp.Event.RemoveListener(kvp.Key, kvp.Value);
        public void RemoveListener(Type type, Delegate handler)
        {
            if (_delegates.TryGetValue(type, out Delegate currentDel))
            {
                // 移除委托
                Delegate newDel = Delegate.Remove(currentDel, handler);
                if (newDel == null)
                {
                    _delegates.Remove(type); // 如果该事件没有监听者了，从字典中移除键值对，节省内存
                }
                else
                {
                    _delegates[type] = newDel;
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
            if (_delegates.TryGetValue(type, out Delegate currentDel))
            {
                // 转型并执行
                if (currentDel is Action<T> action)
                {
                    action.Invoke(eventData);
                }
            }
        }
    }
}