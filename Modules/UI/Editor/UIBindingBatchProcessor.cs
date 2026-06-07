#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GameFramework.Core.UI.Editor
{
    internal static class UIBindingBatchProcessor
    {
        [MenuItem("Tools/SGFCore/UI Binding/Generate All Binding Code")]
        public static void GenerateAllBindingCode()
        {
            int count = ForEachPrefabForm(form =>
            {
                UIBindingCodeGenerator.Generate(form);
            });

            Debug.Log($"[UI Binding] 批量生成绑定代码完成，UIForm 数: {count}");
        }

        [MenuItem("Tools/SGFCore/UI Binding/Bind All Prefab References")]
        public static void BindAllPrefabReferences()
        {
            int count = ForEachPrefabForm(form =>
            {
                UIBindingReferenceBinder.Bind(form);
            });

            AssetDatabase.SaveAssets();
            Debug.Log($"[UI Binding] 批量绑定 Prefab 引用完成，UIForm 数: {count}");
        }

        [MenuItem("Tools/SGFCore/UI Binding/Validate And Generate All")]
        public static void ValidateAndGenerateAll()
        {
            int count = ForEachPrefabForm(form =>
            {
                UIBindingValidator.Validate(form);
                UIBindingCodeGenerator.Generate(form);
            });

            Debug.Log($"[UI Binding] 批量校验并生成完成，UIForm 数: {count}");
        }

        private static int ForEachPrefabForm(System.Action<UIFormBase> action)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            int count = 0;
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
                    count++;
                    action?.Invoke(forms[j]);
                }
            }

            AssetDatabase.Refresh();
            return count;
        }
    }
}
#endif
