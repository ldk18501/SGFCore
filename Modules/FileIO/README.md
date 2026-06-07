# FileIO 模块使用说明

FileIO 模块封装文件读写能力，默认策略是 `StandardFileSystemStrategy`。业务层通过 `GameApp.FileSystem` 访问。

## 初始化

`FileSystemModule` 由 `FrameworkEntry` 注册，优先级为 `5`，早于存档和加密模块。

## 常用 API

```csharp
string root = GameApp.FileSystem.GetPersistentDataPath();

GameApp.FileSystem.WriteText("Saves/Test.txt", "hello");
string text = GameApp.FileSystem.ReadText("Saves/Test.txt");

GameApp.FileSystem.WriteBytes("Cache/Data.bytes", bytes);
byte[] data = GameApp.FileSystem.ReadBytes("Cache/Data.bytes");

bool exists = GameApp.FileSystem.Exists("Saves/Test.txt");
GameApp.FileSystem.DeleteFile("Saves/Test.txt");
```

## 路径约定

- 相对路径会落到 Unity 的 `Application.persistentDataPath` 下。
- 存档业务建议通过 `SaveModule` 读写，不要自己拼存档目录。
- 配置、资源、语言表等打包内容不应放在 FileIO 模块处理，应该走 Addressables 或配置模块。
