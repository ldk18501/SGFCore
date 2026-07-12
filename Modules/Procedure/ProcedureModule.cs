using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameFramework.Core
{
    /// <summary>
    /// 游戏主流程模块，负责启动、切换和驱动 Procedure。
    /// </summary>
    public class ProcedureModule : IFrameworkModule
    {
        private readonly Dictionary<Type, ProcedureBase> _procedures = new Dictionary<Type, ProcedureBase>();
        private readonly Dictionary<string, object> _blackboard = new Dictionary<string, object>();
        private CancellationTokenSource _procedureCts;
        private Type _pendingProcedureType;
        private bool _transitionRunnerActive;

        public object Owner { get; private set; }
        public ProcedureBase CurrentProcedure { get; private set; }
        public bool IsRunning => CurrentProcedure != null;
        public bool IsTransitioning { get; private set; }

        public void OnInit()
        {
            Log.Module("Procedure", "流程模块初始化完成。");
        }

        public void OnUpdate(float deltaTime, float unscaledDeltaTime)
        {
            if (!IsTransitioning)
            {
                CurrentProcedure?.OnUpdate(deltaTime, unscaledDeltaTime);
            }
        }

        public void OnDestroy()
        {
            Stop();
            ClearProcedures();
            Owner = null;
        }

        public void Start(object owner, params ProcedureBase[] procedures)
        {
            if (procedures == null || procedures.Length == 0)
            {
                Log.Error("[Procedure] 启动失败：没有传入任何流程状态。");
                return;
            }

            Stop();
            ClearProcedures();
            RegisterProcedures(owner, procedures);
            ChangeProcedure(procedures[0].GetType());
        }

        public void Start<TFirstProcedure>(object owner, params ProcedureBase[] procedures)
            where TFirstProcedure : ProcedureBase
        {
            Stop();
            ClearProcedures();
            RegisterProcedures(owner, procedures);
            ChangeProcedure<TFirstProcedure>();
        }

        public void RegisterProcedures(object owner, params ProcedureBase[] procedures)
        {
            Owner = owner;

            if (procedures == null)
            {
                return;
            }

            for (int i = 0; i < procedures.Length; i++)
            {
                ProcedureBase procedure = procedures[i];
                if (procedure == null)
                {
                    continue;
                }

                Type type = procedure.GetType();
                if (_procedures.ContainsKey(type))
                {
                    Log.Warning($"[Procedure] 流程状态重复注册，已忽略: {type.Name}");
                    continue;
                }

                procedure.InternalInit(this);
                _procedures.Add(type, procedure);
            }
        }

        public void ChangeProcedure<TProcedure>() where TProcedure : ProcedureBase
        {
            ChangeProcedure(typeof(TProcedure));
        }

        public void ChangeProcedure(Type procedureType)
        {
            if (procedureType == null)
            {
                Log.Error("[Procedure] 切换失败：目标流程类型为空。");
                return;
            }

            if (!_procedures.ContainsKey(procedureType))
            {
                Log.Error($"[Procedure] 切换失败：未注册流程状态 {procedureType.Name}");
                return;
            }

            if (CurrentProcedure != null && CurrentProcedure.GetType() == procedureType && _pendingProcedureType == null)
            {
                return;
            }

            _pendingProcedureType = procedureType;
            CancelCurrentProcedureAsyncWork();
            if (!_transitionRunnerActive)
            {
                RunTransitionQueueAsync().Forget();
            }
        }

        public bool HasProcedure<TProcedure>() where TProcedure : ProcedureBase
        {
            return _procedures.ContainsKey(typeof(TProcedure));
        }

        public TProcedure GetProcedure<TProcedure>() where TProcedure : ProcedureBase
        {
            return _procedures.TryGetValue(typeof(TProcedure), out ProcedureBase procedure)
                ? procedure as TProcedure
                : null;
        }

        public void Stop()
        {
            if (CurrentProcedure == null)
            {
                return;
            }

            ProcedureBase previousProcedure = CurrentProcedure;
            _pendingProcedureType = null;
            previousProcedure.OnLeave();
            CancelCurrentProcedureAsyncWork();
            CurrentProcedure = null;
            IsTransitioning = false;

            GameApp.Event?.Broadcast(new ProcedureStoppedEvent(previousProcedure.GetType()));
        }

        public void SetData(string key, object value)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            _blackboard[key] = value;
        }

        public TData GetData<TData>(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return default;
            }

            return _blackboard.TryGetValue(key, out object value) && value is TData data ? data : default;
        }

        public bool RemoveData(string key)
        {
            return !string.IsNullOrEmpty(key) && _blackboard.Remove(key);
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

        private void ClearProcedures()
        {
            foreach (var procedure in _procedures.Values)
            {
                procedure.OnDestroy();
            }

            _procedures.Clear();
            _blackboard.Clear();
        }

        private void CancelCurrentProcedureAsyncWork()
        {
            if (_procedureCts == null)
            {
                return;
            }

            _procedureCts.Cancel();
            _procedureCts.Dispose();
            _procedureCts = null;
        }

        private async UniTaskVoid RunTransitionQueueAsync()
        {
            _transitionRunnerActive = true;
            try
            {
                while (_pendingProcedureType != null)
                {
                    Type nextType = _pendingProcedureType;
                    _pendingProcedureType = null;
                    ProcedureBase nextProcedure = _procedures[nextType];
                    if (CurrentProcedure == nextProcedure)
                    {
                        continue;
                    }

                    IsTransitioning = true;
                    ProcedureBase previousProcedure = CurrentProcedure;
                    previousProcedure?.OnLeave();
                    CurrentProcedure = nextProcedure;

                    GameApp.Event?.Broadcast(new ProcedureChangedEvent(
                        previousProcedure != null ? previousProcedure.GetType() : null,
                        CurrentProcedure.GetType()));

                    _procedureCts = new CancellationTokenSource();
                    try
                    {
                        await CurrentProcedure.OnEnterAsync(_procedureCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (Exception e)
                    {
                        Log.Error($"[Procedure] 流程进入异常: {nextProcedure.GetType().Name}, {e}");
                    }
                    finally
                    {
                        CancelCurrentProcedureAsyncWork();
                    }
                }
            }
            finally
            {
                IsTransitioning = false;
                _transitionRunnerActive = false;
                if (_pendingProcedureType != null)
                {
                    RunTransitionQueueAsync().Forget();
                }
            }
        }
    }

    public readonly struct ProcedureChangedEvent
    {
        public readonly Type PreviousProcedureType;
        public readonly Type CurrentProcedureType;

        public ProcedureChangedEvent(Type previousProcedureType, Type currentProcedureType)
        {
            PreviousProcedureType = previousProcedureType;
            CurrentProcedureType = currentProcedureType;
        }
    }

    public readonly struct ProcedureStoppedEvent
    {
        public readonly Type PreviousProcedureType;

        public ProcedureStoppedEvent(Type previousProcedureType)
        {
            PreviousProcedureType = previousProcedureType;
        }
    }
}
