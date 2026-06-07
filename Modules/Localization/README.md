# Localization 模块使用说明

Localization 模块负责加载语言表、切换语言，并通知 UI 上的本地化组件自动刷新。模块默认由 `FrameworkEntry.InitFrameworkModules()` 注册，业务层通过 `GameApp.Loc` 访问。

语言表 bytes 的实际加载由 `ConfigModule` 完成，Localization 只负责根据当前语言选择地址，并维护文本查询缓存。

## 语言表命名

默认语言表 Addressables 地址格式为：

```text
LanguageTableConf_Default
LanguageTableConf_EN
LanguageTableConf_ZH
```

如果项目使用不同前缀，可以在加载前设置：

```csharp
GameApp.Loc.SetLanguageTablePrefix("MyLanguageTable");
```

如果项目习惯使用 `CN`、`GE` 这类后缀，可以直接使用枚举里的 `CN/GE`，或者手动映射：

```csharp
GameApp.Loc.SetLanguageSuffix(SystemLanguageType.ZH, "CN");
GameApp.Loc.SetLanguageSuffix(SystemLanguageType.DE, "GE");
```

## 切换语言

兼容旧用法：

```csharp
await GameApp.Loc.ChangeLanguageAsync(SystemLanguageType.EN);
```

如果需要知道是否加载成功，使用：

```csharp
bool ok = await GameApp.Loc.TryChangeLanguageAsync(SystemLanguageType.EN);
```

目标语言表缺失时，模块会自动回退到 `Default` 表，并在 `LanguageChangedEvent.IsFallback` 中标记。

## 获取文本

支持 int key 和 string key：

```csharp
string title = GameApp.Loc.GetString(10001);
string name = GameApp.Loc.GetString("item_sword_name");
```

找不到 key 时会返回明显的占位文本：

```text
#MISSING_10001#
```

需要格式化文本时：

```csharp
string text = GameApp.Loc.Format("coin_count", 120);
```

## UI 自动刷新

文本组件：

- `LocalizedText`：用于 UGUI `Text`
- `LocalizedTextTmp`：用于 `TMP_Text` / `TextMeshProUGUI`

图片组件：

- `LocalizedImage`：根据当前语言加载 `BaseAddress_语言后缀`，失败时回退 `BaseAddress_Default`

这些组件在 `OnEnable` 时刷新一次，并监听语言切换事件；`OnDisable` 时会注销事件，`LocalizedImage` 还会释放已加载 Sprite。

## 语言切换事件

```csharp
public struct LanguageChangedEvent
{
    public SystemLanguageType RequestedLanguage;
    public SystemLanguageType NewLanguage;
    public bool IsFallback;
}
```

一般 UI 不需要手写监听，直接挂本地化组件即可。只有玩法或系统逻辑需要感知语言变化时，再监听这个事件。
