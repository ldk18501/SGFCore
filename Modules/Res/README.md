# Res 模块使用说明

Res 模块基于 Addressables，负责初始化、资源加载、Prefab 实例化、释放和句柄追踪。

## 初始化

`ResourceModule` 实现了 `IAsyncFrameworkModule`。通过推荐的框架异步入口启动时，Addressables 会在框架返回 Ready 前完成初始化：

```csharp
bool ok = await FrameworkEntry.Instance.InitFrameworkModulesAsync(
    frameworkConfig,
    cancellationToken);
if (!ok)
{
    return;
}
```

`EnsureInitializedAsync` 仍保留给资源模块的独立诊断或兼容路径，正常业务不需要重复调用。

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

## 生命周期作用域和审计

```csharp
using (ResourceScope scope = GameApp.Res.CreateScope())
{
    Sprite icon = await scope.LoadAssetAsync<Sprite>("UI/IconCoin");
    GameObject view = await scope.InstantiateAsync("UI/Item", parent);
}

var snapshot = GameApp.Res.GetUsageSnapshot();
Debug.Log(snapshot.InstanceCount);
```

`ResourceScope` 会先释放实例再释放普通资源。全局 `ReleaseAll` 仅作为退出或诊断兜底，业务流程不应使用，以免释放其他模块仍在使用的资源。释放未被模块追踪的实例时，模块会拒绝直接 `Destroy`，避免掩盖句柄所有权错误。

## 注意事项

- `LoadAssetAsync` 用于 `Sprite`、`AudioClip`、`TextAsset`、`ScriptableObject` 等数据资源。
- `InstantiateAsync` 用于 Prefab。
- 模块销毁时会自动清理仍被追踪的资源，但正常业务应主动释放。
