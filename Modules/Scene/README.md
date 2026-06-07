# Scene 模块使用说明

Scene 模块基于 Addressables 封装场景加载、切换、卸载和句柄追踪。业务层通过 `GameApp.Scene` 访问。

## 加载 Additive 场景

```csharp
SceneInstance scene = await GameApp.Scene.LoadSceneAsync(
    "Scenes/Battle",
    LoadSceneMode.Additive,
    setActive: true);
```

如果需要知道是否成功，使用：

```csharp
SceneLoadResult result = await GameApp.Scene.TryLoadSceneAsync("Scenes/Battle");
if (!result.Success)
{
    Log.Error(result.Error);
}
```

## 切换 Single 场景

```csharp
await GameApp.Scene.SwitchSceneAsync("Scenes/MainMenu");
```

或使用带返回值的版本：

```csharp
bool ok = await GameApp.Scene.TrySwitchSceneAsync("Scenes/MainMenu");
```

`SwitchSceneAsync` 内部使用 `LoadSceneMode.Single`，适合启动流程、主菜单、战斗场景等大流程切换。

## 卸载场景

```csharp
await GameApp.Scene.UnloadSceneAsync(scene);
await GameApp.Scene.TryUnloadSceneAsync("Scenes/Battle");
```

模块会追踪由它加载的场景句柄。Additive 场景不要绕过模块直接用 Addressables 卸载，否则模块无法同步状态。

## 场景事件

```csharp
public readonly struct SceneLoadStartedEvent
public readonly struct SceneLoadCompletedEvent
public readonly struct SceneUnloadedEvent
```

这些事件适合 Loading UI、调试面板、流程状态监听。普通业务不需要直接依赖它们。

## 状态快照

```csharp
SceneUsageSnapshot snapshot = GameApp.Scene.GetUsageSnapshot();
```

可用于调试当前场景地址、是否正在加载、已追踪场景数量。

## 注意事项

- 场景需要加入 Addressables。
- 大流程切换前通常要先关闭 UI、停止临时音效、释放临时资源。
- `SceneModule` 只负责场景生命周期，不负责决定游戏处于哪个流程；流程切换请放到 `ProcedureModule`。
