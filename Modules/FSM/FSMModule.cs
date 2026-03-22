using System.Collections.Generic;

namespace GameFramework.Core
{
    public class FsmModule : IFrameworkModule
    {
        public int Priority => 70;

        // 存储所有活跃的状态机
        private readonly Dictionary<string, IFsmBase> _fsms = new Dictionary<string, IFsmBase>();

        public void OnInit()
        {
            Log.Module("FSM", "有限状态机模块初始化完成。");
        }

        public void OnUpdate(float deltaTime, float unscaledDeltaTime)
        {
            // 统一驱动所有的状态机更新
            foreach (var fsm in _fsms.Values)
            {
                fsm.Update(deltaTime, unscaledDeltaTime);
            }
        }

        public void OnDestroy()
        {
            foreach (var fsm in _fsms.Values)
            {
                fsm.Destroy();
            }
            _fsms.Clear();
        }

        /// <summary>
        /// 创建并注册状态机
        /// </summary>
        public IFsm<T> CreateFsm<T>(string name, T owner, params FsmState<T>[] states) where T : class
        {
            if (_fsms.ContainsKey(name))
            {
                Log.Error($"[FSM] 已经存在名为 {name} 的状态机！");
                return null;
            }

            var fsm = new Fsm<T>(name, owner, states);
            _fsms.Add(name, fsm);
            return fsm;
        }

        /// <summary>
        /// 销毁指定状态机
        /// </summary>
        public void DestroyFsm(string name)
        {
            if (_fsms.TryGetValue(name, out var fsm))
            {
                fsm.Destroy();
                _fsms.Remove(name);
            }
        }
    }
}