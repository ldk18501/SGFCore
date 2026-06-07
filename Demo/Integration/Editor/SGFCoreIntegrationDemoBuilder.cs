#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using GameFramework.Core.UI;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameFramework.Core.Demo.Editor
{
    public static class SGFCoreIntegrationDemoBuilder
    {
        private const string DemoGroupName = "SGFCoreDemo";
        private const string DemoUIAddress = SGFCoreIntegrationDemoEntry.DemoUIAddress;
        private const string DemoConfigAddress = SGFCoreIntegrationDemoEntry.DemoConfigAddress;

        [MenuItem("SGFCore/Demo/Build Integration Demo", false, 10)]
        public static void BuildDemo()
        {
            string integrationFolder = GetIntegrationFolder();
            string generatedFolder = NormalizePath(Path.Combine(integrationFolder, "Generated"));
            EnsureFolder(generatedFolder);

            string prefabPath = NormalizePath(Path.Combine(generatedFolder, "SGFCoreIntegrationDemoUI.prefab"));
            string configPath = NormalizePath(Path.Combine(generatedFolder, "SGFCoreDemoConfig.bytes"));
            string imagePath = NormalizePath(Path.Combine(generatedFolder, "SGFCoreDemoNetImage.png"));
            string scenePath = NormalizePath(Path.Combine(generatedFolder, "SGFCoreIntegrationDemo.unity"));

            CreateConfigAsset(configPath);
            CreateNetImageAsset(imagePath);
            CreateDemoUIPrefab(prefabPath);
            CreateDemoScene(scenePath, imagePath);
            ConfigureAddressables(prefabPath, configPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[SGFCoreDemo] 集成测试 Demo 已生成: {scenePath}");
        }

        [MenuItem("SGFCore/Demo/Open Integration Demo Scene", false, 11)]
        public static void OpenDemoScene()
        {
            string scenePath = NormalizePath(Path.Combine(GetIntegrationFolder(), "Generated/SGFCoreIntegrationDemo.unity"));
            if (!File.Exists(scenePath))
            {
                BuildDemo();
            }

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        private static void CreateDemoUIPrefab(string prefabPath)
        {
            GameObject root = new GameObject("SGFCoreIntegrationDemoUI", typeof(RectTransform));
            RectTransform rect = root.transform as RectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            root.AddComponent<GraphicRaycaster>();
            root.AddComponent<SGFCoreIntegrationDemoForm>();

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void CreateDemoScene(string scenePath, string imagePath)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "SGFCoreIntegrationDemo";

            Camera mainCamera = CreateMainCamera();
            CreateEventSystem();
            CreateUIRoot(mainCamera);

            GameObject entryObject = new GameObject("SGFCoreIntegrationDemoEntry");
            SGFCoreIntegrationDemoEntry entry = entryObject.AddComponent<SGFCoreIntegrationDemoEntry>();
            SerializedObject serializedEntry = new SerializedObject(entry);
            serializedEntry.FindProperty("_netImageFileUrl").stringValue = new Uri(Path.GetFullPath(imagePath)).AbsoluteUri;
            serializedEntry.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, scenePath);
        }

        private static void CreateUIRoot(Camera uiCamera)
        {
            GameObject rootObject = new GameObject("UIRoot", typeof(RectTransform));
            RectTransform rootRect = rootObject.transform as RectTransform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            Canvas canvas = rootObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            rootObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            rootObject.AddComponent<GraphicRaycaster>();

            UIRoot uiRoot = rootObject.AddComponent<UIRoot>();
            uiRoot.RootCanvas = canvas;
            uiRoot.UICamera = uiCamera;

            uiRoot.Transform_Background = CreateLayer(rootObject.transform, "Background");
            uiRoot.Transform_Common = CreateLayer(rootObject.transform, "Common");
            uiRoot.Transform_Popup = CreateLayer(rootObject.transform, "Popup");
            uiRoot.Transform_Top = CreateLayer(rootObject.transform, "Top");
            uiRoot.Transform_Guide = CreateLayer(rootObject.transform, "Guide");
            uiRoot.Transform_System = CreateLayer(rootObject.transform, "System");
        }

        private static Transform CreateLayer(Transform parent, string name)
        {
            GameObject layer = new GameObject(name, typeof(RectTransform));
            layer.transform.SetParent(parent, false);
            RectTransform rect = layer.transform as RectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return layer.transform;
        }

        private static void CreateEventSystem()
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        private static Camera CreateMainCamera()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.06f, 0.08f);
            cameraObject.AddComponent<AudioListener>();
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            return camera;
        }

        private static void CreateConfigAsset(string configPath)
        {
            string text = "Hello from SGFCore Addressables config. If this text appears, ConfigModule + ResourceModule worked.";
            File.WriteAllBytes(configPath, Encoding.UTF8.GetBytes(text));
            AssetDatabase.ImportAsset(configPath, ImportAssetOptions.ForceUpdate);
        }

        private static void CreateNetImageAsset(string imagePath)
        {
            Texture2D texture = new Texture2D(96, 96, TextureFormat.RGBA32, false);
            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    float stripe = ((x / 12) + (y / 12)) % 2 == 0 ? 1f : 0.72f;
                    Color color = Color.Lerp(new Color(0.18f, 0.58f, 0.92f), new Color(0.95f, 0.32f, 0.38f), x / 95f);
                    texture.SetPixel(x, y, color * stripe);
                }
            }

            texture.Apply();
            File.WriteAllBytes(imagePath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(imagePath, ImportAssetOptions.ForceUpdate);
        }

        private static void ConfigureAddressables(string prefabPath, string configPath)
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            }

            if (settings == null)
            {
                Debug.LogError("[SGFCoreDemo] Addressables settings could not be created.");
                return;
            }

            AddressableAssetGroup group = settings.FindGroup(DemoGroupName);
            if (group == null)
            {
                group = settings.CreateGroup(
                    DemoGroupName,
                    false,
                    false,
                    true,
                    null,
                    typeof(BundledAssetGroupSchema),
                    typeof(ContentUpdateGroupSchema));
            }

            AddAddressableEntry(settings, group, prefabPath, DemoUIAddress);
            AddAddressableEntry(settings, group, configPath, DemoConfigAddress);
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, group, true);
        }

        private static void AddAddressableEntry(AddressableAssetSettings settings, AddressableAssetGroup group, string assetPath, string address)
        {
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogError($"[SGFCoreDemo] Missing asset guid: {assetPath}");
                return;
            }

            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group);
            entry.address = address;
            entry.SetLabel("SGFCoreDemo", true, true);
        }

        private static string GetIntegrationFolder()
        {
            string[] guids = AssetDatabase.FindAssets("SGFCoreIntegrationDemoBuilder t:MonoScript");
            if (guids.Length == 0)
            {
                return "Assets/SGFCore/Demo/Integration";
            }

            string scriptPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            string editorFolder = Path.GetDirectoryName(scriptPath);
            return NormalizePath(Path.GetDirectoryName(editorFolder));
        }

        private static void EnsureFolder(string assetFolder)
        {
            string[] parts = assetFolder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }
    }
}
#endif
