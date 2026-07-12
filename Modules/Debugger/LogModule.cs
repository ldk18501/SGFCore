using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace GameFramework.Core
{
    /// <summary>
    /// 线程安全的本地日志落盘模块。回调线程只负责入队，主线程分批写入，避免日志高峰阻塞业务。
    /// </summary>
    public sealed class LogModule : IFrameworkModule
    {
        private const string LogFilePattern = "GameLog_*.txt";
        private readonly ConcurrentQueue<string> _pendingLines = new ConcurrentQueue<string>();
        private StreamWriter _streamWriter;
        private string _logDirectory;
        private string _logFilePath;
        private float _flushTimer;
        private int _fileSequence;

        public int MaxLinesPerFrame { get; set; } = 200;
        public float FlushIntervalSeconds { get; set; } = 1f;
        public long MaxFileBytes { get; set; } = 8L * 1024L * 1024L;
        public int RetainedFileCount { get; set; } = 10;
        public int PendingCount => _pendingLines.Count;
        public string CurrentLogFilePath => _logFilePath;

        public void OnInit()
        {
            try
            {
                _logDirectory = Path.Combine(Application.persistentDataPath, "Logs");
                Directory.CreateDirectory(_logDirectory);
                OpenNewFile();
                DeleteExpiredFiles();
                Application.logMessageReceivedThreaded += OnLogMessageReceived;
                Log.Module("LogModule", $"日志模块初始化完成，日志路径: {_logFilePath}");
            }
            catch (Exception exception)
            {
                Debug.LogError($"无法创建日志文件: {exception.Message}");
                CloseWriter();
            }
        }

        public void OnUpdate(float deltaTime, float unscaledDeltaTime)
        {
            DrainQueue(Math.Max(1, MaxLinesPerFrame));
            _flushTimer += unscaledDeltaTime;
            if (_flushTimer >= Math.Max(0.1f, FlushIntervalSeconds))
            {
                _flushTimer = 0f;
                _streamWriter?.Flush();
            }
        }

        public void OnDestroy()
        {
            Application.logMessageReceivedThreaded -= OnLogMessageReceived;
            DrainQueue(int.MaxValue);
            if (_streamWriter != null)
            {
                _streamWriter.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [System] 游戏正常退出。");
            }

            CloseWriter();
        }

        private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            var builder = new StringBuilder(256);
            builder.Append('[').Append(DateTime.Now.ToString("HH:mm:ss.fff")).Append("] [")
                .Append(type).Append("] ").Append(condition);
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                builder.AppendLine().Append(stackTrace);
            }

            _pendingLines.Enqueue(builder.ToString());
        }

        private void DrainQueue(int maxLines)
        {
            if (_streamWriter == null)
            {
                while (_pendingLines.TryDequeue(out _)) { }
                return;
            }

            int written = 0;
            while (written < maxLines && _pendingLines.TryDequeue(out string line))
            {
                _streamWriter.WriteLine(line);
                written++;
                if (_streamWriter.BaseStream.Length >= Math.Max(1024L, MaxFileBytes))
                {
                    RotateFile();
                }
            }
        }

        private void RotateFile()
        {
            CloseWriter();
            OpenNewFile();
            DeleteExpiredFiles();
        }

        private void OpenNewFile()
        {
            string suffix = _fileSequence++ == 0 ? string.Empty : $"_{_fileSequence:D2}";
            string fileName = $"GameLog_{DateTime.Now:yyyyMMdd_HHmmss_fff}{suffix}.txt";
            _logFilePath = Path.Combine(_logDirectory, fileName);
            _streamWriter = new StreamWriter(_logFilePath, false, new UTF8Encoding(false), 4096)
            {
                AutoFlush = false
            };
        }

        private void DeleteExpiredFiles()
        {
            int retainedCount = Math.Max(1, RetainedFileCount);
            var files = new List<FileInfo>();
            foreach (string path in Directory.GetFiles(_logDirectory, LogFilePattern))
            {
                files.Add(new FileInfo(path));
            }

            files.Sort((left, right) => right.LastWriteTimeUtc.CompareTo(left.LastWriteTimeUtc));
            for (int i = retainedCount; i < files.Count; i++)
            {
                try
                {
                    files[i].Delete();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[LogModule] 删除过期日志失败: {files[i].FullName}, {exception.Message}");
                }
            }
        }

        private void CloseWriter()
        {
            if (_streamWriter == null)
            {
                return;
            }

            _streamWriter.Flush();
            _streamWriter.Dispose();
            _streamWriter = null;
        }
    }
}
