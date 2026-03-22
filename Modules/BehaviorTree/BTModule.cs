using System.Collections.Generic;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameFramework.Core
{
    public class BTModule : IFrameworkModule
    {
        public int Priority => 80;

        private readonly List<BehaviorTree> _activeTrees = new List<BehaviorTree>();
        
        // 记录每个行为树组件对应加载了哪个外部资源文件
        private readonly Dictionary<BehaviorTree, ExternalBehaviorTree> _treeAssetMap = new Dictionary<BehaviorTree, ExternalBehaviorTree>();

        public void OnInit()
        {
            Log.Module("AI", "行为树管理模块初始化完成。");
        }

        public void OnUpdate(float deltaTime, float unscaledDeltaTime) { }
        
        public void OnDestroy() 
        {
            // 框架销毁时（比如切大场景或退出游戏），安全卸载所有还在跑的树
            for (int i = _activeTrees.Count - 1; i >= 0; i--)
            {
                DetachTree(_activeTrees[i]);
            }
            _activeTrees.Clear();
            _treeAssetMap.Clear();
        }

        public async UniTask<BehaviorTree> AttachTreeAsync(GameObject owner, string treeAddress, bool autoStart = true)
        {
            if (owner == null) return null;

            var externalTree = await GameApp.Res.LoadAssetAsync<ExternalBehaviorTree>(treeAddress);
            if (externalTree == null)
            {
                Log.Error($"[AI] 行为树加载失败: {treeAddress}");
                return null;
            }

            var tree = owner.AddComponent<BehaviorTree>();
            tree.StartWhenEnabled = false; 
            tree.ExternalBehavior = externalTree;

            _activeTrees.Add(tree);
            _treeAssetMap[tree] = externalTree;

            if (autoStart)
            {
                tree.EnableBehavior();
            }

            return tree;
        }

        public void DetachTree(BehaviorTree tree)
        {
            if (tree == null) return;
            
            tree.DisableBehavior();
            _activeTrees.Remove(tree);

            // 利用追踪器获取真正的资源引用并释放
            if (_treeAssetMap.TryGetValue(tree, out ExternalBehaviorTree externalTree))
            {
                GameApp.Res.ReleaseAsset(externalTree);
                _treeAssetMap.Remove(tree);
                Log.Info($"[AI] 已释放行为树资源引用，当前对象: {tree.gameObject.name}");
            }
            
            UnityEngine.Object.Destroy(tree);
        }

        public void PauseAllAI()
        {
            foreach (var tree in _activeTrees)
            {
                if (tree != null && tree.ExecutionStatus == TaskStatus.Running)
                {
                    tree.DisableBehavior(true);
                }
            }
        }

        public void ResumeAllAI()
        {
            foreach (var tree in _activeTrees)
            {
                if (tree != null)
                {
                    tree.EnableBehavior();
                }
            }
        }
    }
}