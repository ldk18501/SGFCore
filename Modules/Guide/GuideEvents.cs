namespace GameFramework.Core
{
    public readonly struct GuideSignalEvent
    {
        public readonly string Key;

        public GuideSignalEvent(string key)
        {
            Key = key;
        }
    }

    public readonly struct GuideStartedEvent
    {
        public readonly string GroupId;
        public readonly int StepId;

        public GuideStartedEvent(string groupId, int stepId)
        {
            GroupId = groupId;
            StepId = stepId;
        }
    }

    public readonly struct GuideStepStartedEvent
    {
        public readonly string GroupId;
        public readonly int StepId;
        public readonly GuideStepType StepType;

        public GuideStepStartedEvent(string groupId, int stepId, GuideStepType stepType)
        {
            GroupId = groupId;
            StepId = stepId;
            StepType = stepType;
        }
    }

    public readonly struct GuideStepCompletedEvent
    {
        public readonly string GroupId;
        public readonly int StepId;
        public readonly bool Skipped;

        public GuideStepCompletedEvent(string groupId, int stepId, bool skipped)
        {
            GroupId = groupId;
            StepId = stepId;
            Skipped = skipped;
        }
    }

    public readonly struct GuideCompletedEvent
    {
        public readonly string GroupId;

        public GuideCompletedEvent(string groupId)
        {
            GroupId = groupId;
        }
    }

    public readonly struct GuideProgressChangedEvent
    {
        public readonly int StepId;
        public readonly bool Completed;
        public readonly bool Skipped;

        public GuideProgressChangedEvent(int stepId, bool completed, bool skipped)
        {
            StepId = stepId;
            Completed = completed;
            Skipped = skipped;
        }
    }
}
