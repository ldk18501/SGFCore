using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameFramework.Core.UI
{
    public class RedPointBadge : MonoBehaviour
    {
        [SerializeField] private string _path;
        [SerializeField] private GameObject _target;
        [SerializeField] private Text _countText;
        [SerializeField] private TMP_Text _tmpCountText;
        [SerializeField] private bool _showCount;
        [SerializeField] private bool _hideWhenZero = true;
        [SerializeField] private bool _hideCountWhenZero = true;
        [SerializeField] private int _maxDisplayCount = 99;
        [SerializeField] private bool _playScalePulse;
        [SerializeField] private Vector3 _pulseScale = new Vector3(1.12f, 1.12f, 1f);
        [SerializeField] private float _pulseSpeed = 6f;

        private Vector3 _originalScale = Vector3.one;
        private Graphic[] _targetGraphics;
        private bool _isSubscribed;
        private RedPointSnapshot _currentSnapshot;

        public string Path => _path;

        protected virtual void Awake()
        {
            CacheTarget();
        }

        protected virtual void OnEnable()
        {
            CacheTarget();
            Subscribe();
        }

        protected virtual void OnDisable()
        {
            Unsubscribe();
            if (_target != null)
            {
                _target.transform.localScale = _originalScale;
            }
        }

        protected virtual void Update()
        {
            if (!_playScalePulse || _target == null || !_currentSnapshot.IsActive)
            {
                return;
            }

            float t = (Mathf.Sin(Time.unscaledTime * _pulseSpeed) + 1f) * 0.5f;
            _target.transform.localScale = Vector3.Lerp(_originalScale, _pulseScale, t);
        }

        public virtual void SetPath(string path, bool refreshImmediately = true)
        {
            if (_path == path)
            {
                if (refreshImmediately)
                {
                    Refresh();
                }

                return;
            }

            Unsubscribe();
            _path = path;

            if (isActiveAndEnabled)
            {
                Subscribe();
                if (refreshImmediately)
                {
                    Refresh();
                }
            }
        }

        public void Refresh()
        {
            RedPointModule module = GameApp.RedPoint;
            if (module == null || string.IsNullOrWhiteSpace(_path))
            {
                ApplySnapshot(new RedPointSnapshot(_path, 0, 0, 0));
                return;
            }

            ApplySnapshot(module.GetSnapshot(_path));
        }

        protected virtual void ApplySnapshot(RedPointSnapshot snapshot)
        {
            _currentSnapshot = snapshot;

            if (_target != null)
            {
                SetTargetVisible(!_hideWhenZero || snapshot.IsActive);
            }

            string countText = FormatCount(snapshot.Count);
            if (_countText != null)
            {
                SetTextVisible(_countText, _showCount && (!_hideCountWhenZero || snapshot.IsActive));
                _countText.text = countText;
            }

            if (_tmpCountText != null)
            {
                SetTextVisible(_tmpCountText, _showCount && (!_hideCountWhenZero || snapshot.IsActive));
                _tmpCountText.text = countText;
            }
        }

        private void Subscribe()
        {
            if (_isSubscribed || string.IsNullOrWhiteSpace(_path))
            {
                Refresh();
                return;
            }

            RedPointModule module = GameApp.RedPoint;
            if (module == null)
            {
                ApplySnapshot(new RedPointSnapshot(_path, 0, 0, 0));
                return;
            }

            module.AddListener(_path, ApplySnapshot);
            _isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_isSubscribed)
            {
                return;
            }

            RedPointModule module = GameApp.RedPoint;
            if (module != null)
            {
                module.RemoveListener(_path, ApplySnapshot);
            }

            _isSubscribed = false;
        }

        private void CacheTarget()
        {
            if (_target == null)
            {
                _target = gameObject;
            }

            _originalScale = _target.transform.localScale;
            _targetGraphics = _target.GetComponentsInChildren<Graphic>(true);
        }

        private void SetTargetVisible(bool visible)
        {
            if (_target == gameObject)
            {
                if (_targetGraphics != null)
                {
                    for (int i = 0; i < _targetGraphics.Length; i++)
                    {
                        _targetGraphics[i].enabled = visible;
                    }
                }

                _target.transform.localScale = visible ? _originalScale : Vector3.zero;
                return;
            }

            _target.SetActive(visible);
            if (!visible)
            {
                _target.transform.localScale = _originalScale;
            }
        }

        private void SetTextVisible(Text text, bool visible)
        {
            if (text.gameObject == gameObject)
            {
                text.enabled = visible;
                return;
            }

            text.gameObject.SetActive(visible);
        }

        private void SetTextVisible(TMP_Text text, bool visible)
        {
            if (text.gameObject == gameObject)
            {
                text.enabled = visible;
                return;
            }

            text.gameObject.SetActive(visible);
        }

        private string FormatCount(int count)
        {
            if (count <= 0)
            {
                return string.Empty;
            }

            if (_maxDisplayCount > 0 && count > _maxDisplayCount)
            {
                return _maxDisplayCount + "+";
            }

            return count.ToString();
        }
    }
}
