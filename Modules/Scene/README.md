# Scene 模块使用说明

Scene 模块基于 Addressables 封装场景加载、切换和卸载。

## 加载 Additive 场景

```csharp
SceneInstance scene = await GameApp.Scene.LoadSceneAsync(
    "Scenes/Battle",
    LoadSceneMode.Additive,
    setActive: true);
```

## 切换单场景

```csharp
await GameApp.Scene.SwitchSceneAsync("Scenes/MainMenu");
```

`SwitchSceneAsync` 内部使用 `LoadSceneMode.Single`，适合主流程场景切换。

## 卸载场景

```csharp
await GameApp.Scene.UnloadSceneAsync(scene);

// 卸载当前由模块记录的 Single 场景
await GameApp.Scene.UnloadSceneAsync();
```

## 当前场景名

```csharp
string current = GameApp.Scene.CurrentSceneName;
```

## 注意事项

- 场景需要加入 Addressables。
- Additive 场景如果要卸载，建议保存返回的 `SceneInstance`。
- 大场景切换前通常要先关闭 UI、停止音效、释放临时资源。
