# Config 模块使用说明

Config 模块负责加载二进制配置表，并把 bytes 分发给生成代码的 `Load(byte[])` 方法。

## 注册配置表

通常在启动流程中注册：

```csharp
GameApp.Config.RegisterConfig("LanguageConf", LanguageConf.Load);
```

## 加载单张表

```csharp
await GameApp.Config.LoadConfigAsync("Configs/LanguageConf.bytes", "LanguageConf");
```

## 批量加载

```csharp
await GameApp.Config.LoadConfigsBatchAsync(new Dictionary<string, string>
{
    { "ItemConf", "Configs/ItemConf.bytes" },
    { "LevelConf", "Configs/LevelConf.bytes" }
});
```

如果 Addressables 地址和配置名一致，可以使用简化接口：

```csharp
await GameApp.Config.LoadConfigsAsync("ItemConf", "LevelConf");
```

## 注意事项

- `RegisterConfig` 必须在加载前调用。
- 加载完成后模块会释放 `TextAsset`，避免 byte[] 常驻内存。
- 生成配置类通常保存在 `Modules/Config/Preset/Generated`。
