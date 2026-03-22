using UnityEngine;
using GameFramework.Core;
using Cysharp.Threading.Tasks;

namespace GameFramework.Core.Demo
{
    /// <summary>
    /// 流程状态：主菜单
    /// </summary>
    public class ProcedureMainMenu : FsmState<GameDemoEntry>
    {
        public override async void OnEnter()
        {
            Log.Info("=== 进入流程：主菜单 ===");

            // 1. 播放主界面 BGM
            GameApp.Audio.PlayBGMAsync("BGM_MainMenu").Forget();

            // 2. 打开主界面 UI
            // await GameApp.UI.OpenUIAsync(MyGameUI.MainMenuPanel);

            // 接下来，玩家在主界面点击“开始游戏”按钮时，
            // 只需要在 UI 脚本里调用 GameApp.Fsm.GetFsm("GameProcedure").ChangeState<ProcedureBattle>(); 即可！
        }

        public override void OnLeave()
        {
            // 离开主菜单时，清理主界面的 UI
        }
    }
}