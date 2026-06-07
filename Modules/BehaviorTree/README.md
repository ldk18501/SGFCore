# BehaviorTree 模块使用说明

BehaviorTree 模块是对 Behavior Designer 的轻量封装，用于运行时给 GameObject 挂载外部行为树资源。

## 挂载行为树

```csharp
BehaviorTree tree = await GameApp.BT.AttachTreeAsync(
    owner: enemyGameObject,
    treeAddress: "AI/EnemyMelee",
    autoStart: true);
```

模块会：

- 通过 `ResourceModule` 加载 `ExternalBehaviorTree`。
- 给 owner 添加 `BehaviorTree` 组件。
- 记录资源引用，方便卸载时释放。

## 卸载行为树

```csharp
GameApp.BT.DetachTree(tree);
```

卸载时会停止行为树、释放外部行为树资源，并销毁组件。

## 全局暂停和恢复

```csharp
GameApp.BT.PauseAllAI();
GameApp.BT.ResumeAllAI();
```

## 注意事项

- 依赖 Behavior Designer 插件。
- 行为树资源需要加入 Addressables。
- 敌人销毁前建议主动 `DetachTree`，避免资源引用残留到模块销毁时才清理。
