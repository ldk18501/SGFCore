using System.Collections.Generic;
using PrimeTween;
using TMPro;
using UnityEngine;

namespace GameFramework.Core.Utility
{
    /// <summary>
    /// DOLookAt 的轴向锁定约束
    /// </summary>
    public enum LookAtConstraint
    {
        None,
        KeepY, // 锁定 Y 轴（即 LookAtXZ，常用于地面单位水平转身）
        KeepX, // 锁定 X 轴
        KeepZ // 锁定 Z 轴 (常用于 2D 游戏)
    }

    /// <summary>
    /// 路径插值类型
    /// </summary>
    public enum PathType
    {
        Linear, // 点对点直线连接
        CatmullRom // 平滑曲线连接 (DoTween 同款算法)
    }

    /// <summary>
    /// PrimeTween 终极语法糖扩展
    /// </summary>
    public static class PrimeTweenExtension
    {
        // ==========================================
        // 1. DOJump & DOLocalJump (抛物线跳跃 + 零GC池化)
        // ==========================================

        // 改为 class 以满足 PrimeTween 泛型约束
        private class JumpData
        {
            public Transform Target;
            public Vector3 StartPos;
            public Vector3 EndPos;
            public float JumpPower;
            public int Jumps;
            public bool IsLocal;

            public void Clear()
            {
                Target = null; // 清空引用，防止内存泄漏
            }
        }

        // 微型对象池，保障 DOJump 绝对零 GC！
        private static readonly Stack<JumpData> _jumpDataPool = new Stack<JumpData>();

        private static JumpData GetJumpData()
        {
            return _jumpDataPool.Count > 0 ? _jumpDataPool.Pop() : new JumpData();
        }

        /// <summary> 世界坐标下的跳跃 </summary>
        public static Tween DOJump(this Transform target, Vector3 endValue, float jumpPower, int numJumps, float duration, Ease ease = Ease.Linear)
        {
            var data = GetJumpData();
            data.Target = target;
            data.StartPos = target.position;
            data.EndPos = endValue;
            data.JumpPower = jumpPower;
            data.Jumps = numJumps;
            data.IsLocal = false;

            return StartJumpTween(data, duration, ease);
        }

        /// <summary> 局部坐标下的跳跃 (UI 极其常用) </summary>
        public static Tween DOLocalJump(this Transform target, Vector3 endValue, float jumpPower, int numJumps, float duration, Ease ease = Ease.Linear)
        {
            var data = GetJumpData();
            data.Target = target;
            data.StartPos = target.localPosition;
            data.EndPos = endValue;
            data.JumpPower = jumpPower;
            data.Jumps = numJumps;
            data.IsLocal = true;

            return StartJumpTween(data, duration, ease);
        }

        private static Tween StartJumpTween(JumpData data, float duration, Ease ease)
        {
            return Tween.Custom(data, 0f, 1f, duration, delegate(JumpData jumpData, float t)
                {
                    // 安全检查：如果目标在跳跃中途被销毁，则安全阻断
                    if (jumpData.Target == null) return;

                    Vector3 currentPos = Vector3.Lerp(jumpData.StartPos, jumpData.EndPos, t);
                    float yOffset = Mathf.Abs(Mathf.Sin(t * Mathf.PI * jumpData.Jumps)) * jumpData.JumpPower;
                    currentPos.y += yOffset;

                    if (jumpData.IsLocal)
                        jumpData.Target.localPosition = currentPos;
                    else
                        jumpData.Target.position = currentPos;
                }, ease: ease)
                // 动画结束（或被中断）时，将数据放回池子
                .OnComplete(data, d =>
                {
                    d.Clear();
                    _jumpDataPool.Push(d);
                });
        }

        // ==========================================
        // 2. DOPath & DOLocalPath (支持曲线、零GC池化、自动面向前方！)
        // ==========================================

        private class PathData
        {
            public Transform Target;
            public PathType PathType;
            public bool IsLocal;

            // 新增：朝向控制参数
            public bool OrientToPath;
            public LookAtConstraint Constraint;

            public readonly List<Vector3> Points = new List<Vector3>();
            public readonly List<float> SegmentRatios = new List<float>();

            public void Clear()
            {
                Target = null;
                Points.Clear();
                SegmentRatios.Clear();
            }
        }

