namespace GameFramework.Core
{
    /// <summary>
    /// 游戏流程状态基类，例如 Launch、Preload、MainMenu、Battle。
    /// </summary>
    public abstract class ProcedureBase
    {
        protected ProcedureModule Module { get; private set; }

        protected object Owner => Module.Owner;

        internal void InternalInit(ProcedureModule module)
        {
            Module = module;
            OnInit();
        }

        protected virtual void OnInit()
        {
        }

        public virtual void OnEnter()
        {
        }

        public virtual void OnUpdate(float deltaTime, float unscaledDeltaTime)
        {
        }

        public virtual void OnLeave()
        {
        }

        public virtual void OnDestroy()
        {
        }

        protected void ChangeProcedure<TProcedure>() where TProcedure : ProcedureBase
        {
            Module.ChangeProcedure<TProcedure>();
        }

        protected void SetData(string key, object value)
        {
            Module.SetData(key, value);
        }

        protected TData GetData<TData>(string key)
        {
            return Module.GetData<TData>(key);
        }

        protected TOwner GetOwner<TOwner>() where TOwner : class
        {
            return Owner as TOwner;
        }
    }
}
