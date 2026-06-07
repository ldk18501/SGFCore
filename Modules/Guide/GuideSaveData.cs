using System;
using System.Collections.Generic;

namespace GameFramework.Core
{
    [Serializable]
    public sealed class GuideSaveData : SaveDataBase
    {
        public List<int> completedStepIds = new List<int>();
        public List<int> skippedStepIds = new List<int>();

        public void SetCompleted(IEnumerable<int> stepIds)
        {
            completedStepIds.Clear();
            if (stepIds != null)
            {
                completedStepIds.AddRange(stepIds);
                completedStepIds.Sort();
            }

            MarkDirty();
        }

        public void SetSkipped(IEnumerable<int> stepIds)
        {
            skippedStepIds.Clear();
            if (stepIds != null)
            {
                skippedStepIds.AddRange(stepIds);
                skippedStepIds.Sort();
            }

            MarkDirty();
        }
    }
}
