using UnityEngine;

namespace GameFramework.Core.UI
{
    /// <summary>
    /// 可在多个 UI Prefab 间复用的过渡动画配置。
    /// </summary>
    [CreateAssetMenu(menuName = "SGFCore/UI/Tween Profile", fileName = "UITweenProfile")]
    public sealed class UITweenProfile : ScriptableObject
    {
        [SerializeField] private UITweenStateConfig _in = new UITweenStateConfig();
        [SerializeField] private UITweenStateConfig _out = new UITweenStateConfig();

        public UITweenStateConfig In => _in;
        public UITweenStateConfig Out => _out;
    }
}
