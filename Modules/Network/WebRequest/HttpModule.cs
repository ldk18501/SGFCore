using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace GameFramework.Core
{
    public enum HttpErrorType
    {
        None,
        Network,
        Timeout,
        Server,
        Deserialize,
        Canceled,
        Unknown
    }

    public sealed class HttpRequestOptions
    {
        public int Timeout = -1;
        public int RetryCount = -1;
        public float RetryDelay = -1f;
        public bool RetryNonIdempotent;
        public bool UseExponentialBackoff = true;
        [Range(0f, 1f)] public float RetryJitter = 0.2f;
        public readonly Dictionary<string, string> Headers = new Dictionary<string, string>();
    }

    public readonly struct HttpResult<T>
    {
        public readonly bool Success;
        public readonly long StatusCode;
        public readonly string Url;
        public readonly string RawText;
        public readonly string Error;
        public readonly HttpErrorType ErrorType;
        public readonly T Data;

        private HttpResult(bool success, long statusCode, string url, string rawText, string error, HttpErrorType errorType, T data)
        {
            Success = success;
            StatusCode = statusCode;
            Url = url;
            RawText = rawText;
            Error = error;
            ErrorType = errorType;
            Data = data;
        }

        public static HttpResult<T> Succeeded(long statusCode, string url, string rawText, T data)
        {
            return new HttpResult<T>(true, statusCode, url, rawText, null, HttpErrorType.None, data);
        }

        public static HttpResult<T> Failed(long statusCode, string url, string rawText, string error, HttpErrorType errorType)
        {
            return new HttpResult<T>(false, statusCode, url, rawText, error, errorType, default);
        }
    }

    public readonly struct HttpRequestCompletedEvent
    {
        public readonly string Url;
        public readonly string Method;
        public readonly bool Success;
        public readonly long StatusCode;
        public readonly HttpErrorType ErrorType;

        public HttpRequestCompletedEvent(string url, string method, bool success, long statusCode, HttpErrorType errorType)
        {
            Url = url;
            Method = method;
            Success = success;
            StatusCode = statusCode;
            ErrorType = errorType;
        }
    }

    /// <summary>
    /// 项目级 HTTP 模块：统一结果、错误类型、重试、取消、公共 Header 和超时策略。
    /// </summary>
    public class HttpModule : IAsyncFrameworkModule
    {
        private readonly Dictionary<string, string> _defaultHeaders = new Dictionary<string, string>();

        public int DefaultTimeout { get; set; } = 10;
        public int DefaultRetryCount { get; set; } = 0;
        public float DefaultRetryDelay { get; set; } = 0.25f;

        private string _authorizationToken = string.Empty;
        private CancellationTokenSource _lifecycleCts;

        public void OnInit()
        {
            _lifecycleCts = new CancellationTokenSource();
            Log.Module("Http", "网络请求模块初始化完成。");
        }

        public UniTask OnInitAsync(CancellationToken cancellationToken)
        {
            return UniTask.CompletedTask;
        }

        public void OnUpdate(float deltaTime, float unscaledDeltaTime)
        {
        }

        public void OnDestroy()
        {
            _lifecycleCts?.Cancel();
            _lifecycleCts?.Dispose();
            _lifecycleCts = null;
            _defaultHeaders.Clear();
            _authorizationToken = string.Empty;
        }

        public UniTask OnDestroyAsync(CancellationToken cancellationToken)
        {
            _lifecycleCts?.Cancel();
            return UniTask.CompletedTask;
        }

        public void SetAuthToken(string token)
        {
            _authorizationToken = token ?? string.Empty;
        }

        public void SetDefaultHeader(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            if (string.IsNullOrEmpty(value))
            {
                _defaultHeaders.Remove(key);
            }
            else
            {
                _defaultHeaders[key] = value;
            }
        }

        public void ClearDefaultHeaders()
        {
            _defaultHeaders.Clear();
        }

        public async UniTask<T> GetAsync<T>(string url, int timeout = -1)
        {
            HttpResult<T> result = await GetResultAsync<T>(url, new HttpRequestOptions { Timeout = timeout });
            return result.Success ? result.Data : default;
        }

        public async UniTask<HttpResult<T>> GetResultAsync<T>(
            string url,
            HttpRequestOptions options = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await SendAsync<T>(() => UnityWebRequest.Get(url), "GET", url, options, cancellationToken);
        }

        public async UniTask<TResponse> PostJsonAsync<TRequest, TResponse>(string url, TRequest postData, int timeout = -1)
        {
            HttpResult<TResponse> result = await PostJsonResultAsync<TRequest, TResponse>(
                url,
                postData,
                new HttpRequestOptions { Timeout = timeout });
            return result.Success ? result.Data : default;
        }

        public async UniTask<HttpResult<TResponse>> PostJsonResultAsync<TRequest, TResponse>(
            string url,
            TRequest postData,
            HttpRequestOptions options = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            string jsonBody = JsonUtility.ToJson(postData);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            return await SendAsync<TResponse>(() =>
            {
                UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                return request;
            }, "POST", url, options, cancellationToken);
        }

        public async UniTask<HttpResult<string>> GetTextAsync(
            string url,
            HttpRequestOptions options = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await SendAsync<string>(() => UnityWebRequest.Get(url), "GET", url, options, cancellationToken, text => text);
        }

        private async UniTask<HttpResult<T>> SendAsync<T>(
            Func<UnityWebRequest> requestFactory,
            string method,
            string url,
            HttpRequestOptions options,
            CancellationToken cancellationToken,
            Func<string, T> customParser = null)
        {
            options ??= new HttpRequestOptions();
            int retryCount = options.RetryCount >= 0 ? options.RetryCount : DefaultRetryCount;
            if (!string.Equals(method, UnityWebRequest.kHttpVerbGET, StringComparison.OrdinalIgnoreCase) &&
                !options.RetryNonIdempotent)
            {
                retryCount = 0;
            }

            float retryDelay = options.RetryDelay >= 0f ? options.RetryDelay : DefaultRetryDelay;
            HttpResult<T> lastResult = default;

            using (CancellationTokenSource linkedCts = _lifecycleCts != null
                       ? CancellationTokenSource.CreateLinkedTokenSource(
                           cancellationToken,
                           _lifecycleCts.Token)
                       : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                CancellationToken requestToken = linkedCts.Token;

                for (int attempt = 0; attempt <= retryCount; attempt++)
                {
                    using (UnityWebRequest request = requestFactory.Invoke())
                    {
                        SetupRequest(request, options);

                        try
                        {
                            await request.SendWebRequest().ToUniTask(cancellationToken: requestToken);
                            lastResult = BuildResult(request, url, customParser);
                        }
                        catch (OperationCanceledException)
                        {
                            lastResult = HttpResult<T>.Failed(request.responseCode, url, ReadText(request), "请求已取消。", HttpErrorType.Canceled);
                        }
                        catch (Exception e)
                        {
                            lastResult = HttpResult<T>.Failed(request.responseCode, url, ReadText(request), e.Message, Classify(request));
                        }
                    }

                    if (lastResult.Success || !ShouldRetry(lastResult) || attempt >= retryCount)
                    {
                        Broadcast(method, lastResult);
                        return lastResult;
                    }

                    if (retryDelay > 0f)
                    {
                        float multiplier = options.UseExponentialBackoff
                            ? Mathf.Pow(2f, attempt)
                            : 1f;
                        float jitter = Mathf.Clamp01(options.RetryJitter);
                        float jitterMultiplier = 1f + UnityEngine.Random.Range(-jitter, jitter);
                        TimeSpan delay = TimeSpan.FromSeconds(retryDelay * multiplier * jitterMultiplier);
                        try
                        {
                            await UniTask.Delay(
                                delay,
                                DelayType.Realtime,
                                cancellationToken: requestToken);
                        }
                        catch (OperationCanceledException)
                        {
                            lastResult = HttpResult<T>.Failed(
                                0,
                                url,
                                string.Empty,
                                "请求已取消。",
                                HttpErrorType.Canceled);
                            Broadcast(method, lastResult);
                            return lastResult;
                        }
                    }
                }
            }

            Broadcast(method, lastResult);
            return lastResult;
        }

        private HttpResult<T> BuildResult<T>(UnityWebRequest request, string url, Func<string, T> customParser)
        {
            string rawText = ReadText(request);
            if (request.result != UnityWebRequest.Result.Success)
            {
                return HttpResult<T>.Failed(request.responseCode, url, rawText, request.error, Classify(request));
            }

            try
            {
                T data = customParser != null ? customParser(rawText) : JsonUtility.FromJson<T>(rawText);
                return HttpResult<T>.Succeeded(request.responseCode, url, rawText, data);
            }
            catch (Exception e)
            {
                return HttpResult<T>.Failed(request.responseCode, url, rawText, e.Message, HttpErrorType.Deserialize);
            }
        }

        private void SetupRequest(UnityWebRequest request, HttpRequestOptions options)
        {
            request.timeout = options.Timeout > 0 ? options.Timeout : DefaultTimeout;

            foreach (KeyValuePair<string, string> pair in _defaultHeaders)
            {
                request.SetRequestHeader(pair.Key, pair.Value);
            }

            foreach (KeyValuePair<string, string> pair in options.Headers)
            {
                request.SetRequestHeader(pair.Key, pair.Value);
            }

            if (!string.IsNullOrEmpty(_authorizationToken))
            {
                request.SetRequestHeader("Authorization", $"Bearer {_authorizationToken}");
            }
        }

        private static string ReadText(UnityWebRequest request)
        {
            return request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
        }

        private static HttpErrorType Classify(UnityWebRequest request)
        {
            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                return request.error != null && request.error.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0
                    ? HttpErrorType.Timeout
                    : HttpErrorType.Network;
            }

            if (request.result == UnityWebRequest.Result.ProtocolError)
            {
                return HttpErrorType.Server;
            }

            return HttpErrorType.Unknown;
        }

        private static bool ShouldRetry<T>(HttpResult<T> result)
        {
            if (result.ErrorType == HttpErrorType.Network || result.ErrorType == HttpErrorType.Timeout)
            {
                return true;
            }

            // 408、429 和 5xx 通常属于暂时性故障；其余 4xx 重试只会放大服务端压力。
            return result.ErrorType == HttpErrorType.Server &&
                   (result.StatusCode == 408 || result.StatusCode == 429 || result.StatusCode >= 500);
        }

        private void Broadcast<T>(string method, HttpResult<T> result)
        {
            GameApp.Event?.Broadcast(new HttpRequestCompletedEvent(
                result.Url,
                method,
                result.Success,
                result.StatusCode,
                result.ErrorType));
        }
    }
}
