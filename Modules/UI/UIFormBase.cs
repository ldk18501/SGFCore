using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using PrimeTween;

namespace GameFramework.Core.UI
{
    [RequireComponent(typeof(Canvas), typeof(GraphicRaycaster))]
    public abstract class UIFormBase : MonoBehaviour
    {
        // 缓存面板内所有的动效组件
        private UITweenElement[] _tweenElements;

        public int SerialId { get; internal set; }
        public UILayer Layer { get; internal set; }
        public int FormId { get; internal set; }
        public bool IsCached { get; internal set; } // 标记当前面板是否是缓存模式

        private Canvas _canvas;

        // --- 生命周期追踪器 ---
        // 记录在这个 UI 上注册的所有事件
        private readonly Dictionary<Type, Delegate> _scopedEvents = new Dictionary<Type, Delegate>();

        // 记录在这个 UI 上动态加载的资源和预制体实例
        private readonly List<object> _scopedAssets = new List<object>();
        private readonly List<GameObject> _scopedInstances = new List<GameObject>();

        protected virtual void Awake()
        {
            // true 表示包含隐藏的子节点
            _tweenElements = GetComponentsInChildren<UITweenElement>(true);
        }

        internal void InternalInit(int serialId, int formId, UILayer layer, bool isCached)
        {
            SerialId = serialId;
            FormId = formId;
            Layer = layer;
            IsCached = isCached;

            _canvas = GetComponent<Canvas>();
            _canvas.overrideSorting = true;
        }

        internal void SetSortingOrder(int order)
        {
            if (_canvas != null) _canvas.sortingOrder = order;
        }

        // ==========================================
        // UI 专属 API：自动管理的事件订阅
        // ==========================================

        /// <summary>
        /// 订阅事件（面板关闭时会自动注销，极其安全）
        /// </summary>
        protected void Subscribe<T>(Action<T> handler) where T : struct
        {
            Type type = typeof(T);
            if (!_scopedEvents.ContainsKey(type))
            {
                _scopedEvents[type] = handler;
                GameApp.Event.AddListener(handler);
            }
            else
            {
                Log.Warning($"[UI] 事件 {type.Name} 重复注册！");
            }
        }

        private void UnsubscribeAllEvents()
        {
            foreach (var kvp in _scopedEvents)
            {
                // 利用反射调用泛型的 RemoveListener，或者在 EventModule 里加一个非泛型的移除接口
                // 为了简单高效，我们建议直接在 EventModule 提供一个 RemoveListener(Type, Delegate) 的重载
                GameApp.Event.RemoveListener(kvp.Key, kvp.Value);
            }

            _scopedEvents.Clear();
        }

        // ==========================================
        // UI 专属 API：自动管理的资源加载
        // ==========================================

        /// <summary>
        /// 动态加载图片/音效等数据资源（面板彻底销毁时自动卸载）
        /// </summary>
        protected async UniTask<T> LoadAssetAsync<T>(string address) where T : UnityEngine.Object
        {
            T asset = await GameApp.Res.LoadAssetAsync<T>(address);
            if (asset != null)
            {
                _scopedAssets.Add(asset);
            }

            return asset;
        }

        /// <summary>
        /// 动态加载特效/子节点等预制体（面板彻底销毁时自动回收）
        /// </summary>
        protected async UniTask<GameObject> InstantiateAsync(string address, Transform parent)
        {
            GameObject go = await GameApp.Res.InstantiateAsync(address, parent);
            if (go != null)
            {
                _scopedInstances.Add(go);
            }

            return go;
        }

        private void UnloadAllResources()
        {
            foreach (var asset in _scopedAssets) GameApp.Res.ReleaseAsset(asset);
            foreach (var go in _scopedInstances) GameApp.Res.ReleaseInstance(go);
            _scopedAssets.Clear();
            _scopedInstances.Clear();
        }

        // ==========================================
        // 生命周期流转
        // ==========================================

        public virtual void OnInit()
        {
        }

        public virtual void OnOpen(params object[] args)
        {
        }

        public virtual void OnClose()
        {
        }

        public virtual void OnDestroyUI()
        {
        }

        // 内部调用的生命周期包装器
        internal void InternalClose()
        {
            OnClose();
            UnsubscribeAllEvents(); // 隐藏时立刻断开事件，防止后台耗性能
        }

        internal void InternalDestroy()
        {
            OnDestroyUI();
            UnloadAllResources(); // 彻底销毁时才释放图片和特效
        }


        // ==========================================
        // 异步等待所有入场动画完成
        // ==========================================
        public async UniTask PlayOpenAnimationAsync()
        {
            if (_tweenElements == null || _tweenElements.Length == 0) return;

            // 收集所有正在播放的动画序列
            List<UniTask> tasks = new List<UniTask>(_tweenElements.Length);

            foreach (var elem in _tweenElements)
            {
                if (elem.gameObject.activeInHierarchy)
                {
                    Sequence seq = elem.PlayIn();
                    if (seq.isAlive)
                    {
                        tasks.Add(seq.ToUniTask());
                    }
                }
            }

            // 并发等待所有动画（包含延迟）彻底结束
            if (tasks.Count > 0)
            {
                await UniTask.WhenAll(tasks);
            }
        }

        // ==========================================
        // 异步等待所有退场动画完成
        // ==========================================
        public async UniTask PlayCloseAnimationAsync()
        {
            if (_tweenElements == null || _tweenElements.Length == 0) return;

            List<UniTask> tasks = new List<UniTask>(_tweenElements.Length);

            foreach (var elem in _tweenElements)
            {
                if (elem.gameObject.activeInHierarchy)
                {
                    Sequence seq = elem.PlayOut();
                    if (seq.isAlive)
                    {
                        tasks.Add(seq.ToUniTask());
                    }
                }
            }

            if (tasks.Count > 0)
            {
                await UniTask.WhenAll(tasks);
            }
        }
    }
}