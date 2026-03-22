using System.Collections.Generic;
using UnityEngine;

namespace GameFramework.Core.Utility
{
    public static class CollectionExtension
    {
        /// <summary>
        /// 经典的 Fisher-Yates 洗牌算法，将列表元素随机打乱
        /// 业务层调用：myDeckList.Shuffle();
        /// </summary>
        public static void Shuffle<T>(this IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = Random.Range(0, n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        /// <summary>
        /// 安全地从列表中随机获取一个元素
        /// </summary>
        public static T GetRandomElement<T>(this IList<T> list)
        {
            if (list == null || list.Count == 0) return default;
            return list[Random.Range(0, list.Count)];
        }
    }
}