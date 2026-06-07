# Config 模块使用说明

Config 模块负责加载二进制配置表，并把 bytes 分发给生成代码的 `Load(byte[])` 方法。导表工具负责从 Excel 生成配置类和加密 bytes。

## Excel 表头规范

默认读取第一个 Sheet，前 4 行是表头：

| 行号 | 内容 |
| --- | --- |
| 第 1 行 | 字段描述 |
| 第 2 行 | 导出标记，默认 `A` 表示客户端导出 |
| 第 3 行 | 字段类型 |
| 第 4 行 | 字段名 |
| 第 5 行起 | 数据 |

默认主键字段名是 `id`。如果找不到 `id`，会使用第一列客户端字段作为主键。

## 支持类型

基础类型：

```text
int long float double bool string
Vector2 Vector3 Vector2Int Vector3Int
```

数组类型：

```text
int[] float[] string[] ...
```

枚举类型：

```text
enum_MyEnum
```

数组默认使用 `|` 分隔，Vector 默认也使用 `|` 分隔，可以在 `ConfigExportSettings` 中修改。

## 普通表导出

普通表是一张 Excel 对应一份代码和一份 bytes：

```text
Item.xlsx      -> ItemConfConfigGenerated.cs      -> ItemConf.bytes
LevelConf.xlsx -> LevelConfConfigGenerated.cs     -> LevelConf.bytes
```

生成的配置类会继承：

```csharp
ConfigManagerBase<TKey, TConfig>
```

并自动提供：

```csharp
ItemConf.List
ItemConf.Dict
ItemConf.Get(id)
ItemConf.TryGet(id, out var item)
ItemConf.Contains(id)
```

## 多语言表导出

多语言表结构和 key 必须一致，只有 `value` 字段允许不同。导表工具只生成一份代码，但会导出多份 bytes：

```text
LanguageConf_EN.xlsx -> LanguageConfConfigGenerated.cs -> LanguageTableConf_EN.bytes
LanguageConf_CN.xlsx ->                         -> LanguageTableConf_CN.bytes
LanguageConf_GE.xlsx ->                         -> LanguageTableConf_GE.bytes
```

运行时由 `LocalizationModule` 根据语言选择具体 bytes；`ConfigModule` 只负责通用加载和解析分发。

## 注册配置表

普通表通常在启动流程中注册：

```csharp
GameApp.Config.RegisterConfig("ItemConf", ItemConf.Load);
```

多语言表由 `LocalizationModule` 自动注册 `LanguageConf.Load`，普通业务不需要手动注册。

## 加载单张表

兼容旧用法：

```csharp
await GameApp.Config.LoadConfigAsync("ItemConf", "ItemConf");
```

推荐新用法：

```csharp
ConfigLoadResult result = await GameApp.Config.TryLoadConfigAsync("ItemConf", "ItemConf");
if (!result.Success)
{
    Log.Error(result.Error);
}
```

## 批量加载

```csharp
ConfigBatchLoadResult result = await GameApp.Config.TryLoadConfigsBatchAsync(
    new Dictionary<string, string>
    {
        { "ItemConf", "ItemConf" },
        { "LevelConf", "LevelConf" }
    });
```

如果 Addressables 地址和配置名一致，可以使用：

```csharp
await GameApp.Config.TryLoadConfigsAsync("ItemConf", "LevelConf");
```

## 导表工具

菜单：

```text
Tools/Framework/配置表一键导出
```

首次打开会创建：

```text
Assets/SGFCore/Modules/Config/ConfigExportSettings.asset
```

设置项包括：

- Excel 输入目录
- 生成代码目录
- 扩展代码目录
- bytes 输出目录
- 多语言表类名和 bytes 前缀
- 是否自动设置 Addressables
- Addressables Group

## 注意事项

- 导出前会校验字段类型、字段名、主键重复、多语言表结构和 key 是否一致。
- bytes 会通过 `ConfigBinaryCodec` 做轻量 XOR 混淆。
- 生成的 `Generated.cs` 不要手改；业务扩展写在 `Ext.cs`。
- 如果开启 Addressables 自动配置，导出的 bytes 会被设置到指定 Group，并使用配置名作为 address。
