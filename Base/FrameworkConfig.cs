using UnityEngine;

namespace GameFramework.Core
{
    /// <summary>
    /// 框架启动配置。可以在启动代码里手动创建，也可以做成项目内的 ScriptableObject 资产引用。
    /// </summary>
    [CreateAssetMenu(menuName = "SGFCore/Framework Config", fileName = "SGFCoreFrameworkConfig")]
    public class FrameworkConfig : ScriptableObject
    {
        [SerializeField] private bool _configureCryptoOnStartup;
        [SerializeField] private string _cryptoKey;
        [SerializeField] private string _cryptoIV;

        public bool ConfigureCryptoOnStartup => _configureCryptoOnStartup;
        public string CryptoKey => _cryptoKey;
        public string CryptoIV => _cryptoIV;
    }
}
