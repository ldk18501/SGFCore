using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace GameFramework.Core.UI
{
    [RequireComponent(typeof(Text))]
    public class LocalizedText : MonoBehaviour
    {
        [FormerlySerializedAs("KeyId")]
        [SerializeField] private int _keyId;

        private Text _textComponent;
        private bool _subscribed;

        public int KeyId
        {
            get => _keyId;
            set => SetKeyId(value);
        }

        private void Awake()
        {
            _textComponent = GetComponent<Text>();
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
            RefreshText();
        }

        public void RefreshText()
        {
            LocalizationModule localization = GameApp.Loc;
            if (_textComponent == null || _keyId <= 0 || localization == null)
            {
                return;
            }

            _textComponent.text = localization.GetString(_keyId);
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
