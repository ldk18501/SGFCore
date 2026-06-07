using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Random = UnityEngine.Random;

namespace GameFramework.Core
{
    public enum AudioGroup
    {
        Master,
        BGM,
        SFX,
        UI,
        Voice,
        Ambient
    }

    public readonly struct AudioHandle
    {
        public readonly long Id;
        public readonly AudioGroup Group;
        public readonly string Address;

        public bool IsValid => Id > 0;

        public AudioHandle(long id, AudioGroup group, string address)
        {
            Id = id;
            Group = group;
            Address = address;
        }
    }

    public readonly struct AudioVolumeChangedEvent
    {
        public readonly AudioGroup Group;
        public readonly float Volume;
        public readonly bool Muted;

        public AudioVolumeChangedEvent(AudioGroup group, float volume, bool muted)
        {
            Group = group;
            Volume = volume;
            Muted = muted;
        }
    }

    public readonly struct AudioPlaybackEvent
    {
        public readonly AudioHandle Handle;
        public readonly string State;

        public AudioPlaybackEvent(AudioHandle handle, string state)
        {
            Handle = handle;
            State = state;
        }
    }

    /// <summary>
    /// 项目级音频模块：支持分组音量、静音、暂停恢复、BGM 淡入淡出和 SFX 句柄生命周期。
    /// </summary>
    public class AudioModule : IFrameworkModule
    {
        private sealed class AudioTask : IReference
        {
            public long TaskId;
            public string Address;
            public AudioGroup Group;
            public AudioSource Source;
            public AudioClip Clip;
            public Transform FollowTarget;
            public bool IsLoaded;
            public bool IsPaused;
            public bool IsSingleton;
            public bool IsAborted;
            public float BaseVolume;

            public AudioHandle Handle => new AudioHandle(TaskId, Group, Address);

            public void Clear()
            {
                TaskId = 0;
                Address = null;
                Group = AudioGroup.SFX;
                if (Source != null)
                {
                    Source.clip = null;
                    Source.Stop();
                    Source.loop = false;
                    Source.pitch = 1f;
                }

                Clip = null;
                FollowTarget = null;
                IsLoaded = false;
                IsPaused = false;
                IsSingleton = false;
                IsAborted = false;
                BaseVolume = 1f;
            }
        }

        private struct GroupState
        {
            public float Volume;
            public bool Muted;
            public bool Paused;
        }

        private struct BgmFade
        {
            public bool Active;
            public float Duration;
            public float Elapsed;
            public float FromVolume;
            public float ToVolume;
            public bool StopWhenDone;
        }

        public int Priority => 60;

        private readonly Queue<AudioSource> _sourcePool = new Queue<AudioSource>();
        private readonly List<AudioTask> _activeTasks = new List<AudioTask>();
        private readonly Dictionary<long, AudioTask> _taskMap = new Dictionary<long, AudioTask>();
        private readonly Dictionary<string, AudioTask> _singletonMap = new Dictionary<string, AudioTask>();
        private readonly Dictionary<AudioGroup, GroupState> _groups = new Dictionary<AudioGroup, GroupState>();

        private Transform _audioRoot;
        private AudioSource _bgmSource;
        private AudioClip _bgmClip;
        private string _bgmAddress;
        private float _bgmBaseVolume = 1f;
        private bool _bgmPaused;
        private long _nextAudioId = 1;
        private BgmFade _bgmFade;

        public float BGMVolume => GetGroupVolume(AudioGroup.BGM);
        public float SFXVolume => GetGroupVolume(AudioGroup.SFX);
        public bool IsBgmPlaying => _bgmSource != null && _bgmSource.isPlaying;
        public int ActiveSfxCount => _activeTasks.Count;

