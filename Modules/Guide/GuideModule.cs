using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameFramework.Core
{
    public sealed class GuideModule : IFrameworkModule
    {
        private const string DefaultSaveName = "Guide";

        private readonly Dictionary<int, GuideDefinition> _definitions =
            new Dictionary<int, GuideDefinition>();
        private readonly Dictionary<string, List<GuideDefinition>> _groups =
            new Dictionary<string, List<GuideDefinition>>();
        private readonly Dictionary<string, List<GuideDefinition>> _triggers =
            new Dictionary<string, List<GuideDefinition>>();
        private readonly HashSet<int> _completedStepIds = new HashSet<int>();
        private readonly HashSet<int> _skippedStepIds = new HashSet<int>();

        private SaveModule _saveModule;
        private EventModule _eventModule;
        private GuideSaveData _saveData;
        private GuideRuntimeStep _current;
        private IGuideView _view;
        private string _saveName = DefaultSaveName;
        private bool _useEncryption = true;

        public int Priority => 51;
        public bool IsRunning => _current != null;
        public GuideDefinition CurrentDefinition => _current?.Definition;

        public void OnInit()
        {
            _saveModule = FrameworkEntry.Instance.GetModule<SaveModule>();
            _eventModule = FrameworkEntry.Instance.GetModule<EventModule>();
            LoadProgress();

            if (_eventModule != null)
            {
                _eventModule.AddListener<GuideSignalEvent>(OnGuideSignal);
            }

            Log.Module("Guide", "GuideModule initialized.");
        }

        public void OnUpdate(float deltaTime, float unscaledDeltaTime)
        {
            if (_current == null || _current.DelayRemaining <= 0f)
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
            if (_eventModule != null)
            {
                _eventModule.RemoveListener<GuideSignalEvent>(OnGuideSignal);
            }

            _definitions.Clear();
            _groups.Clear();
            _triggers.Clear();
            _completedStepIds.Clear();
            _skippedStepIds.Clear();
            _current = null;
            _view = null;
            GuideTargetRegistry.Clear();
        }

        public void SetView(IGuideView view)
        {
            _view = view;
            if (_current != null && _view != null)
            {
                _view.Show(BuildViewContext(_current.Definition));
            }
        }

        public void SetSaveOptions(string saveName, bool useEncryption = true)
        {
            _saveName = string.IsNullOrWhiteSpace(saveName) ? DefaultSaveName : saveName.Trim();
            _useEncryption = useEncryption;
            LoadProgress();
        }

        public void RegisterDefinition(GuideDefinition definition)
        {
            if (definition == null || definition.id <= 0)
            {
                Log.Warning("[Guide] Ignored invalid guide definition.");
                return;
            }

            _definitions[definition.id] = definition;
            RebuildIndices();
        }

        public void RegisterDefinitions(IEnumerable<GuideDefinition> definitions)
        {
            if (definitions == null)
            {
                return;
            }

            foreach (GuideDefinition definition in definitions)
            {
                if (definition == null || definition.id <= 0)
                {
                    continue;
                }

                _definitions[definition.id] = definition;
            }

            RebuildIndices();
        }

        public void RegisterDefinitions<T>(IEnumerable<T> rows, Func<T, GuideDefinition> converter)
        {
            if (rows == null || converter == null)
            {
                return;
            }

            foreach (T row in rows)
            {
                GuideDefinition definition = converter(row);
                if (definition == null || definition.id <= 0)
                {
                    continue;
                }

                _definitions[definition.id] = definition;
            }

            RebuildIndices();
        }

        public bool TryStartByTrigger(string trigger)
        {
            string triggerKey = NormalizeKey(trigger);
            if (string.IsNullOrEmpty(triggerKey) || IsRunning)
            {
                return false;
            }

            if (!_triggers.TryGetValue(triggerKey, out List<GuideDefinition> definitions))
            {
                return false;
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                GuideDefinition definition = definitions[i];
                if (CanStart(definition))
                {
                    StartStep(definition, true);
                    return true;
                }
            }

            return false;
        }

        public bool TryStartGuide(int stepId, bool ignoreProgress = false)
        {
            if (IsRunning)
            {
                return false;
            }

            if (!_definitions.TryGetValue(stepId, out GuideDefinition definition))
            {
                Log.Warning($"[Guide] Guide step not registered: {stepId}");
                return false;
            }

            if (!ignoreProgress && !CanStart(definition))
            {
                return false;
            }

            StartStep(definition, true);
            return true;
        }

        public void NotifyEvent(string eventKey)
        {
            string key = NormalizeKey(eventKey);
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            if (_current != null && _current.Definition.ShouldCompleteByEvent(key))
            {
                CompleteCurrentStep();
                return;
            }

            TryStartByTrigger(key);
        }

        public void NotifyUIOpened(string uiKey)
        {
            NotifyEvent($"UI:{uiKey}");
        }

        public void NotifyTargetClicked(string targetKey)
        {
            if (_current == null)
            {
                return;
            }

            if (_current.Definition.ShouldCompleteByTargetClick(NormalizeKey(targetKey)))
            {
                CompleteCurrentStep();
            }
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
                Log.Warning($"[Guide] Step cannot be skipped: {_current.Definition.id}");
                return;
            }

            CompleteStepInternal(_current.Definition, true, true);
        }

        public bool MarkStepCompleted(int stepId)
        {
            if (!_definitions.TryGetValue(stepId, out GuideDefinition definition))
            {
                return false;
            }

            CompleteStepInternal(definition, false, false);
            return true;
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

        public bool IsGuideCompleted(string groupId)
        {
            string groupKey = NormalizeKey(groupId);
            if (!_groups.TryGetValue(groupKey, out List<GuideDefinition> definitions))
            {
                return false;
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                if (!_completedStepIds.Contains(definitions[i].id))
                {
                    return false;
                }
            }

            return true;
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

            if (changed)
            {
                SaveProgress();
                BroadcastProgressChanged(stepId);
            }
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
                changed |= _completedStepIds.Remove(stepId);
                changed |= _skippedStepIds.Remove(stepId);
                BroadcastProgressChanged(stepId);
            }

            if (changed)
            {
                SaveProgress();
            }
        }

        public void ClearProgress()
        {
            _completedStepIds.Clear();
            _skippedStepIds.Clear();
            SaveProgress();
        }

        public void RefreshViewTarget()
        {
            if (_current != null && _view != null)
            {
                _view.RefreshTarget(BuildViewContext(_current.Definition));
            }
        }

        private void StartStep(GuideDefinition definition, bool broadcastGuideStart)
        {
            _current = new GuideRuntimeStep(definition);

            if (definition.type == GuideStepType.Delay && definition.autoCompleteDelay <= 0f)
            {
                _current.DelayRemaining = 0.01f;
            }
            else if (definition.autoCompleteDelay > 0f)
            {
                _current.DelayRemaining = definition.autoCompleteDelay;
            }

            if (broadcastGuideStart)
            {
                Broadcast(new GuideStartedEvent(definition.GroupKey, definition.id));
            }

            Broadcast(new GuideStepStartedEvent(definition.GroupKey, definition.id, definition.type));

            if (_view != null)
            {
                _view.Show(BuildViewContext(definition));
            }

            if (definition.autoCompleteOnShow && _current.DelayRemaining <= 0f)
            {
                CompleteCurrentStep();
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
            if (wasCurrent)
            {
                _current = null;
                _view?.Hide();
            }

            SaveProgress();
            BroadcastProgressChanged(stepId);
            Broadcast(new GuideStepCompletedEvent(definition.GroupKey, stepId, skipped));

            if (!wasCurrent || !advanceNext)
            {
                return;
            }

            if (TryGetNextStep(definition, out GuideDefinition nextDefinition))
            {
                StartStep(nextDefinition, false);
                return;
            }

            Broadcast(new GuideCompletedEvent(definition.GroupKey));
        }

        private bool TryGetNextStep(GuideDefinition current, out GuideDefinition next)
        {
            next = null;

            if (current.HasExplicitNext)
            {
                if (_definitions.TryGetValue(current.nextId, out GuideDefinition explicitNext) &&
                    CanStart(explicitNext))
                {
                    next = explicitNext;
                    return true;
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

                if (CanStart(candidate))
                {
                    next = candidate;
                    return true;
                }
            }

            return false;
        }

        private bool CanStart(GuideDefinition definition)
        {
            if (definition == null || IsStepFinished(definition.id))
            {
                return false;
            }

            foreach (int prerequisiteId in definition.EnumeratePrerequisiteIds())
            {
                if (!IsStepFinished(prerequisiteId))
                {
                    return false;
                }
            }

            return true;
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
                ShowContinueButton = ShouldShowContinueButton(definition)
            };
        }

        private bool ShouldShowContinueButton(GuideDefinition definition)
        {
            if (!definition.showContinueButton)
            {
                return false;
            }

            return definition.type == GuideStepType.Dialog ||
                   definition.type == GuideStepType.Highlight ||
                   definition.type == GuideStepType.Custom;
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

        private void RebuildIndices()
        {
            _groups.Clear();
            _triggers.Clear();

            foreach (GuideDefinition definition in _definitions.Values)
            {
                AddToIndex(_groups, definition.GroupKey, definition);

                string triggerKey = definition.TriggerKey;
                if (!string.IsNullOrEmpty(triggerKey))
                {
                    AddToIndex(_triggers, triggerKey, definition);
                }
            }

            SortIndex(_groups);
            SortIndex(_triggers);
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

        private void OnGuideSignal(GuideSignalEvent eventData)
        {
            NotifyEvent(eventData.Key);
        }

        private static string NormalizeKey(string key)
        {
            return string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim();
        }

        private sealed class GuideRuntimeStep
        {
            public readonly GuideDefinition Definition;
            public float DelayRemaining;

            public GuideRuntimeStep(GuideDefinition definition)
            {
                Definition = definition;
            }
        }
    }
}
