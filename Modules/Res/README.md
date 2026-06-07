# Res 模块使用说明

Res 模块基于 Addressables，负责初始化、资源加载、Prefab 实例化、释放和句柄追踪。

## 初始化

启动流程中建议显式等待：

```csharp
bool ok = await GameApp.Res.EnsureInitializedAsync();
if (!ok)
{
    return;
}
```

## 加载资源

```csharp
Sprite icon = await GameApp.Res.LoadAssetAsync<Sprite>("UI/IconCoin");
// 使用完释放
GameApp.Res.ReleaseAsset(icon);
```

同一个资源多次加载会追踪多个句柄，因此应按加载次数释放。

## 实例化 Prefab

```csharp
GameObject panel = await GameApp.Res.InstantiateAsync("UI/MainPanel", parent);
GameApp.Res.ReleaseInstance(panel);
```

Addressables 实例不要直接 `Destroy`，应使用 `ReleaseInstance`。

## 释放和审计

```csharp
GameApp.Res.ReleaseAll();

var snapshot = GameApp.Res.GetUsageSnapshot();
Debug.Log(snapshot.InstanceCount);
```

## 注意事项

- `LoadAssetAsync` 用于 `Sprite`、`AudioClip`、`TextAsset`、`ScriptableObject` 等数据资源。
- `InstantiateAsync` 用于 Prefab。
- 模块销毁时会自动清理仍被追踪的资源，但正常业务应主动释放。
