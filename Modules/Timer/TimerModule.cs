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
            public bool IsPaused;

            public void Clear()
            {
                Id = 0;
                Callback = null;
                Delay = 0;
                IsUnscaled = false;
                LoopCount = 0;
                CurrentTime = 0;
                IsDone = false;
                IsPaused = false;
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
            for (int i = _tasks.Count - 1; i >= 0; i--)
            {
                var task = _tasks[i];
                if (task.IsDone || task.IsPaused) continue;

                task.CurrentTime += task.IsUnscaled ? unscaledDeltaTime : deltaTime;

                if (task.CurrentTime >= task.Delay)
                {
                    task.Callback?.Invoke();
                    if (task.IsDone || !_tasks.Contains(task))
                    {
                        continue;
                    }

                    if (task.LoopCount > 0) task.LoopCount--;

                    if (task.LoopCount == 0)
                    {
                        task.IsDone = true;
                        _tasks.RemoveAt(i);
                        _pool.ReleaseClass(task);
                    }
                    else
                    {
                        task.CurrentTime -= task.Delay;
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
            if (delay <= 0f)
            {
                Log.Warning($"[Timer] 添加定时器失败：delay 必须大于 0，当前值 {delay}。");
                return 0;
            }

            if (callback == null)
            {
                Log.Warning("[Timer] 添加定时器失败：callback 为空。");
                return 0;
            }

            if (loopCount == 0 || loopCount < -1)
            {
                Log.Warning($"[Timer] 添加定时器失败：loopCount 只能为 -1 或大于 0，当前值 {loopCount}。");
                return 0;
            }

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

        public void PauseTimer(long timerId)
        {
            var task = _tasks.Find(t => t.Id == timerId);
            if (task != null) task.IsPaused = true;
        }

        public void ResumeTimer(long timerId)
        {
            var task = _tasks.Find(t => t.Id == timerId);
            if (task != null) task.IsPaused = false;
        }

        public float GetRemainingTime(long timerId)
        {
            var task = _tasks.Find(t => t.Id == timerId);
            return task != null ? Math.Max(0f, task.Delay - task.CurrentTime) : 0f;
        }

        public void CancelAllTimers()
        {
            foreach (var task in _tasks) _pool.ReleaseClass(task);
            _tasks.Clear();
        }

        public int GetActiveTimerCount()
        {
            return _tasks.Count;
        }
    }
}
