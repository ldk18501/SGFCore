# 配置表与本地化

## 目录

- 配置表完整流程
- Excel 规范
- 导出设置与产物
- 运行时注册和加载
- 配置访问与扩展
- 本地化
- 常见错误

## 配置表完整流程

按以下顺序实施：

1. 在 `ConfigExportSettings.excelFolder` 放置 Excel。
2. 遵循四行表头和支持类型。
3. 运行 `Tools/Framework/配置表一键导出`。
4. 检查生成类、扩展类、bytes 和 Addressables entry。
5. 在 Preload composition 中注册每张普通表的 `Load(byte[])`。
6. 使用带结果 API 批量加载；全部成功后才能进入依赖配置的流程。
7. 业务通过生成类的静态 `List/Dict/Get/TryGet/Contains` 查询。

## Excel 规范

默认只读取第一个 Sheet：

| Excel 行 | 含义 |
| --- | --- |
| 1 | 字段描述 |
| 2 | 导出标记；默认 `A` 表示客户端字段 |
| 3 | 字段类型 |
| 4 | 字段名 |
| 5 起 | 数据 |

默认主键名为 `id`；找不到时使用第一个客户端字段。主键必须唯一。

支持类型：

```text
int long float double bool string
Vector2 Vector3 Vector2Int Vector3Int
T[]
enum_MyEnum
```

数组和 Vector 默认以 `|` 分隔，可通过设置修改。枚举单元格写可转换为目标枚举的整数值；修改生成规则前检查导出器实现。

字段名必须是合法 C# 标识符，避免与关键字冲突。不要用空字符串表达复杂空值语义；需要可空或复合对象时先扩展导表器和二进制协议。

## 导出设置与产物

默认设置资产：

```text
Assets/SGFCore/Modules/Config/ConfigExportSettings.asset
```

关键设置：

| 字段 | 默认值/用途 |
| --- | --- |
| `excelFolder` | `Assets/ExcelConfigs` |
| `generatedCodeFolder` | 自动生成代码目录 |
| `extensionCodeFolder` | 项目扩展 partial 目录 |
| `bytesFolder` | `Assets/AddressableResources/ConfigData` |
| `namespaceName` | 生成类命名空间 |
| `keyFieldName` | `id` |
| `languageClassName` | `LanguageConf` |
| `languageSourcePrefix` | `LanguageConf_` |
| `languageBytesPrefix` | `LanguageTableConf` |
| `configureAddressables` | 是否自动配置 Addressables |
| `addressablesGroupName` | 默认 `ConfigData` |

普通表：

```text
Item.xlsx -> ItemConfConfigGenerated.cs + ItemConfConfigExt.cs + ItemConf.bytes
```

生成文件包含序列化协议和索引，禁止手改。扩展文件只在不存在时创建，可安全维护业务逻辑。

bytes 会经 `ConfigBinaryCodec` XOR 混淆。这只是防止直接查看，不是安全加密；敏感信息不能依赖它保密。

## 运行时注册和加载

集中注册名称和加载器：

```csharp
public static class GameConfigs
{
    public const string Item = "ItemConf";
    public const string Level = "LevelConf";

    public static void RegisterAll()
    {
        GameApp.Config.RegisterConfig(Item, ItemConf.Load);
        GameApp.Config.RegisterConfig(Level, LevelConf.Load);
    }
}
```

名称和地址相同时：

```csharp
ConfigBatchLoadResult result = await GameApp.Config.TryLoadConfigsAsync(
    GameConfigs.Item,
    GameConfigs.Level);

if (!result.Success)
{
    foreach (ConfigLoadResult item in result.Results)
    {
        if (!item.Success)
        {
            Log.Error($"配置加载失败: {item.ConfigName}, {item.Error}");
        }
    }
    return;
}
```

名称和地址不同时：

```csharp
var map = new Dictionary<string, string>
{
    { GameConfigs.Item, "Config/Item" },
    { GameConfigs.Level, "Config/Level" }
};

ConfigBatchLoadResult result =
    await GameApp.Config.TryLoadConfigsBatchAsync(map, cancellationToken);
```

规则：

