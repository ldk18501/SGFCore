using System;
using System.IO;
using UnityEngine;

namespace GameFramework.Core
{
    /// <summary>
    /// 全局存档管理模块
    /// 结合了 FileSystemModule 和 CryptoModule 实现安全的跨平台存档
    /// </summary>
    public class SaveModule : IFrameworkModule
    {
        // 优先级设为 30，确保在 FileSystem (5) 和 Crypto (20) 之后初始化
        public int Priority => 30;

        private FileSystemModule _fileSystem;
        private CryptoModule _crypto;
        private string _saveDirectory;

        public void OnInit()
        {
            // 获取依赖的底层模块
            _fileSystem = FrameworkEntry.Instance.GetModule<FileSystemModule>();
            _crypto = FrameworkEntry.Instance.GetModule<CryptoModule>();

            if (_fileSystem == null)
            {
                Log.Fatal("[Save] 存档模块初始化失败：找不到 FileSystemModule！");
                return;
            }

            // 统一管理存档目录（如：持久化目录下的 Saves 文件夹）
            _saveDirectory = Path.Combine(_fileSystem.GetPersistentDataPath(), "Saves");

            Log.Module("Save", $"存档模块初始化完成，存档目录: {_saveDirectory}");
        }

        public void OnUpdate(float deltaTime, float unscaledDeltaTime)
        {
        }

        public void OnDestroy()
        {
        }

        public void TrackAutoSave(string saveName, SaveDataBase saveData, Action saveAction)
        {
            if (!saveData.IsAutoSaveEnabled) return;

            // 如果之前已经追踪过，先停止
            StopAutoSave(saveName, saveData);

            var timerModule = FrameworkEntry.Instance.GetModule<TimerModule>();

            // 使用 TimerModule 开启无限循环的自动存档检查，且不受 timeScale 影响（即使游戏暂停也会按真实时间存档）
            saveData.AutoSaveTimerId = timerModule.AddTimer(saveData.AutoSaveInterval, () =>
            {
                // 核心：只在有脏标记时才执行序列化和 IO 写入！
                if (saveData.CheckIsDirty())
                {
                    saveAction?.Invoke();
                    saveData.ClearDirty(); // 存完后清理标记
                    Log.Info($"[Save] 检测到数据变更，已触发自动存档: {saveName}");
                }
            }, isUnscaled: true, loopCount: -1);

            Log.Module("Save", $"开启自动存档追踪: {saveName}, 间隔: {saveData.AutoSaveInterval}秒");
        }

        public void StopAutoSave(string saveName, SaveDataBase saveData)
        {
            if (saveData.AutoSaveTimerId != 0)
            {
                FrameworkEntry.Instance.GetModule<TimerModule>().CancelTimer(saveData.AutoSaveTimerId);
                saveData.AutoSaveTimerId = 0;
            }
        }

        // ==========================================
        // API: 核心存取档功能
        // ==========================================

        /// <summary>
        /// 保存数据到本地（默认开启加密）
        /// </summary>
        /// <typeparam name="T">存档数据类的类型</typeparam>
        /// <param name="saveName">存档槽位名称（如 "Slot_1", "GlobalSetting"）</param>
        /// <param name="saveData">存档数据对象</param>
        /// <param name="useEncryption">是否加密</param>
        public void SaveData<T>(string saveName, T saveData, bool useEncryption = true)
        {
            try
            {
                // 1. 将对象序列化为 JSON 字符串
                string jsonContent = JsonUtility.ToJson(saveData);

                // 2. 如果需要加密，调用加密模块
                if (useEncryption && _crypto != null)
                {
                    jsonContent = _crypto.EncryptString(jsonContent);
                }

                // 3. 写入文件系统
                string filePath = GetSaveFilePath(saveName);
                _fileSystem.WriteText(filePath, jsonContent);

                Log.Info($"[Save] 存档成功: {saveName}");

                // 提示：如果要接入 Google Play Cloud Save，可以在这里抛出一个事件
                // FrameworkEntry.Instance.GetModule<EventModule>().Broadcast(new LocalSaveCompletedEvent { SaveName = saveName, JsonData = jsonContent });
            }
            catch (Exception e)
            {
                Log.Error($"[Save] 存档失败 ({saveName}): {e.Message}");
            }
        }

        /// <summary>
        /// 从本地读取存档（默认开启解密）
        /// 注意泛型约束 new()，当找不到存档时会自动返回一个初始化的新对象
        /// </summary>
        public T LoadData<T>(string saveName, bool useEncryption = true) where T : new()
        {
            string filePath = GetSaveFilePath(saveName);

            if (!_fileSystem.Exists(filePath))
            {
                Log.Warning($"[Save] 找不到存档文件: {saveName}，将创建全新存档。");
                T newData = new T();
                // 【新增】：新建存档时也需要绑定上下文
                if (newData is SaveDataBase newSaveBase) newSaveBase.OnBindContext(); 
                return newData;
            }

            try
            {
                string fileContent = _fileSystem.ReadText(filePath);
                if (useEncryption && _crypto != null)
                {
                    fileContent = _crypto.DecryptString(fileContent);
                    if (string.IsNullOrEmpty(fileContent)) return new T();
                }

                T data = JsonUtility.FromJson<T>(fileContent);
                Log.Info($"[Save] 读档成功: {saveName}");

                // 【新增】：读档完成后，立刻触发绑定钩子
                if (data is SaveDataBase saveBase)
                {
                    saveBase.OnBindContext(); // 绑定所有子节点

                    if (saveBase.IsAutoSaveEnabled)
                    {
                        TrackAutoSave(saveName, saveBase, () => SaveData(saveName, data, useEncryption));
                    }
                }

                return data;
            }
            catch (Exception e)
            {
                Log.Error($"[Save] 读档异常: {e.Message}");
                return new T();
            }
        }
        
        /// <summary>
        /// 检查存档是否存在
        /// </summary>
        public bool HasSave(string saveName)
        {
            return _fileSystem.Exists(GetSaveFilePath(saveName));
        }

        /// <summary>
        /// 删除存档
        /// </summary>
        public void DeleteSave(string saveName)
        {
            string filePath = GetSaveFilePath(saveName);
            if (_fileSystem.Exists(filePath))
            {
                _fileSystem.DeleteFile(filePath);
                Log.Info($"[Save] 存档已删除: {saveName}");
            }
        }

        /// <summary>
        /// 获取完整的存档文件路径，统一后缀名为 .sav
        /// </summary>
        public string GetSaveFilePath(string saveName)
        {
            return Path.Combine(_saveDirectory, $"{saveName}.sav");
        }
    }
}