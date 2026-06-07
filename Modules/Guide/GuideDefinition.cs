using System;
using System.Collections.Generic;

namespace GameFramework.Core
{
    [Serializable]
    public class GuideDefinition
    {
        public int id;
        public string groupId;
        public int order;
        public string trigger;
        public GuideStepType type = GuideStepType.Dialog;
        public string prerequisiteIds;
        public int nextId;

        public string targetKey;
        public string titleKey;
        public string textKey;
        public string title;
        public string content;
        public string completeEvent;
        public string customKey;
        public string param;

        public bool canSkip = true;
        public bool blockInput = true;
        public bool showContinueButton = true;
        public bool autoCompleteOnShow;
        public bool completeOnTargetClick;
        public float autoCompleteDelay;

        public string GroupKey => string.IsNullOrWhiteSpace(groupId) ? id.ToString() : groupId.Trim();
        public string TriggerKey => string.IsNullOrWhiteSpace(trigger) ? string.Empty : trigger.Trim();
        public string CompleteEventKey => string.IsNullOrWhiteSpace(completeEvent) ? string.Empty : completeEvent.Trim();
        public string TargetKey => string.IsNullOrWhiteSpace(targetKey) ? string.Empty : targetKey.Trim();

        public bool HasExplicitNext => nextId > 0;

        public IEnumerable<int> EnumeratePrerequisiteIds()
        {
            if (string.IsNullOrWhiteSpace(prerequisiteIds))
            {
                yield break;
            }

            string[] parts = prerequisiteIds.Split(',', ';', '|');
            for (int i = 0; i < parts.Length; i++)
            {
                if (int.TryParse(parts[i].Trim(), out int prerequisiteId) && prerequisiteId > 0)
                {
                    yield return prerequisiteId;
                }
            }
        }

        public bool ShouldCompleteByEvent(string eventKey)
        {
            return !string.IsNullOrEmpty(CompleteEventKey) &&
                   string.Equals(CompleteEventKey, eventKey, StringComparison.Ordinal);
        }

        public bool ShouldCompleteByTargetClick(string clickedTargetKey)
        {
            if (!completeOnTargetClick)
            {
                return false;
            }

            return !string.IsNullOrEmpty(TargetKey) &&
                   string.Equals(TargetKey, clickedTargetKey, StringComparison.Ordinal);
        }
    }

    public sealed class GuideViewContext
    {
        public GuideDefinition Definition { get; internal set; }
        public GuideTarget Target { get; internal set; }
        public string ResolvedTitle { get; internal set; }
        public string ResolvedContent { get; internal set; }
        public bool CanSkip { get; internal set; }
        public bool ShowContinueButton { get; internal set; }
    }
}
