# Debugger 模块使用说明

Debugger 模块负责统一日志输出。实际业务通常直接使用静态 `Log` 类，不需要手动持有 `LogModule`。

## 初始化

`LogModule` 在 `FrameworkEntry` 的模块依赖图中作为基础节点注册。依赖它的模块会在它完成初始化后启动，不再依赖魔法优先级数字。

文件日志使用 `Application.logMessageReceivedThreaded` 入队，主线程分批写入；默认单文件 8 MB、保留 10 个文件。可通过 `MaxLinesPerFrame`、`FlushIntervalSeconds`、`MaxFileBytes` 和 `RetainedFileCount` 调整策略。

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
