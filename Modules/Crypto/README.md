# Crypto 模块使用说明

Crypto 模块提供 AES 字符串和字节数组加密解密，主要服务于本地存档、敏感配置缓存等场景。

## 初始化密钥

推荐创建 `FrameworkConfig` 资产，启用启动时配置并填写项目自己的 Key/IV，然后传给框架入口：

```csharp
bool ready = await FrameworkEntry.Instance.InitFrameworkModulesAsync(
    frameworkConfig,
    cancellationToken);
```

也可以在框架初始化后显式调用 `GameApp.Crypto.SetCryptoKey(...)`。正式项目应使用自己的 Key 和 IV，不要把示例值提交到公共仓库。

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
- 存档业务优先通过 `SaveModule`，它会写入带 `SGF2` 前缀的随机 IV + HMAC 认证格式，并兼容读取旧的固定 IV Base64 格式。
