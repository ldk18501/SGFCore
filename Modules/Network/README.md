# Network 模块使用说明

Network 模块当前提供 `HttpModule`，基于 `UnityWebRequest` 和 `UniTask` 封装 JSON GET/POST。

## 设置 Token

```csharp
GameApp.Http.SetAuthToken(loginToken);
```

设置后会自动添加：

```text
Authorization: Bearer {token}
```

## GET 请求

```csharp
[Serializable]
public class RankResponse
{
    public int code;
    public string message;
}

RankResponse result = await GameApp.Http.GetAsync<RankResponse>(
    "https://example.com/rank");
```

## POST JSON

```csharp
[Serializable]
public class SubmitScoreRequest
{
    public int score;
}

RankResponse result = await GameApp.Http.PostJsonAsync<SubmitScoreRequest, RankResponse>(
    "https://example.com/score",
    new SubmitScoreRequest { score = 100 });
```

## 超时

```csharp
GameApp.Http.DefaultTimeout = 10;
var result = await GameApp.Http.GetAsync<RankResponse>(url, timeout: 5);
```

## 注意事项

- 当前使用 `JsonUtility`，不适合复杂 JSON、字典或顶层数组。
- 网络错误会记录日志并返回 `default`。
- 更复杂的重试、签名、错误码分发可以在 HttpModule 上继续扩展。
