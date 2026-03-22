using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace GameFramework.Editor
{
    /// <summary>
    /// Addressables 目录增强工具
    /// 包含：文件夹 Inspector 面板重写 + Project 右键菜单自动化管理
    /// </summary>
    [CustomEditor(typeof(DefaultAsset))]
    public class AddressableFolderTool : UnityEditor.Editor
    {
        // 你的 Addressables 根目录
        private const string TARGET_FOLDER = "Assets/ResAddressable";

        // ==========================================
        // 核心功能 1：重写目录的 Inspector 面板
        // ==========================================
        public override void OnInspectorGUI()
        {
            string path = AssetDatabase.GetAssetPath(target);

            // 如果不是文件夹，或者不在我们的目标管线目录下，退回 Unity 默认的绘制逻辑
            if (!AssetDatabase.IsValidFolder(path) || !path.StartsWith(TARGET_FOLDER))
            {
                base.OnInspectorGUI();
                return;
            }

            GUI.enabled = true;
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("📦 Addressables 目录控制台", EditorStyles.boldLabel);

            string groupName = GetGroupName(path);
            EditorGUILayout.HelpBox($"当前目录映射的 Group 名称: \n{groupName}", MessageType.Info);

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                EditorGUILayout.HelpBox("未找到 Addressable Settings，请先在项目中初始化！", MessageType.Error);
                return;
            }

            var group = settings.FindGroup(groupName);
            if (group == null)
            {
                EditorGUILayout.HelpBox("该目录对应的 Group 尚未创建（通常因为目录为空）。", MessageType.Warning);
                if (GUILayout.Button("立刻强制创建 Group", GUILayout.Height(30)))
                {
                    CreateGroup(settings, groupName);
                }
                return;
            }

            // 获取打包模式 Schema
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema != null)
            {
                EditorGUILayout.Space(5);
                
                // 监听下拉框改变
                EditorGUI.BeginChangeCheck();
                var newMode = (BundledAssetGroupSchema.BundlePackingMode)EditorGUILayout.EnumPopup(
                    "打包模式 (Bundle Mode)", schema.BundleMode);
                    
                if (EditorGUI.EndChangeCheck())
                {
                    schema.BundleMode = newMode;
                    EditorUtility.SetDirty(settings);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"<color=#00FF00>[Addressables]</color> Group '{groupName}' 打包模式已切换为 {newMode}");
                }

                EditorGUILayout.Space(15);
                if (GUILayout.Button("递归应用此 Bundle Mode 到所有子目录", GUILayout.Height(35)))
                {
                    ApplyBundleModeToSubFolders(path, settings, newMode);
                }
            }
        }

        // ==========================================
        // 核心功能 2：Project 面板右键菜单
        // ==========================================

        [MenuItem("Assets/Addressables 管线/1. 一键检查并补全所有缺失的 Group", false, 100)]
        public static void SyncAndCreateGroups()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return;

            string selectedPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            
            // 获取选中目录及其所有子目录
            string[] allFolders = Directory.GetDirectories(selectedPath, "*", SearchOption.AllDirectories);
            int createCount = 0;

            // 包含当前选中的根目录自己
            CheckAndCreateGroupForFolder(selectedPath, settings, ref createCount);

            foreach (string folder in allFolders)
            {
                // 统一路径格式为斜杠
                string normalizedFolder = folder.Replace('\\', '/');
                CheckAndCreateGroupForFolder(normalizedFolder, settings, ref createCount);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"<color=#00FF00>[Addressables] 检查完毕！共补全创建了 {createCount} 个确实包含资源的 Group。</color>");
        }

        [MenuItem("Assets/Addressables 管线/2. 一键清理空 Group 与失效引用", false, 101)]
        public static void CleanEmptyGroups()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return;

            int removeCount = 0;
            int missingEntryCount = 0;

            // 倒序遍历，因为我们要执行删除操作
            for (int i = settings.groups.Count - 1; i >= 0; i--)
            {
                var group = settings.groups[i];
                
                // 跳过 Unity 的默认内置 Group
                if (group.IsDefaultGroup() || group.Name == "Built In Data") continue;

                // 1. 清理 Group 内部的失效引用 (Missing Reference)
                // 将 ICollection 转存为一个临时的 List 副本，完美避开遍历时修改集合的异常，且无需索引器
                var entriesCopy = new System.Collections.Generic.List<UnityEditor.AddressableAssets.Settings.AddressableAssetEntry>(group.entries);
                
                foreach (var entry in entriesCopy)
                {
                    if (entry.TargetAsset == null) // 文件已在硬盘被删，但 Addressables 里的引用变成了 Missing
                    {
                        group.RemoveAssetEntry(entry);
                        missingEntryCount++;
                    }
                }

                // 2. 如果清理后或者本身就是空 Group，直接删掉
                if (group.entries.Count == 0)
                {
                    settings.RemoveGroup(group);
                    removeCount++;
                }
            }

            if (removeCount > 0 || missingEntryCount > 0)
            {
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }

            Debug.Log($"<color=#00FFFF>[Addressables] 清理完毕！删除了 {removeCount} 个空 Group，清除了 {missingEntryCount} 个失效引用。</color>");
        }

        // ==========================================
        // 右键菜单可用性验证 (只在目标目录下显示)
        // ==========================================
        [MenuItem("Assets/Addressables 管线/1. 一键检查并补全所有缺失的 Group", true)]
        [MenuItem("Assets/Addressables 管线/2. 一键清理空 Group 与失效引用", true)]
        public static bool ValidateAddressableTools()
        {
            if (Selection.activeObject == null) return false;
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            return AssetDatabase.IsValidFolder(path) && path.StartsWith(TARGET_FOLDER);
        }

        // ==========================================
        // 内部辅助方法
        // ==========================================

        private static string GetGroupName(string folderPath)
        {
            // 算法必须与 AddressableAutoBuilder 保持绝对一致
            string relativePath = folderPath.Substring(TARGET_FOLDER.Length);
            if (relativePath.StartsWith("/")) relativePath = relativePath.Substring(1);

            return string.IsNullOrEmpty(relativePath) 
                ? "root-assets" 
                : relativePath.Replace('\\', '-').Replace('/', '-').ToLower();
        }

        private static AddressableAssetGroup CreateGroup(AddressableAssetSettings settings, string groupName)
        {
            var group = settings.CreateGroup(groupName, false, false, true, settings.DefaultGroup.Schemas);
            EditorUtility.SetDirty(settings);
            return group;
        }

        private static void ApplyBundleModeToSubFolders(string rootFolderPath, AddressableAssetSettings settings, BundledAssetGroupSchema.BundlePackingMode targetMode)
        {
            string[] subFolders = Directory.GetDirectories(rootFolderPath, "*", SearchOption.AllDirectories);
            int modifyCount = 0;

            foreach (string folder in subFolders)
            {
                string normalizedFolder = folder.Replace('\\', '/');
                string subGroupName = GetGroupName(normalizedFolder);
                var subGroup = settings.FindGroup(subGroupName);

                if (subGroup != null)
                {
                    var schema = subGroup.GetSchema<BundledAssetGroupSchema>();
                    if (schema != null && schema.BundleMode != targetMode)
                    {
                        schema.BundleMode = targetMode;
                        modifyCount++;
                    }
                }
            }

            if (modifyCount > 0)
            {
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
            Debug.Log($"<color=#00FF00>[Addressables] 已成功将 {modifyCount} 个子目录的 Bundle Mode 批量修改为 {targetMode}。</color>");
        }

        private static void CheckAndCreateGroupForFolder(string folderPath, AddressableAssetSettings settings, ref int createCount)
        {
            // 安全检查：只有当这个目录下真的有资源文件时，才去补全 Group。防止美术建了一堆空壳文件夹导致生成垃圾 Group
            string[] files = Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly);
            bool hasRealAssets = false;
            foreach (var file in files)
            {
                if (!file.EndsWith(".meta")) // 忽略 Unity 的 meta 文件
                {
                    hasRealAssets = true;
                    break;
                }
            }

            if (hasRealAssets)
            {
                string groupName = GetGroupName(folderPath);
                if (settings.FindGroup(groupName) == null)
                {
                    CreateGroup(settings, groupName);
                    createCount++;
                }
            }
        }
    }
}