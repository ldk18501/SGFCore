using GameFramework.Core;
using UnityEngine;
using UnityEngine.UI;

namespace GameFramework.Core.UI
{
    /// <summary>
    /// 给 Button 添加点击音效，适合挂在通用按钮预制体上。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public class UIButtonSound : MonoBehaviour
    {
        public static string DefaultClickSoundAddress = "UI_Click";

        [SerializeField] private Button _button;
        [SerializeField] private string _soundAddress;
        [SerializeField] private bool _useDefaultAddress = true;
        [SerializeField] private bool _playOnlyWhenInteractable = true;
        [SerializeField] private bool _singletonSound = true;
        [SerializeField] private float _pitchRange;

        private void Reset()
        {
            _button = GetComponent<Button>();
            _useDefaultAddress = true;
            _singletonSound = true;
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
                _button.onClick.AddListener(PlayClickSound);
            }
        }

        private void OnDisable()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(PlayClickSound);
            }
        }

        public void PlayClickSound()
        {
            if (_button != null && _playOnlyWhenInteractable && !_button.interactable)
            {
                return;
            }

            string address = _useDefaultAddress ? DefaultClickSoundAddress : _soundAddress;
            if (string.IsNullOrWhiteSpace(address))
            {
                return;
            }

            GameApp.Audio.PlaySFX(address, isSingleton: _singletonSound, pitchRange: _pitchRange);
        }
    }
}
