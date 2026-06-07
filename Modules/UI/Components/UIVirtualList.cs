using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GameFramework.Core.UI
{
    /// <summary>
    /// 固定尺寸 Item 的虚拟滚动列表，适合背包、任务、商店、排行等大量 UI Item。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ScrollRect))]
    public class UIVirtualList : MonoBehaviour
    {
        public enum LayoutMode
        {
            Vertical,
            Horizontal,
            VerticalGrid
        }

        [Serializable]
        public class ItemEvent : UnityEvent<int, GameObject> { }

        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private RectTransform _content;
        [SerializeField] private RectTransform _viewport;
        [SerializeField] private RectTransform _itemTemplate;
        [SerializeField] private LayoutMode _layoutMode = LayoutMode.Vertical;
        [SerializeField] private Vector2 _itemSize = new Vector2(100f, 100f);
        [SerializeField] private Vector2 _spacing = new Vector2(0f, 4f);
        [SerializeField] private RectOffset _padding = new RectOffset();
        [SerializeField] private int _constraintCount = 1;
        [SerializeField] private int _extraBuffer = 2;
        [SerializeField] private bool _hideTemplateOnAwake = true;
        [SerializeField] private ItemEvent _onRefreshItem = new ItemEvent();

        private readonly Dictionary<int, GameObject> _activeItems = new Dictionary<int, GameObject>();
        private readonly Stack<GameObject> _pooledItems = new Stack<GameObject>();

        private Action<int, GameObject> _itemRenderer;
        private int _dataCount;
        private bool _initialized;

        public int DataCount => _dataCount;
        public ItemEvent OnRefreshItem => _onRefreshItem;

        private void Reset()
        {
            _scrollRect = GetComponent<ScrollRect>();
            _content = _scrollRect != null ? _scrollRect.content : null;
            _viewport = _scrollRect != null ? _scrollRect.viewport : null;
            _itemTemplate = _content != null && _content.childCount > 0 ? _content.GetChild(0) as RectTransform : null;
        }

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            Initialize();
            if (_scrollRect != null)
            {
                _scrollRect.onValueChanged.AddListener(OnScrollValueChanged);
            }

            RefreshVisibleItems();
        }

        private void OnDisable()
        {
            if (_scrollRect != null)
            {
                _scrollRect.onValueChanged.RemoveListener(OnScrollValueChanged);
            }
        }

        private void OnDestroy()
        {
            foreach (var item in _activeItems.Values)
            {
                if (item != null)
                {
                    Destroy(item);
                }
            }
            _activeItems.Clear();

            while (_pooledItems.Count > 0)
            {
                GameObject item = _pooledItems.Pop();
                if (item != null)
                {
                    Destroy(item);
                }
            }
        }

        public void SetItemRenderer(Action<int, GameObject> renderer)
        {
            _itemRenderer = renderer;
        }

        public void SetDataCount(int count, bool resetPosition = false)
        {
            Initialize();
            _dataCount = Mathf.Max(0, count);
            UpdateContentSize();

            if (resetPosition && _content != null)
            {
                _content.anchoredPosition = Vector2.zero;
            }

            RefreshVisibleItems();
        }

        public void Refresh()
        {
            UpdateContentSize();
            RefreshVisibleItems(true);
        }

        public void ScrollToIndex(int index)
        {
            if (_content == null || _dataCount <= 0)
            {
                return;
            }

            index = Mathf.Clamp(index, 0, _dataCount - 1);
            int row = GetRow(index);
            int column = GetColumn(index);
            Vector2 position = _content.anchoredPosition;

            if (_layoutMode == LayoutMode.Horizontal)
            {
                position.x = -(_padding.left + column * GetStrideX());
            }
            else
            {
                position.y = _padding.top + row * GetStrideY();
            }

            _content.anchoredPosition = position;
            RefreshVisibleItems();
        }

        private void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            if (_scrollRect == null)
            {
                _scrollRect = GetComponent<ScrollRect>();
            }

            if (_content == null && _scrollRect != null)
            {
                _content = _scrollRect.content;
            }

            if (_viewport == null && _scrollRect != null)
            {
                _viewport = _scrollRect.viewport;
            }

            if (_viewport == null && _scrollRect != null)
            {
                _viewport = _scrollRect.transform as RectTransform;
            }

            if (_itemTemplate == null && _content != null && _content.childCount > 0)
            {
                _itemTemplate = _content.GetChild(0) as RectTransform;
            }

            if (_itemTemplate != null && _hideTemplateOnAwake)
            {
                _itemTemplate.gameObject.SetActive(false);
            }

            _constraintCount = Mathf.Max(1, _constraintCount);
            _extraBuffer = Mathf.Max(0, _extraBuffer);
            _itemSize.x = Mathf.Max(1f, _itemSize.x);
            _itemSize.y = Mathf.Max(1f, _itemSize.y);
            _initialized = true;
        }

        private void OnScrollValueChanged(Vector2 value)
        {
            RefreshVisibleItems();
        }

        private void UpdateContentSize()
        {
            if (_content == null)
            {
                return;
            }

            int rowCount = GetRowCount();
            int columnCount = GetColumnCount();
            float width = _padding.left + _padding.right + columnCount * _itemSize.x + Mathf.Max(0, columnCount - 1) * _spacing.x;
            float height = _padding.top + _padding.bottom + rowCount * _itemSize.y + Mathf.Max(0, rowCount - 1) * _spacing.y;

            if (_viewport != null)
            {
                width = Mathf.Max(width, _viewport.rect.width);
                height = Mathf.Max(height, _viewport.rect.height);
            }

            _content.sizeDelta = new Vector2(width, height);
        }

        private void RefreshVisibleItems(bool forceRefresh = false)
        {
            if (_content == null || _viewport == null || _itemTemplate == null)
            {
                return;
            }

            if (_dataCount <= 0)
            {
                RecycleAll();
                return;
            }

            GetVisibleIndexRange(out int firstIndex, out int lastIndex);

            List<int> recycleKeys = null;
            foreach (int index in _activeItems.Keys)
            {
                if (index < firstIndex || index > lastIndex)
                {
                    if (recycleKeys == null)
                    {
                        recycleKeys = new List<int>();
                    }

                    recycleKeys.Add(index);
                }
            }

            if (recycleKeys != null)
            {
                for (int i = 0; i < recycleKeys.Count; i++)
                {
                    RecycleItem(recycleKeys[i]);
                }
            }

            for (int i = firstIndex; i <= lastIndex; i++)
            {
                if (i < 0 || i >= _dataCount)
                {
                    continue;
                }

                if (!_activeItems.TryGetValue(i, out GameObject item) || item == null)
                {
                    item = GetItem();
                    _activeItems[i] = item;
                    BindItem(i, item);
                }
                else if (forceRefresh)
                {
                    BindItem(i, item);
                }

                SetItemPosition(i, item.transform as RectTransform);
            }
        }

        private void GetVisibleIndexRange(out int firstIndex, out int lastIndex)
        {
            if (_layoutMode == LayoutMode.Horizontal)
            {
                float scrollX = Mathf.Max(0f, -_content.anchoredPosition.x);
                float viewportWidth = _viewport.rect.width;
                int firstColumn = Mathf.FloorToInt((scrollX - _padding.left) / GetStrideX()) - _extraBuffer;
                int lastColumn = Mathf.CeilToInt((scrollX + viewportWidth - _padding.left) / GetStrideX()) + _extraBuffer;
                firstIndex = Mathf.Max(0, firstColumn);
                lastIndex = Mathf.Min(_dataCount - 1, lastColumn);
                return;
            }

            float scrollY = Mathf.Max(0f, _content.anchoredPosition.y);
            float viewportHeight = _viewport.rect.height;
            int firstRow = Mathf.FloorToInt((scrollY - _padding.top) / GetStrideY()) - _extraBuffer;
            int lastRow = Mathf.CeilToInt((scrollY + viewportHeight - _padding.top) / GetStrideY()) + _extraBuffer;

            int columns = GetColumnCount();
            firstIndex = Mathf.Max(0, firstRow * columns);
            lastIndex = Mathf.Min(_dataCount - 1, ((lastRow + 1) * columns) - 1);
        }

        private void SetItemPosition(int index, RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = _itemSize;

            int row = GetRow(index);
            int column = GetColumn(index);
            rect.anchoredPosition = new Vector2(
                _padding.left + column * GetStrideX(),
                -_padding.top - row * GetStrideY());
        }

        private void BindItem(int index, GameObject item)
        {
            if (item == null)
            {
                return;
            }

            item.name = $"{_itemTemplate.name}_{index}";
            _itemRenderer?.Invoke(index, item);
            _onRefreshItem.Invoke(index, item);
        }

        private GameObject GetItem()
        {
            GameObject item = _pooledItems.Count > 0 ? _pooledItems.Pop() : Instantiate(_itemTemplate.gameObject, _content);
            item.transform.SetParent(_content, false);
            item.SetActive(true);
            return item;
        }

        private void RecycleItem(int index)
        {
            if (!_activeItems.TryGetValue(index, out GameObject item))
            {
                return;
            }

            _activeItems.Remove(index);
            if (item == null)
            {
                return;
            }

            item.SetActive(false);
            _pooledItems.Push(item);
        }

        private void RecycleAll()
        {
            List<int> keys = new List<int>(_activeItems.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                RecycleItem(keys[i]);
            }
        }

        private int GetColumnCount()
        {
            switch (_layoutMode)
            {
                case LayoutMode.VerticalGrid:
                    return Mathf.Max(1, _constraintCount);
                case LayoutMode.Horizontal:
                    return Mathf.Max(0, _dataCount);
                default:
                    return 1;
            }
        }

        private int GetRowCount()
        {
            if (_dataCount <= 0)
            {
                return 0;
            }

            if (_layoutMode == LayoutMode.Horizontal)
            {
                return 1;
            }

            return Mathf.CeilToInt((float)_dataCount / GetColumnCount());
        }

        private int GetRow(int index)
        {
            if (_layoutMode == LayoutMode.Horizontal)
            {
                return 0;
            }

            return index / GetColumnCount();
        }

        private int GetColumn(int index)
        {
            if (_layoutMode == LayoutMode.Vertical)
            {
                return 0;
            }

            return _layoutMode == LayoutMode.Horizontal ? index : index % GetColumnCount();
        }

        private float GetStrideX()
        {
            return _itemSize.x + _spacing.x;
        }

        private float GetStrideY()
        {
            return _itemSize.y + _spacing.y;
        }
    }
}
