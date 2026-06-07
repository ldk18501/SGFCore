# Audio 模块使用说明

Audio 模块管理 BGM、2D/3D SFX、跟随音效、单例防重、随机变调和音量。

## 播放 BGM

```csharp
await GameApp.Audio.PlayBGMAsync("Audio/BGM_Main");
GameApp.Audio.StopBGM();
```

切换 BGM 时模块会释放旧的 AudioClip。

## 播放 2D 音效

```csharp
long id = GameApp.Audio.PlaySFX(
    "Audio/SFX_Click",
    isSingleton: true,
    pitchRange: 0.05f);
```

`isSingleton` 适合按钮点击、连续升级等高频音效，避免同一音效叠太多。

## 播放 3D 音效

```csharp
GameApp.Audio.PlaySFX(
    "Audio/SFX_Explosion",
    is3D: true,
    position: transform.position,
    minDistance: 1f,
    maxDistance: 30f);
```

## 跟随目标的 3D 音效

```csharp
long engineId = GameApp.Audio.PlaySFX(
    "Audio/SFX_EngineLoop",
    followTarget: car.transform,
    loop: true);

GameApp.Audio.StopAudio(engineId);
```

## 音量和停止

```csharp
GameApp.Audio.SetBGMVolume(0.8f);
GameApp.Audio.SetSFXVolume(0.6f);

GameApp.Audio.StopAudio(id);
GameApp.Audio.StopAllSFX();
GameApp.Audio.StopAll();
```

## 注意事项

- AudioClip 通过 `ResourceModule` 加载，播放完会自动释放。
- 循环音效需要手动 `StopAudio`。
- 3D 音效的衰减参数按项目镜头距离调校。
