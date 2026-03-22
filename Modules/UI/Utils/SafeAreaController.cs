using UnityEngine;

namespace GameFramework.Core.UI
{
    /// <summary>
    /// 全面屏/刘海屏安全区适配控制器
    /// 建议挂载在 Canvas 下的 UIRoot 节点或特定面板的 Content 节点上
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    // 加上 ExecuteAlways 可以在 Unity 的 Device Simulator (设备模拟器) 中实时预览刘海屏效果！
    [ExecuteAlways] 
    public class SafeAreaController : MonoBehaviour
    {
        [Header("--- 适配轴向控制 ---")]
        [Tooltip("是否适配左右两侧的安全区 (横屏游戏通常勾选)")]
        public bool ConformX = true;
        [Tooltip("是否适配上下两侧的安全区 (竖屏游戏通常勾选)")]
        public bool ConformY = true;

        private RectTransform _rectTransform;
        
        // 缓存上一次的状态，避免每帧修改 Transform 导致 Canvas 重绘 (零 GC 脏标记)
        private Rect _lastSafeArea = new Rect(0, 0, 0, 0);
        private Vector2Int _lastScreenSize = Vector2Int.zero;
        private ScreenOrientation _lastOrientation = ScreenOrientation.AutoRotation;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            RefreshSafeArea();
        }

        private void Update()
        {
            // 运行时屏幕旋转或设备模拟器切换设备时，自动触发刷新
            RefreshSafeArea();
        }

        /// <summary>
        /// 检查并刷新安全区
        /// </summary>
        private void RefreshSafeArea()
        {
            Rect safeArea = Screen.safeArea;

            // 脏标记检查：如果安全区、分辨率、屏幕方向都没变，直接跳过 (极其省性能)
            if (safeArea != _lastSafeArea || 
                Screen.width != _lastScreenSize.x || 
                Screen.height != _lastScreenSize.y || 
                Screen.orientation != _lastOrientation)
            {
                _lastSafeArea = safeArea;
                _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
                _lastOrientation = Screen.orientation;

                ApplySafeArea(safeArea);
            }
        }

        private void ApplySafeArea(Rect safeArea)
        {
            if (_rectTransform == null) return;

            // 将屏幕像素坐标转换为 0~1 的 Anchor 比例坐标
            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;

            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            // 如果不适配 X 轴，还原默认的左右铺满状态
            if (!ConformX)
            {
                anchorMin.x = 0f;
                anchorMax.x = 1f;
            }

            // 如果不适配 Y 轴，还原默认的上下铺满状态
            if (!ConformY)
            {
                anchorMin.y = 0f;
                anchorMax.y = 1f;
            }

            // 应用到 RectTransform (这会自动缩放并挤压节点到刘海屏之外)
            _rectTransform.anchorMin = anchorMin;
            _rectTransform.anchorMax = anchorMax;

            Log.Info($"[SafeArea] 屏幕自适应更新完成. 屏幕尺寸: {Screen.width}x{Screen.height}, 安全区: {safeArea}");
        }
    }
}