        public void OnInit()
        {
            foreach (AudioGroup group in Enum.GetValues(typeof(AudioGroup)))
            {
                _groups[group] = new GroupState { Volume = 1f, Muted = false, Paused = false };
            }

            GameObject rootGO = new GameObject("[Framework_AudioRoot]");
            UnityEngine.Object.DontDestroyOnLoad(rootGO);
            _audioRoot = rootGO.transform;

            _bgmSource = rootGO.AddComponent<AudioSource>();
            _bgmSource.loop = true;
            _bgmSource.playOnAwake = false;
            _bgmSource.spatialBlend = 0f;

            Log.Module("Audio", "音频模块初始化完成。");
        }

        public void OnUpdate(float deltaTime, float unscaledDeltaTime)
        {
            UpdateBgmFade(unscaledDeltaTime);

            for (int i = _activeTasks.Count - 1; i >= 0; i--)
            {
                AudioTask task = _activeTasks[i];
                if (task.FollowTarget != null && task.Source != null)
                {
                    task.Source.transform.position = task.FollowTarget.position;
                }

                if (task.IsLoaded && !task.IsPaused && task.Source != null && !task.Source.loop && !task.Source.isPlaying)
                {
                    RemoveTaskAt(i, "Completed");
                }
            }
        }

        public void OnDestroy()
        {
            StopAll();
            if (_bgmClip != null)
            {
                GameApp.Res.ReleaseAsset(_bgmClip);
                _bgmClip = null;
            }
        }

        public async UniTask PlayBGMAsync(string address, float fadeDuration = 0f)
        {
            await PlayBGMAsync(address, fadeDuration, 1f, true);
        }

        public async UniTask PlayBGMAsync(string address, float fadeDuration, float volume, bool loop = true)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return;
            }

            if (_bgmSource != null && _bgmClip != null && _bgmAddress == address)
            {
                _bgmBaseVolume = Mathf.Clamp01(volume);
                _bgmSource.loop = loop;
                _bgmSource.volume = GetEffectiveVolume(AudioGroup.BGM, _bgmBaseVolume);
                if (!_bgmSource.isPlaying)
                {
                    _bgmSource.Play();
                }
                return;
            }

            await StopBGMAsync(fadeDuration);

            AudioClip clip = await GameApp.Res.LoadAssetAsync<AudioClip>(address);
            if (clip == null || _bgmSource == null)
            {
                Log.Warning($"[Audio] BGM 加载失败: {address}");
                return;
            }

            _bgmClip = clip;
            _bgmAddress = address;
            _bgmBaseVolume = Mathf.Clamp01(volume);
            _bgmSource.clip = clip;
            _bgmSource.loop = loop;
            _bgmSource.volume = fadeDuration > 0f ? 0f : GetEffectiveVolume(AudioGroup.BGM, _bgmBaseVolume);
            _bgmSource.Play();

            if (fadeDuration > 0f)
            {
                StartBgmFade(0f, GetEffectiveVolume(AudioGroup.BGM, _bgmBaseVolume), fadeDuration, false);
            }

