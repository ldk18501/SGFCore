using System;
using System.Collections.Generic;

namespace GameFramework.Core
{
    /// <summary>
    /// 游戏主流程模块，负责启动、切换和驱动 Procedure。
    /// </summary>
    public class ProcedureModule : IFrameworkModule
    {
        public int Priority => 75;

        private readonly Dictionary<Type, ProcedureBase> _procedures = new Dictionary<Type, ProcedureBase>();
        private readonly Dictionary<string, object> _blackboard = new Dictionary<string, object>();

        public object Owner { get; private set; }
        public ProcedureBase CurrentProcedure { get; private set; }
        public bool IsRunning => CurrentProcedure != null;

        public void OnInit()
        {
            Log.Module("Procedure", "流程模块初始化完成。");
        }

        public void OnUpdate(float deltaTime, float unscaledDeltaTime)
        {
            CurrentProcedure?.OnUpdate(deltaTime, unscaledDeltaTime);
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

            if (!_procedures.TryGetValue(procedureType, out ProcedureBase nextProcedure))
            {
                Log.Error($"[Procedure] 切换失败：未注册流程状态 {procedureType.Name}");
                return;
            }

            if (CurrentProcedure == nextProcedure)
            {
                return;
            }

            ProcedureBase previousProcedure = CurrentProcedure;
            previousProcedure?.OnLeave();
            CurrentProcedure = nextProcedure;

            GameApp.Event?.Broadcast(new ProcedureChangedEvent(
                previousProcedure != null ? previousProcedure.GetType() : null,
                CurrentProcedure.GetType()));

            CurrentProcedure.OnEnter();
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
            previousProcedure.OnLeave();
            CurrentProcedure = null;

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

        private void ClearProcedures()
        {
            foreach (var procedure in _procedures.Values)
            {
                procedure.OnDestroy();
            }

            _procedures.Clear();
            _blackboard.Clear();
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
