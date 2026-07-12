using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameFramework.Core
{
    /// <summary>
    /// 框架总入口。负责模块组装、依赖排序、异步初始化、轮询和逆序释放。
    /// </summary>
    public class FrameworkEntry : TMonoSingleton<FrameworkEntry>
    {
        private readonly List<IFrameworkModule> _modules = new List<IFrameworkModule>();
        private readonly List<ModuleRegistration> _registrations = new List<ModuleRegistration>();
        private readonly Dictionary<Type, IFrameworkModule> _moduleDict =
            new Dictionary<Type, IFrameworkModule>();

        private FrameworkConfig _config;
        private CancellationTokenSource _lifecycleCts;
        private bool _isInitialized;
        private bool _isInitializing;
        private bool _isShuttingDown;
        private bool _isComposingBuiltIns;
        private int _nextRegistrationOrder;

        public bool IsInitialized => _isInitialized;
        public bool IsInitializing => _isInitializing;
        public bool IsShuttingDown => _isShuttingDown;
        public IReadOnlyList<IFrameworkModule> Modules => _modules;

        /// <summary>
        /// 兼容旧项目的非等待入口。新代码必须 await InitFrameworkModulesAsync。
        /// </summary>
        [Obsolete("请改用并等待 InitFrameworkModulesAsync，确保异步模块准备完成后再启动业务流程。")]
        public void InitFrameworkModules(FrameworkConfig config = null)
        {
            InitFrameworkModulesAsync(config)
                .Forget(Debug.LogException);
        }

        public async UniTask<bool> InitFrameworkModulesAsync(
            FrameworkConfig config = null,
            CancellationToken cancellationToken = default)
        {
            if (_isInitialized || _isInitializing)
            {
                Debug.LogWarning("[Framework] 框架模块已经初始化，重复调用已忽略。");
                return _isInitialized;
            }

            if (_isShuttingDown)
            {
                Debug.LogError("[Framework] 框架正在关闭，不能重新初始化。");
                return false;
            }

            _isInitializing = true;
            _config = config;
            GameApp.Reset();
            _lifecycleCts?.Dispose();
            _lifecycleCts = new CancellationTokenSource();
            int initializedModuleCount = 0;

            using (CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                       cancellationToken,
                       _lifecycleCts.Token))
            {
                try
                {
                    _isComposingBuiltIns = true;
                    try
                    {
                        RegisterBuiltInModules();
                    }
                    finally
                    {
                        _isComposingBuiltIns = false;
                    }

                    ApplySortedRegistrations();

                    for (int i = 0; i < _modules.Count; i++)
                    {
                        linkedCts.Token.ThrowIfCancellationRequested();
                        IFrameworkModule module = _modules[i];
                        initializedModuleCount = i + 1;
                        module.OnInit();

                        if (module is IAsyncFrameworkModule asyncModule)
                        {
                            await asyncModule.OnInitAsync(linkedCts.Token);
                        }
                    }

                    ApplyStartupConfig();
                    Log.Module("Framework", $"模块初始化顺序: {BuildInitializationOrderText()}");
                    _isInitialized = true;
                    Log.Info("<color=#00FF00>[GameEntry] 框架模块依赖图初始化完成。</color>");
                    return true;
                }
                catch (OperationCanceledException)
                {
                    await DestroyModulesAsync(initializedModuleCount, CancellationToken.None);
                    GameApp.Reset();
                    return false;
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    await DestroyModulesAsync(initializedModuleCount, CancellationToken.None);
                    GameApp.Reset();
                    throw;
                }
                finally
                {
                    _isInitializing = false;
                }
            }
        }

        public async UniTask ShutdownFrameworkAsync(CancellationToken cancellationToken = default)
        {
            if (_isShuttingDown)
            {
                return;
            }

            _isShuttingDown = true;
            _isInitialized = false;
            _lifecycleCts?.Cancel();

            try
            {
                await DestroyModulesAsync(_modules.Count, cancellationToken);
            }
            finally
            {
                _config = null;
                GameApp.Reset();
                _lifecycleCts?.Dispose();
                _lifecycleCts = null;
                _isShuttingDown = false;
            }
        }

        private void RegisterBuiltInModules()
        {
            RegisterModule(new LogModule());
            RegisterModule(new FileSystemModule(), typeof(LogModule));
            RegisterModule(new EventModule(), typeof(LogModule));
            RegisterModule(new PoolModule(), typeof(LogModule));
            RegisterModule(new TimerModule(), typeof(PoolModule));
            RegisterModule(new TimeModule(), typeof(EventModule));
            RegisterModule(new CryptoModule(), typeof(LogModule));
            RegisterModule(
                new SaveModule(),
                typeof(FileSystemModule),
                typeof(CryptoModule),
                typeof(TimerModule),
                typeof(EventModule));
            RegisterModule(new ResourceModule(), typeof(LogModule));
            RegisterModule(new SceneModule(), typeof(ResourceModule), typeof(EventModule));
            RegisterModule(new ConfigModule(), typeof(ResourceModule), typeof(EventModule));
            RegisterModule(new LocalizationModule(), typeof(ConfigModule), typeof(EventModule));
            RegisterModule(new RedPointModule(), typeof(EventModule));
            RegisterModule(new UIModule(), typeof(ResourceModule), typeof(EventModule));
            RegisterModule(
                new GuideModule(),
                typeof(SaveModule),
                typeof(EventModule),
                typeof(LocalizationModule));
            RegisterModule(
                new AudioModule(),
                typeof(ResourceModule),
                typeof(PoolModule),
                typeof(EventModule));
            RegisterModule(new FsmModule(), typeof(LogModule));
            RegisterModule(new ProcedureModule(), typeof(EventModule));
            RegisterModule(new BTModule(), typeof(ResourceModule));
            RegisterModule(new HttpModule(), typeof(EventModule));
        }

        private void Update()
        {
            if (!_isInitialized || _isShuttingDown)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            float unscaledDeltaTime = Time.unscaledDeltaTime;
            for (int i = 0; i < _modules.Count; i++)
            {
                _modules[i].OnUpdate(deltaTime, unscaledDeltaTime);
            }
        }

        protected override void OnDestroy()
        {
            _isInitialized = false;
            _lifecycleCts?.Cancel();

            if (_modules.Count > 0)
            {
                Debug.LogWarning("[Framework] 未显式等待 ShutdownFrameworkAsync，正在执行同步兜底清理。");
                DestroyModulesSynchronously(_modules.Count);
            }

            _config = null;
            GameApp.Reset();
            _lifecycleCts?.Dispose();
            _lifecycleCts = null;
            base.OnDestroy();
        }

        /// <summary>
        /// 注册模块及其直接依赖。依赖必须使用具体模块类型，并且全部注册后才能初始化框架。
        /// </summary>
        public void RegisterModule(IFrameworkModule module, params Type[] dependencies)
        {
            if (module == null)
            {
                throw new ArgumentNullException(nameof(module));
            }

            if ((_isInitialized || _isInitializing) && !_isComposingBuiltIns)
            {
                throw new InvalidOperationException("框架初始化开始后不能同步注册模块，请在初始化前完成组装。");
            }

            Type moduleType = module.GetType();
            if (_moduleDict.ContainsKey(moduleType))
            {
                Debug.LogWarning($"[Framework] 模块已经注册，已忽略: {moduleType.Name}");
                return;
            }

            Type[] normalizedDependencies = NormalizeDependencies(moduleType, dependencies);
            _moduleDict.Add(moduleType, module);
            _registrations.Add(new ModuleRegistration(
                module,
                normalizedDependencies,
                _nextRegistrationOrder++));
        }

        public T GetModule<T>() where T : class, IFrameworkModule
        {
            Type type = typeof(T);
            if (_moduleDict.TryGetValue(type, out IFrameworkModule module))
            {
                return module as T;
            }

            Debug.LogError($"[Framework] 找不到模块: {type.Name}");
            return null;
        }

        public bool TryGetModule<T>(out T module) where T : class, IFrameworkModule
        {
            if (_moduleDict.TryGetValue(typeof(T), out IFrameworkModule value))
            {
                module = value as T;
                return module != null;
            }

            module = null;
            return false;
        }

        public IReadOnlyList<Type> GetModuleDependencies<T>() where T : class, IFrameworkModule
        {
            Type moduleType = typeof(T);
            for (int i = 0; i < _registrations.Count; i++)
            {
                if (_registrations[i].Module.GetType() == moduleType)
                {
                    return _registrations[i].Dependencies;
                }
            }

            return Array.Empty<Type>();
        }

        public IReadOnlyList<FrameworkModuleSnapshot> GetModuleGraphSnapshot()
        {
            var snapshots = new List<FrameworkModuleSnapshot>(_modules.Count);
            for (int i = 0; i < _modules.Count; i++)
            {
                IFrameworkModule module = _modules[i];
                IReadOnlyList<Type> dependencies = Array.Empty<Type>();
                for (int j = 0; j < _registrations.Count; j++)
                {
                    if (_registrations[j].Module == module)
                    {
                        dependencies = _registrations[j].Dependencies;
                        break;
                    }
                }

                snapshots.Add(new FrameworkModuleSnapshot(
                    module.GetType(),
                    dependencies,
                    i));
            }

            return snapshots;
        }

        private void ApplySortedRegistrations()
        {
            List<ModuleRegistration> sorted = TopologicalSort(_registrations);
            _modules.Clear();
            for (int i = 0; i < sorted.Count; i++)
            {
                _modules.Add(sorted[i].Module);
            }
        }

        private string BuildInitializationOrderText()
        {
            var names = new string[_modules.Count];
            for (int i = 0; i < _modules.Count; i++)
            {
                names[i] = _modules[i].GetType().Name;
            }

            return string.Join(" -> ", names);
        }

        private List<ModuleRegistration> TopologicalSort(List<ModuleRegistration> registrations)
        {
            var byType = new Dictionary<Type, ModuleRegistration>(registrations.Count);
            var indegrees = new Dictionary<Type, int>(registrations.Count);
            var dependents = new Dictionary<Type, List<Type>>(registrations.Count);

            for (int i = 0; i < registrations.Count; i++)
            {
                ModuleRegistration registration = registrations[i];
                Type type = registration.Module.GetType();
                byType[type] = registration;
                indegrees[type] = registration.Dependencies.Count;
                dependents[type] = new List<Type>();
            }

            for (int i = 0; i < registrations.Count; i++)
            {
                ModuleRegistration registration = registrations[i];
                Type moduleType = registration.Module.GetType();
                for (int j = 0; j < registration.Dependencies.Count; j++)
                {
                    Type dependencyType = registration.Dependencies[j];
                    if (!byType.ContainsKey(dependencyType))
                    {
                        throw new InvalidOperationException(
                            $"模块 {moduleType.Name} 缺少依赖 {dependencyType.Name}。请先注册依赖模块。");
                    }

                    dependents[dependencyType].Add(moduleType);
                }
            }

            var ready = new List<ModuleRegistration>();
            for (int i = 0; i < registrations.Count; i++)
            {
                Type type = registrations[i].Module.GetType();
                if (indegrees[type] == 0)
                {
                    InsertByRegistrationOrder(ready, registrations[i]);
                }
            }

            var result = new List<ModuleRegistration>(registrations.Count);
            while (ready.Count > 0)
            {
                ModuleRegistration current = ready[0];
                ready.RemoveAt(0);
                result.Add(current);

                List<Type> currentDependents = dependents[current.Module.GetType()];
                for (int i = 0; i < currentDependents.Count; i++)
                {
                    Type dependentType = currentDependents[i];
                    indegrees[dependentType]--;
                    if (indegrees[dependentType] == 0)
                    {
                        InsertByRegistrationOrder(ready, byType[dependentType]);
                    }
                }
            }

            if (result.Count != registrations.Count)
            {
                var cycleTypes = new List<string>();
                foreach (KeyValuePair<Type, int> pair in indegrees)
                {
                    if (pair.Value > 0)
                    {
                        cycleTypes.Add(pair.Key.Name);
                    }
                }

                cycleTypes.Sort(StringComparer.Ordinal);
                throw new InvalidOperationException(
                    $"检测到模块循环依赖: {string.Join(" -> ", cycleTypes)}");
            }

            return result;
        }

        private async UniTask DestroyModulesAsync(int moduleCount, CancellationToken cancellationToken)
        {
            int lastIndex = Math.Min(moduleCount, _modules.Count) - 1;
            for (int i = lastIndex; i >= 0; i--)
            {
                IFrameworkModule module = _modules[i];
                try
                {
                    if (module is IAsyncFrameworkModule asyncModule)
                    {
                        await asyncModule.OnDestroyAsync(cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    Debug.LogWarning($"[Framework] 模块异步销毁被取消: {module.GetType().Name}");
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[Framework] 模块异步销毁失败: {module.GetType().Name}");
                    Debug.LogException(exception);
                }

                DestroyModuleSynchronously(module);
            }

            ClearRegistrations();
        }

        private void DestroyModulesSynchronously(int moduleCount)
        {
            int lastIndex = Math.Min(moduleCount, _modules.Count) - 1;
            for (int i = lastIndex; i >= 0; i--)
            {
                DestroyModuleSynchronously(_modules[i]);
            }

            ClearRegistrations();
        }

        private static void DestroyModuleSynchronously(IFrameworkModule module)
        {
            try
            {
                module.OnDestroy();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Framework] 模块同步销毁失败: {module.GetType().Name}");
                Debug.LogException(exception);
            }
        }

        private void ClearRegistrations()
        {
            _modules.Clear();
            _registrations.Clear();
            _moduleDict.Clear();
            _nextRegistrationOrder = 0;
        }

        private static Type[] NormalizeDependencies(Type moduleType, Type[] dependencies)
        {
            if (dependencies == null || dependencies.Length == 0)
            {
                return Array.Empty<Type>();
            }

            var unique = new HashSet<Type>();
            var result = new List<Type>(dependencies.Length);
            for (int i = 0; i < dependencies.Length; i++)
            {
                Type dependency = dependencies[i];
                if (dependency == null)
                {
                    throw new ArgumentException($"模块 {moduleType.Name} 的依赖类型不能为 null。", nameof(dependencies));
                }

                if (!typeof(IFrameworkModule).IsAssignableFrom(dependency))
                {
                    throw new ArgumentException(
                        $"{dependency.Name} 没有实现 IFrameworkModule，不能作为模块依赖。",
                        nameof(dependencies));
                }

                if (dependency == moduleType)
                {
                    throw new InvalidOperationException($"模块 {moduleType.Name} 不能依赖自身。");
                }

                if (unique.Add(dependency))
                {
                    result.Add(dependency);
                }
            }

            return result.ToArray();
        }

        private static void InsertByRegistrationOrder(
            List<ModuleRegistration> registrations,
            ModuleRegistration registration)
        {
            int index = registrations.Count;
            for (int i = 0; i < registrations.Count; i++)
            {
                if (registration.RegistrationOrder < registrations[i].RegistrationOrder)
                {
                    index = i;
                    break;
                }
            }

            registrations.Insert(index, registration);
        }

        private void ApplyStartupConfig()
        {
            if (_config == null || !_config.ConfigureCryptoOnStartup)
            {
                return;
            }

            if (TryGetModule(out CryptoModule crypto))
            {
                crypto.SetCryptoKey(_config.CryptoKey, _config.CryptoIV);
            }
        }

        private sealed class ModuleRegistration
        {
            public ModuleRegistration(
                IFrameworkModule module,
                Type[] dependencies,
                int registrationOrder)
            {
                Module = module;
                Dependencies = dependencies;
                RegistrationOrder = registrationOrder;
            }

            public IFrameworkModule Module { get; }
            public IReadOnlyList<Type> Dependencies { get; }
            public int RegistrationOrder { get; }
        }
    }

    public readonly struct FrameworkModuleSnapshot
    {
        public FrameworkModuleSnapshot(
            Type moduleType,
            IReadOnlyList<Type> dependencies,
            int initializationIndex)
        {
            ModuleType = moduleType;
            Dependencies = dependencies;
            InitializationIndex = initializationIndex;
        }

        public Type ModuleType { get; }
        public IReadOnlyList<Type> Dependencies { get; }
        public int InitializationIndex { get; }
    }
}
