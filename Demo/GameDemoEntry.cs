using UnityEngine;
using GameFramework.Core;

namespace GameFramework.Core.Demo
{
    /// <summary>
    /// 游戏绝对入口 (Bootstrap)
    /// 负责按严格顺序挂载和初始化框架底层模块，随后把控制权交接给游戏主流程。
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

            // 2. 基础框架就绪，把控制权交给业务流程
            StartGameProcedure();
        }

        private void InitFrameworkModules()
        {
            FrameworkEntry.Instance.InitFrameworkModules();
        }

        private void StartGameProcedure()
        {
            GameApp.Procedure.Start(
                this,
                new ProcedureLaunch(),
                new ProcedurePreload(),
                new ProcedureMainMenu());
        }
    }
}
