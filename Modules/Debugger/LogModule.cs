using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace GameFramework.Core
{
    /// <summary>
    /// 日志模块：负责捕获全局异常并将日志持久化到本地文件
    /// </summary>
    public class LogModule : IFrameworkModule
    {
        public int Priority => 0; // 优先级极高，最先启动以捕获启动阶段的异常

        private string _logFilePath;
        private StreamWriter _streamWriter;
        private readonly StringBuilder _logBuilder = new StringBuilder();

        public void OnInit()
        {
            // 定义日志保存路径（持久化数据目录）
            string logDirectory = Path.Combine(Application.persistentDataPath, "Logs");
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            // 使用时间戳作为日志文件名
            string fileName = $"GameLog_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            _logFilePath = Path.Combine(logDirectory, fileName);

            try
            {
                _streamWriter = new StreamWriter(_logFilePath, false, Encoding.UTF8)
                {
                    AutoFlush = true // 自动刷新，防止崩溃时日志丢失
                };
                
                // 监听 Unity 的全局日志回调
                Application.logMessageReceived += OnLogMessageReceived;
                
                Log.Module("LogModule", $"日志模块初始化完成，日志路径: {_logFilePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"无法创建日志文件: {e.Message}");
            }
        }

        public void OnUpdate(float deltaTime, float unscaledDeltaTime)
        {
            // 此处可以扩展：如果是高频日志，可以将日志先存入队列，在 Update 中分批写入文件，优化 IO 性能
        }

        public void OnDestroy()
        {
            Application.logMessageReceived -= OnLogMessageReceived;

            if (_streamWriter != null)
            {
                _streamWriter.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [System] 游戏正常退出。");
                _streamWriter.Close();
                _streamWriter.Dispose();
                _streamWriter = null;
            }
        }

        /// <summary>
        /// 捕获日志并写入文件
        /// </summary>
        private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            if (_streamWriter == null) return;

            _logBuilder.Clear();
            _logBuilder.Append($"[{DateTime.Now:HH:mm:ss.fff}] ");
            _logBuilder.Append($"[{type.ToString()}] ");
            _logBuilder.Append(condition);

            // 如果是错误或异常，把堆栈信息也写进去
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                _logBuilder.AppendLine();
                _logBuilder.Append(stackTrace);
            }

            _streamWriter.WriteLine(_logBuilder.ToString());
        }
    }
}