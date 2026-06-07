#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace GameFramework.Core.UI.Editor
{
    internal static class UIBindingReferenceBinder
    {
        public static void Bind(UIFormBase form)
        {
            if (form == null)
            {
                return;
            }

            SerializedObject serializedObject = new SerializedObject(form);
            UIBindingField[] fields = form.GetBindingFields();
            int bindCount = 0;

            for (int i = 0; i < fields.Length; i++)
            {
                UIBindingField field = fields[i];
                if (field == null || field.Access == UIBindingAccess.Header || field.Targets == null)
                {
                    continue;
                }

                SerializedProperty property = serializedObject.FindProperty(field.VarName);
                if (property == null)
                {
                    Debug.LogWarning($"[UI Binding] 找不到字段 {field.VarName}。请先生成代码并等待编译完成。", form);
                    continue;
                }

                Type componentType = UIBindingEditorUtility.ResolveType(field.ComponentTypeName);
                if (componentType == null)
                {
                    Debug.LogWarning($"[UI Binding] 找不到类型 {field.ComponentTypeName}。", form);
                    continue;
                }

                if (property.isArray)
                {
                    property.ClearArray();
                    for (int j = 0; j < field.Targets.Length; j++)
                    {
                        property.InsertArrayElementAtIndex(j);
                        SerializedProperty element = property.GetArrayElementAtIndex(j);
                        element.objectReferenceValue = GetReference(field.Targets[j], componentType);
                    }
                    bindCount++;
                }
                else
                {
                    property.objectReferenceValue = field.Targets.Length > 0
                        ? GetReference(field.Targets[0], componentType)
                        : null;
                    bindCount++;
                }
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(form);
            Debug.Log($"[UI Binding] {form.GetType().Name} 绑定完成，字段数: {bindCount}", form);
        }

        private static UnityEngine.Object GetReference(GameObject target, Type componentType)
        {
            if (target == null)
            {
                return null;
            }

            if (componentType == typeof(GameObject))
            {
                return target;
            }

            return target.GetComponent(componentType);
        }
    }
}
#endif
