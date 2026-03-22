using Cysharp.Threading.Tasks;
using UnityEngine;
using GameFramework.Core;

namespace GameFramework.Core.Demo
{
    /// <summary>
    /// 流程状态：启动预热
    /// 负责 Addressables 初始化、检查热更新等耗时操作
    /// </summary>
    public class ProcedureLaunch : FsmState<GameDemoEntry>
    {
        public override async void OnEnter()
        {
            Log.Info("=== 进入流程：启动预热 ===");

            // 1. 强制等待一帧，确保所有 MonoBehaviour 的 Awake/Start 彻底走完
            await UniTask.Yield();

            // 2. 初始化 Addressables 资源系统 (这是个异步过程)
            // 我们之前的 ResourceModule.OnInit 里面其实可以把 InitializeAddressables 暴露出来返回 UniTask
            // 这里为了演示，假设它内部已经初始化好了，我们检查一下
            // await GameApp.Res.EnsureInitializedAsync(); 

            // TODO: 如果有热更新逻辑（Addressables CheckForCatalogUpdates），在这里执行

            // 3. 基建和资源系统热机完毕，进入预加载流程
            ChangeState<ProcedurePreload>();
        }
    }
}