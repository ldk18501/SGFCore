using UnityEngine;
using UnityEngine.UI;

namespace GameFramework.Core.UI
{
    /// <summary>
    /// 多语言文本组件
    /// 挂载在 Text 组件同级节点，配置好 KeyId，语言切换时自动刷新
    /// </summary>
    [RequireComponent(typeof(Text))] // 如果你用 TextMeshPro，改成 TMP_Text
    public class LocalizedText : MonoBehaviour
    {
        [Tooltip("多语言表中的 ID")]
        public int KeyId;

        private Text _textComponent;

        private void Awake()
        {
            _textComponent = GetComponent<Text>();
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
            // UI 关闭时注销监听，防止报错
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
                _textComponent.text = GameApp.Loc.GetString(KeyId);
            }
        }

        // 提供给代码动态修改 Key 的接口
        public void SetKeyId(int newKeyId)
        {
            KeyId = newKeyId;
            RefreshText();
        }
    }
}