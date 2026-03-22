using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace GameFramework.Editor
{
    /// <summary>
    /// Addressables 自动化构建管线
    /// 监听资源的导入和移动，自动划分 Group 并精简 Address
    /// </summary>
    public class AddressableAutoBuilder : AssetPostprocessor
    {
        // 监控的目标根目录
        private const string TARGET_FOLDER = "Assets/ResAddressable";

        /// <summary>
        /// 当任何资源被导入、删除、移动时，Unity 会自动调用这个方法
        /// </summary>
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            // 获取当前项目的 Addressables 设置
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return; // 如果项目还没初始化 Addressables，则跳过

            bool isSettingsModified = false;

            // 处理新导入的资源
            foreach (string path in importedAssets)
            {
                if (ProcessAsset(path, settings)) isSettingsModified = true;
            }

            // 处理被移动的资源
            foreach (string path in movedAssets)
            {
                if (ProcessAsset(path, settings)) isSettingsModified = true;
            }

            // 如果有修改，保存 Addressables 配置文件
            if (isSettingsModified)
            {
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
                Debug.Log("<color=#00FF00>[Addressables] 自动分组与命名刷新完成！</color>");
            }
        }

        private static bool ProcessAsset(string assetPath, AddressableAssetSettings settings)
        {
            // 1. 过滤：只处理目标文件夹下的资源，且忽略文件夹本身
            if (!assetPath.StartsWith(TARGET_FOLDER)) return false;
            if (AssetDatabase.IsValidFolder(assetPath)) return false;

            // 2. 计算 Group 名称
            // 例如: Assets/ResAddressable/UI/Sprites/Test/a.png
            // 相对路径: UI/Sprites/Test/a.png
            string relativePath = assetPath.Substring(TARGET_FOLDER.Length + 1);
            string directoryPath = Path.GetDirectoryName(relativePath);

            // 如果直接放在根目录下，给个默认组，否则按文件夹层级转换 (UI/Sprites/Test -> ui-sprites-test)
            string groupName = string.IsNullOrEmpty(directoryPath) 
                ? "root-assets" 
                : directoryPath.Replace('\\', '-').Replace('/', '-').ToLower();

            // 3. 计算极简 Address 名称
            // 默认去掉后缀：a.png -> "a"
            string addressName = Path.GetFileNameWithoutExtension(assetPath);
            
            // 【强烈建议】如果你的项目同名文件多，请注释掉上面那行，改用下面这行保留后缀：
            // string addressName = Path.GetFileName(assetPath); // a.png -> "a.png"

            // 4. 获取或创建对应的 Group
            AddressableAssetGroup group = settings.FindGroup(groupName);
            if (group == null)
            {
                // 创建新 Group，沿用默认组的 Schema 配置（打包方式等）
                group = settings.CreateGroup(groupName, false, false, true, settings.DefaultGroup.Schemas);
            }

            // 5. 将资源加入 Addressables 体系
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, readOnly: false, postEvent: false);

            if (entry != null)
            {
                // 6. 核心：修改 Address 名称为极简名
                if (entry.address != addressName)
                {
                    entry.SetAddress(addressName);
                    return true; // 返回 true 告诉外层有修改发生
                }
            }

            return false;
        }
    }
}