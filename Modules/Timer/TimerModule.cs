using System;
using System.Collections.Generic;

namespace GameFramework.Core
{
    public class TimerModule : IFrameworkModule
    {
        public int Priority => 16; 

        // 内部的定时器任务类，实现 IReference 以便被内存池复用
        private class TimerTask : IReference
        {
            public long Id;
            public Action Callback;
            public float Delay;
            public bool IsUnscaled;
            public int LoopCount; // -1 表示无限循环，>0 表示特定次数
            
            public float CurrentTime;
            public bool IsDone;

            public void Clear()
            {
                Id = 0;
                Callback = null;
                Delay = 0;
                IsUnscaled = false;
                LoopCount = 0;
                CurrentTime = 0;
                IsDone = false;
            }
        }

        private PoolModule _pool;
        private readonly List<TimerTask> _tasks = new List<TimerTask>();
        private long _nextTimerId = 1;

        public void OnInit()
        {
            _pool = FrameworkEntry.Instance.GetModule<PoolModule>();
            Log.Module("Timer", "定时器模块初始化完成。");
        }

        public void OnUpdate(float deltaTime, float unscaledDeltaTime)
        {
            // 倒序遍历，方便在遍历时直接移除完成的任务，并且安全处理回调中新增定时器的情况
            for (int i = _tasks.Count - 1; i >= 0; i--)
            {
                var task = _tasks[i];
                if (task.IsDone) continue;

                task.CurrentTime += task.IsUnscaled ? unscaledDeltaTime : deltaTime;

                if (task.CurrentTime >= task.Delay)
                {
                    task.Callback?.Invoke();

                    if (task.LoopCount > 0) task.LoopCount--;

                    if (task.LoopCount == 0)
                    {
                        task.IsDone = true;
                        _tasks.RemoveAt(i);
                        _pool.ReleaseClass(task); // 回收进内存池
                    }
                    else
                    {
                        task.CurrentTime -= task.Delay; // 扣除延迟时间，准备下一次循环（保证精度）
                    }
                }
            }
        }

        public void OnDestroy()
        {
            foreach (var task in _tasks) _pool.ReleaseClass(task);
            _tasks.Clear();
        }

        /// <summary>
        /// 添加定时器
        /// </summary>
        /// <param name="delay">延迟时间(秒)</param>
        /// <param name="callback">回调函数</param>
        /// <param name="isUnscaled">是否不受 Time.timeScale 影响(真实时间)</param>
        /// <param name="loopCount">循环次数(1为单次，-1为无限)</param>
        /// <returns>定时器唯一ID，用于取消</returns>
        public long AddTimer(float delay, Action callback, bool isUnscaled = false, int loopCount = 1)
        {
            // 从对象池获取，零 GC 分配！
            var task = _pool.AllocateClass<TimerTask>();
            task.Id = _nextTimerId++;
            task.Delay = delay;
            task.Callback = callback;
            task.IsUnscaled = isUnscaled;
            task.LoopCount = loopCount;
            task.CurrentTime = 0f;
            task.IsDone = false;

            _tasks.Add(task);
            return task.Id;
        }

        /// <summary>
        /// 取消指定定时器
        /// </summary>
        public void CancelTimer(long timerId)
        {
            for (int i = 0; i < _tasks.Count; i++)
            {
                if (_tasks[i].Id == timerId)
                {
                    _tasks[i].IsDone = true;
                    var task = _tasks[i];
                    _tasks.RemoveAt(i);
                    _pool.ReleaseClass(task);
                    break;
                }
            }
        }
    }
}