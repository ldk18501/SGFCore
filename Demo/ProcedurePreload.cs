using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using GameFramework.Core;

namespace GameFramework.Core.Demo
{
    /// <summary>
    /// 流程状态：数据预加载
    /// 负责加载配置表、玩家存档、以及一些全局常驻预制体
    /// </summary>
    public class ProcedurePreload : FsmState<GameDemoEntry>
    {
        public override async void OnEnter()
        {
            Log.Info("=== 进入流程：资源与配置预加载 ===");

            // 1. 打开 Loading UI 面板 (传入 0 表示进度 0%)
            // int loadingPanelId = await GameApp.UI.OpenUIAsync(MyGameUI.LoadingPanel, 0f);

            // 2. 注册并批量加载所有配置表
            // GameApp.Config.RegisterConfig("ItemValueConf", ItemValueConfConfig.Load);
            // ... 注册其他表 ...

            var configsToLoad = new Dictionary<string, string>
            {
                { "ItemValueConf", "ItemValueConf" } // 因为你有了自动化工具，这里地址直接写简写即可！
            };
            
            // 模拟进度条更新 (在真实项目中可以细分加载步骤)
            // GameApp.Event.Broadcast(new UpdateLoadingProgressEvent { Progress = 0.5f });

            await GameApp.Config.LoadConfigsBatchAsync(configsToLoad);

            // 3. 读取玩家本地存档
            var save = GameApp.Save.LoadData<SimulationSaveData>("MainSave");
            // 把它存到 FSM 黑板里，或者丢给全局的 DataManager
            _fsm.SetData("CurrentSave", save);

            // 4. 预加载完毕，准备进入游戏！
            // GameApp.UI.CloseUI(loadingPanelId); // 关闭 Loading 界面的示例
            
            ChangeState<ProcedureMainMenu>();
        }
    }
}