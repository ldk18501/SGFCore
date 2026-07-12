using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using GameFramework.Core.UI;

namespace GameFramework.Core
{
    public class UIModule : IFrameworkModule
    {
        private const int ORDER_STEP = 10;

        private readonly Dictionary<int, UIFormConfig> _configs = new Dictionary<int, UIFormConfig>();
        private UIRoot _uiRoot;

        // --- 新增：缓存池回收节点 ---
        private Transform _recyclePoolNode;

        // 记录所有活跃状态的 UI (SerialId -> 实例)
        private readonly Dictionary<int, UIFormBase> _activeForms = new Dictionary<int, UIFormBase>();

        // 记录各层级的激活列表，用于计算 SortingOrder
        private readonly Dictionary<UILayer, List<UIFormBase>> _layerActiveList = new Dictionary<UILayer, List<UIFormBase>>();

        // 每种 FormId 使用独立栈，兼容可缓存的非单例界面。
        private readonly Dictionary<int, Stack<UIFormBase>> _cachedForms =
            new Dictionary<int, Stack<UIFormBase>>();

        // --- 新增：单例模式记录器 (FormId -> 正在显示的 SerialId) ---
        private readonly Dictionary<int, int> _singletonForms = new Dictionary<int, int>();
        private readonly Dictionary<int, UniTask<int>> _openingSingletons =
            new Dictionary<int, UniTask<int>>();
        private readonly Dictionary<int, CancellationTokenSource> _transitionTokens =
            new Dictionary<int, CancellationTokenSource>();

        private int _nextSerialId = 1;
        private CancellationTokenSource _lifecycleCts;

        public void OnInit()
        {
            _lifecycleCts = new CancellationTokenSource();

            foreach (UILayer layer in Enum.GetValues(typeof(UILayer)))
            {
                _layerActiveList[layer] = new List<UIFormBase>();
            }

            // 优先查找场景中已存在的 UIRoot
            _uiRoot = UnityEngine.Object.FindObjectOfType<UIRoot>();
            if (_uiRoot == null)
            {
                Log.Error("[UIModule] 场景中未找到 UIRoot，请将 UIRoot 预制体拖入场景。");
                return;
            }

            UnityEngine.Object.DontDestroyOnLoad(_uiRoot.gameObject);

            // 动态创建隐藏缓存层
            var recycleNode = new GameObject("RecyclePool_Hidden");
            recycleNode.transform.SetParent(_uiRoot.transform, false);
            recycleNode.SetActive(false);
            _recyclePoolNode = recycleNode.transform;
        }

        public void RegisterUI(
            int formId,
            string address,
            Type type,
            UILayer layer,
            bool isSingleton = true,
            bool isCached = true,
            int maxCachedInstances = -1)
        {
            int cacheLimit = !isCached
                ? 0
                : maxCachedInstances >= 0
                    ? maxCachedInstances
                    : isSingleton ? 1 : 3;
            _configs[formId] = new UIFormConfig
            {
                FormId = formId, PrefabAddress = address, ScriptType = type,
                Layer = layer, IsSingleton = isSingleton, IsCached = isCached,
                MaxCachedInstances = cacheLimit
            };
        }

        public void OnUpdate(float deltaTime, float unscaledDeltaTime)
        {
        }

        public void OnDestroy()
        {
            if (_lifecycleCts != null)
            {
                _lifecycleCts.Cancel();
                _lifecycleCts.Dispose();
                _lifecycleCts = null;
            }

            CancelAllTransitions();

            DestroyAllForms();

            _configs.Clear();
            _activeForms.Clear();
            _cachedForms.Clear();
            _singletonForms.Clear();
            _openingSingletons.Clear();
            foreach (List<UIFormBase> list in _layerActiveList.Values)
            {
                list.Clear();
            }

            if (_recyclePoolNode != null)
            {
                UnityEngine.Object.Destroy(_recyclePoolNode.gameObject);
                _recyclePoolNode = null;
            }
        }

        // ==========================================
        // 核心：打开界面的终极逻辑
        // ==========================================
        public async UniTask<int> OpenUIAsync(int formId, params object[] args)
        {
            if (!_configs.TryGetValue(formId, out UIFormConfig config))
            {
                Log.Error($"[UI] 未注册界面: {formId}");
                return 0;
            }

            if (!config.IsSingleton)
            {
                return await OpenUIInternalAsync(config, args);
            }

            if (_openingSingletons.TryGetValue(formId, out UniTask<int> inflight))
            {
                int existingSerialId = await inflight;
                if (_activeForms.TryGetValue(existingSerialId, out UIFormBase existingForm))
                {
                    RefreshSortingOrder(existingForm, config.Layer);
                    if (!TryInvokeOpen(existingForm, args))
                    {
                        return 0;
                    }
                }

                return existingSerialId;
            }

            UniTask<int> openTask = OpenUIInternalAsync(config, args).Preserve();
            _openingSingletons[formId] = openTask;
            try
            {
                return await openTask;
            }
            finally
            {
                _openingSingletons.Remove(formId);
            }
        }

