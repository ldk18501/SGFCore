#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameFramework.Core.UI.Editor
{
    internal static class UIBindingEditorUtility
    {
        private const string ArraySuffix = "Array";

        public static UIFormBase FindActiveForm()
        {
            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null && prefabStage.prefabContentsRoot != null)
            {
                return prefabStage.prefabContentsRoot.GetComponent<UIFormBase>();
            }

            if (Selection.activeGameObject == null)
            {
                return null;
            }

            return Selection.activeGameObject.GetComponentInParent<UIFormBase>(true);
        }

        public static GameObject[] GetOrderedSelection()
        {
            if (Selection.gameObjects == null || Selection.gameObjects.Length == 0)
            {
                return new GameObject[0];
            }

            return Selection.gameObjects
                .Where(go => go != null)
                .OrderBy(go => go.transform.GetSiblingIndex())
                .ToArray();
        }

        public static Type[] GetCommonBindableTypes(GameObject[] targets)
        {
            if (targets == null || targets.Length == 0 || targets[0] == null)
            {
                return new Type[0];
            }

            HashSet<Type> commonTypes = new HashSet<Type>();
            Component[] firstComponents = targets[0].GetComponents<Component>();
            for (int i = 0; i < firstComponents.Length; i++)
            {
                if (firstComponents[i] != null)
                {
                    commonTypes.Add(firstComponents[i].GetType());
                }
            }

            for (int i = 1; i < targets.Length; i++)
            {
                GameObject target = targets[i];
                if (target == null)
                {
                    commonTypes.Clear();
                    break;
                }

                HashSet<Type> targetTypes = new HashSet<Type>();
                Component[] components = target.GetComponents<Component>();
                for (int j = 0; j < components.Length; j++)
                {
                    if (components[j] != null)
                    {
                        targetTypes.Add(components[j].GetType());
                    }
                }

                commonTypes.IntersectWith(targetTypes);
            }

            List<Type> result = new List<Type> { typeof(GameObject) };
            result.AddRange(commonTypes.OrderBy(type => type.Name));
            return result.ToArray();
        }

        public static void AddBinding(UIFormBase form, UIBindingAccess access, Type componentType, GameObject[] targets)
        {
            if (form == null || componentType == null || targets == null || targets.Length == 0)
            {
                return;
            }

            UIBindingField[] oldFields = form.GetBindingFields() ?? new UIBindingField[0];
            List<UIBindingField> fields = new List<UIBindingField>(oldFields);
            string fieldName = GenerateUniqueFieldName(fields, targets);

            fields.Add(new UIBindingField(
                fieldName,
                componentType.FullName,
                targets,
                access));

            Undo.RecordObject(form, "Add UI Binding");
            form.SetBindingFields(fields.ToArray());
            EditorUtility.SetDirty(form);
        }

        public static void AddHeader(UIFormBase form, string title = "Group")
        {
            if (form == null)
            {
                return;
            }

            UIBindingField[] oldFields = form.GetBindingFields() ?? new UIBindingField[0];
            List<UIBindingField> fields = new List<UIBindingField>(oldFields)
            {
                new UIBindingField(title, string.Empty, new GameObject[0], UIBindingAccess.Header)
            };

            Undo.RecordObject(form, "Add UI Binding Header");
            form.SetBindingFields(fields.ToArray());
            EditorUtility.SetDirty(form);
        }

        public static void RemoveSelectedBindings(UIFormBase form, GameObject[] targets)
        {
            if (form == null || targets == null || targets.Length == 0)
            {
                return;
            }

            HashSet<GameObject> targetSet = new HashSet<GameObject>(targets);
            UIBindingField[] oldFields = form.GetBindingFields() ?? new UIBindingField[0];
            List<UIBindingField> fields = new List<UIBindingField>();

            for (int i = 0; i < oldFields.Length; i++)
            {
                UIBindingField field = oldFields[i];
                if (field == null)
                {
                    continue;
                }

                if (field.Access == UIBindingAccess.Header)
                {
                    fields.Add(field);
                    continue;
                }

                if (field.Targets == null)
                {
                    continue;
                }

                List<GameObject> keptTargets = new List<GameObject>();
                for (int j = 0; j < field.Targets.Length; j++)
                {
                    if (field.Targets[j] != null && !targetSet.Contains(field.Targets[j]))
                    {
                        keptTargets.Add(field.Targets[j]);
                    }
                }

                if (keptTargets.Count > 0)
                {
                    field.Targets = keptTargets.ToArray();
                    fields.Add(field);
                }
            }

            Undo.RecordObject(form, "Remove UI Binding");
            form.SetBindingFields(fields.ToArray());
            EditorUtility.SetDirty(form);
        }

        public static Type ResolveType(string fullName)
        {
            if (string.IsNullOrEmpty(fullName))
            {
                return null;
            }

            if (fullName == typeof(GameObject).FullName)
            {
                return typeof(GameObject);
            }

            Type type = Type.GetType(fullName);
            if (type != null)
            {
                return type;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(fullName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        public static string GetTypeCodeName(Type type)
        {
            if (type == typeof(GameObject))
            {
                return "GameObject";
            }

            return type.IsNested ? type.FullName.Replace('+', '.') : type.Name;
        }

        public static string GetDisplayName(Type type)
        {
            if (type == null)
            {
                return "<Missing>";
            }

            return string.IsNullOrEmpty(type.Namespace) ? type.Name : $"{type.Name} ({type.Namespace})";
        }

        private static string GenerateUniqueFieldName(List<UIBindingField> fields, GameObject[] targets)
        {
            string baseName = SanitizeIdentifier(targets[0].name);
            if (targets.Length > 1)
            {
                baseName += ArraySuffix;
            }

            string fieldName = "m_" + char.ToUpperInvariant(baseName[0]) + baseName.Substring(1);
            string uniqueName = fieldName;
            int index = 2;
            while (fields.Any(field => field != null && field.VarName == uniqueName))
            {
                uniqueName = fieldName + index;
                index++;
            }

            return uniqueName;
        }

        private static string SanitizeIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Field";
            }

            string sanitized = Regex.Replace(value, "[^a-zA-Z0-9_]", string.Empty);
            if (string.IsNullOrEmpty(sanitized))
            {
                sanitized = "Field";
            }

            if (char.IsDigit(sanitized[0]))
            {
                sanitized = "_" + sanitized;
            }

            return sanitized;
        }
    }
}
#endif
