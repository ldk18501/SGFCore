using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameFramework.Core
{
    /// <summary>
    /// 框架模块基础接口
    /// </summary>
    public interface IFrameworkModule
    {
        /// <summary>
        /// 模块优先级（决定初始化和轮询的顺序，越小越早）
        /// </summary>
        int Priority { get; }

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
}