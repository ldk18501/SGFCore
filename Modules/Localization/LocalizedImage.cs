using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace GameFramework.Core.UI
{
    [RequireComponent(typeof(Image))]
    public class LocalizedImage : MonoBehaviour
    {
        [FormerlySerializedAs("BaseAddress")]
        [SerializeField] private string _baseAddress;
        [SerializeField] private bool _clearWhenMissing;

        private Image _imageComponent;
        private Sprite _loadedSprite;
        private bool _subscribed;
        private int _refreshVersion;

        public string BaseAddress
        {
            get => _baseAddress;
            set => SetBaseAddress(value);
        }

        private void Awake()
        {
            _imageComponent = GetComponent<Image>();
        }

        private void OnEnable()
        {
            RefreshImage().Forget();
            Subscribe();
        }

        private void OnDisable()
        {
            _refreshVersion++;
            Unsubscribe();
            ReleaseCurrentSprite();
        }

        public void SetBaseAddress(string baseAddress)
        {
            _baseAddress = baseAddress;
            if (isActiveAndEnabled)
            {
                RefreshImage().Forget();
            }
        }

        public async UniTaskVoid RefreshImage()
        {
            if (string.IsNullOrWhiteSpace(_baseAddress))
            {
                return;
            }

            LocalizationModule localization = GameApp.Loc;
            ResourceModule resource = GameApp.Res;
            if (localization == null || resource == null)
            {
                return;
            }

            int version = ++_refreshVersion;
            string languageSuffix = localization.GetLanguageSuffix(localization.CurrentLanguage);
            Sprite sprite = await LoadSpriteWithFallback(resource, languageSuffix);

            if (version != _refreshVersion || !isActiveAndEnabled)
            {
                ReleaseSpriteIfNeeded(resource, sprite);
                return;
            }

            if (sprite == null)
            {
                if (_clearWhenMissing && _imageComponent != null)
                {
                    _imageComponent.sprite = null;
                }

                ReleaseCurrentSprite();
                return;
            }

            if (sprite == _loadedSprite)
            {
                resource.ReleaseAsset(sprite);
                return;
            }

            ReleaseCurrentSprite();
            _loadedSprite = sprite;
            if (_imageComponent != null)
            {
                _imageComponent.sprite = _loadedSprite;
            }
        }

        private async UniTask<Sprite> LoadSpriteWithFallback(ResourceModule resource, string languageSuffix)
        {
            string address = $"{_baseAddress}_{languageSuffix}";
            Sprite sprite = await resource.LoadAssetAsync<Sprite>(address);
            if (sprite != null || languageSuffix == "Default")
            {
                return sprite;
            }

            Log.Warning($"[LocalizedImage] 找不到本地化图片 {address}，尝试回退 Default。");
            return await resource.LoadAssetAsync<Sprite>($"{_baseAddress}_Default");
        }

        private void Subscribe()
        {
            EventModule eventModule = GameApp.Event;
            if (_subscribed || eventModule == null)
            {
                return;
            }

            eventModule.AddListener<LanguageChangedEvent>(OnLanguageChanged);
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            EventModule eventModule = GameApp.Event;
            if (!_subscribed || eventModule == null)
            {
                return;
            }

            eventModule.RemoveListener<LanguageChangedEvent>(OnLanguageChanged);
            _subscribed = false;
        }

        private void OnLanguageChanged(LanguageChangedEvent evt)
        {
            RefreshImage().Forget();
        }

        private void ReleaseCurrentSprite()
        {
            if (_loadedSprite == null)
            {
                return;
            }

            GameApp.Res.ReleaseAsset(_loadedSprite);
            _loadedSprite = null;
        }

        private void ReleaseSpriteIfNeeded(ResourceModule resource, Sprite sprite)
        {
            if (sprite != null && sprite != _loadedSprite)
            {
                resource.ReleaseAsset(sprite);
            }
        }

    }
}