            Broadcast(new AudioPlaybackEvent(new AudioHandle(0, AudioGroup.BGM, address), "BGMStarted"));
        }

        public void StopBGM()
        {
            _bgmFade.Active = false;
            if (_bgmSource != null)
            {
                _bgmSource.Stop();
                _bgmSource.clip = null;
            }

            if (_bgmClip != null)
            {
                GameApp.Res.ReleaseAsset(_bgmClip);
                _bgmClip = null;
            }

            _bgmAddress = null;
            Broadcast(new AudioPlaybackEvent(new AudioHandle(0, AudioGroup.BGM, string.Empty), "BGMStopped"));
        }

        public async UniTask StopBGMAsync(float fadeDuration = 0f)
        {
            if (_bgmSource == null || _bgmClip == null)
            {
                return;
            }

            if (fadeDuration <= 0f)
            {
                StopBGM();
                return;
            }

            StartBgmFade(_bgmSource.volume, 0f, fadeDuration, true);
            await UniTask.Delay(TimeSpan.FromSeconds(fadeDuration), ignoreTimeScale: true);
        }

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
            return PlaySFXEx(address, AudioGroup.SFX, is3D, position, loop, isSingleton, pitchRange, 1f, minDistance, maxDistance).Id;
        }

        public long PlaySFX(
            string address,
            Transform followTarget,
            bool loop = false,
            bool isSingleton = false,
            float pitchRange = 0f,
            float minDistance = 1f,
            float maxDistance = 50f)
        {
            AudioHandle handle = PlaySFXEx(
                address,
                AudioGroup.SFX,
                true,
                followTarget != null ? followTarget.position : Vector3.zero,
                loop,
                isSingleton,
                pitchRange,
                1f,
                minDistance,
                maxDistance);

            if (_taskMap.TryGetValue(handle.Id, out AudioTask task))
            {
                task.FollowTarget = followTarget;
            }

            return handle.Id;
        }

        public AudioHandle PlaySFXEx(
            string address,
            AudioGroup group = AudioGroup.SFX,
            bool is3D = false,
            Vector3 position = default,
            bool loop = false,
            bool isSingleton = false,
            float pitchRange = 0f,
            float volume = 1f,
            float minDistance = 1f,
            float maxDistance = 50f)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return default;
            }

            string singletonKey = $"{group}:{address}";
            if (isSingleton && _singletonMap.TryGetValue(singletonKey, out AudioTask existingTask))
            {
                ReplayTask(existingTask, pitchRange);
                return existingTask.Handle;
            }

            long taskId = _nextAudioId++;
            AudioTask task = GameApp.Pool.AllocateClass<AudioTask>();
            task.TaskId = taskId;
            task.Address = address;
            task.Group = group == AudioGroup.Master ? AudioGroup.SFX : group;
            task.IsSingleton = isSingleton;
            task.BaseVolume = Mathf.Clamp01(volume);
            task.Source = GetAudioSource();
            ConfigureSource(task.Source, task, is3D, position, loop, pitchRange, minDistance, maxDistance);

            _activeTasks.Add(task);
            _taskMap[task.TaskId] = task;
            if (isSingleton)
            {
                _singletonMap[singletonKey] = task;
            }

            LoadAndPlayAsync(task).Forget();
            return task.Handle;
        }

        public void StopAudio(long taskId)
        {
            StopAudio(new AudioHandle(taskId, AudioGroup.SFX, string.Empty));
        }

        public void StopAudio(AudioHandle handle)
        {
            if (!handle.IsValid || !_taskMap.TryGetValue(handle.Id, out AudioTask task))
            {
                return;
            }

            task.IsAborted = true;
            if (task.Source != null)
            {
                task.Source.Stop();
            }

            int index = _activeTasks.IndexOf(task);
            if (index >= 0)
            {
                RemoveTaskAt(index, "Stopped");
            }
        }

        public void PauseAudio(AudioHandle handle)
        {
            if (!handle.IsValid || !_taskMap.TryGetValue(handle.Id, out AudioTask task) || task.Source == null)
            {
                return;
            }

            task.IsPaused = true;
            task.Source.Pause();
            Broadcast(new AudioPlaybackEvent(task.Handle, "Paused"));
        }

        public void ResumeAudio(AudioHandle handle)
        {
            if (!handle.IsValid || !_taskMap.TryGetValue(handle.Id, out AudioTask task) || task.Source == null)
            {
                return;
            }

            task.IsPaused = false;
            task.Source.UnPause();
            Broadcast(new AudioPlaybackEvent(task.Handle, "Resumed"));
        }

        public void StopGroup(AudioGroup group)
        {
            for (int i = _activeTasks.Count - 1; i >= 0; i--)
            {
                if (_activeTasks[i].Group == group)
                {
                    _activeTasks[i].IsAborted = true;
                    RemoveTaskAt(i, "Stopped");
                }
            }
        }

        public void StopAllSFX()
        {
            for (int i = _activeTasks.Count - 1; i >= 0; i--)
            {
                _activeTasks[i].IsAborted = true;
                RemoveTaskAt(i, "Stopped");
            }
        }

        public void StopAll()
        {
            StopBGM();
            StopAllSFX();
        }

        public void PauseGroup(AudioGroup group)
        {
            SetGroupPaused(group, true);
        }

        public void ResumeGroup(AudioGroup group)
        {
            SetGroupPaused(group, false);
        }

        public void PauseAll()
        {
            foreach (AudioGroup group in Enum.GetValues(typeof(AudioGroup)))
            {
                SetGroupPaused(group, true);
            }
        }

        public void ResumeAll()
        {
            foreach (AudioGroup group in Enum.GetValues(typeof(AudioGroup)))
            {
                SetGroupPaused(group, false);
            }
        }

        public void SetMasterVolume(float volume)
        {
            SetGroupVolume(AudioGroup.Master, volume);
        }

        public void SetBGMVolume(float volume)
        {
            SetGroupVolume(AudioGroup.BGM, volume);
        }

        public void SetSFXVolume(float volume)
        {
            SetGroupVolume(AudioGroup.SFX, volume);
        }

        public void SetGroupVolume(AudioGroup group, float volume)
        {
            GroupState state = GetState(group);
            state.Volume = Mathf.Clamp01(volume);
            _groups[group] = state;
            RefreshGroupVolumes();
            Broadcast(new AudioVolumeChangedEvent(group, state.Volume, state.Muted));
        }

        public float GetGroupVolume(AudioGroup group)
        {
            return GetState(group).Volume;
        }

        public void SetMuted(AudioGroup group, bool muted)
        {
            GroupState state = GetState(group);
            state.Muted = muted;
            _groups[group] = state;
            RefreshGroupVolumes();
            Broadcast(new AudioVolumeChangedEvent(group, state.Volume, state.Muted));
        }

        public bool IsMuted(AudioGroup group)
        {
            return GetState(group).Muted;
        }

        private async UniTaskVoid LoadAndPlayAsync(AudioTask task)
        {
            AudioClip clip = await GameApp.Res.LoadAssetAsync<AudioClip>(task.Address);
            if (task.IsAborted)
            {
                if (clip != null)
                {
                    GameApp.Res.ReleaseAsset(clip);
                }

                int abortedIndex = _activeTasks.IndexOf(task);
                if (abortedIndex >= 0)
                {
                    RemoveTaskAt(abortedIndex, "Aborted");
                }
                return;
            }

            if (clip == null || task.Source == null)
            {
                int failedIndex = _activeTasks.IndexOf(task);
                if (failedIndex >= 0)
                {
                    RemoveTaskAt(failedIndex, "LoadFailed");
                }
                return;
            }

            task.Clip = clip;
            task.Source.clip = clip;
            task.IsLoaded = true;
            task.Source.volume = GetEffectiveVolume(task.Group, task.BaseVolume);
            task.Source.Play();
            Broadcast(new AudioPlaybackEvent(task.Handle, "Started"));
        }

        private void ConfigureSource(
            AudioSource source,
            AudioTask task,
            bool is3D,
            Vector3 position,
            bool loop,
            float pitchRange,
            float minDistance,
            float maxDistance)
        {
            source.transform.position = position;
            source.spatialBlend = is3D ? 1f : 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.loop = loop;
            source.volume = GetEffectiveVolume(task.Group, task.BaseVolume);
            source.pitch = 1f + Random.Range(-pitchRange, pitchRange);
        }

        private void ReplayTask(AudioTask task, float pitchRange)
        {
            if (task.Source == null)
            {
                return;
            }

            task.Source.pitch = 1f + Random.Range(-pitchRange, pitchRange);
            if (task.IsLoaded)
            {
                task.Source.time = 0f;
                task.Source.Play();
            }
        }

        private AudioSource GetAudioSource()
        {
            if (_sourcePool.Count > 0)
            {
                AudioSource source = _sourcePool.Dequeue();
                source.gameObject.SetActive(true);
                return source;
            }

            GameObject node = new GameObject("AudioNode");
            node.transform.SetParent(_audioRoot);
            return node.AddComponent<AudioSource>();
        }

        private void RemoveTaskAt(int index, string state)
        {
            AudioTask task = _activeTasks[index];
            _activeTasks.RemoveAt(index);
            _taskMap.Remove(task.TaskId);
            if (task.IsSingleton)
            {
                _singletonMap.Remove($"{task.Group}:{task.Address}");
            }

            Broadcast(new AudioPlaybackEvent(task.Handle, state));
            RecycleAudioTask(task);
        }

        private void RecycleAudioTask(AudioTask task)
        {
            if (task.Clip != null)
            {
                GameApp.Res.ReleaseAsset(task.Clip);
            }

            if (task.Source != null)
            {
                task.Source.clip = null;
                task.Source.Stop();
                task.Source.gameObject.SetActive(false);
                _sourcePool.Enqueue(task.Source);
            }

            GameApp.Pool.ReleaseClass(task);
        }

        private GroupState GetState(AudioGroup group)
        {
            if (_groups.TryGetValue(group, out GroupState state))
            {
                return state;
            }

            return new GroupState { Volume = 1f };
        }

        private float GetEffectiveVolume(AudioGroup group, float baseVolume)
        {
            GroupState master = GetState(AudioGroup.Master);
            GroupState state = GetState(group);
            if (master.Muted || state.Muted)
            {
                return 0f;
            }

            return Mathf.Clamp01(baseVolume) * Mathf.Clamp01(master.Volume) * Mathf.Clamp01(state.Volume);
        }

        private void RefreshGroupVolumes()
        {
            if (_bgmSource != null)
            {
                _bgmSource.volume = GetEffectiveVolume(AudioGroup.BGM, _bgmBaseVolume);
            }

            for (int i = 0; i < _activeTasks.Count; i++)
            {
                AudioTask task = _activeTasks[i];
                if (task.Source != null)
                {
                    task.Source.volume = GetEffectiveVolume(task.Group, task.BaseVolume);
                }
            }
        }

        private void SetGroupPaused(AudioGroup group, bool paused)
        {
            GroupState state = GetState(group);
            state.Paused = paused;
            _groups[group] = state;

            if (group == AudioGroup.Master || group == AudioGroup.BGM)
            {
                _bgmPaused = paused;
                if (_bgmSource != null)
                {
                    if (paused) _bgmSource.Pause();
                    else _bgmSource.UnPause();
                }
            }

            for (int i = 0; i < _activeTasks.Count; i++)
            {
                AudioTask task = _activeTasks[i];
                if (group != AudioGroup.Master && task.Group != group)
                {
                    continue;
                }

                task.IsPaused = paused;
                if (task.Source != null)
                {
                    if (paused) task.Source.Pause();
                    else task.Source.UnPause();
                }
            }
        }

        private void StartBgmFade(float from, float to, float duration, bool stopWhenDone)
        {
            _bgmFade = new BgmFade
            {
                Active = true,
                Duration = Mathf.Max(0.01f, duration),
                Elapsed = 0f,
                FromVolume = from,
                ToVolume = to,
                StopWhenDone = stopWhenDone
            };
        }

        private void UpdateBgmFade(float unscaledDeltaTime)
        {
            if (!_bgmFade.Active || _bgmSource == null || _bgmPaused)
            {
                return;
            }

            _bgmFade.Elapsed += unscaledDeltaTime;
            float t = Mathf.Clamp01(_bgmFade.Elapsed / _bgmFade.Duration);
            _bgmSource.volume = Mathf.Lerp(_bgmFade.FromVolume, _bgmFade.ToVolume, t);

            if (t < 1f)
            {
                return;
            }

            bool stopWhenDone = _bgmFade.StopWhenDone;
            _bgmFade.Active = false;
            if (stopWhenDone)
            {
                StopBGM();
            }
        }

        private void Broadcast<T>(T eventData) where T : struct
        {
            GameApp.Event?.Broadcast(eventData);
        }
    }
}
