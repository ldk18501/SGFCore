using System;

namespace GameFramework.Core
{
    /// <summary>
    /// 强类型黑板键，避免业务代码依赖容易拼错的字符串和运行时类型转换。
    /// </summary>
    public readonly struct BlackboardKey<T> : IEquatable<BlackboardKey<T>>
    {
        public string Name { get; }

        public BlackboardKey(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Blackboard key cannot be empty.", nameof(name));
            }

            Name = name;
        }

        public bool Equals(BlackboardKey<T> other) => string.Equals(Name, other.Name, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is BlackboardKey<T> other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Name);
        public override string ToString() => Name;

    }
}
