using UnityEngine;
using UnityEngine.UI;

namespace GameFramework.Core.Demo
{
    public class SGFCoreIntegrationDemoItemView : MonoBehaviour
    {
        private Text _label;
        private Image _background;

        private void Awake()
        {
            CacheReferences();
        }

        public void Refresh(int index)
        {
            CacheReferences();

            if (_label != null)
            {
                _label.text = $"Item {index:000}";
            }

            if (_background != null)
            {
                _background.color = index % 2 == 0
                    ? new Color(0.18f, 0.22f, 0.30f, 0.95f)
                    : new Color(0.14f, 0.18f, 0.24f, 0.95f);
            }
        }

        private void CacheReferences()
        {
            if (_label == null)
            {
                _label = GetComponentInChildren<Text>(true);
            }

            if (_background == null)
            {
                _background = GetComponent<Image>();
            }
        }
    }
}
