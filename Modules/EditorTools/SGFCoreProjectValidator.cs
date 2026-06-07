#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace GameFramework.Editor
{
    public static class SGFCoreProjectValidator
    {
        [MenuItem("Tools/SGFCore/Validation/Run Project Validation")]
        public static void RunProjectValidation()
        {
            int missingCount = FindMissingScriptsAndReferences();
            int duplicateAddressCount = CheckAddressableAddresses();
            Debug.Log($"[SGFCore Validation] 完成。Missing/Null 引用: {missingCount}, Addressables 重复地址: {duplicateAddressCount}");
        }

        [MenuItem("Tools/SGFCore/Validation/Find Missing Scripts And References")]
        public static int FindMissingScriptsAndReferences()
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            int issueCount = 0;

            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }

                Component[] components = prefab.GetComponentsInChildren<Component>(true);
                for (int j = 0; j < components.Length; j++)
                {
                    if (components[j] == null)
                    {
                        issueCount++;
                        Debug.LogWarning($"[SGFCore Validation] Prefab 缺失脚本: {path}", prefab);
                        continue;
                    }

                    SerializedObject serializedObject = new SerializedObject(components[j]);
                    SerializedProperty iterator = serializedObject.GetIterator();
                    while (iterator.NextVisible(true))
                    {
                        if (iterator.propertyType == SerializedPropertyType.ObjectReference &&
                            iterator.objectReferenceValue == null &&
                            iterator.objectReferenceInstanceIDValue != 0)
                        {
                            issueCount++;
                            Debug.LogWarning(
                                $"[SGFCore Validation] Prefab 缺失引用: {path} -> {components[j].GetType().Name}.{iterator.propertyPath}",
                                prefab);
                        }
                    }
                }
            }

            Debug.Log($"[SGFCore Validation] Prefab 缺失脚本/引用扫描完成，问题数: {issueCount}");
            return issueCount;
        }

        [MenuItem("Tools/SGFCore/Validation/Check Addressables Addresses")]
        public static int CheckAddressableAddresses()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogWarning("[SGFCore Validation] 未找到 Addressables Settings。");
                return 0;
            }

            Dictionary<string, List<string>> addressToAssets = new Dictionary<string, List<string>>();
            foreach (AddressableAssetGroup group in settings.groups)
            {
                if (group == null)
                {
                    continue;
                }

                foreach (AddressableAssetEntry entry in group.entries)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.address))
                    {
                        continue;
                    }

                    if (!addressToAssets.TryGetValue(entry.address, out List<string> assets))
                    {
                        assets = new List<string>();
                        addressToAssets[entry.address] = assets;
                    }

                    assets.Add(entry.AssetPath);
                }
            }

            int duplicateCount = 0;
            foreach (KeyValuePair<string, List<string>> pair in addressToAssets)
            {
                if (pair.Value.Count <= 1)
                {
                    continue;
                }

                duplicateCount++;
                Debug.LogWarning($"[SGFCore Validation] Addressables 地址重复: {pair.Key}\n{string.Join("\n", pair.Value)}");
            }

            Debug.Log($"[SGFCore Validation] Addressables 地址检查完成，重复地址数: {duplicateCount}");
            return duplicateCount;
        }
    }
}
#endif
