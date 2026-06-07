using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GameFramework.Core
{
    /// <summary>
    /// 统一存档模块：支持版本、迁移、多槽位、模块化存档和脏标记事件。
    /// </summary>
    public class SaveModule : IFrameworkModule
    {
        private const string DefaultSlot = "Default";

        private readonly Dictionary<Type, object> _migrations = new Dictionary<Type, object>();
        private readonly Dictionary<SaveDataBase, TrackedSave> _trackedSaves =
            new Dictionary<SaveDataBase, TrackedSave>();
        private readonly HashSet<SaveDataBase> _dirtyBroadcasted = new HashSet<SaveDataBase>();

        private FileSystemModule _fileSystem;
        private CryptoModule _crypto;
        private string _saveDirectory;

        public int Priority => 30;
        public string CurrentSlot { get; private set; } = DefaultSlot;
        public bool EnableBackupOnSave { get; set; } = true;
        public bool EnableCorruptFileQuarantine { get; set; } = true;

        public void OnInit()
        {
            _fileSystem = FrameworkEntry.Instance.GetModule<FileSystemModule>();
            _crypto = FrameworkEntry.Instance.GetModule<CryptoModule>();

            if (_fileSystem == null)
            {
                Log.Fatal("[Save] 存档模块初始化失败：找不到 FileSystemModule。");
                return;
            }

            _saveDirectory = Path.Combine(_fileSystem.GetPersistentDataPath(), "Saves");
            Log.Module("Save", $"存档模块初始化完成，存档目录: {_saveDirectory}");
        }

        public void OnUpdate(float deltaTime, float unscaledDeltaTime)
        {
            foreach (KeyValuePair<SaveDataBase, TrackedSave> pair in _trackedSaves)
            {
                SaveDataBase data = pair.Key;
                TrackedSave tracked = pair.Value;
                if (data != null && data.CheckIsDirty())
                {
                    if (_dirtyBroadcasted.Add(data))
                    {
                        Broadcast(new SaveDataDirtyEvent(tracked.Slot, tracked.SaveName, data.SaveModuleName));
                    }
                }
                else if (data != null)
                {
                    _dirtyBroadcasted.Remove(data);
                }
            }
        }

        public void OnDestroy()
        {
            SaveDataBase[] trackedData = new SaveDataBase[_trackedSaves.Count];
            _trackedSaves.Keys.CopyTo(trackedData, 0);
            for (int i = 0; i < trackedData.Length; i++)
            {
                StopAutoSave(string.Empty, trackedData[i]);
            }

            _trackedSaves.Clear();
            _dirtyBroadcasted.Clear();
            _migrations.Clear();
        }

        public void SetCurrentSlot(string slot)
        {
            CurrentSlot = NormalizeSlot(slot);
        }

        public string[] GetSlots()
        {
            if (string.IsNullOrEmpty(_saveDirectory) || !Directory.Exists(_saveDirectory))
            {
                return new string[0];
            }

            string[] directories = Directory.GetDirectories(_saveDirectory);
            string[] slots = new string[directories.Length];
            for (int i = 0; i < directories.Length; i++)
            {
                slots[i] = Path.GetFileName(directories[i]);
            }

            return slots;
        }

        public void RegisterMigration<T>(ISaveDataMigration<T> migration) where T : class, new()
        {
            if (migration == null)
            {
                return;
            }

            _migrations[typeof(T)] = migration;
        }

        public void TrackAutoSave(string saveName, SaveDataBase saveData, Action saveAction)
        {
            TrackAutoSave(CurrentSlot, saveName, saveData, saveAction);
        }

        public void TrackAutoSave(string slot, string saveName, SaveDataBase saveData, Action saveAction)
        {
            if (saveData == null || !saveData.IsAutoSaveEnabled)
            {
                return;
            }

            StopAutoSave(saveName, saveData);
            TimerModule timerModule = FrameworkEntry.Instance.GetModule<TimerModule>();
            if (timerModule == null)
            {
                return;
            }

            string normalizedSlot = NormalizeSlot(slot);
            saveData.AutoSaveTimerId = timerModule.AddTimer(saveData.AutoSaveInterval, () =>
            {
                if (!saveData.CheckIsDirty())
                {
                    return;
                }

                saveAction?.Invoke();
                saveData.ClearDirty();
                Log.Info($"[Save] 自动存档完成: slot={normalizedSlot}, save={saveName}");
            }, isUnscaled: true, loopCount: -1);

            _trackedSaves[saveData] = new TrackedSave(normalizedSlot, saveName);
        }

        public void StopAutoSave(string saveName, SaveDataBase saveData)
        {
            if (saveData == null)
            {
                return;
            }

            if (saveData.AutoSaveTimerId != 0)
            {
                FrameworkEntry.Instance.GetModule<TimerModule>()?.CancelTimer(saveData.AutoSaveTimerId);
                saveData.AutoSaveTimerId = 0;
            }

            _trackedSaves.Remove(saveData);
            _dirtyBroadcasted.Remove(saveData);
        }

        public void SaveData<T>(string saveName, T saveData, bool useEncryption = true)
        {
            SaveData(CurrentSlot, saveName, saveData, useEncryption);
        }

        public void SaveData<T>(string slot, string saveName, T saveData, bool useEncryption = true)
        {
            if (string.IsNullOrWhiteSpace(saveName) || saveData == null)
            {
                return;
            }

            try
            {
                string normalizedSlot = NormalizeSlot(slot);
                string moduleName = string.Empty;
                int version = 0;
                if (saveData is SaveDataBase saveBase)
                {
                    saveBase.OnBeforeSave();
                    moduleName = saveBase.SaveModuleName;
                    version = saveBase.SaveVersion;
                    _dirtyBroadcasted.Remove(saveBase);
                }

                string jsonContent = JsonUtility.ToJson(saveData);
                if (useEncryption && _crypto != null)
                {
                    jsonContent = _crypto.EncryptString(jsonContent);
                }

                string filePath = GetSaveFilePath(normalizedSlot, saveName);
                BackupExistingSave(filePath);
                _fileSystem.WriteText(filePath, jsonContent);
                Broadcast(new SaveDataSavedEvent(normalizedSlot, saveName, moduleName, version));
                Log.Info($"[Save] 存档成功: slot={normalizedSlot}, save={saveName}");
            }
            catch (Exception e)
            {
                Log.Error($"[Save] 存档失败 ({saveName}): {e.Message}");
            }
        }

        public T LoadData<T>(string saveName, bool useEncryption = true) where T : new()
        {
            return LoadData<T>(CurrentSlot, saveName, useEncryption);
        }

        public T LoadData<T>(string slot, string saveName, bool useEncryption = true) where T : new()
        {
            string normalizedSlot = NormalizeSlot(slot);
            string filePath = GetSaveFilePath(normalizedSlot, saveName);
            if (!_fileSystem.Exists(filePath))
            {
                T newData = CreateNewData<T>();
                BindSaveContext(saveName, newData);
                return newData;
            }

            if (TryLoadFromFile(filePath, useEncryption, out T loadedData, out string error))
            {
                return PrepareLoadedData(normalizedSlot, saveName, loadedData, useEncryption);
            }

            Log.Error($"[Save] 读档异常: slot={normalizedSlot}, save={saveName}, {error}");
            if (TryRecoverFromBackup(normalizedSlot, saveName, filePath, useEncryption, out T recoveredData))
            {
                return PrepareLoadedData(normalizedSlot, saveName, recoveredData, useEncryption);
            }

            T data = CreateNewData<T>();
            BindSaveContext(saveName, data);
            return data;
        }

        public void SaveModuleData<T>(string moduleName, T saveData, bool useEncryption = true)
        {
            SaveModuleData(CurrentSlot, moduleName, saveData, useEncryption);
        }

        public void SaveModuleData<T>(string slot, string moduleName, T saveData, bool useEncryption = true)
        {
            if (saveData is SaveDataBase saveBase)
            {
                saveBase.SaveModuleName = moduleName;
            }

            SaveData(slot, BuildModuleSaveName(moduleName), saveData, useEncryption);
        }

        public T LoadModuleData<T>(string moduleName, bool useEncryption = true) where T : new()
        {
            return LoadModuleData<T>(CurrentSlot, moduleName, useEncryption);
        }

        public T LoadModuleData<T>(string slot, string moduleName, bool useEncryption = true) where T : new()
        {
            T data = LoadData<T>(slot, BuildModuleSaveName(moduleName), useEncryption);
            if (data is SaveDataBase saveBase)
            {
                saveBase.SaveModuleName = moduleName;
            }

            return data;
        }

        public bool HasSave(string saveName)
        {
            return HasSave(CurrentSlot, saveName);
        }

        public bool HasSave(string slot, string saveName)
        {
            return _fileSystem.Exists(GetSaveFilePath(slot, saveName));
        }

        public void DeleteSave(string saveName)
        {
            DeleteSave(CurrentSlot, saveName);
        }

        public void DeleteSave(string slot, string saveName)
        {
            string filePath = GetSaveFilePath(slot, saveName);
            if (_fileSystem.Exists(filePath))
            {
                _fileSystem.DeleteFile(filePath);
                Log.Info($"[Save] 存档已删除: slot={NormalizeSlot(slot)}, save={saveName}");
            }
        }

        public void DeleteSlot(string slot)
        {
            string slotDirectory = GetSlotDirectory(slot);
            if (Directory.Exists(slotDirectory))
            {
                Directory.Delete(slotDirectory, true);
                Log.Info($"[Save] 存档槽已删除: {NormalizeSlot(slot)}");
            }
        }

        public string GetSaveFilePath(string saveName)
        {
            return GetSaveFilePath(CurrentSlot, saveName);
        }

        public string GetSaveFilePath(string slot, string saveName)
        {
            return Path.Combine(GetSlotDirectory(slot), $"{SanitizeName(saveName)}.sav");
        }

        public string GetBackupFilePath(string slot, string saveName)
        {
            return GetSaveFilePath(slot, saveName) + ".bak";
        }

        public string GetSlotDirectory(string slot)
        {
            return Path.Combine(_saveDirectory, SanitizeName(NormalizeSlot(slot)));
        }

        private T CreateNewData<T>() where T : new()
        {
            return new T();
        }

        private T PrepareLoadedData<T>(string normalizedSlot, string saveName, T data, bool useEncryption) where T : new()
        {
            if (data == null)
            {
                data = CreateNewData<T>();
            }

            int fromVersion = GetVersion(data);
            ApplyMigration(data, fromVersion, normalizedSlot, saveName);
            BindSaveContext(saveName, data);
            Broadcast(new SaveDataLoadedEvent(normalizedSlot, saveName, GetModuleName(data), GetVersion(data)));

            if (data is SaveDataBase saveBase && saveBase.IsAutoSaveEnabled)
            {
                TrackAutoSave(normalizedSlot, saveName, saveBase, () => SaveData(normalizedSlot, saveName, data, useEncryption));
            }

            Log.Info($"[Save] 读档成功: slot={normalizedSlot}, save={saveName}");
            return data;
        }

        private bool TryLoadFromFile<T>(string filePath, bool useEncryption, out T data, out string error) where T : new()
        {
            data = default;
            error = null;

            try
            {
                string fileContent = _fileSystem.ReadText(filePath);
                if (useEncryption && _crypto != null)
                {
                    fileContent = _crypto.DecryptString(fileContent);
                    if (string.IsNullOrEmpty(fileContent))
                    {
                        error = "解密结果为空。";
                        return false;
                    }
                }

                data = JsonUtility.FromJson<T>(fileContent);
                if (data == null)
                {
                    error = "JSON 反序列化结果为空。";
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        private bool TryRecoverFromBackup<T>(
            string normalizedSlot,
            string saveName,
            string filePath,
            bool useEncryption,
            out T data) where T : new()
        {
            data = default;
            string backupPath = GetBackupFilePath(normalizedSlot, saveName);
            if (!_fileSystem.Exists(backupPath))
            {
                QuarantineCorruptFile(filePath, saveName, out _);
                return false;
            }

            if (!TryLoadFromFile(backupPath, useEncryption, out data, out string backupError))
            {
                Log.Error($"[Save] 备份恢复失败: slot={normalizedSlot}, save={saveName}, {backupError}");
                QuarantineCorruptFile(filePath, saveName, out _);
                return false;
            }

            string corruptPath = QuarantineCorruptFile(filePath, saveName, out bool quarantined);
            File.Copy(backupPath, filePath, true);
            Broadcast(new SaveDataRecoveredEvent(
                normalizedSlot,
                saveName,
                backupPath,
                quarantined ? corruptPath : string.Empty));
            Log.Warning($"[Save] 已从备份恢复存档: slot={normalizedSlot}, save={saveName}");
            return true;
        }

        private void BackupExistingSave(string filePath)
        {
            if (!EnableBackupOnSave || string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return;
            }

            try
            {
                File.Copy(filePath, filePath + ".bak", true);
            }
            catch (Exception e)
            {
                Log.Warning($"[Save] 创建备份失败: {filePath}, {e.Message}");
            }
        }

        private string QuarantineCorruptFile(string filePath, string saveName, out bool quarantined)
        {
            quarantined = false;
            if (!EnableCorruptFileQuarantine || string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return string.Empty;
            }

            string directory = Path.GetDirectoryName(filePath);
            string corruptPath = Path.Combine(
                directory,
                $"{SanitizeName(saveName)}.corrupt_{DateTime.Now:yyyyMMddHHmmss}.sav");

            try
            {
                File.Move(filePath, corruptPath);
                quarantined = true;
                return corruptPath;
            }
            catch (Exception e)
            {
                Log.Warning($"[Save] 隔离损坏存档失败: {filePath}, {e.Message}");
                return string.Empty;
            }
        }

        private void BindSaveContext<T>(string saveName, T data)
        {
            if (data is SaveDataBase saveBase)
            {
                if (string.IsNullOrEmpty(saveBase.SaveModuleName))
                {
                    saveBase.SaveModuleName = saveName;
                }

                saveBase.OnBindContext();
                saveBase.OnAfterLoad();
            }
        }

        private void ApplyMigration<T>(T data, int fromVersion, string slot, string saveName) where T : new()
        {
            if (data == null || !_migrations.TryGetValue(typeof(T), out object migrationObject))
            {
                return;
            }

            if (migrationObject is ISaveDataMigration<T> migration && migration.TargetVersion > fromVersion)
            {
                migration.Migrate(data, fromVersion);
                if (data is SaveDataBase saveBase)
                {
                    saveBase.SaveVersion = migration.TargetVersion;
                }

                Broadcast(new SaveDataMigratedEvent(slot, saveName, fromVersion, migration.TargetVersion));
            }
        }

        private int GetVersion<T>(T data)
        {
            return data is SaveDataBase saveBase ? saveBase.SaveVersion : 0;
        }

        private string GetModuleName<T>(T data)
        {
            return data is SaveDataBase saveBase ? saveBase.SaveModuleName : string.Empty;
        }

        private static string BuildModuleSaveName(string moduleName)
        {
            return $"Module_{SanitizeName(moduleName)}";
        }

        private static string NormalizeSlot(string slot)
        {
            return string.IsNullOrWhiteSpace(slot) ? DefaultSlot : slot.Trim();
        }

        private static string SanitizeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Empty";
            }

            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalidChar, '_');
            }

            return value.Trim();
        }

        private void Broadcast<T>(T eventData) where T : struct
        {
            GameApp.Event?.Broadcast(eventData);
        }

        private readonly struct TrackedSave
        {
            public readonly string Slot;
            public readonly string SaveName;

            public TrackedSave(string slot, string saveName)
            {
                Slot = slot;
                SaveName = saveName;
            }
        }
    }
}
