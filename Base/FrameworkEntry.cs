using UnityEngine;
using System;
using System.Collections.Generic;

namespace GameFramework.Core
{
    /// <summary>
    /// 框架总入口（挂载在游戏的常驻节点上）
    /// </summary>
    public class FrameworkEntry : TMonoSingleton<FrameworkEntry>
    {
        private readonly List<IFrameworkModule> _modules = new List<IFrameworkModule>();
        private readonly Dictionary<Type, IFrameworkModule> _moduleDict = new Dictionary<Type, IFrameworkModule>();
        private FrameworkConfig _config;
        private bool _isInitialized;
        private bool _isInitializing;

        public void InitFrameworkModules(FrameworkConfig config = null)
        {
            if (_isInitialized || _isInitializing)
            {
                Debug.LogWarning("[Framework] 框架模块已经初始化，重复调用已忽略。");
                return;
            }

            _isInitializing = true;
            _config = config;
            GameApp.Reset();

            // 【严格的初始化顺序】千万不能乱！

            // 1. 最先启动日志，确保后续的报错都能存下来
            RegisterModule(new LogModule());

            // 2. 启动事件中心，打通全局通讯血管
            RegisterModule(new EventModule());

            // 3. 启动文件系统，准备好读写硬盘的能力
            RegisterModule(new FileSystemModule());

            // 4. 启动加密模块，密钥由 FrameworkConfig 或业务层显式注入
            RegisterModule(new CryptoModule());

            // 5. 启动存档模块 (依赖文件系统和加密)
            RegisterModule(new SaveModule());

            // 6. 启动时间与池化基建
            RegisterModule(new PoolModule());
            RegisterModule(new TimerModule());
            RegisterModule(new TimeModule());

            // 7. 启动表现层核心：资源、场景、配置、多语言、UI、音效
            RegisterModule(new ResourceModule());
            RegisterModule(new SceneModule());
            RegisterModule(new ConfigModule());
            RegisterModule(new LocalizationModule());
            RegisterModule(new UIModule());
            RegisterModule(new RedPointModule());
            RegisterModule(new GuideModule());
            RegisterModule(new AudioModule());

            // 8. 启动 FSM 与游戏主流程模块
            RegisterModule(new FsmModule());
            RegisterModule(new ProcedureModule());

            _modules.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            for (int i = 0; i < _modules.Count; i++)
            {
                _modules[i].OnInit();
            }

            ApplyStartupConfig();
            _isInitializing = false;
            _isInitialized = true;
            Log.Info("<color=#00FF00>[GameEntry] 框架基础核心模块组装完毕！</color>");
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            float unscaledDt = Time.unscaledDeltaTime;

            foreach (var module in _modules)
            {
                module.OnUpdate(dt, unscaledDt);
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy(); // 标记程序退出
            for (int i = _modules.Count - 1; i >= 0; i--)
            {
                _modules[i].OnDestroy();
            }

            _modules.Clear();
            _moduleDict.Clear();
            _isInitialized = false;
            _isInitializing = false;
            _config = null;
            GameApp.Reset();
        }

        /// <summary>
        /// 注册并初始化模块
        /// </summary>
        public void RegisterModule(IFrameworkModule module)
        {
            Type type = module.GetType();
            if (_moduleDict.ContainsKey(type))
            {
                Debug.LogWarning($"模块 {type.Name} 已经注册过了！");
                return;
            }

            _modules.Add(module);
            _moduleDict.Add(type, module);

            if (_isInitialized && !_isInitializing)
            {
                _modules.Sort((a, b) => a.Priority.CompareTo(b.Priority));
                module.OnInit();
            }

            Debug.Log($"[Framework] 模块注册成功: {type.Name}");
        }

        /// <summary>
        /// 获取指定模块
        /// </summary>
        public T GetModule<T>() where T : class, IFrameworkModule
        {
            Type type = typeof(T);
            if (_moduleDict.TryGetValue(type, out var module))
            {
                return module as T;
            }

            Debug.LogError($"[Framework] 找不到模块: {type.Name}");
            return null;
        }

        private void ApplyStartupConfig()
        {
            if (_config == null || !_config.ConfigureCryptoOnStartup)
            {
                return;
            }

            CryptoModule crypto = GetModule<CryptoModule>();
            if (crypto == null)
            {
                return;
            }

            crypto.SetCryptoKey(_config.CryptoKey, _config.CryptoIV);
        }
    }
}
