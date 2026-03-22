using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

namespace GameFramework.Core.BT
{
    /// <summary>
    /// 框架定制的行为树 Action 基类
    /// 封装了快捷访问接口与黑板安全读写
    /// </summary>
    public abstract class GameAction : Action
    {
        // 快捷获取宿主的常用组件（利用 BD 自带的 GetComponent 缓存优化）
        protected Transform Trans => transform;
        protected GameObject GO => gameObject;

        // ==========================================
        // 黑板数据安全操作扩展
        // ==========================================

        /// <summary>
        /// 安全读取黑板共享变量
        /// </summary>
        protected T GetSharedVariable<T>(string varName) where T : SharedVariable
        {
            var variable = Owner.GetVariable(varName) as T;
            if (variable == null)
            {
                Log.Warning($"[BD] 节点 {FriendlyName} 尝试获取不存在或类型错误的变量: {varName}");
            }
            return variable;
        }

        /// <summary>
        /// 安全设置黑板共享变量
        /// </summary>
        protected void SetSharedVariable<T>(string varName, object value) where T : SharedVariable
        {
            var variable = Owner.GetVariable(varName) as T;
            if (variable != null)
            {
                variable.SetValue(value);
            }
            else
            {
                Log.Error($"[BD] 无法设置变量 {varName}，变量不存在或不是 {typeof(T).Name} 类型");
            }
        }
    }
}