# Crypto 模块使用说明

Crypto 模块提供 AES 字符串和字节数组加密解密，主要服务于本地存档、敏感配置缓存等场景。

## 初始化密钥

默认在 `FrameworkEntry.InitFrameworkModules()` 中创建并设置密钥：

```csharp
var crypto = new CryptoModule();
RegisterModule(crypto);
crypto.SetCryptoKey("Your16Or32ByteKey", "Your16ByteIVValue");
```

正式项目应替换为自己的 Key 和 IV。

## 字符串加密

```csharp
string encrypted = GameApp.Crypto.EncryptString(json);
string plain = GameApp.Crypto.DecryptString(encrypted);
```

## 字节数组加密

```csharp
byte[] encryptedBytes = GameApp.Crypto.EncryptBytes(rawBytes);
byte[] rawBytes = GameApp.Crypto.DecryptBytes(encryptedBytes);
```

## 注意事项

- 当前模块是轻量本地保护，不等于强安全反作弊。
- 加密密钥不要长期使用示例值。
- 存档业务优先通过 `SaveModule`，它会自动调用 Crypto。
