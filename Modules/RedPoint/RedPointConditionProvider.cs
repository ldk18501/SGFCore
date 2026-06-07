using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameFramework.Core
{
    public class RedPointConditionProvider : MonoBehaviour
    {
        [SerializeField] private string _path;
        [SerializeField] private bool _keepRegisteredWhenDisabled = true;

        private readonly List<TriggerBinding> _triggerBindings = new List<TriggerBinding>();
        private bool _conditionRegistered;

        public string Path => _path;

        public void SetPath(string path, bool refreshImmediately = true)
        {
            bool wasRegistered = _conditionRegistered;
            if (wasRegistered)
            {
                UnregisterCondition();
            }

            _path = path;

            if (wasRegistered || isActiveAndEnabled)
            {
                EnsureConditionRegistered();
                if (refreshImmediately)
                {
                    RefreshCondition();
                }
            }
        }

        protected virtual void OnEnable()
        {
            EnsureConditionRegistered();
            RefreshCondition();
        }

        protected virtual void OnDisable()
        {
            if (!_keepRegisteredWhenDisabled)
            {
                UnregisterCondition();
                UnregisterTriggers();
            }
        }

        protected virtual void OnDestroy()
        {
            UnregisterCondition();
            UnregisterTriggers();
        }

        protected virtual bool IsReady()
        {
            return false;
        }

        protected virtual int GetRedPointCount()
        {
            return IsReady() ? 1 : 0;
        }

        protected virtual void RegisterTriggers()
        {
        }

        protected void SubscribeTrigger<T>() where T : struct
        {
            Action<T> handler = _ => RefreshCondition();
            SubscribeTrigger(handler);
        }

        protected void SubscribeTrigger<T>(Action<T> handler) where T : struct
        {
            if (handler == null)
            {
                return;
            }

            EventModule eventModule = GameApp.Event;
            if (eventModule == null)
            {
                return;
            }

            eventModule.AddListener(handler);
            _triggerBindings.Add(new TriggerBinding(typeof(T), handler));
        }

        protected void RefreshCondition()
        {
            RedPointModule module = GameApp.RedPoint;
            if (module != null)
            {
                module.Evaluate(_path, this);
            }
        }

        private void EnsureConditionRegistered()
        {
            if (!_conditionRegistered)
            {
                RedPointModule module = GameApp.RedPoint;
                if (module != null)
                {
                    module.SetCondition(_path, GetRedPointCount, this);
                    _conditionRegistered = true;
                }
            }

            if (_triggerBindings.Count == 0)
            {
                RegisterTriggers();
            }
        }

        private void UnregisterCondition()
        {
            if (!_conditionRegistered)
            {
                return;
            }

            RedPointModule module = GameApp.RedPoint;
            if (module != null)
            {
                module.ClearCondition(_path, this);
            }

            _conditionRegistered = false;
        }

        private void UnregisterTriggers()
        {
            EventModule eventModule = GameApp.Event;
            if (eventModule == null)
            {
                _triggerBindings.Clear();
                return;
            }

            for (int i = 0; i < _triggerBindings.Count; i++)
            {
                TriggerBinding binding = _triggerBindings[i];
                eventModule.RemoveListener(binding.EventType, binding.Handler);
            }

            _triggerBindings.Clear();
        }

        private struct TriggerBinding
        {
            public Type EventType;
            public Delegate Handler;

            public TriggerBinding(Type eventType, Delegate handler)
            {
                EventType = eventType;
                Handler = handler;
            }
        }
    }
}
