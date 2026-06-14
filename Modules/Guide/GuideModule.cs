using System;
using System.Collections.Generic;
using System.Globalization;

namespace GameFramework.Core
{
    public sealed class GuideModule : IFrameworkModule, IGuideRegistry, IGuideTriggerSink
    {
        private const string DefaultSaveName = "Guide";
        private const int MaxPendingTriggerCount = 128;

        private readonly Dictionary<int, GuideDefinition> _definitions =
            new Dictionary<int, GuideDefinition>();
        private readonly Dictionary<string, List<GuideDefinition>> _groups =
            new Dictionary<string, List<GuideDefinition>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<GuideDefinition>> _triggerIndex =
            new Dictionary<string, List<GuideDefinition>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IGuideTrigger> _triggers =
            new Dictionary<string, IGuideTrigger>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IGuideCondition> _conditions =
            new Dictionary<string, IGuideCondition>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IGuideAction> _actions =
            new Dictionary<string, IGuideAction>(StringComparer.OrdinalIgnoreCase);
        private readonly Queue<GuideTriggerContext> _pendingTriggers =
            new Queue<GuideTriggerContext>();
        private readonly HashSet<int> _completedStepIds = new HashSet<int>();
        private readonly HashSet<int> _skippedStepIds = new HashSet<int>();

        private SaveModule _saveModule;
        private EventModule _eventModule;
        private GuideSaveData _saveData;
        private GuideRuntimeStep _current;
        private IGuideView _view;
        private string _saveName = DefaultSaveName;
        private bool _useEncryption = true;
        private bool _isStarted;
        private bool _isEvaluating;

        public int Priority => 51;
        public bool IsStarted => _isStarted;
        public bool IsRunning => _current != null;
        public GuideDefinition CurrentDefinition => _current?.Definition;

        public void OnInit()
        {
            _saveModule = FrameworkEntry.Instance.GetModule<SaveModule>();
            _eventModule = FrameworkEntry.Instance.GetModule<EventModule>();
            RegisterBuiltInConditions();
            RegisterBuiltInActions();
            Log.Module("Guide", "Guide module initialized. Waiting for StartGuide.");
        }

        public void OnUpdate(float deltaTime, float unscaledDeltaTime)
        {
            if (!_isStarted || _current == null || _current.DelayRemaining <= 0f)
            {
                return;
            }

            _current.DelayRemaining -= unscaledDeltaTime;
            if (_current.DelayRemaining <= 0f)
            {
                CompleteCurrentStep();
            }
        }

        public void OnDestroy()
        {
            StopGuide();
            _conditions.Clear();
            _actions.Clear();
        }

        public void StartGuide(GuideStartOptions options)
        {
            if (options == null)
            {
                Log.Error("[Guide] StartGuide failed: options is null.");
                return;
            }

            StopGuide();

            _conditions.Clear();
            _actions.Clear();
            RegisterBuiltInConditions();
            RegisterBuiltInActions();

            _saveName = string.IsNullOrWhiteSpace(options.SaveName)
                ? DefaultSaveName
                : options.SaveName.Trim();
            _useEncryption = options.UseEncryption;
            _view = options.View;

            LoadProgress();
            RegisterBuiltInTriggers();
            RegisterDefinitions(options.Definitions);
            InstallProjectExtensions(options.Installers);

            if (options.ValidateOnStart)
            {
                List<string> errors = ValidateDefinitions();
                for (int i = 0; i < errors.Count; i++)
                {
                    Log.Error(errors[i]);
                }
            }

            BindTriggers();
            GuideTargetRegistry.TargetRegistered += OnTargetRegistered;
            GuideTargetRegistry.TargetClicked += OnTargetClicked;
            _isStarted = true;

            if (options.AutoEvaluateOnStart)
            {
                Fire("GuideStart");
                EnqueueRegisteredTargetTriggers();
                EvaluatePendingTriggers();
            }

            Log.Module("Guide", $"Guide started. Definition count: {_definitions.Count}");
        }

