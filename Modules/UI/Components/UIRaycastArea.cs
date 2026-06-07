using UnityEngine;
using UnityEngine.UI;

namespace GameFramework.Core.UI
{
    /// <summary>
    /// 无绘制开销的 UI 点击区域，可替代透明 Image。
    /// </summary>
    [DisallowMultipleComponent]
    public class UIRaycastArea : MaskableGraphic
    {
        protected override void Awake()
        {
            base.Awake();
            raycastTarget = true;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            color = Color.clear;
            raycastTarget = true;
        }
#endif
    }
}
