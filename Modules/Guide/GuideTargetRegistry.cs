using System;
using System.Collections.Generic;

namespace GameFramework.Core
{
    public static class GuideTargetRegistry
    {
        private static readonly Dictionary<string, List<GuideTarget>> Targets =
            new Dictionary<string, List<GuideTarget>>(StringComparer.OrdinalIgnoreCase);

        public static event Action<string> TargetRegistered;
        public static event Action<string> TargetClicked;

        public static void Register(string key, GuideTarget target)
        {
            if (string.IsNullOrWhiteSpace(key) || target == null)
            {
                return;
            }

            string normalizedKey = NormalizeKey(key);
            if (!Targets.TryGetValue(normalizedKey, out List<GuideTarget> list))
            {
                list = new List<GuideTarget>();
                Targets[normalizedKey] = list;
            }

            if (!list.Contains(target))
            {
                list.Add(target);
                TargetRegistered?.Invoke(normalizedKey);
            }
        }

        public static void Unregister(string key, GuideTarget target)
        {
            if (string.IsNullOrWhiteSpace(key) || target == null)
            {
                return;
            }

            string normalizedKey = NormalizeKey(key);
            if (!Targets.TryGetValue(normalizedKey, out List<GuideTarget> list))
            {
                return;
            }

            list.Remove(target);
            if (list.Count == 0)
            {
                Targets.Remove(normalizedKey);
            }
        }

        public static GuideTarget Find(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            string normalizedKey = NormalizeKey(key);
            if (!Targets.TryGetValue(normalizedKey, out List<GuideTarget> list))
            {
                return null;
            }

            for (int i = list.Count - 1; i >= 0; i--)
            {
                GuideTarget target = list[i];
                if (target == null)
                {
                    list.RemoveAt(i);
                    continue;
                }

                if (target.isActiveAndEnabled)
                {
                    return target;
                }
            }

            if (list.Count == 0)
            {
                Targets.Remove(normalizedKey);
            }

            return null;
        }

        public static void GetRegisteredKeys(List<string> keys)
        {
            if (keys == null)
            {
                return;
            }

            foreach (KeyValuePair<string, List<GuideTarget>> pair in Targets)
            {
                List<GuideTarget> list = pair.Value;
                for (int i = 0; i < list.Count; i++)
                {
                    GuideTarget target = list[i];
                    if (target != null && target.isActiveAndEnabled)
                    {
                        keys.Add(pair.Key);
                        break;
                    }
                }
            }
        }

        public static void NotifyClicked(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            TargetClicked?.Invoke(NormalizeKey(key));
        }

        public static void Clear()
        {
            Targets.Clear();
        }

        private static string NormalizeKey(string key)
        {
            return key.Trim();
        }
    }
}
