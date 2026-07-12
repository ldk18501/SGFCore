using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameFramework.Core
{
    /// <summary>
    /// 框架模块基础接口
    /// </summary>
    public interface IFrameworkModule
    {
        /// <summary>
        /// 模块初始化
        /// </summary>
        void OnInit();

        /// <summary>
        /// 模块轮询
        /// </summary>
        /// <param name="deltaTime">逻辑流逝时间</param>
        /// <param name="unscaledDeltaTime">真实流逝时间</param>
        void OnUpdate(float deltaTime, float unscaledDeltaTime);

        /// <summary>
        /// 模块清理与销毁
        /// </summary>
        void OnDestroy();
    }

    /// <summary>
    /// 需要等待异步准备或异步释放的框架模块。
    /// 同步 OnInit/OnDestroy 仍分别作为异步阶段前后的轻量生命周期钩子。
    /// </summary>
    public interface IAsyncFrameworkModule : IFrameworkModule
    {
        UniTask OnInitAsync(CancellationToken cancellationToken);
        UniTask OnDestroyAsync(CancellationToken cancellationToken);
    }
}
