using UnityEngine;

namespace GameFramework.Core.Utility
{
    public static class UIExtension
    {
        // ==========================================
        // 核心：3D 世界坐标 转 UI 局部坐标
        // ==========================================

        /// <summary>
        /// 将 3D 世界坐标转换为 UI 局部坐标 (完美兼容 Overlay 和 Camera 模式)
        /// </summary>
        /// <param name="parentRect">你要放置 UI 的父节点 (必须是父节点，因为 anchoredPosition 是相对于父节点的)</param>
        /// <param name="worldPos">3D 世界坐标</param>
        /// <param name="worldCam">渲染 3D 世界的主摄像机</param>
        /// <param name="uiCam">渲染 UI 的摄像机 (如果 Canvas 是 Overlay 模式，此处必须传 null)</param>
        /// <param name="localPos">输出的 UI 局部坐标</param>
        /// <returns>如果物体在摄像机前方，返回 true；如果跑到摄像机背后了，返回 false</returns>
        public static bool WorldToUIPosition(this RectTransform parentRect, Vector3 worldPos, Camera worldCam, Camera uiCam, out Vector2 localPos)
        {
            localPos = Vector2.zero;
            if (worldCam == null || parentRect == null) return false;

            // 1. 将 3D 世界坐标转换为屏幕像素坐标
            Vector3 screenPos = worldCam.WorldToScreenPoint(worldPos);

            // 2. 致命判定：如果 Z < 0，说明 3D 物体在摄像机背后！
            // 此时直接返回 false，业务层应当隐藏 UI，防止诡异的屏幕反向映射
            if (screenPos.z < 0) return false;

            // 3. 将屏幕坐标转换为目标 UI 父节点的局部坐标
            // 神奇的 RectTransformUtility：如果 uiCam 传 null，它会自动按 Overlay 模式计算
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPos, uiCam, out localPos);
            
            return true;
        }

        // ==========================================
        // 核心：UI 屏幕坐标 转 3D 世界坐标
        // ==========================================

        /// <summary>
        /// 将 UI 屏幕坐标转换到 3D 空间 (常用于拖拽 UI 往 3D 世界里丢东西，比如放置建筑)
        /// </summary>
        /// <param name="screenPos">UI 的屏幕坐标 (通常来自事件 eventData.position)</param>
        /// <param name="worldCam">渲染 3D 世界的主摄像机</param>
        /// <param name="zDepth">投射到 3D 空间中的深度 (距离摄像机多远)</param>
        public static Vector3 ScreenToWorldPosition(Vector2 screenPos, Camera worldCam, float zDepth = 10f)
        {
            if (worldCam == null) return Vector3.zero;

            // 组装带深度的屏幕坐标
            Vector3 screenPosWithDepth = new Vector3(screenPos.x, screenPos.y, zDepth);
            
            // 转换为世界坐标
            return worldCam.ScreenToWorldPoint(screenPosWithDepth);
        }
    }
}