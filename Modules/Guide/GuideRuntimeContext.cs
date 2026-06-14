using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace GameFramework.Core
{
    public sealed class GuideStartOptions
    {
        public IEnumerable<GuideDefinition> Definitions { get; set; }
        public IGuideView View { get; set; }
        public string SaveName { get; set; } = "Guide";
        public bool UseEncryption { get; set; } = true;
        public bool ValidateOnStart { get; set; } = true;
        public bool AutoEvaluateOnStart { get; set; } = true;
        public IList<IGuideInstaller> Installers { get; } = new List<IGuideInstaller>();
    }

    public interface IGuideInstaller
    {
        void Install(IGuideRegistry registry);
    }

    public interface IGuideRegistry
    {
        void RegisterTrigger(string name, IGuideTrigger trigger);
        void RegisterCondition(string name, IGuideCondition condition);
        void RegisterCondition(string name, Func<GuideConditionContext, string, bool> evaluator);
        void RegisterAction(string name, IGuideAction action);
        void RegisterAction(string name, Action<GuideActionContext, string> executor);
    }

    public sealed class GuideTriggerContext
    {
        private readonly Dictionary<string, string> _parameters;

        public GuideTriggerContext(
            string key,
            object payload = null,
            object source = null,
            IDictionary<string, string> parameters = null)
        {
            Key = NormalizeKey(key);
            Payload = payload;
            Source = source;
            _parameters = parameters != null
                ? new Dictionary<string, string>(parameters)
                : new Dictionary<string, string>();
        }

        public string Key { get; }
        public object Payload { get; }
        public object Source { get; }
        public IReadOnlyDictionary<string, string> Parameters => _parameters;

        public bool TryGetParameter(string name, out string value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            return _parameters.TryGetValue(name.Trim(), out value);
        }

        public bool TryGetPayload<T>(out T value)
        {
            if (Payload is T typedValue)
            {
                value = typedValue;
                return true;
            }

            value = default(T);
            return false;
        }

        internal string GetValue(string name)
        {
            if (TryGetParameter(name, out string parameterValue))
            {
                return parameterValue;
            }

            if (Payload == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            string key = name.Trim();
            if (Payload is IDictionary<string, string> stringMap &&
                stringMap.TryGetValue(key, out string stringValue))
            {
                return stringValue;
            }

            if (Payload is IDictionary<string, object> objectMap &&
                objectMap.TryGetValue(key, out object objectValue))
            {
                return Convert.ToString(objectValue, CultureInfo.InvariantCulture);
            }

            Type payloadType = Payload.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase;

            PropertyInfo property = payloadType.GetProperty(key, flags);
            if (property != null)
            {
                object propertyValue = property.GetValue(Payload, null);
                return Convert.ToString(propertyValue, CultureInfo.InvariantCulture);
            }

            FieldInfo field = payloadType.GetField(key, flags);
            if (field != null)
            {
                object fieldValue = field.GetValue(Payload);
                return Convert.ToString(fieldValue, CultureInfo.InvariantCulture);
            }

            return null;
        }

        private static string NormalizeKey(string key)
        {
            return string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim();
        }
    }

    public sealed class GuideConditionContext
    {
        public GuideConditionContext(
            GuideModule module,
            GuideDefinition definition,
            GuideTriggerContext triggerContext)
        {
            Module = module;
            Definition = definition;
            Trigger = triggerContext;
        }

        public GuideModule Module { get; }
        public GuideDefinition Definition { get; }
        public GuideTriggerContext Trigger { get; }
    }

    public sealed class GuideActionContext
    {
        public GuideActionContext(
            GuideModule module,
            GuideDefinition definition,
            GuideTriggerContext triggerContext,
            GuideViewContext viewContext)
        {
            Module = module;
            Definition = definition;
            Trigger = triggerContext;
            ViewContext = viewContext;
        }

        public GuideModule Module { get; }
        public GuideDefinition Definition { get; }
        public GuideTriggerContext Trigger { get; }
        public GuideViewContext ViewContext { get; }
    }

    public interface IGuideTrigger
    {
        void Bind(IGuideTriggerSink sink);
        void Unbind(IGuideTriggerSink sink);
    }

    public interface IGuideTriggerSink
    {
        void Fire(string triggerKey);
        void Fire(string triggerKey, object payload);
        void Fire(string triggerKey, object payload, object source, IDictionary<string, string> parameters = null);
    }

    public sealed class GuideEventTrigger<T> : IGuideTrigger where T : struct
    {
        private readonly string _triggerKey;
        private readonly Func<T, bool> _filter;
        private readonly Func<T, object> _payloadFactory;
        private readonly Func<T, IDictionary<string, string>> _parametersFactory;
        private IGuideTriggerSink _sink;

        public GuideEventTrigger(
            string triggerKey,
            Func<T, bool> filter = null,
            Func<T, object> payloadFactory = null,
            Func<T, IDictionary<string, string>> parametersFactory = null)
        {
            _triggerKey = triggerKey;
            _filter = filter;
            _payloadFactory = payloadFactory;
            _parametersFactory = parametersFactory;
        }

        public void Bind(IGuideTriggerSink sink)
        {
            _sink = sink;
            GameApp.Event?.AddListener<T>(OnEvent);
        }

        public void Unbind(IGuideTriggerSink sink)
        {
            GameApp.Event?.RemoveListener<T>(OnEvent);
            _sink = null;
        }

        private void OnEvent(T eventData)
        {
            if (_sink == null || (_filter != null && !_filter(eventData)))
            {
                return;
            }

            object payload = _payloadFactory != null ? _payloadFactory(eventData) : eventData;
            IDictionary<string, string> parameters = _parametersFactory != null
                ? _parametersFactory(eventData)
                : null;
            _sink.Fire(_triggerKey, payload, eventData, parameters);
        }
    }

    public interface IGuideCondition
    {
        bool IsSatisfied(GuideConditionContext context, string parameter);
    }

    public interface IGuideAction
    {
        void Execute(GuideActionContext context, string parameter);
    }

    public sealed class GuideConditionDelegate : IGuideCondition
    {
        private readonly Func<GuideConditionContext, string, bool> _evaluator;

        public GuideConditionDelegate(Func<GuideConditionContext, string, bool> evaluator)
        {
            _evaluator = evaluator;
        }

        public bool IsSatisfied(GuideConditionContext context, string parameter)
        {
            return _evaluator != null && _evaluator(context, parameter);
        }
    }

    public sealed class GuideActionDelegate : IGuideAction
    {
        private readonly Action<GuideActionContext, string> _executor;

        public GuideActionDelegate(Action<GuideActionContext, string> executor)
        {
            _executor = executor;
        }

        public void Execute(GuideActionContext context, string parameter)
        {
            _executor?.Invoke(context, parameter);
        }
    }

    public readonly struct GuideConfigExpression
    {
        public GuideConfigExpression(string name, string parameter)
        {
            Name = string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();
            Parameter = parameter == null ? string.Empty : parameter.Trim();
        }

        public string Name { get; }
        public string Parameter { get; }
        public bool IsValid => !string.IsNullOrEmpty(Name);

        public static bool TryParse(string value, out GuideConfigExpression expression)
        {
            expression = default(GuideConfigExpression);
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string trimmed = value.Trim();
            int openIndex = trimmed.IndexOf('(');
            if (openIndex > 0 && trimmed.EndsWith(")", StringComparison.Ordinal))
            {
                string name = trimmed.Substring(0, openIndex);
                string parameter = trimmed.Substring(openIndex + 1, trimmed.Length - openIndex - 2);
                expression = new GuideConfigExpression(name, parameter);
                return expression.IsValid;
            }

            int colonIndex = trimmed.IndexOf(':');
            if (colonIndex > 0)
            {
                expression = new GuideConfigExpression(
                    trimmed.Substring(0, colonIndex),
                    trimmed.Substring(colonIndex + 1));
                return expression.IsValid;
            }

            expression = new GuideConfigExpression(trimmed, string.Empty);
            return expression.IsValid;
        }

        public static List<GuideConfigExpression> ParseList(string value)
        {
            List<GuideConfigExpression> expressions = new List<GuideConfigExpression>();
            if (string.IsNullOrWhiteSpace(value))
            {
                return expressions;
            }

            int start = 0;
            int depth = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '(')
                {
                    depth++;
                }
                else if (c == ')' && depth > 0)
                {
                    depth--;
                }
                else if ((c == ';' || c == '|') && depth == 0)
                {
                    AddExpression(value.Substring(start, i - start), expressions);
                    start = i + 1;
                }
            }

            AddExpression(value.Substring(start), expressions);
            return expressions;
        }

        private static void AddExpression(string value, List<GuideConfigExpression> expressions)
        {
            if (TryParse(value, out GuideConfigExpression expression))
            {
                expressions.Add(expression);
            }
        }
    }
}
