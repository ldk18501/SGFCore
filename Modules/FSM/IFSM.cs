using System;
using System.Collections.Generic;

namespace GameFramework.Core
{
    // 非泛型接口，供 FsmModule 统一存取和轮询
    public interface IFsmBase
    {
        string Name { get; }
        void Update(float deltaTime, float unscaledDeltaTime);
        void Destroy();
    }

    // 泛型接口，供状态内部使用
    public interface IFsm<T> : IFsmBase where T : class
    {
        T Owner { get; }
        FsmState<T> CurrentState { get; }
        void Start<TState>() where TState : FsmState<T>;
        void ChangeState<TState>() where TState : FsmState<T>;
        
        // 黑板功能：在状态机内部共享数据
        void SetData(string key, object value);
        TData GetData<TData>(string key);
    }
}