using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameFramework.Core.UI
{
    /// <summary>
    /// 长按按钮重复触发，适合加减数量、连续升级等 UI。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public class UIHoldRepeatButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private Button _button;
        [SerializeField] private float _initialDelay = 0.35f;
        [SerializeField] private float _repeatInterval = 0.08f;
        [SerializeField] private bool _invokeImmediately = true;
        [SerializeField] private bool _invokeButtonOnRepeat = true;
        [SerializeField] private bool _respectInteractable = true;
        [SerializeField] private bool _useUnscaledTime = true;
        [SerializeField] private UnityEvent _onRepeat = new UnityEvent();

        private Coroutine _repeatCoroutine;

        public UnityEvent OnRepeat => _onRepeat;

        private void Reset()
        {
            _button = GetComponent<Button>();
        }

        private void Awake()
        {
            if (_button == null)
            {
                _button = GetComponent<Button>();
            }
        }

        private void OnDisable()
        {
            StopRepeat();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_repeatCoroutine != null || (_respectInteractable && _button != null && !_button.interactable))
            {
                return;
            }

            _repeatCoroutine = StartCoroutine(RepeatRoutine());
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            StopRepeat();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            StopRepeat();
        }

        private IEnumerator RepeatRoutine()
        {
            if (_invokeImmediately)
            {
                InvokeRepeat();
            }

            yield return Wait(_initialDelay);

            while (isActiveAndEnabled)
            {
                if (_respectInteractable && _button != null && !_button.interactable)
                {
                    break;
                }

                InvokeRepeat();
                yield return Wait(_repeatInterval);
            }

            _repeatCoroutine = null;
        }

        private void InvokeRepeat()
        {
            if (_invokeButtonOnRepeat && _button != null)
            {
                _button.onClick.Invoke();
            }

            _onRepeat.Invoke();
        }

        private IEnumerator Wait(float seconds)
        {
            if (seconds <= 0f)
            {
                yield break;
            }

            if (_useUnscaledTime)
            {
                float endTime = Time.unscaledTime + seconds;
                while (Time.unscaledTime < endTime)
                {
                    yield return null;
                }
            }
            else
            {
                yield return new WaitForSeconds(seconds);
            }
        }

        private void StopRepeat()
        {
            if (_repeatCoroutine == null)
            {
                return;
            }

            StopCoroutine(_repeatCoroutine);
            _repeatCoroutine = null;
        }
    }
}
