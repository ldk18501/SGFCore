using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using GameFramework.Core.UI;

namespace GameFramework.Core
{
    public class UIModule : IFrameworkModule
    {
        public int Priority => 50;
        private const int ORDER_STEP = 10;

        private readonly Dictionary<int, UIFormConfig> _configs = new Dictionary<int, UIFormConfig>();
        private UIRoot _uiRoot;

        // --- 新增：缓存池回收节点 ---
        private Transform _recyclePoolNode;

        // 记录所有活跃状态的 UI (SerialId -> 实例)
        private readonly Dictionary<int, UIFormBase> _activeForms = new Dictionary<int, UIFormBase>();

        // 记录各层级的激活列表，用于计算 SortingOrder
        private readonly Dictionary<UILayer, List<UIFormBase>> _layerActiveList = new Dictionary<UILayer, List<UIFormBase>>();

        // --- 新增：休眠缓存池 (FormId -> 实例) ---
        private readonly Dictionary<int, UIFormBase> _cachedForms = new Dictionary<int, UIFormBase>();

        // --- 新增：单例模式记录器 (FormId -> 正在显示的 SerialId) ---
        private readonly Dictionary<int, int> _singletonForms = new Dictionary<int, int>();

        private int _nextSerialId = 1;

        public void OnInit()
        {
            foreach (UILayer layer in Enum.GetValues(typeof(UILayer)))
            {
                _layerActiveList[layer] = new List<UIFormBase>();
            }

            GameObject rootPrefab = Resources.Load<GameObject>("UI/UIRoot");
            if (rootPrefab != null)
            {
                GameObject rootInstance = UnityEngine.Object.Instantiate(rootPrefab);
                rootInstance.name = "[Framework_UIRoot]";
                UnityEngine.Object.DontDestroyOnLoad(rootInstance);
                _uiRoot = rootInstance.GetComponent<UIRoot>();

                // 动态创建一个隐藏层，用于存放被缓存的 UI
                GameObject recycleNode = new GameObject("RecyclePool_Hidden");
                recycleNode.transform.SetParent(_uiRoot.transform, false);
                recycleNode.SetActive(false); // 整个节点隐藏
                _recyclePoolNode = recycleNode.transform;
            }
            // ...
        }

        public void RegisterUI(int formId, string address, Type type, UILayer layer, bool isSingleton = true, bool isCached = true)
        {
            _configs[formId] = new UIFormConfig
            {
                FormId = formId, PrefabAddress = address, ScriptType = type,
                Layer = layer, IsSingleton = isSingleton, IsCached = isCached
            };
        }

        public void OnUpdate(float deltaTime, float unscaledDeltaTime)
        {
        }

        public void OnDestroy()
        {
        }

        // ==========================================
        // 核心：打开界面的终极逻辑
        // ==========================================
        public async UniTask<int> OpenUIAsync(int formId, params object[] args)
        {
            if (!_configs.TryGetValue(formId, out UIFormConfig config)) return 0;

            // 1. 【单例检查】如果它是单例，且当前已经在显示了，直接刷新 Order 并重调 OnOpen
            if (config.IsSingleton && _singletonForms.TryGetValue(formId, out int activeSerialId))
            {
                if (_activeForms.TryGetValue(activeSerialId, out UIFormBase activeForm))
                {
                    RefreshSortingOrder(activeForm, config.Layer);
                    activeForm.OnOpen(args); // 传入新参数刷新
                    return activeSerialId;
                }
            }

            UIFormBase form = null;
            int serialId = 0;

            // 2. 【缓存检查】尝试从休眠池中捞出它
            if (config.IsCached && _cachedForms.TryGetValue(formId, out UIFormBase cachedForm))
            {
                form = cachedForm;
                serialId = form.SerialId;
                _cachedForms.Remove(formId); // 移出休眠池

                // 重新挂载到正确的渲染层级，并激活
                form.transform.SetParent(_uiRoot.GetLayerNode(config.Layer), false);
                form.gameObject.SetActive(true);

                Log.Info($"[UI] 极速秒开缓存界面: {form.GetType().Name}");
            }
            else
            {
                // 3. 【全新加载】
                Transform parentNode = _uiRoot.GetLayerNode(config.Layer);
                GameObject uiInstance = await GameApp.Res.InstantiateAsync(config.PrefabAddress, parentNode);
                if (uiInstance == null) return 0;

                form = uiInstance.GetComponent(config.ScriptType) as UIFormBase;
                serialId = _nextSerialId++;

                form.InternalInit(serialId, formId, config.Layer, config.IsCached);
                form.OnInit(); // 只有全新实例化才调用 OnInit
            }

            // 4. 记录状态与生命周期
            _activeForms[serialId] = form;
            if (config.IsSingleton) _singletonForms[formId] = serialId;

            RefreshSortingOrder(form, config.Layer);
            form.OnOpen(args); // 无论怎样都会调用 OnOpen

            // 5. 播放惊艳的入场动画！(不阻塞业务逻辑，让动画自己去飞)
            form.PlayOpenAnimationAsync().Forget();

            return serialId;
        }

