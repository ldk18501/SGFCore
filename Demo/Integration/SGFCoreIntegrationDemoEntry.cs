using Cysharp.Threading.Tasks;
using System.Threading;
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

        private void Start()
        {
            StartAsync(this.GetCancellationTokenOnDestroy()).Forget(Debug.LogException);
        }

        private async UniTask StartAsync(CancellationToken cancellationToken)
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 60;

            Debug.Log("[SGFCoreDemo] Boot begin.");

            bool initialized = await FrameworkEntry.Instance.InitFrameworkModulesAsync(
                cancellationToken: cancellationToken);
            if (!initialized)
            {
                Debug.LogError("[SGFCoreDemo] Framework initialization failed or was canceled.");
                return;
            }

            Debug.Log("[SGFCoreDemo] Framework and Addressables are ready.");

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