        public void StopGuide()
        {
            if (_view != null)
            {
                _view.Hide();
            }

            GuideTargetRegistry.TargetRegistered -= OnTargetRegistered;
            GuideTargetRegistry.TargetClicked -= OnTargetClicked;
            UnbindTriggers();

            _isStarted = false;
            _current = null;
            _view = null;
            _definitions.Clear();
            _groups.Clear();
            _triggerIndex.Clear();
            _triggers.Clear();
            _pendingTriggers.Clear();
        }

        public void RegisterTrigger(string name, IGuideTrigger trigger)
        {
            string key = NormalizeKey(name);
            if (string.IsNullOrEmpty(key) || trigger == null)
            {
                Log.Warning("[Guide] RegisterTrigger ignored: name or trigger is empty.");
                return;
            }

            if (_triggers.TryGetValue(key, out IGuideTrigger oldTrigger) && _isStarted)
            {
                oldTrigger.Unbind(this);
            }

            _triggers[key] = trigger;
            if (_isStarted)
            {
                trigger.Bind(this);
            }
        }

        public void RegisterCondition(string name, IGuideCondition condition)
        {
            string key = NormalizeKey(name);
            if (string.IsNullOrEmpty(key) || condition == null)
            {
                Log.Warning("[Guide] RegisterCondition ignored: name or condition is empty.");
                return;
            }

            _conditions[key] = condition;
        }

        public void RegisterCondition(string name, Func<GuideConditionContext, string, bool> evaluator)
        {
            RegisterCondition(name, new GuideConditionDelegate(evaluator));
        }

        public void RegisterAction(string name, IGuideAction action)
        {
            string key = NormalizeKey(name);
            if (string.IsNullOrEmpty(key) || action == null)
            {
                Log.Warning("[Guide] RegisterAction ignored: name or action is empty.");
                return;
            }

            _actions[key] = action;
        }

        public void RegisterAction(string name, Action<GuideActionContext, string> executor)
        {
            RegisterAction(name, new GuideActionDelegate(executor));
        }

        public void Fire(string triggerKey)
        {
            Fire(triggerKey, null, null, null);
        }

        public void Fire(string triggerKey, object payload)
        {
            Fire(triggerKey, payload, null, null);
        }

        public void Fire(
            string triggerKey,
            object payload,
            object source,
            IDictionary<string, string> parameters = null)
        {
            if (!_isStarted)
            {
                return;
            }

            string key = NormalizeKey(triggerKey);
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            GuideTriggerContext triggerContext = new GuideTriggerContext(key, payload, source, parameters);
            TryCompleteCurrentStepByEvent(triggerContext);

            TrimPendingTriggersIfNeeded();
            _pendingTriggers.Enqueue(triggerContext);
            EvaluatePendingTriggers();
        }

        public void CompleteCurrentStep()
        {
            if (_current == null)
            {
                return;
            }

            CompleteStepInternal(_current.Definition, false, true);
        }

        public void SkipCurrentStep()
        {
            if (_current == null)
            {
                return;
            }

            if (!_current.Definition.canSkip)
            {
                Log.Warning($"[Guide] Current guide step does not allow manual skip. id={_current.Definition.id}");
                return;
            }

            CompleteStepInternal(_current.Definition, true, true);
        }

        public bool IsStepCompleted(int stepId)
        {
            return _completedStepIds.Contains(stepId);
        }

        public bool IsStepSkipped(int stepId)
        {
            return _skippedStepIds.Contains(stepId);
        }

        public bool IsStepFinished(int stepId)
        {
            return _completedStepIds.Contains(stepId) || _skippedStepIds.Contains(stepId);
        }

        public bool IsGuideFinished(string groupId)
        {
            string groupKey = NormalizeKey(groupId);
            if (!_groups.TryGetValue(groupKey, out List<GuideDefinition> definitions))
            {
                return false;
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                if (!IsStepFinished(definitions[i].id))
                {
                    return false;
                }
            }

            return true;
        }

        public void ResetStep(int stepId)
        {
            bool changed = _completedStepIds.Remove(stepId);
            changed |= _skippedStepIds.Remove(stepId);
            if (!changed)
            {
                return;
            }

            SaveProgress();
            BroadcastProgressChanged(stepId);
            EvaluatePendingTriggers();
        }