        private static readonly Stack<PathData> _pathDataPool = new Stack<PathData>();

        private static PathData GetPathData()
        {
            return _pathDataPool.Count > 0 ? _pathDataPool.Pop() : new PathData();
        }

        /// <summary> 
        /// 沿给定路点移动
        /// <param name="orientToPath">是否让物体自动看向路径前进的方向</param>
        /// <param name="lookAtConstraint">转身时的轴向锁定（例如 KeepY 防止怪物爬坡时仰头）</param>
        /// </summary>
        public static Tween DOPath(this Transform target, Vector3[] waypoints, float duration, PathType pathType = PathType.Linear, Ease ease = Ease.Linear, bool orientToPath = false,
            LookAtConstraint lookAtConstraint = LookAtConstraint.None)
        {
            return StartPathTween(target, waypoints, duration, pathType, ease, isLocal: false, orientToPath, lookAtConstraint);
        }

        public static Tween DOLocalPath(this Transform target, Vector3[] waypoints, float duration, PathType pathType = PathType.Linear, Ease ease = Ease.Linear, bool orientToPath = false,
            LookAtConstraint lookAtConstraint = LookAtConstraint.None)
        {
            return StartPathTween(target, waypoints, duration, pathType, ease, isLocal: true, orientToPath, lookAtConstraint);
        }

        private static Tween StartPathTween(Transform target, Vector3[] waypoints, float duration, PathType pathType, Ease ease, bool isLocal, bool orientToPath, LookAtConstraint constraint)
        {
            if (waypoints == null || waypoints.Length == 0) return default;

            var data = GetPathData();
            data.Target = target;
            data.PathType = pathType;
            data.IsLocal = isLocal;
            data.OrientToPath = orientToPath;
            data.Constraint = constraint;

            Vector3 startPos = isLocal ? target.localPosition : target.position;
            data.Points.Add(startPos);
            for (int i = 0; i < waypoints.Length; i++)
            {
                data.Points.Add(waypoints[i]);
            }

            float totalDist = 0f;
            List<float> dists = new List<float>(data.Points.Count);
            for (int i = 0; i < data.Points.Count - 1; i++)
            {
                float d = Vector3.Distance(data.Points[i], data.Points[i + 1]);
                dists.Add(d);
                totalDist += d;
            }

            data.SegmentRatios.Add(0f);
            float accum = 0f;
            for (int i = 0; i < dists.Count; i++)
            {
                accum += dists[i];
                data.SegmentRatios.Add(totalDist == 0f ? 0f : accum / totalDist);
            }

            return Tween.Custom(data, 0f, 1f, duration, delegate(PathData pd, float t)
                {
                    if (pd.Target == null) return;

                    // 1. 获取当前时刻的位置，并移动过去
                    Vector3 currentPos = EvaluatePathPosition(pd, t);
                    if (pd.IsLocal) pd.Target.localPosition = currentPos;
                    else pd.Target.position = currentPos;

                    // 2. 如果开启了面向前方功能，计算方向！
                    if (pd.OrientToPath)
                    {
                        // 采样未来 1% 进度的地方作为视线目标点
                        float nextT = t + 0.01f;
                        Vector3 nextPos;
                        Vector3 dir;

                        if (nextT <= 1f)
                        {
                            nextPos = EvaluatePathPosition(pd, nextT);
                            dir = nextPos - currentPos;
                        }
                        else
                        {
                            // 已经走到终点了，往回退 1% 采样，取反方向作为最后时刻的朝向
                            float prevT = t - 0.01f;
                            if (prevT < 0f) prevT = 0f;
                            Vector3 prevPos = EvaluatePathPosition(pd, prevT);
                            dir = currentPos - prevPos;
                        }

                        if (dir != Vector3.zero)
                        {
                            // 应用轴向锁定
                            switch (pd.Constraint)
                            {
                                case LookAtConstraint.KeepY: dir.y = 0; break;
                                case LookAtConstraint.KeepX: dir.x = 0; break;
                                case LookAtConstraint.KeepZ: dir.z = 0; break;
                            }

                            if (dir != Vector3.zero)
                            {
                                Quaternion targetRot = Quaternion.LookRotation(dir);
                                if (pd.IsLocal) pd.Target.localRotation = targetRot;
                                else pd.Target.rotation = targetRot;
                            }
                        }
                    }
                }, ease: ease)
                .OnComplete(data, d =>
                {
                    d.Clear();
                    _pathDataPool.Push(d);
                });
        }

