using UnityEngine;
using UnityEngine.EventSystems;

namespace GameFramework.Core
{
    public class GuideTarget : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private string _targetKey;
        [SerializeField] private bool _registerOnEnable = true;
        [SerializeField] private bool _notifyClick = true;
        [SerializeField] private Vector2 _worldHighlightSize = new Vector2(160f, 80f);

        public string TargetKey => _targetKey;
        public Transform TargetTransform => transform;
        public RectTransform RectTransform => transform as RectTransform;
        public Vector2 WorldHighlightSize => _worldHighlightSize;

        protected virtual void OnEnable()
        {
            if (_registerOnEnable)
            {
                GuideTargetRegistry.Register(_targetKey, this);
            }
        }

        protected virtual void OnDisable()
        {
            if (_registerOnEnable)
            {
                GuideTargetRegistry.Unregister(_targetKey, this);
            }
        }

        public void SetTargetKey(string targetKey)
        {
            if (_registerOnEnable && isActiveAndEnabled)
            {
                GuideTargetRegistry.Unregister(_targetKey, this);
            }

            _targetKey = targetKey;

            if (_registerOnEnable && isActiveAndEnabled)
            {
                GuideTargetRegistry.Register(_targetKey, this);
            }
        }

        public virtual void OnPointerClick(PointerEventData eventData)
        {
            if (!_notifyClick || string.IsNullOrWhiteSpace(_targetKey))
            {
                return;
            }

            GuideModule module = GameApp.Guide;
            if (module != null)
            {
                module.NotifyTargetClicked(_targetKey);
            }
        }
    }
}
