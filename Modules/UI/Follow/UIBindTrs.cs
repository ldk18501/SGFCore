using GameFramework.Core.Utility;
using UnityEngine;

namespace GameFramework.Core.UI
{
    /// <summary>
    /// UI 绑定并跟随 3D 动态物体 (Transform)
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))] // 使用 CanvasGroup 控制显隐，不改 SetActive 避免 Rebuild 掉帧
    public class UIBindTrs : MonoBehaviour
    {
        [Header("--- 绑定目标 ---")] public Transform Target;
        public Vector3 WorldOffset = new Vector3(0, 2f, 0); // 比如在头顶 2 米处

        [Header("--- 摄像机配置 ---")] public Camera WorldCamera;

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
            if (Target == null || WorldCamera == null || _parentRect == null) return;

            // 1. 获取目标带偏移量的世界坐标
            Vector3 targetWorldPos = Target.position + WorldOffset;

            var uiLocalPos = Vector2.zero;
            // 2. 坐标转换
            bool isFront = UIRoot.Instance.RootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? _parentRect.WorldToUIPosition(targetWorldPos, WorldCamera, null, out uiLocalPos)
                : _parentRect.WorldToUIPosition(targetWorldPos, WorldCamera, UIRoot.Instance.UICamera, out uiLocalPos);

            if (isFront)
            {
                // 3. 物体在前方，更新坐标并显示
                _rect.anchoredPosition = uiLocalPos;
                if (_canvasGroup.alpha < 1f) _canvasGroup.alpha = 1f;
            }
            else
            {
                // 4. 物体在背后，安全隐藏 (使用 alpha=0 比 SetActive(false) 性能高得多)
                if (_canvasGroup.alpha > 0f) _canvasGroup.alpha = 0f;
            }
        }

        /// <summary> 提供给代码动态绑定的接口 </summary>
        public void Bind(Transform target, Vector3 worldOffset)
        {
            Target = target;
            WorldOffset = worldOffset;
        }
    }
}