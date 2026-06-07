# Debugger 模块使用说明

Debugger 模块负责统一日志输出。实际业务通常直接使用静态 `Log` 类，不需要手动持有 `LogModule`。

## 初始化

`LogModule` 由 `FrameworkEntry` 最先注册，优先级为 `0`，保证后续模块初始化时可以正常输出日志。

## 常用 API

```csharp
Log.Info("普通日志");
Log.Warning("警告日志");
Log.Error("错误日志");
Log.Fatal("致命错误");
Log.Module("Save", "存档模块初始化完成");
```

## 使用建议

- 模块初始化、关键流程完成时用 `Log.Module`。
- 可恢复的问题用 `Warning`，会导致功能失败的问题用 `Error`。
- `Fatal` 适合基础模块缺失、核心资源缺失等无法继续运行的问题。
