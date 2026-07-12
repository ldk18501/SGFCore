using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace GameFramework.Core.UI
{
    [RequireComponent(typeof(TMP_Text))]
    public class LocalizedTextTmp : MonoBehaviour
    {
        [FormerlySerializedAs("KeyId")]
        [SerializeField] private int _keyId;
        [SerializeField] private string _key;

        private TMP_Text _textComponent;
        private bool _subscribed;

        public int KeyId
        {
            get => _keyId;
            set => SetKeyId(value);
        }

        public string Key
        {
            get => _key;
            set => SetKey(value);
        }

        private void Awake()
        {
            _textComponent = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            RefreshText();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void SetKeyId(int newKeyId)
        {
            _keyId = newKeyId;
            _key = string.Empty;
            RefreshText();
        }

        public void SetKey(string newKey)
        {
            _key = newKey ?? string.Empty;
            RefreshText();
        }

        public void RefreshText()
        {
            LocalizationModule localization = GameApp.Loc;
            if (_textComponent == null || localization == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(_key))
            {
                _textComponent.text = localization.GetString(_key);
            }
            else if (_keyId > 0)
            {
                _textComponent.text = localization.GetString(_keyId);
            }
        }

        private void Subscribe()
        {
            EventModule eventModule = GameApp.Event;
            if (_subscribed || eventModule == null)
            {
                return;
            }

            eventModule.AddListener<LanguageChangedEvent>(OnLanguageChanged);
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            EventModule eventModule = GameApp.Event;
            if (!_subscribed || eventModule == null)
            {
                return;
            }

            eventModule.RemoveListener<LanguageChangedEvent>(OnLanguageChanged);
            _subscribed = false;
        }

        private void OnLanguageChanged(LanguageChangedEvent evt)
        {
            RefreshText();
        }
    }
}
