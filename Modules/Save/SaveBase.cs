using System;

namespace GameFramework.Core
{
    [Serializable]
    public abstract class SaveDataBase
    {
        public int SaveVersion = 1;
        public string SaveModuleName = string.Empty;

        [NonSerialized] public bool IsAutoSaveEnabled = false;
        [NonSerialized] public float AutoSaveInterval = 60f;
        [NonSerialized] internal long AutoSaveTimerId = 0;
        [NonSerialized] protected bool _isDirty = false;

        protected SaveDataBase()
        {
        }

        public void MarkDirty()
        {
            _isDirty = true;
        }

        public virtual void ClearDirty()
        {
            _isDirty = false;
        }

        // 现在这里永远只需要检查自身的标记即可，永远不会臃肿！
        public virtual bool CheckIsDirty()
        {
            return _isDirty;
        }

        /// <summary>
        /// 关键钩子：在对象被新建 或 从 JSON 反序列化之后调用。
        /// 子类需要重写此方法，将 this 绑定给所有的 SaveDataNode 子节点。
        /// </summary>
        public virtual void OnBindContext()
        {
        }

        public virtual void OnBeforeSave()
        {
        }

        public virtual void OnAfterLoad()
        {
        }
    }

    /// <summary>
    /// 存档子节点基类
    /// 继承此类的子结构可以自动将脏标记向上传递给根节点
    /// </summary>
    [Serializable]
    public abstract class SaveDataNode
    {
        // 必须加上 NonSerialized，绝对不能让它被写入 JSON！
        // 否则会导致 JSON 序列化死循环崩溃
        [NonSerialized] private SaveDataBase _rootContext;

        /// <summary>
        /// 绑定根节点上下文
        /// </summary>
        public void BindContext(SaveDataBase rootContext)
        {
            _rootContext = rootContext;
        }

        /// <summary>
        /// 标记数据已变动，直接影响最终的主存档节点
        /// </summary>
        protected void MarkDirty()
        {
            if (_rootContext != null)
            {
                _rootContext.MarkDirty();
            }
            else
            {
                Log.Warning("[Save] SaveDataNode 未绑定根节点，脏标记可能丢失！");
            }
        }
    }

    public interface ISaveDataMigration<T> where T : new()
    {
        int TargetVersion { get; }
        void Migrate(T data, int fromVersion);
    }

    public readonly struct SaveDataLoadedEvent
    {
        public readonly string Slot;
        public readonly string SaveName;
        public readonly string ModuleName;
        public readonly int Version;

        public SaveDataLoadedEvent(string slot, string saveName, string moduleName, int version)
        {
            Slot = slot;
            SaveName = saveName;
            ModuleName = moduleName;
            Version = version;
        }
    }

    public readonly struct SaveDataSavedEvent
    {
        public readonly string Slot;
        public readonly string SaveName;
        public readonly string ModuleName;
        public readonly int Version;

        public SaveDataSavedEvent(string slot, string saveName, string moduleName, int version)
        {
            Slot = slot;
            SaveName = saveName;
            ModuleName = moduleName;
            Version = version;
        }
    }

    public readonly struct SaveDataDirtyEvent
    {
        public readonly string Slot;
        public readonly string SaveName;
        public readonly string ModuleName;

        public SaveDataDirtyEvent(string slot, string saveName, string moduleName)
        {
            Slot = slot;
            SaveName = saveName;
            ModuleName = moduleName;
        }
    }

    public readonly struct SaveDataMigratedEvent
    {
        public readonly string Slot;
        public readonly string SaveName;
        public readonly int FromVersion;
        public readonly int ToVersion;

        public SaveDataMigratedEvent(string slot, string saveName, int fromVersion, int toVersion)
        {
            Slot = slot;
            SaveName = saveName;
            FromVersion = fromVersion;
            ToVersion = toVersion;
        }
    }

    public readonly struct SaveDataRecoveredEvent
    {
        public readonly string Slot;
        public readonly string SaveName;
        public readonly string BackupPath;
        public readonly string CorruptPath;

        public SaveDataRecoveredEvent(string slot, string saveName, string backupPath, string corruptPath)
        {
            Slot = slot;
            SaveName = saveName;
            BackupPath = backupPath;
            CorruptPath = corruptPath;
        }
    }
}
