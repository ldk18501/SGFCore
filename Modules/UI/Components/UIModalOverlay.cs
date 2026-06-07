using GameFramework.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameFramework.Core.UI
{
    /// <summary>
    /// 弹窗遮罩点击行为，可用于点击空白处关闭弹窗或只派发自定义事件。
    /// </summary>
    [DisallowMultipleComponent]
    public class UIModalOverlay : MonoBehaviour, IPointerClickHandler
    {
        public enum ClickAction
        {
            None,
            SetTargetInactive,
            DestroyTarget,
            CloseUIForm
        }

        [SerializeField] private bool _closeOnClick = true;
        [SerializeField] private bool _leftButtonOnly = true;
        [SerializeField] private ClickAction _clickAction = ClickAction.CloseUIForm;
        [SerializeField] private UIFormBase _targetForm;
        [SerializeField] private GameObject _targetObject;
        [SerializeField] private UnityEvent _onOverlayClick = new UnityEvent();

        public UnityEvent OnOverlayClick => _onOverlayClick;

        private void Reset()
        {
            if (GetComponent<Graphic>() == null)
            {
                gameObject.AddComponent<UIRaycastArea>();
            }

            _targetForm = GetComponentInParent<UIFormBase>(true);
            _targetObject = _targetForm != null ? _targetForm.gameObject : transform.parent != null ? transform.parent.gameObject : gameObject;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_closeOnClick || (_leftButtonOnly && eventData.button != PointerEventData.InputButton.Left))
            {
                return;
            }

            _onOverlayClick.Invoke();
            ExecuteClickAction();
        }

        public void ExecuteClickAction()
        {
            switch (_clickAction)
            {
                case ClickAction.SetTargetInactive:
                    GetTargetObject()?.SetActive(false);
                    break;
                case ClickAction.DestroyTarget:
                    GameObject target = GetTargetObject();
                    if (target != null)
                    {
                        Destroy(target);
                    }
                    break;
                case ClickAction.CloseUIForm:
                    CloseTargetForm();
                    break;
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

            GetTargetObject()?.SetActive(false);
        }

        private GameObject GetTargetObject()
        {
            if (_targetObject != null)
            {
                return _targetObject;
            }

            if (_targetForm != null)
            {
                return _targetForm.gameObject;
            }

            return transform.parent != null ? transform.parent.gameObject : gameObject;
        }
    }
}
