using System.IO;

namespace GameFramework.Core
{
    /// <summary>
    /// 标准文件系统策略（适用于 PC, Android, iOS）
    /// </summary>
    public class StandardFileSystemStrategy : IFileSystemStrategy
    {
        public bool Exists(string filePath)
        {
            return File.Exists(filePath);
        }

        public string ReadText(string filePath)
        {
            if (!Exists(filePath)) return string.Empty;
            return File.ReadAllText(filePath);
        }

        public void WriteText(string filePath, string content)
        {
            // 确保目录存在
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(filePath, content);
        }

        public byte[] ReadBytes(string filePath)
        {
            if (!Exists(filePath)) return null;
            return File.ReadAllBytes(filePath);
        }

        public void WriteBytes(string filePath, byte[] bytes)
        {
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllBytes(filePath, bytes);
        }

        public void DeleteFile(string filePath)
        {
            if (Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}