        public void ResetGroup(string groupId)
        {
            string groupKey = NormalizeKey(groupId);
            if (!_groups.TryGetValue(groupKey, out List<GuideDefinition> definitions))
            {
                return;
            }

            bool changed = false;
            for (int i = 0; i < definitions.Count; i++)
            {
                int stepId = definitions[i].id;
                bool stepChanged = _completedStepIds.Remove(stepId);
                stepChanged |= _skippedStepIds.Remove(stepId);
                if (stepChanged)
                {
                    changed = true;
                    BroadcastProgressChanged(stepId);
                }
            }

            if (changed)
            {
                SaveProgress();
                EvaluatePendingTriggers();
            }
        }

        public void ClearProgress()
        {
            _completedStepIds.Clear();
            _skippedStepIds.Clear();
            SaveProgress();
            EvaluatePendingTriggers();
        }

        public void RefreshViewTarget()
        {
            if (_current != null && _view != null)
            {
                _view.RefreshTarget(BuildViewContext(_current.Definition));
            }
        }

        public List<string> ValidateDefinitions()
        {
            List<string> errors = new List<string>();
            HashSet<int> visitedPrerequisites = new HashSet<int>();

            foreach (GuideDefinition definition in _definitions.Values)
            {
                if (string.IsNullOrEmpty(definition.TriggerKey) &&
                    !HasIncomingLink(definition.id) &&
                    !HasPreviousStepInGroup(definition))
                {
                    errors.Add($"[Guide] Definition {definition.id} has no trigger and cannot be reached by group order or nextId.");
                }

                foreach (int prerequisiteId in definition.EnumeratePrerequisiteIds())
                {
                    if (!_definitions.ContainsKey(prerequisiteId))
                    {
                        errors.Add($"[Guide] Definition {definition.id} references missing prerequisite id {prerequisiteId}.");
                    }
                }

                if (definition.HasExplicitNext && !_definitions.ContainsKey(definition.nextId))
                {
                    errors.Add($"[Guide] Definition {definition.id} references missing next id {definition.nextId}.");
                }

                visitedPrerequisites.Clear();
                if (HasPrerequisiteCycle(definition, visitedPrerequisites))
                {
                    errors.Add($"[Guide] Definition {definition.id} has a prerequisite cycle.");
                }

                ValidateConditions(definition, definition.EnumerateTriggerConditions(), "triggerConditions", errors);
                ValidateConditions(definition, definition.EnumerateStartConditions(), "startConditions", errors);
                ValidateConditions(definition, definition.EnumerateSkipConditions(), "skipConditions", errors);

                bool hasAction = false;
                foreach (GuideConfigExpression action in definition.EnumerateActions())
                {
                    hasAction = true;
                    if (!_actions.ContainsKey(action.Name))
                    {
                        errors.Add($"[Guide] Definition {definition.id} uses unregistered action: {action.Name}");
                    }
                }

                if (!hasAction)
                {
                    errors.Add($"[Guide] Definition {definition.id} has no action.");
                }

                GuideConfigExpression completion = definition.CompletionExpression;
                if (!IsKnownCompletion(completion.Name))
                {
                    errors.Add($"[Guide] Definition {definition.id} uses unknown completion: {completion.Name}");
                }
            }

            return errors;
        }

        private void RegisterDefinitions(IEnumerable<GuideDefinition> definitions)
        {
            if (definitions == null)
            {
                return;
            }

            foreach (GuideDefinition definition in definitions)
            {
                if (definition == null || definition.id <= 0)
                {
                    Log.Warning("[Guide] Ignored invalid guide definition.");
                    continue;
                }

                _definitions[definition.id] = definition;
            }

            RebuildIndices();
        }

        private void InstallProjectExtensions(IList<IGuideInstaller> installers)
        {
            if (installers == null)
            {
                return;
            }

            for (int i = 0; i < installers.Count; i++)
            {
                installers[i]?.Install(this);
            }
        }

