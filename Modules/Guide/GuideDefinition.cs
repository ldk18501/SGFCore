using System;
using System.Collections.Generic;

namespace GameFramework.Core
{
    [Serializable]
    public sealed class GuideDefinition
    {
        public int id;
        public string groupId;
        public int order;
        public int nextId;

        public string prerequisiteIds;
        public string trigger;
        public string triggerConditions;
        public string startConditions;
        public string skipConditions;

        public string action;
        public string completion;

        public string targetKey;
        public string titleKey;
        public string textKey;
        public string title;
        public string content;

        public bool canSkip = true;
        public bool blockInput = true;
        public bool showContinueButton = true;

        public string GroupKey => string.IsNullOrWhiteSpace(groupId) ? id.ToString() : groupId.Trim();
        public string TriggerKey => string.IsNullOrWhiteSpace(trigger) ? string.Empty : trigger.Trim();
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

        public IEnumerable<GuideConfigExpression> EnumerateTriggerConditions()
        {
            return GuideConfigExpression.ParseList(triggerConditions);
        }

        public IEnumerable<GuideConfigExpression> EnumerateStartConditions()
        {
            if (!string.IsNullOrWhiteSpace(startConditions))
            {
                List<GuideConfigExpression> expressions = GuideConfigExpression.ParseList(startConditions);
                for (int i = 0; i < expressions.Count; i++)
                {
                    yield return expressions[i];
                }
            }

            foreach (int prerequisiteId in EnumeratePrerequisiteIds())
            {
                yield return new GuideConfigExpression("StepFinished", prerequisiteId.ToString());
            }
        }

        public IEnumerable<GuideConfigExpression> EnumerateSkipConditions()
        {
            return GuideConfigExpression.ParseList(skipConditions);
        }

        public IEnumerable<GuideConfigExpression> EnumerateActions()
        {
            List<GuideConfigExpression> expressions = GuideConfigExpression.ParseList(action);
            for (int i = 0; i < expressions.Count; i++)
            {
                yield return expressions[i];
            }
        }

        public GuideConfigExpression CompletionExpression
        {
            get
            {
                return GuideConfigExpression.TryParse(completion, out GuideConfigExpression expression)
                    ? expression
                    : new GuideConfigExpression("Manual", string.Empty);
            }
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
