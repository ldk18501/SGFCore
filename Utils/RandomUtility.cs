using System.Collections.Generic;
using UnityEngine;

namespace GameFramework.Core.Utility
{
    public static class RandomUtility
    {
        public static bool Chance(float probability)
        {
            return Random.value < Mathf.Clamp01(probability);
        }

        public static int RangeInclusive(int minInclusive, int maxInclusive)
        {
            return Random.Range(minInclusive, maxInclusive + 1);
        }

        public static T PickWeighted<T>(IList<T> items, IList<float> weights)
        {
            if (items == null || weights == null || items.Count == 0 || items.Count != weights.Count)
            {
                return default;
            }

            float total = 0f;
            for (int i = 0; i < weights.Count; i++)
            {
                total += Mathf.Max(0f, weights[i]);
            }

            if (total <= 0f)
            {
                return items[Random.Range(0, items.Count)];
            }

            float roll = Random.value * total;
            for (int i = 0; i < items.Count; i++)
            {
                roll -= Mathf.Max(0f, weights[i]);
                if (roll <= 0f)
                {
                    return items[i];
                }
            }

            return items[items.Count - 1];
        }
    }
}
