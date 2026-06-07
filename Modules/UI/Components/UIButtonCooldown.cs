using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace GameFramework.Core.UI
{
    /// <summary>
    /// 点击后临时禁用 Button，防止连续点击触发重复请求或重复打开界面。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public class UIButtonCooldown : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private float _cooldownSeconds = 0.5f;
        [SerializeField] private bool _useUnscaledTime = true;
        [SerializeField] private bool _restoreInteractableOnDisable = true;

        private Coroutine _cooldownCoroutine;
        private bool _isCoolingDown;
        private bool _interactableBeforeCooldown;

        public bool IsCoolingDown => _isCoolingDown;

        private void Reset()
        {
            _button = GetComponent<Button>();
            _cooldownSeconds = 0.5f;
            _useUnscaledTime = true;
            _restoreInteractableOnDisable = true;
        }

        private void Awake()
        {
            if (_button == null)
            {
                _button = GetComponent<Button>();
            }
        }

        private void OnEnable()
        {
            if (_button != null)
            {
                _button.onClick.AddListener(BeginCooldown);
            }
        }

        private void OnDisable()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(BeginCooldown);
            }

            if (_cooldownCoroutine != null)
            {
                StopCoroutine(_cooldownCoroutine);
                _cooldownCoroutine = null;
            }

            if (_restoreInteractableOnDisable && _button != null && _isCoolingDown)
            {
                _button.interactable = _interactableBeforeCooldown;
            }

            _isCoolingDown = false;
        }

        public void BeginCooldown()
        {
            if (!isActiveAndEnabled || _button == null || _isCoolingDown || _cooldownSeconds <= 0f)
            {
                return;
            }

            _cooldownCoroutine = StartCoroutine(CooldownRoutine());
        }

        public void CancelCooldown(bool restoreInteractable = true)
        {
            if (_cooldownCoroutine != null)
            {
                StopCoroutine(_cooldownCoroutine);
                _cooldownCoroutine = null;
            }

            if (restoreInteractable && _button != null && _isCoolingDown)
            {
                _button.interactable = _interactableBeforeCooldown;
            }

            _isCoolingDown = false;
        }

        private IEnumerator CooldownRoutine()
        {
            _isCoolingDown = true;
            _interactableBeforeCooldown = _button.interactable;
            _button.interactable = false;

            float endTime = (_useUnscaledTime ? Time.unscaledTime : Time.time) + _cooldownSeconds;
            while ((_useUnscaledTime ? Time.unscaledTime : Time.time) < endTime)
            {
                yield return null;
            }

            if (_button != null)
            {
                _button.interactable = _interactableBeforeCooldown;
            }

            _isCoolingDown = false;
            _cooldownCoroutine = null;
        }
    }
}
