namespace GameFramework.Core
{
    /// <summary>
    /// 文件读写底层策略接口
    /// </summary>
    public interface IFileSystemStrategy
    {
        bool Exists(string filePath);
        
        string ReadText(string filePath);
        void WriteText(string filePath, string content);
        
        byte[] ReadBytes(string filePath);
        void WriteBytes(string filePath, byte[] bytes);
        
        void DeleteFile(string filePath);
    }
}