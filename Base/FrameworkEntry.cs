using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameFramework.Core
{
    /// <summary>
    /// 框架总入口（挂载在游戏的常驻节点上）
    /// </summary>
    public class FrameworkEntry : TMonoSingleton<FrameworkEntry>
    {
        private readonly List<IFrameworkModule> _modules = new List<IFrameworkModule>();
        private readonly Dictionary<Type, IFrameworkModule> _moduleDict = new Dictionary<Type, IFrameworkModule>();

        public void InitFrameworkModules()
        {
            // 【严格的初始化顺序】千万不能乱！

            // 1. 最先启动日志，确保后续的报错都能存下来
            RegisterModule(new LogModule());

            // 2. 启动事件中心，打通全局通讯血管
            RegisterModule(new EventModule());

            // 3. 启动文件系统，准备好读写硬盘的能力
            RegisterModule(new FileSystemModule());

            // 4. 启动加密模块，并立刻注入你的游戏专属密钥
            var crypto = new CryptoModule();
            RegisterModule(crypto);
            crypto.SetCryptoKey("MySuperSecretKey", "MySuperSecretIV1"); // 替换为你的密钥

            // 5. 启动存档模块 (依赖文件系统和加密)
            RegisterModule(new SaveModule());

            // 6. 启动时间与池化基建
            RegisterModule(new PoolModule());
            RegisterModule(new TimerModule());

            // 7. 启动表现层核心：资源、UI、音效、配置表
            RegisterModule(new ResourceModule());
            RegisterModule(new UIModule());
            RegisterModule(new AudioModule());
            RegisterModule(new ConfigModule());

            // 8. 启动 FSM 模块
            RegisterModule(new FsmModule());

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
            // 根据优先级排序
            _modules.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            _moduleDict.Add(type, module);

            module.OnInit();
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
    }
}