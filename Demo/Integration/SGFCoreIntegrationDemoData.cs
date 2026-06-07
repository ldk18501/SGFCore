using System;
using System.Text;
using UnityEngine;

namespace GameFramework.Core.Demo
{
    [Serializable]
    public class SGFCoreIntegrationDemoData
    {
        public string ConfigText;
        public string NetImageFileUrl;
    }

    [Serializable]
    public class SGFCoreIntegrationSaveData
    {
        public int OpenCount;
        public string LastOpenTime;
    }

    public static class SGFCoreIntegrationDemoConfig
    {
        public static string Text { get; private set; } = "Config not loaded";

        public static void Load(byte[] bytes)
        {
            Text = bytes == null || bytes.Length == 0
                ? "Config bytes are empty"
                : Encoding.UTF8.GetString(bytes);
            Debug.Log($"[SGFCoreDemo] Config loaded: {Text}");
        }
    }
}
