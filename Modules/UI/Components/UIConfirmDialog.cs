using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GameFramework.Core.UI
{
    /// <summary>
    /// 通用确认弹窗绑定脚本，负责文案、按钮回调和关闭行为。
    /// </summary>
    [DisallowMultipleComponent]
    public class UIConfirmDialog : MonoBehaviour
    {
        public enum CloseAction
        {
            None,
            SetInactive,
            DestroyGameObject,
            CloseUIForm
        }

        [SerializeField] private Text _uguiTitleText;
        [SerializeField] private Text _uguiMessageText;
        [SerializeField] private TMP_Text _tmpTitleText;
        [SerializeField] private TMP_Text _tmpMessageText;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _cancelButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private CloseAction _closeAction = CloseAction.SetInactive;
        [SerializeField] private UIFormBase _targetForm;
        [SerializeField] private UnityEvent _onConfirm = new UnityEvent();
        [SerializeField] private UnityEvent _onCancel = new UnityEvent();

        private Action _confirmCallback;
        private Action _cancelCallback;

        public UnityEvent OnConfirm => _onConfirm;
        public UnityEvent OnCancel => _onCancel;

        private void Reset()
        {
            _targetForm = GetComponentInParent<UIFormBase>(true);
            Button[] buttons = GetComponentsInChildren<Button>(true);
            if (buttons.Length > 0) _confirmButton = buttons[0];
            if (buttons.Length > 1) _cancelButton = buttons[1];
            if (buttons.Length > 2) _closeButton = buttons[2];
        }

        private void Awake()
        {
            RegisterButtons();
        }

        private void OnDestroy()
        {
            UnregisterButtons();
        }

        public void Configure(
            string title,
            string message,
            Action onConfirm = null,
            Action onCancel = null,
            string confirmText = null,
            string cancelText = null)
        {
            SetTitle(title);
            SetMessage(message);
            _confirmCallback = onConfirm;
            _cancelCallback = onCancel;

            if (!string.IsNullOrEmpty(confirmText) && _confirmButton != null)
            {
                SetButtonText(_confirmButton, confirmText);
            }

            if (!string.IsNullOrEmpty(cancelText) && _cancelButton != null)
            {
                SetButtonText(_cancelButton, cancelText);
            }

            gameObject.SetActive(true);
        }

        public void SetTitle(string title)
        {
            if (_tmpTitleText != null)
            {
                _tmpTitleText.text = title;
            }

            if (_uguiTitleText != null)
            {
                _uguiTitleText.text = title;
            }
        }

        public void SetMessage(string message)
        {
            if (_tmpMessageText != null)
            {
                _tmpMessageText.text = message;
            }

            if (_uguiMessageText != null)
            {
                _uguiMessageText.text = message;
            }
        }

        public void Confirm()
        {
            _onConfirm.Invoke();
            _confirmCallback?.Invoke();
            Close();
        }

        public void Cancel()
        {
            _onCancel.Invoke();
            _cancelCallback?.Invoke();
            Close();
        }

        public void Close()
        {
            switch (_closeAction)
            {
                case CloseAction.SetInactive:
                    gameObject.SetActive(false);
                    break;
                case CloseAction.DestroyGameObject:
                    Destroy(gameObject);
                    break;
                case CloseAction.CloseUIForm:
                    CloseTargetForm();
                    break;
            }
        }

        private void RegisterButtons()
        {
            if (_confirmButton != null)
            {
                _confirmButton.onClick.AddListener(Confirm);
            }

            if (_cancelButton != null)
            {
                _cancelButton.onClick.AddListener(Cancel);
            }

            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(Cancel);
            }
        }

        private void UnregisterButtons()
        {
            if (_confirmButton != null)
            {
                _confirmButton.onClick.RemoveListener(Confirm);
            }

            if (_cancelButton != null)
            {
                _cancelButton.onClick.RemoveListener(Cancel);
            }

            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(Cancel);
            }
        }

        private void CloseTargetForm()
        {
            UIFormBase form = _targetForm != null ? _targetForm : GetComponentInParent<UIFormBase>(true);
            if (form != null && form.SerialId > 0)
            {
                GameApp.UI.CloseUI(form.SerialId);
                return;
            }

            gameObject.SetActive(false);
        }

        private static void SetButtonText(Button button, string text)
        {
            TMP_Text tmpText = button.GetComponentInChildren<TMP_Text>(true);
            if (tmpText != null)
            {
                tmpText.text = text;
            }

            Text uguiText = button.GetComponentInChildren<Text>(true);
            if (uguiText != null)
            {
                uguiText.text = text;
            }
        }
    }
}
