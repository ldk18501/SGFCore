using UnityEngine;
using UnityEngine.UI;

namespace GameFramework.Core.UI
{
    /// <summary>
    /// 多段式进度条，可用于章节进度、奖励节点、分段血条等。
    /// </summary>
    [DisallowMultipleComponent]
    public class UIStepProgressBar : MonoBehaviour
    {
        [SerializeField] private Image[] _segments;
        [SerializeField] private float[] _weights;
        [SerializeField] private float _value;
        [SerializeField] private float _maxValue = 1f;
        [SerializeField] private bool _forceFilledImageType = true;

        public float Value => _value;
        public float MaxValue => _maxValue;
        public float NormalizedValue => _maxValue <= 0f ? 0f : Mathf.Clamp01(_value / _maxValue);

        private void Reset()
        {
            CollectChildSegments();
            Refresh();
        }

        private void Awake()
        {
            if (_segments == null || _segments.Length == 0)
            {
                CollectChildSegments();
            }

            Refresh();
        }

        private void OnValidate()
        {
            _maxValue = Mathf.Max(0.0001f, _maxValue);
            Refresh();
        }

        public void SetValue(float value)
        {
            _value = Mathf.Clamp(value, 0f, _maxValue);
            Refresh();
        }

        public void SetNormalizedValue(float normalizedValue)
        {
            _value = Mathf.Clamp01(normalizedValue) * _maxValue;
            Refresh();
        }

        public void SetSegments(Image[] segments, float[] weights = null)
        {
            _segments = segments;
            _weights = weights;
            Refresh();
        }

        public void CollectChildSegments()
        {
            int childCount = transform.childCount;
            if (childCount > 0)
            {
                var images = new System.Collections.Generic.List<Image>(childCount);
                for (int i = 0; i < childCount; i++)
                {
                    Image image = transform.GetChild(i).GetComponent<Image>();
                    if (image != null)
                    {
                        images.Add(image);
                    }
                }

                _segments = images.ToArray();
                return;
            }

            Image selfImage = GetComponent<Image>();
            _segments = selfImage == null ? new Image[0] : new[] { selfImage };
        }

        public void Refresh()
        {
            if (_segments == null || _segments.Length == 0)
            {
                return;
            }

            float normalized = NormalizedValue;
            float totalWeight = GetTotalWeight();
            float consumed = 0f;

            for (int i = 0; i < _segments.Length; i++)
            {
                Image segment = _segments[i];
                if (segment == null)
                {
                    continue;
                }

                if (_forceFilledImageType && segment.type != Image.Type.Filled)
                {
                    segment.type = Image.Type.Filled;
                }

                float segmentWeight = GetWeight(i) / totalWeight;
                float local = segmentWeight <= 0f ? 0f : Mathf.InverseLerp(consumed, consumed + segmentWeight, normalized);
                segment.fillAmount = Mathf.Clamp01(local);
                consumed += segmentWeight;
            }
        }

        private float GetTotalWeight()
        {
            float total = 0f;
            for (int i = 0; i < _segments.Length; i++)
            {
                total += GetWeight(i);
            }

            return Mathf.Max(0.0001f, total);
        }

        private float GetWeight(int index)
        {
            if (_weights == null || index >= _weights.Length || _weights[index] <= 0f)
            {
                return 1f;
            }

            return _weights[index];
        }
    }
}