        private void RegisterBuiltInConditions()
        {
            RegisterCondition("Always", (context, parameter) => true);
            RegisterCondition("Never", (context, parameter) => false);
            RegisterCondition("StepFinished", (context, parameter) =>
                int.TryParse(parameter, out int stepId) && context.Module.IsStepFinished(stepId));
            RegisterCondition("StepCompleted", (context, parameter) =>
                int.TryParse(parameter, out int stepId) && context.Module.IsStepCompleted(stepId));
            RegisterCondition("StepSkipped", (context, parameter) =>
                int.TryParse(parameter, out int stepId) && context.Module.IsStepSkipped(stepId));
            RegisterCondition("StepNotFinished", (context, parameter) =>
                int.TryParse(parameter, out int stepId) && !context.Module.IsStepFinished(stepId));
            RegisterCondition("GuideFinished", (context, parameter) =>
                !string.IsNullOrWhiteSpace(parameter) && context.Module.IsGuideFinished(parameter));
            RegisterCondition("TargetExists", (context, parameter) =>
                GuideTargetRegistry.Find(string.IsNullOrWhiteSpace(parameter)
                    ? context.Definition.TargetKey
                    : parameter) != null);
            RegisterCondition("TriggerParam", EvaluateTriggerParameterCondition);
        }

        private void RegisterBuiltInActions()
        {
            RegisterAction("Overlay", (context, parameter) => context.Module.ShowView(context.ViewContext));
            RegisterAction("Dialog", (context, parameter) => context.Module.ShowView(context.ViewContext));
            RegisterAction("Highlight", (context, parameter) => context.Module.ShowView(context.ViewContext));
            RegisterAction("ForceClick", (context, parameter) => context.Module.ShowView(context.ViewContext));
            RegisterAction("OverlayCircle", (context, parameter) => context.Module.ShowView(context.ViewContext));
        }

        private void RegisterBuiltInTriggers()
        {
            RegisterTrigger("GuideSignal", new GuideSignalTrigger());
        }

        private void BindTriggers()
        {
            foreach (KeyValuePair<string, IGuideTrigger> pair in _triggers)
            {
                pair.Value.Bind(this);
            }
        }

        private void UnbindTriggers()
        {
            foreach (KeyValuePair<string, IGuideTrigger> pair in _triggers)
            {
                pair.Value.Unbind(this);
            }
        }

        private bool EvaluatePendingTriggers()
        {
            if (!_isStarted || _isEvaluating || IsRunning || _pendingTriggers.Count == 0)
            {
                return false;
            }

            _isEvaluating = true;
            bool started = false;
            int count = _pendingTriggers.Count;

            for (int i = 0; i < count; i++)
            {
                GuideTriggerContext context = _pendingTriggers.Dequeue();
                if (TryStartByTrigger(context))
                {
                    started = true;
                    break;
                }

                if (HasUnfinishedDefinitions(context.Key))
                {
                    TrimPendingTriggersIfNeeded();
                    _pendingTriggers.Enqueue(context);
                }
            }

            _isEvaluating = false;
            return started;
        }

        private bool TryStartByTrigger(GuideTriggerContext triggerContext)
        {
            if (triggerContext == null ||
                string.IsNullOrEmpty(triggerContext.Key) ||
                !_triggerIndex.TryGetValue(triggerContext.Key, out List<GuideDefinition> definitions))
            {
                return false;
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                GuideDefinition definition = definitions[i];
                if (ShouldSkip(definition, triggerContext))
                {
                    SkipStepByConfig(definition);
                    continue;
                }

                if (CanStart(definition, triggerContext))
                {
                    StartStep(definition, triggerContext, true);
                    return true;
                }
            }

            return false;
        }

        private bool CanStart(GuideDefinition definition, GuideTriggerContext triggerContext)
        {
            if (definition == null || IsStepFinished(definition.id))
            {
                return false;
            }

            return AreConditionsSatisfied(definition, definition.EnumerateTriggerConditions(), triggerContext) &&
                   AreConditionsSatisfied(definition, definition.EnumerateStartConditions(), triggerContext);
        }

        private bool ShouldSkip(GuideDefinition definition, GuideTriggerContext triggerContext)
        {
            if (definition == null || IsStepFinished(definition.id))
            {
                return false;
            }

            List<GuideConfigExpression> expressions = GuideConfigExpression.ParseList(definition.skipConditions);
            return expressions.Count > 0 && AreConditionsSatisfied(definition, expressions, triggerContext);
        }

