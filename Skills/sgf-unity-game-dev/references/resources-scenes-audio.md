# 资源、场景与音频

## 目录

- Addressables 地址规范
- Resource 所有权
- ResourceScope
- 场景
- 音频
- 诊断清单

## Addressables 地址规范

SGFCore 的 Resource、Scene、Config、Localization、UI、Audio 和 BehaviorTree 都依赖 Addressables。将 address 视为稳定 API：

- 在项目常量、生成配置或集中 Registry 中定义，不在业务代码散落魔法字符串。
- address 必须全项目唯一；移动资源时保持 address 稳定或同步迁移调用方。
- 区分资源路径、GUID、Label 和 address；运行时 API 接收的是 address。
- 资源类型必须与加载泛型匹配。

当前自动管线只监控 `Assets/ResAddressable`，按相对目录创建 Group，并默认用不带扩展名的文件名作为 address。这容易产生同名冲突；若项目采用该管线，必须运行重复地址检查，必要时调整 address 算法或目录命名。其他目录不会自动进入 Addressables。

## Resource 所有权

| 获取方式 | 正确释放 | 禁止 |
| --- | --- | --- |
| `LoadAssetAsync<T>` | 每次加载对应一次 `ReleaseAsset` | 只清空变量、不释放 |
| `InstantiateAsync` | `ReleaseInstance` | `Destroy(instance)` |
| UIForm 受管加载 | UIForm 销毁时自动释放 | 外部重复释放 |
| `ResourceScope` | `Dispose` | scope 释放后继续使用对象 |

普通资源：

```csharp
Sprite icon = await GameApp.Res.LoadAssetAsync<Sprite>(address, token);
if (icon == null)
{
    return;
}

try
{
    Use(icon);
}
finally
{
    GameApp.Res.ReleaseAsset(icon);
}
```

Prefab：

```csharp
GameObject instance = await GameApp.Res.InstantiateAsync(
    address,
    parent,
    instantiateInWorldSpace: false,
    cancellationToken: token);

if (instance != null)
{
    GameApp.Res.ReleaseInstance(instance);
}
```

规则：

- 同一资源加载 N 次会追踪 N 个句柄，必须释放 N 次。
- `TryReleaseAsset`/`TryReleaseInstance` 可用于需要知道所有权是否成立的清理逻辑。
- 释放未追踪实例时模块会拒绝擅自 Destroy；调用方必须查清实例来源。
- 框架初始化完成后不重复调用 `EnsureInitializedAsync`，除非进行独立诊断或兼容接入。
- `GetUsageSnapshot` 用于泄漏审计；关注 pending、asset handle、unique asset、instance 数。

## ResourceScope

一组资源同生共死时使用 scope：

```csharp
using (ResourceScope scope = GameApp.Res.CreateScope("Battle.Round"))
{
    Sprite icon = await scope.LoadAssetAsync<Sprite>(iconAddress, token);
    GameObject enemy = await scope.InstantiateAsync(enemyAddress, root, false, token);
    await RunRoundAsync(token);
}
```

Scope 先释放实例，再释放普通资源。适合 Procedure、关卡、临时玩法或非 UI 视图；不适合把对象返回给生命周期更长的 owner。若必须转移所有权，不要用自动 scope，改为由最终 owner 显式追踪和释放。

## 场景

场景生命周期由 `GameApp.Scene` 管理，游戏阶段由 Procedure 决定。

Additive 加载：

```csharp
SceneLoadResult result = await GameApp.Scene.TryLoadSceneAsync(
    "Scenes/Battle",
    new SceneLoadOptions(
        LoadSceneMode.Additive,
        activateOnLoad: false,
        setAsActiveScene: true),
    token);

if (!result.Success)
{
    Log.Error(result.Error);
    return;
}

if (result.RequiresActivation)
{
    bool activated = await GameApp.Scene.ActivateSceneAsync(
        "Scenes/Battle",
        cancellationToken: token);
    if (!activated) return;
}
```

大流程切换：

```csharp
bool ok = await GameApp.Scene.TrySwitchSceneAsync(
    "Scenes/MainMenu",
    cancellationToken: token);
```

卸载：

```csharp
bool ok = await GameApp.Scene.TryUnloadSceneAsync("Scenes/Battle", token);
```

规则：

- 场景必须加入 Addressables。
- 不绕过 `SceneModule` 直接卸载它加载的场景。
- 相同 address 和 options 的并发加载会合并；不要额外实现重复锁。
- 延迟激活时必须检查 `RequiresActivation`，并在合适时机显式激活。
- 切大流程前关闭临时 UI、停止循环音效、释放局部 scope，再卸载场景。
- Loading UI 可监听 `SceneLoadStartedEvent`、`SceneLoadCompletedEvent`、`SceneUnloadedEvent`。
- 用 `GetUsageSnapshot` 检查 pending 和已追踪场景数量。

## 音频

BGM：

```csharp
await GameApp.Audio.PlayBGMAsync("Audio/BGM_Main", fadeDuration: 0.5f);
await GameApp.Audio.StopBGMAsync(fadeDuration: 0.3f);
```

可控 SFX 优先用句柄：

```csharp
AudioHandle handle = GameApp.Audio.PlaySFXEx(
    "Audio/SFX_Engine",
    AudioGroup.SFX,
    loop: true,
    isSingleton: true,
    priority: 10);

GameApp.Audio.PauseAudio(handle);
GameApp.Audio.ResumeAudio(handle);
GameApp.Audio.StopAudio(handle);
```

一次性音效可用 `PlaySFX`。循环或跟随音效必须保留句柄/ID并在 owner 结束时停止。

分组：`Master`、`BGM`、`SFX`、`UI`、`Voice`、`Ambient`。用 `SetGroupVolume`、`SetMuted`、`PauseGroup` 管理；使用 AudioMixer 时通过 `SetMixerGroup` 绑定。音量默认写 PlayerPrefs，项目存档若另有设置系统，应统一单一数据源或关闭 `PersistVolumeSettings`。

`MaxConcurrentSfx` 默认 32；达到上限会按 priority 抢占较低优先级的非循环音效，循环音效不会自动被抢占。重要循环声必须主动清理。

可监听 `AudioVolumeChangedEvent` 和 `AudioPlaybackEvent` 做设置 UI、调试和埋点，不要用事件反向控制模块内部状态。

## 诊断清单

- 地址存在、唯一、类型正确，并已 Build Addressables Content。
- 每个加载点都有 owner、取消 token 和释放点。
- Prefab 实例没有直接 `Destroy`。
- 场景没有绕过 SceneModule 卸载。
- 循环/跟随 SFX 在 owner 关闭时停止。
- `ResourceUsageSnapshot` 在流程退出后回到预期基线。

源码入口：`Modules/Res/ResourceModule.cs`、`ResourceScope.cs`、`Modules/Scene/SceneModule.cs`、`Modules/Audio/AudioModule.cs`、`Modules/Res/Editor/AddressableAutoBuilder.cs`。
