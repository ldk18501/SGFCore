using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Random = UnityEngine.Random;

namespace GameFramework.Core
{
    /// <summary>
    /// 全局音效管理模块
    /// 支持 BGM、2D/3D 音效、对象池、单例防重、随机变调及 ID 句柄控制
    /// </summary>
    public class AudioModule : IFrameworkModule
    {
        public int Priority => 60; // 依赖 Resource 和 Pool 模块

        // ==========================================
        // 内部数据结构：音效任务 (实现 IReference 以便被 PoolModule 回收)
        // ==========================================
        private class AudioTask : IReference
        {
            public long TaskId;
            public string Address;
            public AudioSource Source;
            public AudioClip Clip;
            
            public bool IsLoaded;
            public bool IsPaused;
            public bool IsSingleton;
            
            // 记录该任务的状态，防止在异步加载完成前就被外部叫停
            public bool IsAborted; 
            
            public Transform FollowTarget; // 追踪目标

            public void Clear()
            {
                TaskId = 0;
                Address = null;
                if (Source != null)
                {
                    Source.clip = null;
                    Source.Stop();
                }
                Clip = null;
                IsLoaded = false;
                IsPaused = false;
                IsSingleton = false;
                IsAborted = false;
                FollowTarget = null;
            }
        }

        private Transform _audioRoot;
        private AudioSource _bgmSource;
        
        // 音效节点对象池（内部维护 GameObject）
        private readonly Queue<AudioSource> _sourcePool = new Queue<AudioSource>();
        
        // 活跃的音效任务
        private readonly List<AudioTask> _activeTasks = new List<AudioTask>();
        // 单例音效查找字典 (Address -> Task)
        private readonly Dictionary<string, AudioTask> _singletonMap = new Dictionary<string, AudioTask>();

        private long _nextAudioId = 1;

        // 全局音量控制
        public float BGMVolume { get; private set; } = 1f;
        public float SFXVolume { get; private set; } = 1f;

        public void OnInit()
        {
            // 创建统一的根节点挂载组件
            GameObject rootGO = new GameObject("[Framework_AudioRoot]");
            UnityEngine.Object.DontDestroyOnLoad(rootGO);
            _audioRoot = rootGO.transform;

            // 专属的 BGM 播放器 (BGM 通常不需要池化，全局唯一)
            _bgmSource = rootGO.AddComponent<AudioSource>();
            _bgmSource.loop = true;
            _bgmSource.playOnAwake = false;
            _bgmSource.spatialBlend = 0f; // BGM 永远是 2D 的

            Log.Module("Audio", "音效模块初始化完成。");
        }

        public void OnUpdate(float deltaTime, float unscaledDeltaTime)
        {
            // 倒序遍历，回收播放完毕的音效
            for (int i = _activeTasks.Count - 1; i >= 0; i--)
            {
                var task = _activeTasks[i];

                // 【新增】：如果绑定了追踪目标，实时更新坐标
                if (task.FollowTarget != null)
                {
                    task.Source.transform.position = task.FollowTarget.position;
                }
                // (注意：Unity 引擎重写了 == null。如果怪物被销毁了，这里会判断为 true，
                // 此时坐标不再更新，音效会留在怪物死亡的最后位置继续把死亡音效播完，非常完美！)

                if (task.IsLoaded && !task.IsPaused && !task.Source.isPlaying)
                {
                    RecycleAudioTask(task);
                    _activeTasks.RemoveAt(i);
                }
            }
        }

        public void OnDestroy() { }

        // ==========================================
        // API：背景音乐 (BGM)
        // ==========================================

        public async UniTask PlayBGMAsync(string address, float fadeDuration = 0f)
        {
            // TODO: 如果需要交叉淡入淡出（Crossfade），可以在这里扩展
            _bgmSource.Stop();
            if (_bgmSource.clip != null)
            {
                GameApp.Res.ReleaseAsset(_bgmSource.clip);
                _bgmSource.clip = null;
            }

            AudioClip clip = await GameApp.Res.LoadAssetAsync<AudioClip>(address);
            if (clip != null)
            {
                _bgmSource.clip = clip;
                _bgmSource.volume = BGMVolume;
                _bgmSource.Play();
                Log.Info($"[Audio] 播放 BGM: {address}");
            }
        }

