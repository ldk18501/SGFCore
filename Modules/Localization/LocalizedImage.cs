using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace GameFramework.Core.UI
{
    [RequireComponent(typeof(Image))]
    public class LocalizedImage : MonoBehaviour
    {
        [Tooltip("图片的 Addressables 基础名称，会自动拼接 _EN, _ZH 等")]
        public string BaseAddress; 

        private Image _imageComponent;
        private Sprite _loadedSprite;

        private void Awake()
        {
            _imageComponent = GetComponent<Image>();
        }

        private void OnEnable()
        {
            RefreshImage().Forget();
            GameApp.Event.AddListener<LanguageChangedEvent>(OnLanguageChanged);
        }

        private void OnDisable()
        {
            GameApp.Event.RemoveListener<LanguageChangedEvent>(OnLanguageChanged);
            ReleaseCurrentSprite();
        }

        private void OnLanguageChanged(LanguageChangedEvent evt)
        {
            RefreshImage().Forget();
        }

        private async UniTaskVoid RefreshImage()
        {
            if (string.IsNullOrEmpty(BaseAddress) || GameApp.Loc == null) return;

            // 释放旧图片
            ReleaseCurrentSprite();

            string currentLangStr = GameApp.Loc.CurrentLanguage == SystemLanguageType.Default 
                ? "Default" 
                : GameApp.Loc.CurrentLanguage.ToString();

            string address = $"{BaseAddress}_{currentLangStr}";

            // 异步加载新语言版本的图片
            _loadedSprite = await GameApp.Res.LoadAssetAsync<Sprite>(address);

            // 容错 Fallback 机制
            if (_loadedSprite == null && currentLangStr != "Default")
            {
                Log.Warning($"[LocalizedImage] 找不到本地化图片 {address}，尝试回退 Default");
                _loadedSprite = await GameApp.Res.LoadAssetAsync<Sprite>($"{BaseAddress}_Default");
            }

            if (_loadedSprite != null && _imageComponent != null)
            {
                _imageComponent.sprite = _loadedSprite;
            }
        }

        private void ReleaseCurrentSprite()
        {
            if (_loadedSprite != null)
            {
                GameApp.Res.ReleaseAsset(_loadedSprite);
                _loadedSprite = null;
            }
        }
    }
}