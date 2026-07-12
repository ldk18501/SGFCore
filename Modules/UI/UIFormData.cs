using System;

namespace GameFramework.Core.UI
{
    /// <summary>
    /// 强类型 UI 打开参数的标记接口。
    /// </summary>
    public interface IUIFormData
    {
    }

    /// <summary>
    /// 新界面优先继承此类型，旧 UIFormBase/params object[] 仍保持兼容。
    /// </summary>
    public abstract class UIFormBase<TData> : UIFormBase where TData : IUIFormData
    {
        public TData Data { get; private set; }

        public sealed override void OnOpen(params object[] args)
        {
            if (args == null || args.Length != 1 || !(args[0] is TData data))
            {
                throw new ArgumentException(
                    $"{GetType().Name} 需要且只接受一个 {typeof(TData).Name} 参数。");
            }

            Data = data;
            OnOpen(data);
        }

        public override void OnClose()
        {
            base.OnClose();
            Data = default;
        }

        protected abstract void OnOpen(TData data);
    }
}
