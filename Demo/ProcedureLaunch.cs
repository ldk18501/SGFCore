using Cysharp.Threading.Tasks;
using UnityEngine;
using GameFramework.Core;

namespace GameFramework.Core.Demo
{
    /// <summary>
    /// 流程状态：启动预热
    /// 负责 Addressables 初始化、检查热更新等耗时操作
    /// </summary>
    public class ProcedureLaunch : ProcedureBase
    {
        public override async void OnEnter()
        {
            Log.Info("=== 进入流程：启动预热 ===");

            // 1. 强制等待一帧，确保所有 MonoBehaviour 的 Awake/Start 彻底走完
            await UniTask.Yield();

            // 2. 初始化 Addressables 资源系统
            bool resourceReady = await GameApp.Res.EnsureInitializedAsync();
            if (!resourceReady)
            {
                Log.Fatal("[ProcedureLaunch] 资源系统初始化失败，启动流程中止。");
                return;
            }

            // TODO: 如果有热更新逻辑（Addressables CheckForCatalogUpdates），在这里执行

            // 3. 基建和资源系统热机完毕，进入预加载流程
            ChangeProcedure<ProcedurePreload>();
        }
    }
}