- 先注册，后加载。
- Preload 必须检查 `Success`，不能无条件切换下一 Procedure。
- 配置资源由 `ConfigModule` 内部加载并释放，业务不要再持有 `TextAsset`。
- `RegisterConfig` 默认替换同名注册；需要防止覆盖时使用 `TryRegisterConfig(..., replaceExisting: false)`。
- `IsRegistered` 与 `IsLoaded` 只用于状态检查，不替代失败处理。

## 配置访问与扩展

生成类继承 `ConfigManagerBase<TKey, T>`，提供：

```csharp
ItemConf.List
ItemConf.Dict
ItemConf.Count
ItemConf.Get(id)
ItemConf.TryGet(id, out ItemConf value)
ItemConf.Contains(id)
ItemConf.Clear()
```

在扩展 partial 中实现：

```csharp
public partial class ItemConf
{
    partial void OnPostLoad()
    {
        // 单行派生字段、轻量校验或规范化。
    }

    static partial void OnAllLoadDone()
    {
        // 整表二级索引或交叉校验。
    }
}
```

不要把运行时可变状态写回配置对象；配置是只读静态数据。每次重新加载会 `Clear` 并重建行对象，业务不要长期持有配置行引用，应保存主键并按需查询。跨表索引应在所有依赖表加载成功后构建，或确保加载顺序明确。

## 本地化

语言表文件使用相同 schema 和 key，通常只有 `value` 不同：

```text
LanguageConf_EN.xlsx -> LanguageConfConfigGenerated.cs -> LanguageTableConf_EN.bytes
LanguageConf_CN.xlsx -> 同一生成类                      -> LanguageTableConf_CN.bytes
```

`LocalizationModule` 自动注册 `LanguageConf.Load`。普通业务不要重复注册语言表。

当前源码直接引用全局命名空间中的 `LanguageConf`。如果 `ConfigExportSettings.namespaceName` 非空，普通配置表可以位于该命名空间，但语言生成类会与内建 Localization 失配。除非同时修改本地化运行时或增加 adapter，否则保持语言表生成类无命名空间。

加载前统一自定义前缀和后缀：

```csharp
GameApp.Loc.SetLanguageTablePrefix("LanguageTableConf");
GameApp.Loc.SetLanguageSuffix(SystemLanguageType.ZH, "CN");
GameApp.Loc.SetLanguageSuffix(SystemLanguageType.DE, "GE");

bool ok = await GameApp.Loc.LoadPreferredLanguageAsync(cancellationToken);
```

切换与查询：

```csharp
bool ok = await GameApp.Loc.TryChangeLanguageAsync(
    SystemLanguageType.EN,
    cancellationToken);

string title = GameApp.Loc.GetString("main_title");
string count = GameApp.Loc.Format("coin_count", 120);
```

UI 优先挂：

- `LocalizedText`：UGUI Text。
- `LocalizedTextTmp`：TMP。
- `LocalizedImage`：按 `BaseAddress_语言后缀` 加载，失败回退 `_Default`。

并发切换时新请求会取消旧请求；加载失败会保留当前语言。目标缺失时可回退 Default，并通过 `LanguageChangedEvent.IsFallback` 标识。`Format` 使用当前 `CultureInfo`；特殊语言可用 `SetCultureName` 覆盖。

## 常见错误

- 生成类找不到：检查导出目录、asmdef 边界、`namespaceName` 和 Unity 编译错误。
- `未注册解析方法`：注册名称与加载时 `configName` 不一致。
- Addressables 找不到：检查 bytes entry 的 address，不要把文件路径误当 address。
- 语言表回退：检查 suffix 映射、前缀、Default 表和多语言 key 一致性。
- 修改 Generated 后被覆盖：把代码迁移到 Ext partial。
- 导出结果异常：先修 Excel 类型、重复主键、非法字段名、语言表 schema/key，再导出；不要在生成代码中补丁式修复数据。

源码入口：`Modules/Config/ConfigModule.cs`、`ConfigManagerBase.cs`、`Editor/ConfigExporterEditor.cs`、`Editor/ConfigExportSettings.cs`、`Modules/Localization/LocalizationModule.cs`。
