using UnityEngine;
using GameFramework.Core;

namespace GameFramework.Core.Demo
{
    /// <summary>
    /// 游戏绝对入口 (Bootstrap)
    /// 负责按严格顺序挂载和初始化框架底层模块，随后把控制权交接给游戏流程 FSM
    /// </summary>
    public class GameDemoEntry : MonoBehaviour
    {
        private void Start()
        {
            // 确保游戏在后台运行
            Application.runInBackground = true;
            // 锁定帧率（手机上通常是 30/60）
            Application.targetFrameRate = 60;

            // 1. 开始框架基础模块的注册与同步初始化
            InitFrameworkModules();

            // 2. 基础框架就绪，把控制权交给业务流程状态机
            StartGameProcedure();
        }

        private void InitFrameworkModules()
        {
            var framework = FrameworkEntry.Instance; // 触发 TMonoSingleton 的自动创建

            // 【严格的初始化顺序】千万不能乱！

            // 1. 最先启动日志，确保后续的报错都能存下来
            framework.RegisterModule(new LogModule());

            // 2. 启动事件中心，打通全局通讯血管
            framework.RegisterModule(new EventModule());

            // 3. 启动文件系统，准备好读写硬盘的能力
            framework.RegisterModule(new FileSystemModule());

            // 4. 启动加密模块，并立刻注入你的游戏专属密钥
            var crypto = new CryptoModule();
            framework.RegisterModule(crypto);
            crypto.SetCryptoKey("MySuperSecretKey", "MySuperSecretIV1"); // 替换为你的密钥

            // 5. 启动存档模块 (依赖文件系统和加密)
            framework.RegisterModule(new SaveModule());

            // 6. 启动时间与池化基建
            framework.RegisterModule(new PoolModule());
            framework.RegisterModule(new TimerModule());

            // 7. 启动表现层核心：资源、UI、音效、配置表
            framework.RegisterModule(new ResourceModule());
            framework.RegisterModule(new UIModule());
            framework.RegisterModule(new AudioModule());
            framework.RegisterModule(new ConfigModule());

            // 8. 启动 FSM 模块
            framework.RegisterModule(new FsmModule());

            Log.Info("<color=#00FF00>[GameEntry] 框架基础核心模块组装完毕！</color>");
        }

        private void StartGameProcedure()
        {
            // 利用我们刚刚写好的 FSM 模块，创建一个全局的“游戏流程状态机”
            // 注意：宿主 (Owner) 直接传 GameEntry 自己即可
            var procedureFsm = GameApp.Fsm.CreateFsm(
                "GameProcedure",
                this,
                new ProcedureLaunch(), // 启动预热状态
                new ProcedurePreload(), // 异步加载状态
                new ProcedureMainMenu() // 主菜单状态
            );

            // 启动引擎！进入第一个状态
            procedureFsm.Start<ProcedureLaunch>();
        }
    }
}