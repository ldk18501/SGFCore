using PrimeTween;
using UnityEngine;

namespace GameFramework.Core.UI
{
    /// <summary> 智能边缘飞入方向 </summary>
    public enum MoveDirection
    {
        CustomOffset, // 保留手动设置坐标的功能
        Top, // 从屏幕上方飞入/飞出
        Bottom, // 从屏幕下方飞入/飞出
        Left, // 从屏幕左侧飞入/飞出
        Right // 从屏幕右侧飞入/飞出
    }

    // ==========================================
    // 独立轨道配置类 (每个属性都有自己的时间轴)
    // ==========================================

    [System.Serializable]
    public class TweenPosConfig
    {
        public bool Enable;

        [Tooltip("智能方向，或选择 CustomOffset 手动填值")]
        public MoveDirection Direction = MoveDirection.Bottom;

        [Tooltip("只有 Direction 为 CustomOffset 时，此项才生效")]
        public Vector2 CustomOffset;

        public float Duration = 0.4f;
        public float Delay = 0f;
        public Ease EaseType = Ease.OutQuart; // 位移推荐用偏平滑的 Ease
    }

    [System.Serializable]
    public class TweenScaleConfig
    {
        public bool Enable;
        public Vector3 ScaleValue = Vector3.zero;

        public float Duration = 0.3f;
        public float Delay = 0.1f; // 缩放稍微延迟一点，动感更强
        public Ease EaseType = Ease.OutBack; // 缩放推荐带点 Q 弹的回弹
    }

    [System.Serializable]
    public class TweenAlphaConfig
    {
        public bool Enable;
        [Range(0, 1)] public float AlphaValue = 0f;

        public float Duration = 0.2f;
        public float Delay = 0f;
        public Ease EaseType = Ease.Linear;
    }

    [System.Serializable]
    public class UITweenStateConfig
    {
        public TweenPosConfig Position = new TweenPosConfig();
        public TweenScaleConfig Scale = new TweenScaleConfig();
        public TweenAlphaConfig Alpha = new TweenAlphaConfig();
    }

    /// <summary>
    /// UI 动效表现组件 (支持挂载到任意 UI 节点)
    /// </summary>
    public class UITweenElement : MonoBehaviour
    {
        [Header("入场动画 (PlayIn)")] public UITweenStateConfig InConfig;

        [Header("退场动画 (PlayOut)")] public UITweenStateConfig OutConfig;

        private RectTransform _rect;
        private CanvasGroup _canvasGroup;
        private RectTransform _rootCanvasRect; // 用于计算屏幕边界的根画布

        // 记录在 Unity 拼好的“完美状态”（最终展示状态）
        private Vector2 _originalPos;
        private Vector3 _originalScale;
        private float _originalAlpha;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();

            // 缓存原本的排版数据
            _originalPos = _rect.anchoredPosition;
            _originalScale = _rect.localScale;
            _originalAlpha = _canvasGroup != null ? _canvasGroup.alpha : 1f;
        }

        // ==========================================
        // 核心：智能计算屏幕外偏移量 (重点！)
        // ==========================================
        private Vector2 CalculateOffset(TweenPosConfig config)
        {
            if (config.Direction == MoveDirection.CustomOffset)
                return config.CustomOffset;

            // 惰性获取 Root Canvas (只需获取一次)
            if (_rootCanvasRect == null)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                if (canvas != null) _rootCanvasRect = canvas.transform as RectTransform;
            }

            if (_rootCanvasRect != null)
            {
                // 获取当前画布的实际宽高 (已经过 CanvasScaler 缩放处理的真实尺寸)
                Vector2 canvasSize = _rootCanvasRect.rect.size;

                // 无论你的 Anchor 是什么，偏移一整个屏幕的宽/高，绝对能保证彻底飞出屏幕可视范围
                switch (config.Direction)
                {
                    case MoveDirection.Top: return new Vector2(0, canvasSize.y);
                    case MoveDirection.Bottom: return new Vector2(0, -canvasSize.y);
                    case MoveDirection.Left: return new Vector2(-canvasSize.x, 0);
                    case MoveDirection.Right: return new Vector2(canvasSize.x, 0);
                }
            }

            return config.CustomOffset; // Fallback
        }

        /// <summary>
        /// 播放入场动画：从“偏移状态”回到“原始状态”
        /// </summary>
        public Sequence PlayIn()
        {
            StopAllTweens();
            Sequence seq = Sequence.Create();

            if (InConfig.Position.Enable)
            {
                Vector2 dynamicOffset = CalculateOffset(InConfig.Position);
                _rect.anchoredPosition = _originalPos + dynamicOffset;

                // 【修正】：使用 PrimeTween 标准的 startDelay 命名参数
                seq.Group(Tween.UIAnchoredPosition(
                    _rect,
                    _originalPos,
                    duration: InConfig.Position.Duration,
                    ease: InConfig.Position.EaseType,
                    startDelay: InConfig.Position.Delay));
            }

            if (InConfig.Scale.Enable)
            {
                _rect.localScale = InConfig.Scale.ScaleValue;

                seq.Group(Tween.Scale(
                    _rect,
                    _originalScale,
                    duration: InConfig.Scale.Duration,
                    ease: InConfig.Scale.EaseType,
                    startDelay: InConfig.Scale.Delay));
            }

            if (InConfig.Alpha.Enable && _canvasGroup != null)
            {
                _canvasGroup.alpha = InConfig.Alpha.AlphaValue;

                seq.Group(Tween.Alpha(
                    _canvasGroup,
                    _originalAlpha,
                    duration: InConfig.Alpha.Duration,
                    ease: InConfig.Alpha.EaseType,
                    startDelay: InConfig.Alpha.Delay));
            }

            return seq;
        }

        /// <summary>
        /// 播放退场动画：从“原始状态”走向“偏移状态”
        /// </summary>
        public Sequence PlayOut()
        {
            StopAllTweens();
            Sequence seq = Sequence.Create();

            if (OutConfig.Position.Enable)
            {
                Vector2 dynamicOffset = CalculateOffset(OutConfig.Position);
                _rect.anchoredPosition = _originalPos;

                // 【修正】：使用 PrimeTween 标准的 startDelay 命名参数
                seq.Group(Tween.UIAnchoredPosition(
                    _rect,
                    _originalPos + dynamicOffset,
                    duration: OutConfig.Position.Duration,
                    ease: OutConfig.Position.EaseType,
                    startDelay: OutConfig.Position.Delay));
            }

            if (OutConfig.Scale.Enable)
            {
                _rect.localScale = _originalScale;

                seq.Group(Tween.Scale(
                    _rect,
                    OutConfig.Scale.ScaleValue,
                    duration: OutConfig.Scale.Duration,
                    ease: OutConfig.Scale.EaseType,
                    startDelay: OutConfig.Scale.Delay));
            }

            if (OutConfig.Alpha.Enable && _canvasGroup != null)
            {
                _canvasGroup.alpha = _originalAlpha;

                seq.Group(Tween.Alpha(
                    _canvasGroup,
                    OutConfig.Alpha.AlphaValue,
                    duration: OutConfig.Alpha.Duration,
                    ease: OutConfig.Alpha.EaseType,
                    startDelay: OutConfig.Alpha.Delay));
            }

            return seq;
        }

        private void StopAllTweens()
        {
            Tween.StopAll(_rect);
            if (_canvasGroup != null) Tween.StopAll(_canvasGroup);
        }
    }
}