        private bool AreConditionsSatisfied(
            GuideDefinition definition,
            IEnumerable<GuideConfigExpression> expressions,
            GuideTriggerContext triggerContext)
        {
            GuideConditionContext context = new GuideConditionContext(this, definition, triggerContext);
            foreach (GuideConfigExpression expression in expressions)
            {
                if (!_conditions.TryGetValue(expression.Name, out IGuideCondition condition))
                {
                    Log.Warning($"[Guide] Unregistered condition: {expression.Name}, guideId={definition.id}");
                    return false;
                }

                if (!condition.IsSatisfied(context, expression.Parameter))
                {
                    return false;
                }
            }

            return true;
        }

        private void StartStep(
            GuideDefinition definition,
            GuideTriggerContext triggerContext,
            bool broadcastGuideStart)
        {
            _current = new GuideRuntimeStep(definition, triggerContext);
            GuideConfigExpression completion = definition.CompletionExpression;
            if (string.Equals(completion.Name, "Delay", StringComparison.OrdinalIgnoreCase) &&
                TryParseFloat(completion.Parameter, out float delay) &&
                delay > 0f)
            {
                _current.DelayRemaining = delay;
            }

            if (broadcastGuideStart)
            {
                Broadcast(new GuideStartedEvent(definition.GroupKey, definition.id));
            }

            Broadcast(new GuideStepStartedEvent(definition.GroupKey, definition.id, GuideStepType.Custom));
            ExecuteActions(definition, triggerContext);

            if (string.Equals(completion.Name, "Auto", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(completion.Name, "Immediate", StringComparison.OrdinalIgnoreCase))
            {
                CompleteCurrentStep();
            }
        }

        private void ExecuteActions(GuideDefinition definition, GuideTriggerContext triggerContext)
        {
            GuideViewContext viewContext = BuildViewContext(definition);
            GuideActionContext actionContext = new GuideActionContext(this, definition, triggerContext, viewContext);
            bool executed = false;

            foreach (GuideConfigExpression expression in definition.EnumerateActions())
            {
                if (!_actions.TryGetValue(expression.Name, out IGuideAction action))
                {
                    Log.Warning($"[Guide] Unregistered action: {expression.Name}, guideId={definition.id}");
                    continue;
                }

                action.Execute(actionContext, expression.Parameter);
                executed = true;
            }

            if (!executed)
            {
                Log.Warning($"[Guide] No action was executed. guideId={definition.id}");
            }
        }

        private void CompleteStepInternal(GuideDefinition definition, bool skipped, bool advanceNext)
        {
            int stepId = definition.id;
            if (skipped)
            {
                _skippedStepIds.Add(stepId);
                _completedStepIds.Remove(stepId);
            }
            else
            {
                _completedStepIds.Add(stepId);
                _skippedStepIds.Remove(stepId);
            }

            bool wasCurrent = _current != null && _current.Definition.id == stepId;
            GuideTriggerContext triggerContext = _current?.TriggerContext;
            if (wasCurrent)
            {
                _current = null;
                _view?.Hide();
            }

            SaveProgress();
            BroadcastProgressChanged(stepId);
            Broadcast(new GuideStepCompletedEvent(definition.GroupKey, stepId, skipped));

            if (wasCurrent && advanceNext)
            {
                if (TryGetNextStep(definition, triggerContext, out GuideDefinition nextDefinition))
                {
                    StartStep(nextDefinition, triggerContext, false);
                    return;
                }

                Broadcast(new GuideCompletedEvent(definition.GroupKey));
            }

            EvaluatePendingTriggers();
        }

        private void SkipStepByConfig(GuideDefinition definition)
        {
            CompleteStepInternal(definition, true, false);
        }

        private bool TryGetNextStep(
            GuideDefinition current,
            GuideTriggerContext triggerContext,
            out GuideDefinition next)
        {
            next = null;

            if (current.HasExplicitNext)
            {
                if (_definitions.TryGetValue(current.nextId, out GuideDefinition explicitNext))
                {
                    if (ShouldSkip(explicitNext, triggerContext))
                    {
                        SkipStepByConfig(explicitNext);
                        return false;
                    }

                    if (CanStart(explicitNext, triggerContext))
                    {
                        next = explicitNext;
                        return true;
                    }
                }

                return false;
            }

            if (!_groups.TryGetValue(current.GroupKey, out List<GuideDefinition> definitions))
            {
                return false;
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                GuideDefinition candidate = definitions[i];
                if (candidate.id == current.id || candidate.order <= current.order)
                {
                    continue;
                }

                if (ShouldSkip(candidate, triggerContext))
                {
                    SkipStepByConfig(candidate);
                    continue;
                }

                if (CanStart(candidate, triggerContext))
                {
                    next = candidate;
                    return true;
                }
            }

            return false;
        }

        private GuideViewContext BuildViewContext(GuideDefinition definition)
        {
            GuideTarget target = GuideTargetRegistry.Find(definition.TargetKey);
            return new GuideViewContext
            {
                Definition = definition,
                Target = target,
                ResolvedTitle = ResolveText(definition.titleKey, definition.title),
                ResolvedContent = ResolveText(definition.textKey, definition.content),
                CanSkip = definition.canSkip,
                ShowContinueButton = definition.showContinueButton
            };
        }

        private string ResolveText(string key, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                LocalizationModule localization = GameApp.Loc;
                if (localization != null && localization.TryGetString(key, out string value))
                {
                    return value;
                }
            }

            return fallback ?? string.Empty;
        }