        public void StopBGM() => _bgmSource.Stop();

        // ==========================================
        // API：音效 (SFX) 核心入口
        // ==========================================

        /// <summary>
        /// 播放 2D/3D 音效
        /// </summary>
        /// <param name="address">资源地址</param>
        /// <param name="is3D">是否为 3D 音效</param>
        /// <param name="position">3D 坐标（2D 时传 Vector3.zero 即可）</param>
        /// <param name="loop">是否循环</param>
        /// <param name="isSingleton">是否为单例防重模式（高频触发的音效如连击声建议开启）</param>
        /// <param name="pitchRange">随机变调范围（例如 0.1 表示在 0.9~1.1 之间随机）</param>
        /// <param name="minDistance">3D 声音开始衰减的最短距离</param>
        /// <param name="maxDistance">3D 声音彻底听不见的最大距离</param>
        /// <returns>返回音效的唯一 ID，可用于手动 Stop</returns>
        public long PlaySFX(
            string address, 
            bool is3D = false, 
            Vector3 position = default, 
            bool loop = false, 
            bool isSingleton = false, 
            float pitchRange = 0f,
            float minDistance = 1f,
            float maxDistance = 50f)
        {
            // 1. 单例音效拦截
            if (isSingleton && _singletonMap.TryGetValue(address, out var existingTask))
            {
                // 如果它已经加载完了，直接重置播放进度和随机 Pitch
                if (existingTask.IsLoaded && existingTask.Source != null)
                {
                    existingTask.Source.pitch = 1f + Random.Range(-pitchRange, pitchRange);
                    existingTask.Source.time = 0f;
                    existingTask.Source.Play();
                }
                // 返回原来的 ID
                return existingTask.TaskId;
            }

            // 2. 创建新任务 (利用我们之前的内存池)
            long taskId = _nextAudioId++;
            AudioTask newTask = GameApp.Pool.AllocateClass<AudioTask>();
            newTask.TaskId = taskId;
            newTask.Address = address;
            newTask.IsSingleton = isSingleton;
            newTask.IsAborted = false;

            // 3. 分配 AudioSource 节点
            newTask.Source = GetAudioSource();
            
            // 4. 配置 2D/3D 属性
            newTask.Source.transform.position = position;
            newTask.Source.spatialBlend = is3D ? 1f : 0f;
            newTask.Source.rolloffMode = AudioRolloffMode.Linear;
            newTask.Source.minDistance = minDistance;
            newTask.Source.maxDistance = maxDistance;
            
            newTask.Source.loop = loop;
            newTask.Source.volume = SFXVolume;
            newTask.Source.pitch = 1f + Random.Range(-pitchRange, pitchRange);

            _activeTasks.Add(newTask);
            if (isSingleton) _singletonMap[address] = newTask;

            // 5. 开启异步加载流程 (Fire and Forget)
            LoadAndPlayAsync(newTask).Forget();

            return taskId;
        }
        
        /// <summary>
        /// 播放会实时跟随目标移动的 3D 音效
        /// </summary>
        public long PlaySFX(
            string address, 
            Transform followTarget, 
            bool loop = false, 
            bool isSingleton = false, 
            float pitchRange = 0f,
            float minDistance = 1f,
            float maxDistance = 50f)
        {
            // 复用之前的播放逻辑，只是把坐标设为目标的初始坐标，并传入 is3D = true
            long taskId = PlaySFX(address, true, followTarget != null ? followTarget.position : Vector3.zero, loop, isSingleton, pitchRange, minDistance, maxDistance);
            
            // 找到刚才创建的任务，把追踪目标绑上去
            foreach (var task in _activeTasks)
            {
                if (task.TaskId == taskId)
                {
                    task.FollowTarget = followTarget;
                    break;
                }
            }
            return taskId;
        }

