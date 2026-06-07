using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameFramework.Core
{
    public sealed class RedPointModule : IFrameworkModule
    {
        private const char PathSeparator = '.';
        private static readonly object DefaultOwner = new object();

        private readonly Dictionary<string, RedPointNode> _nodes = new Dictionary<string, RedPointNode>();
        private readonly Dictionary<string, List<Action<RedPointSnapshot>>> _listeners =
            new Dictionary<string, List<Action<RedPointSnapshot>>>();
        private readonly Dictionary<object, HashSet<string>> _ownerPaths = new Dictionary<object, HashSet<string>>();

        public int Priority => 49;

        public void OnInit()
        {
            Debug.Log("[Framework] RedPointModule initialized.");
        }

        public void OnUpdate(float deltaTime, float unscaledDeltaTime)
        {
        }

        public void OnDestroy()
        {
            _nodes.Clear();
            _listeners.Clear();
            _ownerPaths.Clear();
            Debug.Log("[Framework] RedPointModule destroyed.");
        }

        public int GetCount(string path)
        {
            RedPointNode node = GetNode(path);
            return node != null ? node.TotalCount : 0;
        }

        public int GetSelfCount(string path)
        {
            RedPointNode node = GetNode(path);
            return node != null ? node.SelfCount : 0;
        }

        public bool IsActive(string path)
        {
            return GetCount(path) > 0;
        }

        public RedPointSnapshot GetSnapshot(string path)
        {
            RedPointNode node = GetNode(path);
            return node != null ? node.CreateSnapshot() : new RedPointSnapshot(NormalizePath(path), 0, 0, 0);
        }

        public void SetCount(string path, int count, object owner = null)
        {
            string normalizedPath = NormalizePath(path);
            if (string.IsNullOrEmpty(normalizedPath))
            {
                Debug.LogWarning("[RedPoint] SetCount ignored because path is empty.");
                return;
            }

            object ownerKey = GetOwnerKey(owner);
            RedPointNode node = GetOrCreateNode(normalizedPath);
            node.SetSourceCount(ownerKey, Math.Max(0, count));
            TrackOwnerPath(ownerKey, normalizedPath);
            RecalculateUpwards(node);
        }

        public void ClearCount(string path, object owner = null)
        {
            string normalizedPath = NormalizePath(path);
            RedPointNode node = GetNode(normalizedPath);
            if (node == null)
            {
                return;
            }

            object ownerKey = GetOwnerKey(owner);
            node.RemoveSourceCount(ownerKey);
            if (!node.HasOwner(ownerKey))
            {
                UntrackOwnerPath(ownerKey, normalizedPath);
            }
            RecalculateUpwards(node);
        }

        public void SetCondition(string path, Func<int> evaluator, object owner = null)
        {
            string normalizedPath = NormalizePath(path);
            if (string.IsNullOrEmpty(normalizedPath))
            {
                Debug.LogWarning("[RedPoint] SetCondition ignored because path is empty.");
                return;
            }

            if (evaluator == null)
            {
                ClearCondition(normalizedPath, owner);
                return;
            }

            object ownerKey = GetOwnerKey(owner);
            RedPointNode node = GetOrCreateNode(normalizedPath);
            node.SetEvaluator(ownerKey, evaluator);
            TrackOwnerPath(ownerKey, normalizedPath);
            Evaluate(normalizedPath, owner);
        }

        public void ClearCondition(string path, object owner = null)
        {
            string normalizedPath = NormalizePath(path);
            RedPointNode node = GetNode(normalizedPath);
            if (node == null)
            {
                return;
            }

            object ownerKey = GetOwnerKey(owner);
            node.RemoveEvaluator(ownerKey);
            if (!node.HasOwner(ownerKey))
            {
                UntrackOwnerPath(ownerKey, normalizedPath);
            }
            RecalculateUpwards(node);
        }

        public void Evaluate(string path)
        {
            string normalizedPath = NormalizePath(path);
            RedPointNode node = GetNode(normalizedPath);
            if (node == null)
            {
                return;
            }

            node.EvaluateAll();
            RecalculateUpwards(node);
        }

        public void Evaluate(string path, object owner)
        {
            string normalizedPath = NormalizePath(path);
            RedPointNode node = GetNode(normalizedPath);
            if (node == null)
            {
                return;
            }

            node.Evaluate(GetOwnerKey(owner));
            RecalculateUpwards(node);
        }

        public void EvaluateOwner(object owner)
        {
            object ownerKey = GetOwnerKey(owner);
            if (!_ownerPaths.TryGetValue(ownerKey, out HashSet<string> paths))
            {
                return;
            }

            foreach (string path in paths)
            {
                Evaluate(path, owner);
            }
        }

        public void ClearOwner(object owner)
        {
            object ownerKey = GetOwnerKey(owner);
            if (!_ownerPaths.TryGetValue(ownerKey, out HashSet<string> paths))
            {
                return;
            }

            string[] copiedPaths = new string[paths.Count];
            paths.CopyTo(copiedPaths);
            _ownerPaths.Remove(ownerKey);

            for (int i = 0; i < copiedPaths.Length; i++)
            {
                RedPointNode node = GetNode(copiedPaths[i]);
                if (node == null)
                {
                    continue;
                }

                node.RemoveOwner(ownerKey);
                RecalculateUpwards(node);
            }
        }

        public void AddListener(string path, Action<RedPointSnapshot> listener, bool notifyImmediately = true)
        {
            if (listener == null)
            {
                return;
            }

            string normalizedPath = NormalizePath(path);
            if (string.IsNullOrEmpty(normalizedPath))
            {
                Debug.LogWarning("[RedPoint] AddListener ignored because path is empty.");
                return;
            }

            if (!_listeners.TryGetValue(normalizedPath, out List<Action<RedPointSnapshot>> listeners))
            {
                listeners = new List<Action<RedPointSnapshot>>();
                _listeners[normalizedPath] = listeners;
            }

            if (!listeners.Contains(listener))
            {
                listeners.Add(listener);
            }

            if (notifyImmediately)
            {
                listener.Invoke(GetSnapshot(normalizedPath));
            }
        }

        public void RemoveListener(string path, Action<RedPointSnapshot> listener)
        {
            if (listener == null)
            {
                return;
            }

            string normalizedPath = NormalizePath(path);
            if (!_listeners.TryGetValue(normalizedPath, out List<Action<RedPointSnapshot>> listeners))
            {
                return;
            }

            listeners.Remove(listener);
            if (listeners.Count == 0)
            {
                _listeners.Remove(normalizedPath);
            }
        }

        public string[] GetChildPaths(string path)
        {
            RedPointNode node = GetNode(path);
            if (node == null || node.Children.Count == 0)
            {
                return new string[0];
            }

            string[] result = new string[node.Children.Count];
            for (int i = 0; i < node.Children.Count; i++)
            {
                result[i] = node.Children[i].Path;
            }

            return result;
        }

        private void RecalculateUpwards(RedPointNode startNode)
        {
            RedPointNode current = startNode;
            while (current != null)
            {
                int oldCount = current.TotalCount;
                int oldSelfCount = current.SelfCount;
                int oldChildCount = current.ChildCount;

                current.Recalculate();
                if (oldCount != current.TotalCount ||
                    oldSelfCount != current.SelfCount ||
                    oldChildCount != current.ChildCount)
                {
                    NotifyChanged(current);
                }

                current = current.Parent;
            }
        }

        private void NotifyChanged(RedPointNode node)
        {
            RedPointSnapshot snapshot = node.CreateSnapshot();

            if (_listeners.TryGetValue(node.Path, out List<Action<RedPointSnapshot>> listeners))
            {
                Action<RedPointSnapshot>[] copiedListeners = listeners.ToArray();
                for (int i = 0; i < copiedListeners.Length; i++)
                {
                    copiedListeners[i].Invoke(snapshot);
                }
            }

            EventModule eventModule = GameApp.Event;
            if (eventModule != null)
            {
                eventModule.Broadcast(new RedPointChangedEvent(snapshot));
            }
        }

        private RedPointNode GetNode(string path)
        {
            string normalizedPath = NormalizePath(path);
            if (string.IsNullOrEmpty(normalizedPath))
            {
                return null;
            }

            _nodes.TryGetValue(normalizedPath, out RedPointNode node);
            return node;
        }

        private RedPointNode GetOrCreateNode(string path)
        {
            string normalizedPath = NormalizePath(path);
            if (_nodes.TryGetValue(normalizedPath, out RedPointNode existingNode))
            {
                return existingNode;
            }

            RedPointNode parent = null;
            int separatorIndex = normalizedPath.LastIndexOf(PathSeparator);
            if (separatorIndex > 0)
            {
                string parentPath = normalizedPath.Substring(0, separatorIndex);
                parent = GetOrCreateNode(parentPath);
            }

            string name = separatorIndex >= 0
                ? normalizedPath.Substring(separatorIndex + 1)
                : normalizedPath;
            RedPointNode node = new RedPointNode(name, normalizedPath, parent);
            _nodes.Add(normalizedPath, node);

            if (parent != null)
            {
                parent.Children.Add(node);
            }

            return node;
        }

        private void TrackOwnerPath(object ownerKey, string path)
        {
            if (!_ownerPaths.TryGetValue(ownerKey, out HashSet<string> paths))
            {
                paths = new HashSet<string>();
                _ownerPaths[ownerKey] = paths;
            }

            paths.Add(path);
        }

        private void UntrackOwnerPath(object ownerKey, string path)
        {
            if (!_ownerPaths.TryGetValue(ownerKey, out HashSet<string> paths))
            {
                return;
            }

            paths.Remove(path);
            if (paths.Count == 0)
            {
                _ownerPaths.Remove(ownerKey);
            }
        }

        private static object GetOwnerKey(object owner)
        {
            return owner ?? DefaultOwner;
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            string[] parts = path.Split(PathSeparator);
            List<string> normalizedParts = new List<string>(parts.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                if (!string.IsNullOrEmpty(part))
                {
                    normalizedParts.Add(part);
                }
            }

            return string.Join(PathSeparator.ToString(), normalizedParts.ToArray());
        }

        private sealed class RedPointNode
        {
            private readonly Dictionary<object, int> _sourceCounts = new Dictionary<object, int>();
            private readonly Dictionary<object, int> _conditionCounts = new Dictionary<object, int>();
            private readonly Dictionary<object, Func<int>> _evaluators = new Dictionary<object, Func<int>>();

            public readonly string Name;
            public readonly string Path;
            public readonly RedPointNode Parent;
            public readonly List<RedPointNode> Children = new List<RedPointNode>();

            public int SelfCount { get; private set; }
            public int ChildCount { get; private set; }
            public int TotalCount { get; private set; }

            public RedPointNode(string name, string path, RedPointNode parent)
            {
                Name = name;
                Path = path;
                Parent = parent;
            }

            public void SetSourceCount(object owner, int count)
            {
                if (count <= 0)
                {
                    _sourceCounts.Remove(owner);
                }
                else
                {
                    _sourceCounts[owner] = count;
                }
            }

            public void RemoveSourceCount(object owner)
            {
                _sourceCounts.Remove(owner);
            }

            public void SetEvaluator(object owner, Func<int> evaluator)
            {
                _evaluators[owner] = evaluator;
            }

            public void RemoveEvaluator(object owner)
            {
                _evaluators.Remove(owner);
                _conditionCounts.Remove(owner);
            }

            public void RemoveOwner(object owner)
            {
                _sourceCounts.Remove(owner);
                _conditionCounts.Remove(owner);
                _evaluators.Remove(owner);
            }

            public bool HasOwner(object owner)
            {
                return _sourceCounts.ContainsKey(owner) ||
                       _conditionCounts.ContainsKey(owner) ||
                       _evaluators.ContainsKey(owner);
            }

            public void EvaluateAll()
            {
                foreach (KeyValuePair<object, Func<int>> kvp in _evaluators)
                {
                    Evaluate(kvp.Key);
                }
            }

            public void Evaluate(object owner)
            {
                if (!_evaluators.TryGetValue(owner, out Func<int> evaluator))
                {
                    return;
                }

                int count = 0;
                try
                {
                    count = Math.Max(0, evaluator.Invoke());
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }

                if (count <= 0)
                {
                    _conditionCounts.Remove(owner);
                }
                else
                {
                    _conditionCounts[owner] = count;
                }
            }

            public void Recalculate()
            {
                int selfCount = 0;
                foreach (int count in _sourceCounts.Values)
                {
                    selfCount += Math.Max(0, count);
                }

                foreach (int count in _conditionCounts.Values)
                {
                    selfCount += Math.Max(0, count);
                }

                int childCount = 0;
                for (int i = 0; i < Children.Count; i++)
                {
                    childCount += Math.Max(0, Children[i].TotalCount);
                }

                SelfCount = selfCount;
                ChildCount = childCount;
                TotalCount = selfCount + childCount;
            }

            public RedPointSnapshot CreateSnapshot()
            {
                return new RedPointSnapshot(Path, TotalCount, SelfCount, ChildCount);
            }
        }
    }
}