        // ==========================================
        // 核心：关闭界面的终极逻辑
        // ==========================================
        public void CloseUI(int serialId)
        {
            if (!_activeForms.TryGetValue(serialId, out UIFormBase form)) return;

            // 1. 触发内部关闭流程（会触发 OnClose 和自动清理事件）
            form.InternalClose();

            // 2. 从活跃列表移除
            _activeForms.Remove(serialId);
            _layerActiveList[form.Layer].Remove(form);

            if (_configs[form.FormId].IsSingleton)
            {
                _singletonForms.Remove(form.FormId);
            }

            // 3. 【缓存判定】
            if (form.IsCached)
            {
                // 丢进回收站，静默挂起
                form.transform.SetParent(_recyclePoolNode, false);
                _cachedForms[form.FormId] = form;
                Log.Info($"[UI] 面板已休眠至缓存池: {form.GetType().Name}");
            }
            else
            {
                // 彻底粉碎（会触发 OnDestroyUI 和自动清理资源）
                form.InternalDestroy();
                GameApp.Res.ReleaseInstance(form.gameObject);
                Log.Info($"[UI] 面板已彻底销毁: {form.GetType().Name}");
            }
        }

        public async UniTask CloseUIAsync(int serialId)
        {
            if (!_activeForms.TryGetValue(serialId, out UIFormBase form)) return;

            // 1. 触发内部关闭流程（会触发 OnClose 和自动清理事件）
            form.InternalClose();

            // 2. 从活跃列表移除
            _activeForms.Remove(serialId);
            _layerActiveList[form.Layer].Remove(form);

            if (_configs[form.FormId].IsSingleton)
            {
                _singletonForms.Remove(form.FormId);
            }
            
            // 3. 等待退场动画播完
            await form.PlayCloseAnimationAsync();

            // 4. 【缓存判定】
            if (form.IsCached)
            {
                // 丢进回收站，静默挂起
                form.transform.SetParent(_recyclePoolNode, false);
                _cachedForms[form.FormId] = form;
                Log.Info($"[UI] 面板已休眠至缓存池: {form.GetType().Name}");
            }
            else
            {
                // 彻底粉碎（会触发 OnDestroyUI 和自动清理资源）
                form.InternalDestroy();
                GameApp.Res.ReleaseInstance(form.gameObject);
                Log.Info($"[UI] 面板已彻底销毁: {form.GetType().Name}");
            }
        }

        private void RefreshSortingOrder(UIFormBase form, UILayer layer)
        {
            var list = _layerActiveList[layer];
            if (list.Contains(form)) list.Remove(form);
            list.Add(form);

            int baseOrder = (int)layer * 1000;
            for (int i = 0; i < list.Count; i++)
            {
                list[i].SetSortingOrder(baseOrder + (i + 1) * ORDER_STEP);
            }
        }
    }
}