        private async UniTask<int> OpenUIInternalAsync(UIFormConfig config, object[] args)
        {
            int formId = config.FormId;
            if (_uiRoot == null)
            {
                Log.Error($"[UI] 无法打开界面 {formId}：UIRoot 尚未就绪。");
                return 0;
            }

            CancellationToken cancellationToken = GetLifecycleToken();

            // 1. 【单例检查】如果它是单例，且当前已经在显示了，直接刷新 Order 并重调 OnOpen
            if (config.IsSingleton && _singletonForms.TryGetValue(formId, out int activeSerialId))
            {
                if (_activeForms.TryGetValue(activeSerialId, out UIFormBase activeForm))
                {
                    RefreshSortingOrder(activeForm, config.Layer);
                    if (!TryInvokeOpen(activeForm, args))
                    {
                        return 0;
                    }
                    return activeSerialId;
                }
            }

            UIFormBase form = null;
            int serialId = 0;

            // 2. 【缓存检查】尝试从休眠池中捞出它
            if (config.IsCached && TryTakeCachedForm(formId, out UIFormBase cachedForm))
            {
                form = cachedForm;
                serialId = form.SerialId;

                // 重新挂载到正确的渲染层级，并激活
                form.transform.SetParent(_uiRoot.GetLayerNode(config.Layer), false);
                form.gameObject.SetActive(true);
                form.IsClosing = false;
                form.SetInteractionEnabled(true);

                Log.Info($"[UI] 极速秒开缓存界面: {form.GetType().Name}");
            }
            else
            {
                // 3. 【全新加载】
                Transform parentNode = _uiRoot.GetLayerNode(config.Layer);
                GameObject uiInstance = await GameApp.Res.InstantiateAsync(
                    config.PrefabAddress,
                    parentNode,
                    cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                {
                    if (uiInstance != null)
                    {
                        GameApp.Res.ReleaseInstance(uiInstance);
                    }

                    return 0;
                }

                if (uiInstance == null) return 0;

                form = uiInstance.GetComponent(config.ScriptType) as UIFormBase;
                if (form == null)
                {
                    Log.Error($"[UI] 预制体缺少指定 UI 脚本: formId={formId}, type={config.ScriptType?.Name}");
                    GameApp.Res.ReleaseInstance(uiInstance);
                    return 0;
                }

                serialId = _nextSerialId++;

                form.InternalInit(serialId, formId, config.Layer, config.IsCached);
                form.OnInit(); // 只有全新实例化才调用 OnInit
            }

            // 4. 记录状态与生命周期
            _activeForms[serialId] = form;
            if (config.IsSingleton) _singletonForms[formId] = serialId;

            RefreshSortingOrder(form, config.Layer);
            form.SetInteractionEnabled(true);
            if (!TryInvokeOpen(form, args))
            {
                _activeForms.Remove(serialId);
                _layerActiveList[config.Layer].Remove(form);
                if (config.IsSingleton)
                {
                    _singletonForms.Remove(formId);
                }
                DestroyForm(form);
                return 0;
            }

            // 入场动画不阻塞 Open 返回，但由界面 SerialId 持有取消权。
            PlayOpenTransitionAsync(form, cancellationToken).Forget(Debug.LogException);

            return serialId;
        }

        // ==========================================
        // 核心：关闭界面的终极逻辑
        // ==========================================
        public void CloseUI(int serialId)
        {
            CloseUIAsync(serialId).Forget();
        }

        public async UniTask CloseUIAsync(int serialId)
        {
            if (!_activeForms.TryGetValue(serialId, out UIFormBase form)) return;
            if (form.IsClosing) return;
            CancellationToken cancellationToken = GetLifecycleToken();
            CancelTransition(serialId);

            // 1. 触发内部关闭流程（会触发 OnClose 和自动清理事件）
            form.SetInteractionEnabled(false);
            try
            {
                form.InternalClose();
            }
            catch (Exception exception)
            {
                Log.Error($"[UI] 界面 OnClose 失败: formId={form.FormId}, {exception}");
            }

            // 2. 从活跃列表移除
            _activeForms.Remove(serialId);
            _layerActiveList[form.Layer].Remove(form);

            if (_configs.TryGetValue(form.FormId, out UIFormConfig config) && config.IsSingleton)
            {
                _singletonForms.Remove(form.FormId);
            }

            // 3. 等待退场动画播完
            try
            {
                await form.PlayCloseAnimationAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }

            // 4. 【缓存判定】
            if (cancellationToken.IsCancellationRequested)
            {
                DestroyForm(form);
            }
            else if (form.IsCached && config != null && config.MaxCachedInstances > 0)
            {
                if (_cachedForms.TryGetValue(form.FormId, out Stack<UIFormBase> existingStack) &&
                    existingStack.Count >= config.MaxCachedInstances)
                {
                    DestroyForm(form);
                    Log.Info($"[UI] 缓存达到上限，界面已销毁: {form.GetType().Name}");
                    return;
                }

                // 丢进回收站，静默挂起
                form.transform.SetParent(_recyclePoolNode, false);
                form.gameObject.SetActive(false);
                form.IsClosing = false;
                if (!_cachedForms.TryGetValue(form.FormId, out Stack<UIFormBase> cachedStack))
                {
                    cachedStack = new Stack<UIFormBase>();
                    _cachedForms[form.FormId] = cachedStack;
                }

                cachedStack.Push(form);
                Log.Info($"[UI] 面板已休眠至缓存池: {form.GetType().Name}");
            }
            else
            {
                // 彻底粉碎（会触发 OnDestroyUI 和自动清理资源）
                DestroyForm(form);
                Log.Info($"[UI] 面板已彻底销毁: {form.GetType().Name}");
            }
        }

        private void RefreshSortingOrder(UIFormBase form, UILayer layer)
        {
            var list = _layerActiveList[layer];

            if (list.Count > 0 && list[list.Count - 1] == form)
            {
                return;
            }

            if (list.Contains(form)) list.Remove(form);
            list.Add(form);

            int baseOrder = (int)layer * 1000;
            for (int i = 0; i < list.Count; i++)
            {
                list[i].SetSortingOrder(baseOrder + (i + 1) * ORDER_STEP);
            }
        }

        public async UniTask<T> OpenUIAsync<T>(params object[] args) where T : UIFormBase
        {
            var config = _configs.Values.FirstOrDefault(c => c.ScriptType == typeof(T));
            if (config == null) return null;

            int serialId = await OpenUIAsync(config.FormId, args);
            return _activeForms.TryGetValue(serialId, out var form) ? form as T : null;
        }

        public async UniTask<TForm> OpenUIAsync<TForm, TData>(TData data)
            where TForm : UIFormBase<TData>
            where TData : IUIFormData
        {
            return await OpenUIAsync<TForm>(data);
        }

        public void CloseUI<T>() where T : UIFormBase
        {
            var form = _activeForms.Values.FirstOrDefault(f => f is T);
            if (form != null) CloseUI(form.SerialId);
        }

        private CancellationToken GetLifecycleToken()
        {
            if (_lifecycleCts == null)
            {
                _lifecycleCts = new CancellationTokenSource();
            }

            return _lifecycleCts.Token;
        }

        private void DestroyAllForms()
        {
            List<UIFormBase> forms = new List<UIFormBase>(_activeForms.Values);
            foreach (Stack<UIFormBase> cachedStack in _cachedForms.Values)
            {
                forms.AddRange(cachedStack);
            }

            for (int i = 0; i < forms.Count; i++)
            {
                DestroyForm(forms[i]);
            }
        }

        private bool TryTakeCachedForm(int formId, out UIFormBase form)
        {
            form = null;
            if (!_cachedForms.TryGetValue(formId, out Stack<UIFormBase> cachedStack))
            {
                return false;
            }

            while (cachedStack.Count > 0 && form == null)
            {
                form = cachedStack.Pop();
            }

            if (cachedStack.Count == 0)
            {
                _cachedForms.Remove(formId);
            }

            return form != null;
        }

        private void DestroyForm(UIFormBase form)
        {
            if (form == null || form.IsDestroyed)
            {
                return;
            }

            CancelTransition(form.SerialId);
            try
            {
                form.InternalDestroy();
            }
            catch (Exception exception)
            {
                Log.Error($"[UI] 界面 OnDestroyUI 失败: formId={form.FormId}, {exception}");
            }
            finally
            {
                GameApp.Res.ReleaseInstance(form.gameObject);
            }
        }

        private static bool TryInvokeOpen(UIFormBase form, object[] args)
        {
            try
            {
                form.OnOpen(args);
                return true;
            }
            catch (Exception exception)
            {
                Log.Error($"[UI] 界面 OnOpen 失败: formId={form.FormId}, {exception}");
                return false;
            }
        }

        private async UniTask PlayOpenTransitionAsync(
            UIFormBase form,
            CancellationToken lifecycleToken)
        {
            int serialId = form.SerialId;
            CancellationTokenSource transitionCts = CancellationTokenSource.CreateLinkedTokenSource(lifecycleToken);
            _transitionTokens[serialId] = transitionCts;

            try
            {
                await form.PlayOpenAnimationAsync(transitionCts.Token);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (_transitionTokens.TryGetValue(serialId, out CancellationTokenSource current) &&
                    current == transitionCts)
                {
                    _transitionTokens.Remove(serialId);
                }

                transitionCts.Dispose();
            }
        }

        private void CancelTransition(int serialId)
        {
            if (!_transitionTokens.TryGetValue(serialId, out CancellationTokenSource transitionCts))
            {
                return;
            }

            _transitionTokens.Remove(serialId);
            transitionCts.Cancel();
        }

        private void CancelAllTransitions()
        {
            var tokens = new List<CancellationTokenSource>(_transitionTokens.Values);
            _transitionTokens.Clear();
            for (int i = 0; i < tokens.Count; i++)
            {
                tokens[i].Cancel();
            }
        }
    }
}
