namespace GameFramework.Core
{
    public struct RedPointSnapshot
    {
        public string Path;
        public int Count;
        public int SelfCount;
        public int ChildCount;
        public bool IsActive;

        public RedPointSnapshot(string path, int count, int selfCount, int childCount)
        {
            Path = path;
            Count = count;
            SelfCount = selfCount;
            ChildCount = childCount;
            IsActive = count > 0;
        }
    }

    public struct RedPointChangedEvent
    {
        public string Path;
        public int Count;
        public int SelfCount;
        public int ChildCount;
        public bool IsActive;

        public RedPointChangedEvent(RedPointSnapshot snapshot)
        {
            Path = snapshot.Path;
            Count = snapshot.Count;
            SelfCount = snapshot.SelfCount;
            ChildCount = snapshot.ChildCount;
            IsActive = snapshot.IsActive;
        }
    }
}
