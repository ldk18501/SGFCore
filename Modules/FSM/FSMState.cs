using System;

namespace GameFramework.Core
{
    /// <summary>
    /// 泛型状态基类
    /// T: 状态机的拥有者（宿主）类型，比如 Player, Monster, GameLoop
    /// </summary>
    public abstract class FsmState<T> where T : class
    {
        // 方便在子类中直接通过 _fsm 访问宿主和黑板数据
        protected IFsm<T> _fsm;

        internal void InternalInit(IFsm<T> fsm)
        {
            _fsm = fsm;
            OnInit();
        }

        // 快捷获取宿主，享受 IDE 代码提示的快感
        protected T Owner => _fsm.Owner;

        // ==========================================
        // 生命周期虚方法，供子类重写
        // ==========================================
        
        /// <summary> 状态机创建时初始化一次 </summary>
        protected virtual void OnInit() { }

        /// <summary> 进入本状态时调用 </summary>
        public virtual void OnEnter() { }

        /// <summary> 轮询更新 </summary>
        public virtual void OnUpdate(float deltaTime, float unscaledDeltaTime) { }

        /// <summary> 离开本状态时调用 </summary>
        public virtual void OnLeave() { }

        /// <summary> 状态机被销毁时清理 </summary>
        public virtual void OnDestroy() { }

        // ==========================================
        // 核心能力：切换状态
        // ==========================================
        protected void ChangeState<TState>() where TState : FsmState<T>
        {
            _fsm.ChangeState<TState>();
        }
    }
}