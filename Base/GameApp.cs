using UnityEngine;

namespace GameFramework.Core
{
    /// <summary>
    /// 框架全局静态门面 (Facade)
    /// 用于在业务逻辑中极速访问各个模块，避免冗长的 GetModule 调用
    /// </summary>
    public static class GameApp
    {
        // 我们利用 C# 的属性 (Property) 和 空合并赋值运算符 (??=) 
        // 做到既能简写，又能在第一次调用时缓存引用，省去了每次查字典的性能开销！

        private static EventModule _event;
        public static EventModule Event => _event ??= FrameworkEntry.Instance.GetModule<EventModule>();

        private static FileSystemModule _fileSystem;
        public static FileSystemModule FileSystem => _fileSystem ??= FrameworkEntry.Instance.GetModule<FileSystemModule>();

        private static SaveModule _save;
        public static SaveModule Save => _save ??= FrameworkEntry.Instance.GetModule<SaveModule>();

        private static ResourceModule _res;
        public static ResourceModule Res => _res ??= FrameworkEntry.Instance.GetModule<ResourceModule>();

        private static TimerModule _timer;
        public static TimerModule Timer => _timer ??= FrameworkEntry.Instance.GetModule<TimerModule>();

        private static PoolModule _pool;
        public static PoolModule Pool => _pool ??= FrameworkEntry.Instance.GetModule<PoolModule>();

        private static UIModule _ui;
        public static UIModule UI => _ui ??= FrameworkEntry.Instance.GetModule<UIModule>();

        private static ConfigModule _config;
        public static ConfigModule Config => _config ??= FrameworkEntry.Instance.GetModule<ConfigModule>();

        private static AudioModule _audio;
        public static AudioModule Audio => _audio ??= FrameworkEntry.Instance.GetModule<AudioModule>();

        private static FsmModule _fsm;
        public static FsmModule Fsm => _fsm ??= FrameworkEntry.Instance.GetModule<FsmModule>();

        private static BTModule _bt;
        public static BTModule BT => _bt ??= FrameworkEntry.Instance.GetModule<BTModule>();

        private static LocalizationModule _loc;
        public static LocalizationModule Loc => _loc ??= FrameworkEntry.Instance.GetModule<LocalizationModule>();

        // 甚至可以封装一些最常用的组合操作
        // 例如：极简的全局抛事件接口
        public static void Broadcast<T>(T eventData) where T : struct
        {
            Event.Broadcast(eventData);
        }
    }
}