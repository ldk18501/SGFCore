# Localization 模块使用说明

Localization 模块负责加载语言表、切换语言，并通知 UI 自动刷新。

## 切换语言

```csharp
await GameApp.Loc.ChangeLanguageAsync(SystemLanguageType.EN);
```

模块会尝试加载 `LanguageTableConf_EN`，失败时回退到 `LanguageTableConf_Default`。

## 获取文本

```csharp
string text = GameApp.Loc.GetString(10001);
```

找不到 key 时会返回 `#MISSING_10001#`，方便排查配置问题。

## UI 自动刷新

文本组件：

- `LocalizedText`：基于 UGUI `Text`
- `LocalizedTextTmp`：基于 TMP_Text

图片组件：

- `LocalizedImage`：用于语言相关图片替换

切换语言后模块会广播：

```csharp
public struct LanguageChangedEvent
{
    public SystemLanguageType NewLanguage;
}
```

## 注意事项

- 语言表 Addressables 命名需符合 `LanguageTableConf_语言名`。
- UI 上的本地化组件只负责显示，具体文本仍来自语言表。
