using UnityEngine;

namespace GameFramework.Core
{
    /// <summary>
    /// 全局文件系统模块（对外提供的统一接口）
    /// </summary>
    public class FileSystemModule : IFrameworkModule
    {
        private IFileSystemStrategy _strategy;

        public void OnInit()
        {
            // 根据不同平台宏定义，自动选择不同的底层实现策略
#if WECHAT_MINIGAME
            //_strategy = new WeChatFileSystemStrategy();
#elif BYTEDANCE_MINIGAME
            // _strategy = new DouyinFileSystemStrategy();
#else
            _strategy = new StandardFileSystemStrategy();
#endif
            if (_strategy == null)
            {
                throw new System.InvalidOperationException(
                    "当前平台尚未配置 IFileSystemStrategy，请先接入对应小游戏文件系统实现。");
            }

            Log.Module("FileSystem", $"文件系统模块初始化完成，当前策略: {_strategy.GetType().Name}");
        }

        public void OnUpdate(float deltaTime, float unscaledDeltaTime) { }

        public void OnDestroy() { }

        // --- 以下是对外暴露的 API，内部全部委托给 _strategy 执行 ---

        /// <summary>
        /// 获取当前平台的持久化根目录
        /// </summary>
        public string GetPersistentDataPath()
        {
            // 小游戏平台可能有专门的用户目录，如 wx.env.USER_DATA_PATH
#if WECHAT_MINIGAME
            return "wechat_user_data_path"; // 这里替换为微信 SDK 获取路径的代码
#else
            return Application.persistentDataPath;
#endif
        }

        public bool Exists(string relativeOrAbsolutePath) => _strategy.Exists(ResolvePath(relativeOrAbsolutePath));
        
        public string ReadText(string filePath) => _strategy.ReadText(ResolvePath(filePath));
        
        public void WriteText(string filePath, string content) => _strategy.WriteText(ResolvePath(filePath), content);

        public void WriteTextAtomic(string filePath, string content) =>
            _strategy.WriteTextAtomic(ResolvePath(filePath), content);
        
        public byte[] ReadBytes(string filePath) => _strategy.ReadBytes(ResolvePath(filePath));
        
        public void WriteBytes(string filePath, byte[] bytes) => _strategy.WriteBytes(ResolvePath(filePath), bytes);

        public void DeleteFile(string filePath) => _strategy.DeleteFile(ResolvePath(filePath));

        private string ResolvePath(string relativeOrAbsolutePath)
        {
            if (string.IsNullOrWhiteSpace(relativeOrAbsolutePath) ||
                System.IO.Path.IsPathRooted(relativeOrAbsolutePath))
            {
                return relativeOrAbsolutePath;
            }

            return System.IO.Path.Combine(GetPersistentDataPath(), relativeOrAbsolutePath);
        }
    }
}
