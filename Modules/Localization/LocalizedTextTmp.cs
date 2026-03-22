using TMPro; // 引入 TextMeshPro 核心命名空间
using UnityEngine;

namespace GameFramework.Core.UI
{
    /// <summary>
    /// 多语言文本组件 (基于 TextMeshPro)
    /// 挂载在 TMP 组件同级节点，配置好 KeyId，语言切换时自动刷新
    /// </summary>
    // 使用 TMP_Text 作为基类约束，这样既能支持 UGUI 里的 TextMeshProUGUI，
    // 也能支持 3D 世界空间里的 TextMeshPro 组件，极其通用！
    [RequireComponent(typeof(TMP_Text))] 
    public class LocalizedTextTmp : MonoBehaviour
    {
        [Tooltip("多语言表中的 ID")]
        public int KeyId;

        private TMP_Text _textComponent;

        private void Awake()
        {
            _textComponent = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            // 每次激活（打开 UI）时，立即刷新一次当前语言
            RefreshText();
            // 订阅全局语言切换事件
            GameApp.Event.AddListener<LanguageChangedEvent>(OnLanguageChanged);
        }

        private void OnDisable()
        {
            // UI 关闭时注销监听，防止在后台报错或浪费性能
            GameApp.Event.RemoveListener<LanguageChangedEvent>(OnLanguageChanged);
        }

        private void OnLanguageChanged(LanguageChangedEvent evt)
        {
            RefreshText();
        }

        public void RefreshText()
        {
            if (KeyId > 0 && GameApp.Loc != null)
            {
                // TMP 的 text 属性赋值和原生 Text 一模一样
                _textComponent.text = GameApp.Loc.GetString(KeyId);
            }
        }

        // 提供给代码动态修改 Key 的接口 (例如：动态刷新的任务目标说明)
        public void SetKeyId(int newKeyId)
        {
            KeyId = newKeyId;
            RefreshText();
        }
    }
}