        private void ShowView(GuideViewContext context)
        {
            _view?.Show(context);
        }

        private void OnTargetRegistered(string targetKey)
        {
            RefreshViewTarget();
            Fire($"Target:{targetKey}");
            Fire(
                "TargetRegistered",
                null,
                null,
                new Dictionary<string, string> { { "target", targetKey } });
        }

        private void OnTargetClicked(string targetKey)
        {
            if (_current != null)
            {
                GuideConfigExpression completion = _current.Definition.CompletionExpression;
                if (string.Equals(completion.Name, "TargetClick", StringComparison.OrdinalIgnoreCase))
                {
                    string target = string.IsNullOrWhiteSpace(completion.Parameter)
                        ? _current.Definition.TargetKey
                        : completion.Parameter;
                    if (string.Equals(NormalizeKey(target), NormalizeKey(targetKey), StringComparison.OrdinalIgnoreCase))
                    {
                        CompleteCurrentStep();
                    }
                }
            }

            Fire($"TargetClick:{targetKey}");
            Fire(
                "TargetClick",
                null,
                null,
                new Dictionary<string, string> { { "target", targetKey } });
        }

        private bool TryCompleteCurrentStepByEvent(GuideTriggerContext triggerContext)
        {
            if (_current == null || triggerContext == null)
            {
                return false;
            }

            GuideConfigExpression completion = _current.Definition.CompletionExpression;
            if (!string.Equals(completion.Name, "Event", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!IsCompletionEventMatched(completion.Parameter, triggerContext))
            {
                return false;
            }

            CompleteCurrentStep();
            return true;
        }

        private bool IsCompletionEventMatched(string expected, GuideTriggerContext triggerContext)
        {
            if (string.IsNullOrWhiteSpace(expected))
            {
                return true;
            }

            string normalizedExpected = NormalizeKey(expected);
            if (string.Equals(normalizedExpected, triggerContext.Key, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string signalKey = triggerContext.GetValue("key");
            return string.Equals(normalizedExpected, NormalizeKey(signalKey), StringComparison.OrdinalIgnoreCase);
        }

        private void EnqueueRegisteredTargetTriggers()
        {
            List<string> targetKeys = new List<string>();
            GuideTargetRegistry.GetRegisteredKeys(targetKeys);
            for (int i = 0; i < targetKeys.Count; i++)
            {
                string key = targetKeys[i];
                TrimPendingTriggersIfNeeded();
                _pendingTriggers.Enqueue(new GuideTriggerContext($"Target:{key}"));
                TrimPendingTriggersIfNeeded();
                _pendingTriggers.Enqueue(
                    new GuideTriggerContext(
                        "TargetRegistered",
                        null,
                        null,
                        new Dictionary<string, string> { { "target", key } }));
            }
        }

        private bool EvaluateTriggerParameterCondition(GuideConditionContext context, string parameter)
        {
            if (context.Trigger == null || string.IsNullOrWhiteSpace(parameter))
            {
                return false;
            }

            if (!TrySplitComparison(parameter, out string name, out string op, out string expected))
            {
                return context.Trigger.TryGetParameter(parameter, out _) ||
                       context.Trigger.GetValue(parameter) != null;
            }

            string actual = context.Trigger.GetValue(name);
            if (actual == null)
            {
                return false;
            }

            if (double.TryParse(actual, NumberStyles.Float, CultureInfo.InvariantCulture, out double actualNumber) &&
                double.TryParse(expected, NumberStyles.Float, CultureInfo.InvariantCulture, out double expectedNumber))
            {
                switch (op)
                {
                    case ">":
                        return actualNumber > expectedNumber;
                    case ">=":
                        return actualNumber >= expectedNumber;
                    case "<":
                        return actualNumber < expectedNumber;
                    case "<=":
                        return actualNumber <= expectedNumber;
                    case "!=":
                        return Math.Abs(actualNumber - expectedNumber) > double.Epsilon;
                    default:
                        return Math.Abs(actualNumber - expectedNumber) <= double.Epsilon;
                }
            }

            int compare = string.Compare(actual, expected, StringComparison.Ordinal);
            return op == "!=" ? compare != 0 : compare == 0;
        }

        private void ValidateConditions(
            GuideDefinition definition,
            IEnumerable<GuideConfigExpression> expressions,
            string label,
            List<string> errors)
        {
            foreach (GuideConfigExpression expression in expressions)
            {
                if (!_conditions.ContainsKey(expression.Name))
                {
                    errors.Add($"[Guide] Definition {definition.id} {label} uses unregistered condition: {expression.Name}");
                }
            }
        }

        private bool HasUnfinishedDefinitions(string triggerKey)
        {
            if (!_triggerIndex.TryGetValue(triggerKey, out List<GuideDefinition> definitions))
            {
                return false;
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                if (!IsStepFinished(definitions[i].id))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasIncomingLink(int stepId)
        {
            foreach (GuideDefinition definition in _definitions.Values)
            {
                if (definition.nextId == stepId)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasPreviousStepInGroup(GuideDefinition definition)
        {
            if (definition == null ||
                !_groups.TryGetValue(definition.GroupKey, out List<GuideDefinition> definitions))
            {
                return false;
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                GuideDefinition candidate = definitions[i];
                if (candidate.id != definition.id && candidate.order < definition.order)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasPrerequisiteCycle(GuideDefinition definition, HashSet<int> visited)
        {
            if (definition == null || !visited.Add(definition.id))
            {
                return definition != null;
            }

            foreach (int prerequisiteId in definition.EnumeratePrerequisiteIds())
            {
                if (_definitions.TryGetValue(prerequisiteId, out GuideDefinition prerequisite) &&
                    HasPrerequisiteCycle(prerequisite, visited))
                {
                    return true;
                }
            }

            visited.Remove(definition.id);
            return false;
        }

        private void RebuildIndices()
        {
            _groups.Clear();
            _triggerIndex.Clear();

            foreach (GuideDefinition definition in _definitions.Values)
            {
                AddToIndex(_groups, definition.GroupKey, definition);

                string triggerKey = definition.TriggerKey;
                if (!string.IsNullOrEmpty(triggerKey))
                {
                    AddToIndex(_triggerIndex, triggerKey, definition);
                }
            }

            SortIndex(_groups);
            SortIndex(_triggerIndex);
        }

        private void LoadProgress()
        {
            _completedStepIds.Clear();
            _skippedStepIds.Clear();

            if (_saveModule == null)
            {
                _saveData = new GuideSaveData();
                return;
            }

            _saveData = _saveModule.LoadData<GuideSaveData>(_saveName, _useEncryption);
            if (_saveData.completedStepIds != null)
            {
                for (int i = 0; i < _saveData.completedStepIds.Count; i++)
                {
                    _completedStepIds.Add(_saveData.completedStepIds[i]);
                }
            }

            if (_saveData.skippedStepIds != null)
            {
                for (int i = 0; i < _saveData.skippedStepIds.Count; i++)
                {
                    _skippedStepIds.Add(_saveData.skippedStepIds[i]);
                }
            }
        }

        private void SaveProgress()
        {
            if (_saveData == null)
            {
                _saveData = new GuideSaveData();
            }

            _saveData.SetCompleted(_completedStepIds);
            _saveData.SetSkipped(_skippedStepIds);
            _saveData.ClearDirty();

            if (_saveModule != null)
            {
                _saveModule.SaveData(_saveName, _saveData, _useEncryption);
            }
        }

        private bool IsKnownCompletion(string completionName)
        {
            return string.Equals(completionName, "Manual", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(completionName, "TargetClick", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(completionName, "Event", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(completionName, "Delay", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(completionName, "Auto", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(completionName, "Immediate", StringComparison.OrdinalIgnoreCase);
        }

        private void TrimPendingTriggersIfNeeded()
        {
            while (_pendingTriggers.Count >= MaxPendingTriggerCount)
            {
                _pendingTriggers.Dequeue();
            }
        }

        private static bool TrySplitComparison(
            string expression,
            out string name,
            out string op,
            out string expected)
        {
            name = string.Empty;
            op = string.Empty;
            expected = string.Empty;

            if (string.IsNullOrWhiteSpace(expression))
            {
                return false;
            }

            string[] operators = { ">=", "<=", "!=", "==", "=", ">", "<" };
            for (int i = 0; i < operators.Length; i++)
            {
                int index = expression.IndexOf(operators[i], StringComparison.Ordinal);
                if (index <= 0)
                {
                    continue;
                }

                name = expression.Substring(0, index).Trim();
                op = operators[i];
                expected = expression.Substring(index + operators[i].Length).Trim();
                return !string.IsNullOrEmpty(name);
            }

            return false;
        }

        private static bool TryParseFloat(string value, out float result)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }

        private static void AddToIndex(
            Dictionary<string, List<GuideDefinition>> index,
            string key,
            GuideDefinition definition)
        {
            string normalizedKey = NormalizeKey(key);
            if (string.IsNullOrEmpty(normalizedKey))
            {
                return;
            }

            if (!index.TryGetValue(normalizedKey, out List<GuideDefinition> list))
            {
                list = new List<GuideDefinition>();
                index[normalizedKey] = list;
            }

            list.Add(definition);
        }

        private static void SortIndex(Dictionary<string, List<GuideDefinition>> index)
        {
            foreach (List<GuideDefinition> definitions in index.Values)
            {
                definitions.Sort((a, b) =>
                {
                    int orderCompare = a.order.CompareTo(b.order);
                    return orderCompare != 0 ? orderCompare : a.id.CompareTo(b.id);
                });
            }
        }

        private void Broadcast<T>(T eventData) where T : struct
        {
            _eventModule?.Broadcast(eventData);
        }

        private void BroadcastProgressChanged(int stepId)
        {
            Broadcast(new GuideProgressChangedEvent(
                stepId,
                _completedStepIds.Contains(stepId),
                _skippedStepIds.Contains(stepId)));
        }

        private static string NormalizeKey(string key)
        {
            return string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim();
        }

        private sealed class GuideRuntimeStep
        {
            public readonly GuideDefinition Definition;
            public readonly GuideTriggerContext TriggerContext;
            public float DelayRemaining;

            public GuideRuntimeStep(GuideDefinition definition, GuideTriggerContext triggerContext)
            {
                Definition = definition;
                TriggerContext = triggerContext;
            }
        }

        private sealed class GuideSignalTrigger : IGuideTrigger
        {
            private IGuideTriggerSink _sink;

            public void Bind(IGuideTriggerSink sink)
            {
                _sink = sink;
                GameApp.Event?.AddListener<GuideSignalEvent>(OnSignal);
            }

            public void Unbind(IGuideTriggerSink sink)
            {
                GameApp.Event?.RemoveListener<GuideSignalEvent>(OnSignal);
                _sink = null;
            }

            private void OnSignal(GuideSignalEvent eventData)
            {
                if (_sink == null || string.IsNullOrWhiteSpace(eventData.Key))
                {
                    return;
                }

                string key = eventData.Key.Trim();
                _sink.Fire(
                    key,
                    eventData,
                    eventData,
                    new Dictionary<string, string> { { "key", key } });
            }
        }
    }
}
