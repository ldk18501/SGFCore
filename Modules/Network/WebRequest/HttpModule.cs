using System;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace GameFramework.Core
{
    /// <summary>
    /// 全局 HTTP 网络请求模块
    /// 完全基于 UniTask 和 UnityWebRequest，零额外依赖，极其轻量
    /// </summary>
    public class HttpModule : IFrameworkModule
    {
        public int Priority => 90; 

        // 全局默认超时时间
        public int DefaultTimeout = 10; 
        
        // 可选：全局的 API 请求头 (比如鉴权 Token)
        private string _authorizationToken = string.Empty;

        public void OnInit()
        {
            Log.Module("Http", "网络请求模块初始化完成。");
        }

        public void OnUpdate(float deltaTime, float unscaledDeltaTime) { }
        public void OnDestroy() { }

        /// <summary>
        /// 设置全局 Token (登录后调用)
        /// </summary>
        public void SetAuthToken(string token)
        {
            _authorizationToken = token;
        }

        // ==========================================
        // 核心 API：GET 请求
        // ==========================================
        
        /// <summary>
        /// 发送 GET 请求并自动反序列化为对象
        /// </summary>
        public async UniTask<T> GetAsync<T>(string url, int timeout = -1)
        {
            // 使用 using 保证 UnityWebRequest 即使在异常时也能被彻底 Dispose，绝不漏内存！
            using (var request = UnityWebRequest.Get(url))
            {
                SetupRequest(request, timeout);

                try
                {
                    // 神奇的 ToUniTask()，直接把繁琐的协程变成了优雅的 await
                    await request.SendWebRequest().ToUniTask();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        string json = request.downloadHandler.text;
                        return JsonUtility.FromJson<T>(json);
                    }
                    
                    Log.Error($"[Http] GET 请求失败: {url}\nError: {request.error}");
                    return default;
                }
                catch (Exception e)
                {
                    // 捕获网络断开、DNS 解析失败等异常
                    Log.Error($"[Http] GET 发生异常: {url}\nException: {e.Message}");
                    return default;
                }
            }
        }

        // ==========================================
        // 核心 API：POST 请求 (发 JSON)
        // ==========================================

        /// <summary>
        /// 发送 POST 请求 (将对象序列化为 JSON 提交)，并自动反序列化返回值
        /// </summary>
        public async UniTask<TResponse> PostJsonAsync<TRequest, TResponse>(string url, TRequest postData, int timeout = -1)
        {
            string jsonBody = JsonUtility.ToJson(postData);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

            using (var request = new UnityWebRequest(url, "POST"))
            {
                // 设置上传的 Body
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                
                // 必须设置请求头，告诉服务器这是 JSON
                request.SetRequestHeader("Content-Type", "application/json");
                SetupRequest(request, timeout);

                try
                {
                    await request.SendWebRequest().ToUniTask();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        string responseJson = request.downloadHandler.text;
                        return JsonUtility.FromJson<TResponse>(responseJson);
                    }

                    Log.Error($"[Http] POST 请求失败: {url}\nBody: {jsonBody}\nError: {request.error}");
                    return default;
                }
                catch (Exception e)
                {
                    Log.Error($"[Http] POST 发生异常: {url}\nException: {e.Message}");
                    return default;
                }
            }
        }

        // ==========================================
        // 内部辅助
        // ==========================================

        private void SetupRequest(UnityWebRequest request, int timeout)
        {
            request.timeout = timeout > 0 ? timeout : DefaultTimeout;

            // 如果有全局 Token，自动挂载到请求头
            if (!string.IsNullOrEmpty(_authorizationToken))
            {
                request.SetRequestHeader("Authorization", $"Bearer {_authorizationToken}");
            }
        }
    }
}