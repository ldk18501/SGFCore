# Audio 模块使用说明

Audio 模块管理 BGM、2D/3D SFX、跟随音效、单例防重、随机变调、音量分组、静音、暂停恢复、BGM 淡入淡出和音效句柄生命周期。

## BGM

```csharp
await GameApp.Audio.PlayBGMAsync("Audio/BGM_Main", fadeDuration: 0.5f);
await GameApp.Audio.StopBGMAsync(fadeDuration: 0.3f);
```

切换 BGM 时模块会释放旧的 `AudioClip`。

## 音效句柄

```csharp
AudioHandle handle = GameApp.Audio.PlaySFXEx(
    "Audio/SFX_Click",
    AudioGroup.UI,
    isSingleton: true,
    pitchRange: 0.05f);

GameApp.Audio.PauseAudio(handle);
GameApp.Audio.ResumeAudio(handle);
GameApp.Audio.StopAudio(handle);
```

旧接口仍然可用：

```csharp
long id = GameApp.Audio.PlaySFX("Audio/SFX_Click");
GameApp.Audio.StopAudio(id);
```

## 3D 和跟随音效

```csharp
GameApp.Audio.PlaySFX(
    "Audio/SFX_Explosion",
    is3D: true,
    position: transform.position,
    minDistance: 1f,
    maxDistance: 30f);

long engineId = GameApp.Audio.PlaySFX("Audio/SFX_EngineLoop", car.transform, loop: true);
```

循环音效需要手动停止。

## 分组音量、静音、暂停

```csharp
GameApp.Audio.SetMasterVolume(0.8f);
GameApp.Audio.SetGroupVolume(AudioGroup.UI, 0.7f);
GameApp.Audio.SetMuted(AudioGroup.SFX, true);

GameApp.Audio.PauseGroup(AudioGroup.SFX);
GameApp.Audio.ResumeGroup(AudioGroup.SFX);
GameApp.Audio.PauseAll();
GameApp.Audio.ResumeAll();
```

音量和静音默认写入 `PlayerPrefs`，可将 `PersistVolumeSettings` 设为 `false` 关闭。使用 AudioMixer 时可绑定分组：

```csharp
GameApp.Audio.SetMixerGroup(AudioGroup.BGM, bgmMixerGroup);
GameApp.Audio.SetMixerGroup(AudioGroup.SFX, sfxMixerGroup);
```

`MaxConcurrentSfx` 默认 32。达到上限后会按 `priority` 淘汰较低优先级的非循环音效；循环音效不会被自动抢占。

默认分组：

```text
Master, BGM, SFX, UI, Voice, Ambient
```

## 事件

```csharp
AudioVolumeChangedEvent
AudioPlaybackEvent
```

这些事件适合设置面板、调试面板或埋点监听。
