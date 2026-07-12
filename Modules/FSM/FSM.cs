using System;
using System.Collections.Generic;

namespace GameFramework.Core
{
    /// <summary>
    /// 泛型状态机实现
    /// </summary>
    public class Fsm<T> : IFsm<T> where T : class
    {
        public string Name { get; private set; }
        public T Owner { get; private set; }
        public FsmState<T> CurrentState { get; private set; }

        private readonly Dictionary<Type, FsmState<T>> _states = new Dictionary<Type, FsmState<T>>();
        private readonly Dictionary<string, object> _blackboard = new Dictionary<string, object>();
        private bool _isChangingState;
        private Type _pendingStateType;

        // 初始化状态机
        public Fsm(string name, T owner, params FsmState<T>[] states)
        {
            Name = name;
            Owner = owner;

            // 将所有传入的状态实例缓存起来，实现零 GC 切换
            foreach (var state in states)
            {
                Type type = state.GetType();
                if (!_states.ContainsKey(type))
                {
                    state.InternalInit(this);
                    _states.Add(type, state);
                }
            }
        }

        public void Start<TState>() where TState : FsmState<T>
        {
            if (CurrentState != null) return;
            ChangeState<TState>();
        }

        public void ChangeState<TState>() where TState : FsmState<T>
        {
            RequestStateChange(typeof(TState));
        }

        private void RequestStateChange(Type targetType)
        {
            if (!_states.ContainsKey(targetType))
            {
                Log.Error($"[FSM] 状态切换失败：未在状态机中找到状态 {targetType.Name}");
                return;
            }

            _pendingStateType = targetType;
            if (_isChangingState)
            {
                return;
            }

            _isChangingState = true;
            try
            {
                int transitionCount = 0;
                while (_pendingStateType != null)
                {
                    if (++transitionCount > _states.Count + 1)
                    {
                        Log.Error($"[FSM] 状态机 {Name} 在一次切换中产生了过多连续跳转，已中止以避免死循环。");
                        _pendingStateType = null;
                        break;
                    }

                    Type nextType = _pendingStateType;
                    _pendingStateType = null;
                    FsmState<T> targetState = _states[nextType];
                    if (CurrentState == targetState)
                    {
                        continue;
                    }

                    CurrentState?.OnLeave();
                    CurrentState = targetState;
                    CurrentState.OnEnter();
                }
            }
            finally
            {
                _isChangingState = false;
            }
        }

        public void Update(float deltaTime, float unscaledDeltaTime)
        {
            CurrentState?.OnUpdate(deltaTime, unscaledDeltaTime);
        }

        public void Destroy()
        {
            CurrentState?.OnLeave();
            foreach (var state in _states.Values)
            {
                state.OnDestroy();
            }
            _states.Clear();
            _blackboard.Clear();
            CurrentState = null;
            Owner = null;
        }

        // --- 黑板数据实现 ---
        public void SetData(string key, object value) => _blackboard[key] = value;
        
        public TData GetData<TData>(string key)
        {
            if (_blackboard.TryGetValue(key, out var val) && val is TData data) return data;
            return default;
        }

        public void SetData<TData>(BlackboardKey<TData> key, TData value) => _blackboard[key.Name] = value;

        public bool TryGetData<TData>(BlackboardKey<TData> key, out TData value)
        {
            if (_blackboard.TryGetValue(key.Name, out object rawValue) && rawValue is TData typedValue)
            {
                value = typedValue;
                return true;
            }

            value = default;
            return false;
        }

        public bool RemoveData<TData>(BlackboardKey<TData> key) => _blackboard.Remove(key.Name);
    }
}
