using UnityEngine;
using System.Collections.Generic;

namespace GameFramework.Core.UI
{
    /// <summary>
    /// UI 根节点实体类，挂载在 UIRoot 预制体上
    /// 方便在 Editor 中直接配置和预览
    /// </summary>
    public class UIRoot : TMonoSingleton<UIRoot>
    {
        [Header("Canvas 设置")]
        public Canvas RootCanvas;
        public Camera UICamera;

        [Header("层级节点 (按枚举顺序拖入)")]
        public Transform Transform_Background;
        public Transform Transform_Common;
        public Transform Transform_Popup;
        public Transform Transform_Top;
        public Transform Transform_Guide;
        public Transform Transform_System;

        private Dictionary<UILayer, Transform> _layerDict;

        private void Awake()
        {
            // 初始化字典，方便后续按枚举快速获取挂载点
            _layerDict = new Dictionary<UILayer, Transform>
            {
                { UILayer.Background, Transform_Background },
                { UILayer.Common, Transform_Common },
                { UILayer.Popup, Transform_Popup },
                { UILayer.Top, Transform_Top },
                { UILayer.Guide, Transform_Guide },
                { UILayer.System, Transform_System }
            };
        }

        public Transform GetLayerNode(UILayer layer)
        {
            if (_layerDict.TryGetValue(layer, out Transform node))
            {
                return node;
            }
            Log.Error($"[UIRoot] 找不到层级节点: {layer}");
            return transform; // 默认退化到根节点
        }
    }
}