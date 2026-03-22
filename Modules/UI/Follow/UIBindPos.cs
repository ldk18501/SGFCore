using GameFramework.Core.Utility;
using UnityEngine;

namespace GameFramework.Core.UI
{
    /// <summary>
    /// UI 绑定并跟随 3D 静态坐标 (Vector3)
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public class UIBindPos : MonoBehaviour
    {
        [Header("--- 绑定坐标 ---")]
        public Vector3 WorldPos;

        [Header("--- 摄像机配置 ---")]
        public Camera WorldCamera;

        private RectTransform _rect;
        private RectTransform _parentRect;
        private CanvasGroup _canvasGroup;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _parentRect = _rect.parent as RectTransform;
            _canvasGroup = GetComponent<CanvasGroup>();

            if (WorldCamera == null) WorldCamera = Camera.main;
        }

        private void LateUpdate()
        {
            if (WorldCamera == null || _parentRect == null) return;

            var uiLocalPos = Vector2.zero;
            bool isFront = UIRoot.Instance.RootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? _parentRect.WorldToUIPosition(WorldPos, WorldCamera, null, out uiLocalPos)
                : _parentRect.WorldToUIPosition(WorldPos, WorldCamera, UIRoot.Instance.UICamera, out uiLocalPos); 

            if (isFront)
            {
                _rect.anchoredPosition = uiLocalPos;
                if (_canvasGroup.alpha < 1f) _canvasGroup.alpha = 1f;
            }
            else
            {
                if (_canvasGroup.alpha > 0f) _canvasGroup.alpha = 0f;
            }
        }

        public void Bind(Vector3 fixedWorldPos)
        {
            WorldPos = fixedWorldPos;
        }
    }
}