using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace GameFramework.Core
{
    /// <summary>
    /// 全局静态日志工具类
    /// </summary>
    public static class Log
    {
        // 只有在 PlayerSettings 的 Scripting Define Symbols 中定义了 ENABLE_LOG 才会编译这些方法
        // 或者在 Editor 下默认生效（UNITY_EDITOR）
        
        [Conditional("ENABLE_LOG"), Conditional("UNITY_EDITOR")]
        public static void Info(string message, string color = "#FFFFFF")
        {
            Debug.Log($"<color={color}>[Info] {message}</color>");
        }

        [Conditional("ENABLE_LOG"), Conditional("UNITY_EDITOR")]
        public static void Warning(string message)
        {
            Debug.LogWarning($"<color=#FFFF00>[Warning] {message}</color>");
        }

        // Error 和 Fatal 通常不加条件编译，确保即使在正式包中也能捕获严重错误
        public static void Error(string message)
        {
            Debug.LogError($"<color=#FF0000>[Error] {message}</color>");
        }

        public static void Fatal(string message)
        {
            Debug.LogError($"<color=#FF00FF>[Fatal] {message}</color>");
        }
        
        // 针对特定模块的日志格式化扩展
        [Conditional("ENABLE_LOG"), Conditional("UNITY_EDITOR")]
        public static void Module(string moduleName, string message, string color = "#00FF00")
        {
            Debug.Log($"<color={color}>[{moduleName}]</color> {message}");
        }
    }
}