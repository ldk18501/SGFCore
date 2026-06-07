#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace GameFramework.Core.UI.Editor
{
    internal static class UIBindingValidator
    {
        private static readonly Regex FieldNameRegex =
            new Regex("^(m_|_)?[a-zA-Z][a-zA-Z0-9_]*$", RegexOptions.Compiled);

        public static bool Validate(UIFormBase form, bool logDetails = true)
        {
            return Validate(form, logDetails, out _);
        }

        public static bool Validate(UIFormBase form, bool logDetails, out List<string> issues)
        {
            issues = new List<string>();
            if (form == null)
            {
                issues.Add("UIFormBase 为空。");
                return false;
            }

            HashSet<string> names = new HashSet<string>();
            UIBindingField[] fields = form.GetBindingFields() ?? new UIBindingField[0];
            for (int i = 0; i < fields.Length; i++)
            {
                UIBindingField field = fields[i];
                if (field == null)
                {
                    issues.Add($"第 {i + 1} 个绑定项为空。");
                    continue;
                }

                if (field.Access == UIBindingAccess.Header)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(field.VarName) || !FieldNameRegex.IsMatch(field.VarName))
                {
                    issues.Add($"字段名不规范: {field.VarName}");
                }

                if (!names.Add(field.VarName))
                {
                    issues.Add($"字段名重复: {field.VarName}");
                }

                Type componentType = UIBindingEditorUtility.ResolveType(field.ComponentTypeName);
                if (componentType == null)
                {
                    issues.Add($"字段 {field.VarName} 的组件类型丢失: {field.ComponentTypeName}");
                    continue;
                }

                if (field.Targets == null || field.Targets.Length == 0)
                {
                    issues.Add($"字段 {field.VarName} 没有目标节点。");
                    continue;
                }

                for (int j = 0; j < field.Targets.Length; j++)
                {
                    GameObject target = field.Targets[j];
                    if (target == null)
                    {
                        issues.Add($"字段 {field.VarName} 第 {j + 1} 个目标为空。");
                        continue;
                    }

                    if (componentType != typeof(GameObject) && target.GetComponent(componentType) == null)
                    {
                        issues.Add($"字段 {field.VarName} 的目标 {target.name} 缺少组件 {componentType.Name}。");
                    }
                }
            }

            if (logDetails)
            {
                if (issues.Count == 0)
                {
                    Debug.Log($"[UI Binding] {form.GetType().Name} 校验通过。", form);
                }
                else
                {
                    Debug.LogWarning($"[UI Binding] {form.GetType().Name} 校验发现 {issues.Count} 个问题:\n{string.Join("\n", issues)}", form);
                }
            }

            return issues.Count == 0;
        }

        [MenuItem("Tools/SGFCore/UI Binding/Validate All UI Prefabs")]
        public static void ValidateAllUIPrefabs()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            int formCount = 0;
            int issueCount = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }

                UIFormBase[] forms = prefab.GetComponentsInChildren<UIFormBase>(true);
                for (int j = 0; j < forms.Length; j++)
                {
                    formCount++;
                    Validate(forms[j], false, out List<string> issues);
                    if (issues.Count > 0)
                    {
                        issueCount += issues.Count;
                        Debug.LogWarning($"[UI Binding] {path} 发现 {issues.Count} 个问题:\n{string.Join("\n", issues)}", prefab);
                    }
                }
            }

            Debug.Log($"[UI Binding] 全量校验完成。UIForm 数: {formCount}, 问题数: {issueCount}");
        }
    }
}
#endif
