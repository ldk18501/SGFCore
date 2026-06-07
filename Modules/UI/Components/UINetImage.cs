using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace GameFramework.Core.UI
{
    /// <summary>
    /// 网络图片组件，支持请求取消、占位图、失败图和静态缓存。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public class UINetImage : MonoBehaviour
    {
        private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();

        [SerializeField] private Image _image;
        [SerializeField] private Sprite _placeholderSprite;
        [SerializeField] private Sprite _errorSprite;
        [SerializeField] private bool _useCache = true;
        [SerializeField] private bool _setNativeSize;
        [SerializeField] private bool _cancelOnDisable = true;

        private Coroutine _loadCoroutine;
        private UnityWebRequest _request;
        private string _currentUrl;
        private Sprite _runtimeSprite;
        private int _loadVersion;

        public string CurrentUrl => _currentUrl;
        public bool IsLoading => _loadCoroutine != null;

        private void Reset()
        {
            _image = GetComponent<Image>();
        }

        private void Awake()
        {
            if (_image == null)
            {
                _image = GetComponent<Image>();
            }
        }

        private void OnDisable()
        {
            if (_cancelOnDisable)
            {
                CancelLoad();
            }
        }

        private void OnDestroy()
        {
            CancelLoad();
            ReleaseRuntimeSprite();
        }

        public void Load(string url)
        {
            if (_image == null)
            {
                _image = GetComponent<Image>();
            }

            CancelLoad();
            ReleaseRuntimeSprite();
            _currentUrl = url;

            if (string.IsNullOrWhiteSpace(url))
            {
                SetSprite(_placeholderSprite);
                return;
            }

            if (_useCache && SpriteCache.TryGetValue(url, out Sprite cachedSprite) && cachedSprite != null)
            {
                SetSprite(cachedSprite);
                return;
            }

            SetSprite(_placeholderSprite);
            _loadCoroutine = StartCoroutine(LoadRoutine(url, ++_loadVersion));
        }

        public void Clear()
        {
            CancelLoad();
            ReleaseRuntimeSprite();
            _currentUrl = string.Empty;
            SetSprite(null);
        }

        public void CancelLoad()
        {
            _loadVersion++;

            if (_request != null)
            {
                _request.Abort();
                _request.Dispose();
                _request = null;
            }

            if (_loadCoroutine != null)
            {
                StopCoroutine(_loadCoroutine);
                _loadCoroutine = null;
            }
        }

        public static void ClearCache()
        {
            foreach (var sprite in SpriteCache.Values)
            {
                DestroySprite(sprite);
            }

            SpriteCache.Clear();
        }

        public static void RemoveCache(string url)
        {
            if (string.IsNullOrEmpty(url) || !SpriteCache.TryGetValue(url, out Sprite sprite))
            {
                return;
            }

            SpriteCache.Remove(url);
            DestroySprite(sprite);
        }

        private IEnumerator LoadRoutine(string url, int version)
        {
            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
            {
                _request = request;
                yield return request.SendWebRequest();

                if (version != _loadVersion)
                {
                    _request = null;
                    yield break;
                }

                _request = null;
                _loadCoroutine = null;

                if (!IsRequestSuccess(request))
                {
                    SetSprite(_errorSprite);
                    yield break;
                }

                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                if (texture == null)
                {
                    SetSprite(_errorSprite);
                    yield break;
                }

                Sprite sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f));
                sprite.name = $"NetImage_{texture.width}x{texture.height}";

                if (_useCache)
                {
                    SpriteCache[url] = sprite;
                }
                else
                {
                    _runtimeSprite = sprite;
                }

                SetSprite(sprite);
            }
        }

        private void SetSprite(Sprite sprite)
        {
            if (_image == null)
            {
                return;
            }

            _image.sprite = sprite;
            _image.enabled = sprite != null;

            if (_setNativeSize && sprite != null)
            {
                _image.SetNativeSize();
            }
        }

        private void ReleaseRuntimeSprite()
        {
            if (_runtimeSprite == null)
            {
                return;
            }

            if (_image != null && _image.sprite == _runtimeSprite)
            {
                _image.sprite = null;
            }

            DestroySprite(_runtimeSprite);
            _runtimeSprite = null;
        }

        private static void DestroySprite(Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }

            Texture texture = sprite.texture;
            UnityEngine.Object.Destroy(sprite);

            if (texture != null)
            {
                UnityEngine.Object.Destroy(texture);
            }
        }

        private static bool IsRequestSuccess(UnityWebRequest request)
        {
#if UNITY_2020_2_OR_NEWER
            return request.result == UnityWebRequest.Result.Success;
#else
            return !request.isNetworkError && !request.isHttpError;
#endif
        }
    }
}
