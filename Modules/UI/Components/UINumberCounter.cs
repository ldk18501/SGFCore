using System.Collections;
using GameFramework.Core.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameFramework.Core.UI
{
    /// <summary>
    /// 数字滚动显示组件，支持 UGUI Text 和 TMP_Text。
    /// </summary>
    [DisallowMultipleComponent]
    public class UINumberCounter : MonoBehaviour
    {
        public enum NumberFormatMode
        {
            Raw,
            Unit,
            Custom
        }

        [SerializeField] private Text _uguiText;
        [SerializeField] private TMP_Text _tmpText;
        [SerializeField] private double _value;
        [SerializeField] private float _duration = 0.35f;
        [SerializeField] private bool _useUnscaledTime = true;
        [SerializeField] private NumberFormatMode _formatMode = NumberFormatMode.Unit;
        [SerializeField] private int _decimals = 0;
        [SerializeField] private string _customFormat = "0";

        private Coroutine _counterCoroutine;

        public double Value => _value;

        private void Reset()
        {
            _uguiText = GetComponent<Text>();
            _tmpText = GetComponent<TMP_Text>();
        }

        private void Awake()
        {
            NormalizeSettings();
            CacheTextIfNeeded();
            SetText(_value);
        }

        private void OnValidate()
        {
            NormalizeSettings();
        }

        public void SetValue(long value, bool animate = true)
        {
            SetValue((double)value, animate);
        }

        public void SetValue(double value, bool animate = true)
        {
            if (!isActiveAndEnabled || !animate || _duration <= 0f)
            {
                StopCounter();
                _value = value;
                SetText(_value);
                return;
            }

            StopCounter();
            _counterCoroutine = StartCoroutine(CounterRoutine(_value, value));
        }

        public void SetValueImmediately(double value)
        {
            SetValue(value, false);
        }

        private IEnumerator CounterRoutine(double from, double to)
        {
            float elapsed = 0f;
            while (elapsed < _duration)
            {
                elapsed += _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _duration);
                _value = Lerp(from, to, t);
                SetText(_value);
                yield return null;
            }

            _value = to;
            SetText(_value);
            _counterCoroutine = null;
        }

        private void SetText(double value)
        {
            CacheTextIfNeeded();
            string text = FormatValue(value);

            if (_tmpText != null)
            {
                _tmpText.text = text;
            }

            if (_uguiText != null)
            {
                _uguiText.text = text;
            }
        }

        private string FormatValue(double value)
        {
            int decimals = Mathf.Max(0, _decimals);
            switch (_formatMode)
            {
                case NumberFormatMode.Unit:
                    return value < 0d ? "-" + (-value).ToUnitString(decimals) : value.ToUnitString(decimals);
                case NumberFormatMode.Custom:
                    return value.ToString(string.IsNullOrEmpty(_customFormat) ? "0" : _customFormat);
                default:
                    return decimals <= 0 ? System.Math.Round(value).ToString("0") : value.ToString("0." + new string('#', decimals));
            }
        }

        private static double Lerp(double from, double to, float t)
        {
            return from + (to - from) * t;
        }

        private void CacheTextIfNeeded()
        {
            if (_uguiText == null)
            {
                _uguiText = GetComponent<Text>();
            }

            if (_tmpText == null)
            {
                _tmpText = GetComponent<TMP_Text>();
            }
        }

        private void StopCounter()
        {
            if (_counterCoroutine == null)
            {
                return;
            }

            StopCoroutine(_counterCoroutine);
            _counterCoroutine = null;
        }

        private void NormalizeSettings()
        {
            _duration = Mathf.Max(0f, _duration);
            _decimals = Mathf.Max(0, _decimals);
        }
    }
}
