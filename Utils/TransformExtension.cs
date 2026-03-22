using UnityEngine;

namespace GameFramework.Core.Utility
{
    public static class TransformExtension
    {
        /// <summary>
        /// 安全且彻底地摧毁所有子节点
        /// 业务层调用：myUIPanel.transform.DestroyAllChildren();
        /// </summary>
        public static void DestroyAllChildren(this Transform parent)
        {
            // 倒序遍历，防止索引错乱
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                // 解除父子关系，防止引擎在销毁前还在计算层级
                child.SetParent(null);
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }

        /// <summary>
        /// 递归设置当前物体及其所有子物体的 Layer
        /// （极其适用于动态实例化的 3D 兵种或 UI 特效，防止由于 Layer 错误导致不被相机渲染）
        /// </summary>
        public static void SetLayerRecursively(this GameObject obj, int newLayer)
        {
            if (obj == null) return;

            obj.layer = newLayer;
            foreach (Transform child in obj.transform)
            {
                if (child != null)
                {
                    child.gameObject.SetLayerRecursively(newLayer);
                }
            }
        }

        /// <summary>
        /// 一键归零 Transform 的本地坐标、旋转和缩放
        /// </summary>
        public static void ResetLocal(this Transform trans, float scale = 1f)
        {
            trans.localPosition = Vector3.zero;
            trans.localRotation = Quaternion.identity;
            trans.localScale = Vector3.one * scale;
        }
    }
}