#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace GameFramework.Core.UI.Editor
{
    [CustomEditor(typeof(UIFormBase), true)]
    public class UIFormBindingEditor : UnityEditor.Editor
    {
        private SerializedProperty _bindingFields;
        private bool _showBindings = true;

        private void OnEnable()
        {
            _bindingFields = serializedObject.FindProperty("_bindingFields");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawBindingToolbar();
            DrawBindingOutputPath();
            DrawBindingList();

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(6);
            DrawDefaultInspector();
        }

        private void DrawBindingToolbar()
        {
            UIFormBase form = target as UIFormBase;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("SGFCore UI Binding", EditorStyles.boldLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("生成绑定代码", GUILayout.Height(28)))
                    {
                        UIBindingCodeGenerator.Generate(form);
                    }

                    if (GUILayout.Button("绑定引用", GUILayout.Height(28)))
                    {
                        UIBindingReferenceBinder.Bind(form);
                    }

                    if (GUILayout.Button("校验", GUILayout.Height(28)))
                    {
                        UIBindingValidator.Validate(form);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("添加分组"))
                    {
                        UIBindingEditorUtility.AddHeader(form);
                        serializedObject.Update();
                    }

                    if (GUILayout.Button("打开绑定代码"))
                    {
                        string path = UIBindingCodeGenerator.GetGeneratedScriptPath(form);
                        if (!string.IsNullOrEmpty(path))
                        {
                            UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(path, 1);
                        }
                    }

                    if (GUILayout.Button("清空绑定"))
                    {
                        if (EditorUtility.DisplayDialog("UI Binding", "确定清空当前 UI 的所有绑定记录？", "清空", "取消"))
                        {
                            Undo.RecordObject(form, "Clear UI Bindings");
                            form.SetBindingFields(new UIBindingField[0]);
                            EditorUtility.SetDirty(form);
                            serializedObject.Update();
                        }
                    }
                }
            }
        }

        private void DrawBindingOutputPath()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                string path = UIBindingCodeGenerator.OutputPath;
                string newPath = EditorGUILayout.TextField("生成目录", path);
                if (newPath != path)
                {
                    UIBindingCodeGenerator.OutputPath = newPath;
                }

                EditorGUILayout.HelpBox("业务 UI 类需要声明为 partial class。生成文件默认放在 Assets/Scripts/UIBindings，可按项目习惯调整。", MessageType.Info);
            }
        }

        private void DrawBindingList()
        {
            if (_bindingFields == null)
            {
                return;
            }

            _showBindings = EditorGUILayout.Foldout(_showBindings, $"绑定项 ({_bindingFields.arraySize})", true);
            if (!_showBindings)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                for (int i = 0; i < _bindingFields.arraySize; i++)
                {
                    SerializedProperty item = _bindingFields.GetArrayElementAtIndex(i);
                    SerializedProperty varName = item.FindPropertyRelative("VarName");
                    SerializedProperty componentTypeName = item.FindPropertyRelative("ComponentTypeName");
                    SerializedProperty access = item.FindPropertyRelative("Access");
                    SerializedProperty targets = item.FindPropertyRelative("Targets");

                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.PropertyField(access, GUIContent.none, GUILayout.Width(86));
                            EditorGUILayout.PropertyField(varName, GUIContent.none);
                            if (GUILayout.Button("X", GUILayout.Width(24)))
                            {
                                _bindingFields.DeleteArrayElementAtIndex(i);
                                break;
                            }
                        }

                        if ((UIBindingAccess)access.enumValueIndex == UIBindingAccess.Header)
                        {
                            EditorGUILayout.HelpBox("生成代码时会输出为 [Header]，不会绑定引用。", MessageType.None);
                            continue;
                        }

                        Type type = UIBindingEditorUtility.ResolveType(componentTypeName.stringValue);
                        EditorGUILayout.LabelField("类型", UIBindingEditorUtility.GetDisplayName(type));
                        EditorGUILayout.PropertyField(targets, new GUIContent("目标节点"), true);
                    }
                }
            }
        }
    }
}
#endif
