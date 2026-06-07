using Cysharp.Threading.Tasks;
using GameFramework.Core.UI;
using UnityEngine;

namespace GameFramework.Core.Demo
{
    /// <summary>
    /// SGFCore 集成测试入口：初始化框架、加载配置、注册 UI，并打开 Demo UI。
    /// </summary>
    public class SGFCoreIntegrationDemoEntry : MonoBehaviour
    {
        public const int DemoFormId = 990001;
        public const string DemoUIAddress = "SGFCore_Demo_UI";
        public const string DemoConfigAddress = "SGFCore_Demo_Config";
        public const string DemoConfigName = "SGFCoreDemoConfig";

        [SerializeField] private string _netImageFileUrl;

        private async void Start()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 60;

            Debug.Log("[SGFCoreDemo] Boot begin.");

            FrameworkEntry.Instance.InitFrameworkModules();

            bool resourceReady = await GameApp.Res.EnsureInitializedAsync();
            Debug.Log($"[SGFCoreDemo] Resource ready: {resourceReady}");

            TestSaveModule();
            await TestConfigModule();
            TestRedPointModule();

            GameApp.UI.RegisterUI(
                DemoFormId,
                DemoUIAddress,
                typeof(SGFCoreIntegrationDemoForm),
                UILayer.Common,
                isSingleton: true,
                isCached: false);

            int serialId = await GameApp.UI.OpenUIAsync(
                DemoFormId,
                new SGFCoreIntegrationDemoData
                {
                    ConfigText = SGFCoreIntegrationDemoConfig.Text,
                    NetImageFileUrl = _netImageFileUrl
                });

            Debug.Log($"[SGFCoreDemo] Open demo UI serialId: {serialId}");
        }

        public void SetNetImageFileUrl(string fileUrl)
        {
            _netImageFileUrl = fileUrl;
        }

        private static void TestSaveModule()
        {
            SGFCoreIntegrationSaveData saveData = GameApp.Save.LoadData<SGFCoreIntegrationSaveData>(
                "SGFCoreIntegrationDemo",
                useEncryption: false);

            saveData.OpenCount++;
            saveData.LastOpenTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            GameApp.Save.SaveData("SGFCoreIntegrationDemo", saveData, useEncryption: false);
            Debug.Log($"[SGFCoreDemo] Save roundtrip ok. OpenCount={saveData.OpenCount}");
        }

        private static async UniTask TestConfigModule()
        {
            GameApp.Config.RegisterConfig(DemoConfigName, SGFCoreIntegrationDemoConfig.Load);
            await GameApp.Config.LoadConfigAsync(DemoConfigAddress, DemoConfigName);
        }

        private static void TestRedPointModule()
        {
            GameApp.RedPoint.SetCount("demo.mail.unread", 1);
            GameApp.RedPoint.SetCount("demo.task.daily", 2);
            Debug.Log($"[SGFCoreDemo] RedPoint demo total: {GameApp.RedPoint.GetCount("demo")}");
        }
    }
}