        private async UniTaskVoid LoadAndPlayAsync(AudioTask task)
        {
            AudioClip clip = await GameApp.Res.LoadAssetAsync<AudioClip>(task.Address);
            
            // 如果在加载期间被调用了 StopAudio 终止，则直接回收释放
            if (task.IsAborted)
            {
                if (clip != null) GameApp.Res.ReleaseAsset(clip);
                RecycleAudioTask(task);
                _activeTasks.Remove(task);
                return;
            }

            if (clip != null)
            {
                task.Clip = clip;
                task.Source.clip = clip;
                task.IsLoaded = true;
                task.Source.Play();
            }
            else
            {
                // 加载失败，回收处理
                RecycleAudioTask(task);
                _activeTasks.Remove(task);
            }
        }
        
        
        // ==========================================
        // 新增 API：一键清理所有音效
        // ==========================================
        
        /// <summary>
        /// 停止所有正在播放和加载中的短促音效 (SFX)
        /// </summary>
        public void StopAllSFX()
        {
            for (int i = _activeTasks.Count - 1; i >= 0; i--)
            {
                var task = _activeTasks[i];
                task.IsAborted = true; // 掐断还在异步加载路上的音效
                
                if (task.IsLoaded)
                {
                    task.Source.Stop();
                }
                
                RecycleAudioTask(task);
            }
            _activeTasks.Clear();
            Log.Info("[Audio] 已强制停止并清理所有 SFX 音效。");
        }

        /// <summary>
        /// 停止整个世界的所有声音（包括 BGM 和 SFX），通常用于退回主菜单或游戏结束
        /// </summary>
        public void StopAll()
        {
            StopBGM();
            StopAllSFX();
        }

        // ==========================================
        // API：控制与回收
        // ==========================================

        /// <summary>
        /// 根据 ID 停止指定音效
        /// </summary>
        public void StopAudio(long taskId)
        {
            for (int i = 0; i < _activeTasks.Count; i++)
            {
                if (_activeTasks[i].TaskId == taskId)
                {
                    var task = _activeTasks[i];
                    task.IsAborted = true; // 标记中止（针对还在 Loading 的）
                    
                    if (task.IsLoaded)
                    {
                        task.Source.Stop();
                        // 触发 Update 里的自动回收
                    }
                    break;
                }
            }
        }

        private AudioSource GetAudioSource()
        {
            if (_sourcePool.Count > 0)
            {
                var source = _sourcePool.Dequeue();
                source.gameObject.SetActive(true);
                return source;
            }

            // 池子空了，新建一个节点
            GameObject node = new GameObject("AudioNode");
            node.transform.SetParent(_audioRoot);
            return node.AddComponent<AudioSource>();
        }

        private void RecycleAudioTask(AudioTask task)
        {
            if (task.IsSingleton)
            {
                _singletonMap.Remove(task.Address);
            }

            // 卸载 AudioClip 释放内存
            if (task.Clip != null)
            {
                GameApp.Res.ReleaseAsset(task.Clip);
            }

            // 回收 AudioSource 节点
            if (task.Source != null)
            {
                task.Source.gameObject.SetActive(false);
                _sourcePool.Enqueue(task.Source);
            }

            // 将 C# 任务类放回内存池
            GameApp.Pool.ReleaseClass(task);
        }

        // 全局音量设置
        public void SetBGMVolume(float volume)
        {
            BGMVolume = Mathf.Clamp01(volume);
            if (_bgmSource != null) _bgmSource.volume = BGMVolume;
        }

        public void SetSFXVolume(float volume)
        {
            SFXVolume = Mathf.Clamp01(volume);
            foreach (var task in _activeTasks)
            {
                if (task.Source != null) task.Source.volume = SFXVolume;
            }
        }
    }
}