        /// <summary> 抽离出来的核心：根据时间 t (0~1) 计算曲线或直线上任意一点的坐标 </summary>
        private static Vector3 EvaluatePathPosition(PathData pd, float t)
        {
            t = Mathf.Clamp01(t);

            int segment = 0;
            for (int i = 0; i < pd.SegmentRatios.Count - 1; i++)
            {
                if (t <= pd.SegmentRatios[i + 1])
                {
                    segment = i;
                    break;
                }
            }

            if (t >= 1f) segment = pd.SegmentRatios.Count - 2;

            float startRatio = pd.SegmentRatios[segment];
            float endRatio = pd.SegmentRatios[segment + 1];
            float segmentT = (endRatio - startRatio) == 0f ? 1f : (t - startRatio) / (endRatio - startRatio);

            if (pd.PathType == PathType.Linear)
            {
                return Vector3.Lerp(pd.Points[segment], pd.Points[segment + 1], segmentT);
            }
            else
            {
                Vector3 p0 = GetPathPointSafe(pd.Points, segment - 1);
                Vector3 p1 = pd.Points[segment];
                Vector3 p2 = pd.Points[segment + 1];
                Vector3 p3 = GetPathPointSafe(pd.Points, segment + 2);

                return GetCatmullRomPosition(segmentT, p0, p1, p2, p3);
            }
        }

        // --- 曲线算法辅助方法 ---
        private static Vector3 GetPathPointSafe(List<Vector3> pts, int i)
        {
            if (i < 0) return pts[0] - (pts[1] - pts[0]);
            if (i >= pts.Count) return pts[pts.Count - 1] + (pts[pts.Count - 1] - pts[pts.Count - 2]);
            return pts[i];
        }

        private static Vector3 GetCatmullRomPosition(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3
            );
        }
        // ==========================================
        // 3. DOLookAt (平滑看向，带轴向锁定)
        // ==========================================

        /// <summary> 
        /// 平滑旋转面向目标点
        /// constraint 允许你锁定某个轴 (例如 KeepY 就能保证角色水平转身而不抬头)
        /// </summary>
        public static Tween DOLookAt(this Transform target, Vector3 lookAtPosition, float duration, LookAtConstraint constraint = LookAtConstraint.None, Ease ease = Ease.OutQuad)
        {
            Vector3 dir = lookAtPosition - target.position;

            // 应用轴向锁定
            switch (constraint)
            {
                case LookAtConstraint.KeepY: dir.y = 0; break; // 抹平高度差，只在 XZ 面旋转
                case LookAtConstraint.KeepX: dir.x = 0; break;
                case LookAtConstraint.KeepZ: dir.z = 0; break;
            }

            // 避免目标点和自身重合导致 LookRotation 报错
            if (dir == Vector3.zero) return default;

            Quaternion targetRot = Quaternion.LookRotation(dir);
            return Tween.Rotation(target, targetRot, duration, ease);
        }

        // ==========================================
        // 4. DOText (TMP 打字机效果)
        // ==========================================

        public static Tween DOText(this TMP_Text textComponent, string newText, float duration, Ease ease = Ease.Linear)
        {
            textComponent.text = newText;
            textComponent.maxVisibleCharacters = 0;

            int totalChars = newText.Length;

            return Tween.Custom(textComponent, 0f, totalChars, duration, (t, val) =>
            {
                if (t != null) t.maxVisibleCharacters = Mathf.FloorToInt(val);
            }, ease: ease);
        }

        // ==========================================
        // 5. DOPunchScale (震动缩放)
        // ==========================================

        public static Sequence DOPunchScale(this Transform target, Vector3 punchScale, float duration)
        {
            Vector3 originalScale = target.localScale;
            Vector3 peakScale = originalScale + punchScale;

            return Sequence.Create()
                .Chain(Tween.Scale(target, peakScale, duration * 0.3f, Ease.OutQuad))
                .Chain(Tween.Scale(target, originalScale, duration * 0.7f, Ease.OutElastic));
        }
    }
}