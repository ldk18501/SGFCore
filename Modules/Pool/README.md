# Pool 模块使用说明

Pool 模块包含两类池：C# 引用池和 GameObject 对象池。业务层通过 `GameApp.Pool` 访问。

## C# 引用池

适合频繁创建销毁的临时对象。对象需要实现 `IReference`。

```csharp
public class DamageInfo : IReference
{
    public int Value;

    public void Clear()
    {
        Value = 0;
    }
}

DamageInfo info = GameApp.Pool.AllocateClass<DamageInfo>();
info.Value = 10;
GameApp.Pool.ReleaseClass(info);
```

## GameObject 对象池

```csharp
GameApp.Pool.SetPoolConfig("Bullet", new PoolConfig
{
    MaxCapacity = 200,
    PrewarmCount = 20
});

GameApp.Pool.PrewarmGameObject("Bullet", bulletPrefab, 20);

GameObject bullet = GameApp.Pool.SpawnGameObject("Bullet", bulletPrefab, parent);
GameApp.Pool.RecycleGameObject("Bullet", bullet);
```

## 注意事项

- 回收对象时必须用同一个 `poolName`。
- `ReleaseClass` 会调用 `Clear()`，不要在 `Clear()` 中访问已经销毁的 Unity 对象。
- Addressables 实例如果由 `ResourceModule.InstantiateAsync` 创建，不建议混用普通 GameObject 池，避免释放路径不一致。
