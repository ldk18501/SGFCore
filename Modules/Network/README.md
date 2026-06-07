# Network 模块使用说明

Network 模块当前提供 `HttpModule`，基于 `UnityWebRequest` 和 `UniTask` 封装 JSON GET/POST，支持统一结果、错误类型、重试、取消、公共 Header 和超时策略。

## 公共 Header 和 Token

```csharp
GameApp.Http.SetAuthToken(loginToken);
GameApp.Http.SetDefaultHeader("X-Client-Version", Application.version);
```

## GET 请求

```csharp
HttpResult<RankResponse> result = await GameApp.Http.GetResultAsync<RankResponse>(url);
if (!result.Success)
{
    Log.Error(result.Error);
    return;
}

RankResponse data = result.Data;
```

旧接口仍然可用，但失败时只返回 `default`：

```csharp
RankResponse data = await GameApp.Http.GetAsync<RankResponse>(url);
```

## POST JSON

```csharp
HttpResult<RankResponse> result =
    await GameApp.Http.PostJsonResultAsync<SubmitScoreRequest, RankResponse>(
        url,
        new SubmitScoreRequest { score = 100 });
```

## 超时、重试、取消

```csharp
CancellationTokenSource cts = new CancellationTokenSource();

HttpRequestOptions options = new HttpRequestOptions
{
    Timeout = 5,
    RetryCount = 2,
    RetryDelay = 0.3f
};
options.Headers["X-Request-Id"] = requestId;

HttpResult<RankResponse> result = await GameApp.Http.GetResultAsync<RankResponse>(
    url,
    options,
    cts.Token);
```

## 错误类型

```csharp
HttpErrorType.None
HttpErrorType.Network
HttpErrorType.Timeout
HttpErrorType.Server
HttpErrorType.Deserialize
HttpErrorType.Canceled
HttpErrorType.Unknown
```

请求完成后会广播：

```csharp
HttpRequestCompletedEvent
```

## 注意事项

- 当前使用 `JsonUtility`，不适合复杂 JSON、字典或顶层数组。
- 如果后端返回统一 `code/message/data`，建议在业务层定义对应响应结构，不要让 HttpModule 绑定具体协议。
