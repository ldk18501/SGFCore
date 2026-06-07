#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace GameFramework.Core.UI.Editor
{
    internal static class UIBindingMenuItems
    {
        [MenuItem("GameObject/SGFCore/UI Binding/Add Private", false, -100020)]
        private static void AddPrivateBinding()
        {
            ShowAddMenu(UIBindingAccess.Private);
        }

        [MenuItem("GameObject/SGFCore/UI Binding/Add Protected", false, -100019)]
        private static void AddProtectedBinding()
        {
            ShowAddMenu(UIBindingAccess.Protected);
        }

        [MenuItem("GameObject/SGFCore/UI Binding/Add Public", false, -100018)]
        private static void AddPublicBinding()
        {
            ShowAddMenu(UIBindingAccess.Public);
        }

        [MenuItem("GameObject/SGFCore/UI Binding/Add Header", false, -100017)]
        private static void AddHeader()
        {
            UIFormBase form = UIBindingEditorUtility.FindActiveForm();
            UIBindingEditorUtility.AddHeader(form);
        }

        [MenuItem("GameObject/SGFCore/UI Binding/Remove Selected", false, -100016)]
        private static void RemoveSelectedBinding()
        {
            UIFormBase form = UIBindingEditorUtility.FindActiveForm();
            GameObject[] targets = UIBindingEditorUtility.GetOrderedSelection();
            UIBindingEditorUtility.RemoveSelectedBindings(form, targets);
        }

        [MenuItem("GameObject/SGFCore/UI Binding/Generate Binding Code", false, -100014)]
        private static void GenerateBindingCode()
        {
            UIFormBase form = UIBindingEditorUtility.FindActiveForm();
            UIBindingCodeGenerator.Generate(form);
        }

        [MenuItem("GameObject/SGFCore/UI Binding/Validate", false, -100013)]
        private static void ValidateBinding()
        {
            UIFormBase form = UIBindingEditorUtility.FindActiveForm();
            UIBindingValidator.Validate(form);
        }

        [MenuItem("GameObject/SGFCore/UI Binding/Bind References", false, -100012)]
        private static void BindReferences()
        {
            UIFormBase form = UIBindingEditorUtility.FindActiveForm();
            UIBindingReferenceBinder.Bind(form);
        }

        [MenuItem("GameObject/SGFCore/UI Binding/Generate And Bind", false, -100011)]
        private static void GenerateAndBind()
        {
            UIFormBase form = UIBindingEditorUtility.FindActiveForm();
            UIBindingCodeGenerator.Generate(form);
            UIBindingReferenceBinder.Bind(form);
            UIBindingValidator.Validate(form);
        }

        [MenuItem("GameObject/SGFCore/UI Binding/Add Private", true)]
        [MenuItem("GameObject/SGFCore/UI Binding/Add Protected", true)]
        [MenuItem("GameObject/SGFCore/UI Binding/Add Public", true)]
        [MenuItem("GameObject/SGFCore/UI Binding/Add Header", true)]
        [MenuItem("GameObject/SGFCore/UI Binding/Remove Selected", true)]
        [MenuItem("GameObject/SGFCore/UI Binding/Generate Binding Code", true)]
        [MenuItem("GameObject/SGFCore/UI Binding/Validate", true)]
        [MenuItem("GameObject/SGFCore/UI Binding/Bind References", true)]
        [MenuItem("GameObject/SGFCore/UI Binding/Generate And Bind", true)]
        private static bool ValidateBindingMenu()
        {
            return UIBindingEditorUtility.FindActiveForm() != null;
        }

        private static void ShowAddMenu(UIBindingAccess access)
        {
            UIFormBase form = UIBindingEditorUtility.FindActiveForm();
            GameObject[] targets = UIBindingEditorUtility.GetOrderedSelection();
            Type[] types = UIBindingEditorUtility.GetCommonBindableTypes(targets);

            if (form == null || targets.Length == 0 || types.Length == 0)
            {
                return;
            }

            GenericMenu menu = new GenericMenu();
            for (int i = 0; i < types.Length; i++)
            {
                Type type = types[i];
                menu.AddItem(new GUIContent(UIBindingEditorUtility.GetDisplayName(type)), false, () =>
                {
                    UIBindingEditorUtility.AddBinding(form, access, type, targets);
                });
            }

            menu.ShowAsContext();
        }
    }
}
#endif
