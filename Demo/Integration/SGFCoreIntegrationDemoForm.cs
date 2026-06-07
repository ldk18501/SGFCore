using System;
using System.Collections;
using GameFramework.Core.UI;
using UnityEngine;
using UnityEngine.UI;

namespace GameFramework.Core.Demo
{
    /// <summary>
    /// 动态搭建 UI，用于验证 SGFCore UI 常用组件和模块联动。
    /// </summary>
    public class SGFCoreIntegrationDemoForm : UIFormBase
    {
        private Text _titleText;
        private Text _statusText;
        private Text _redPointText;
        private UIVirtualList _virtualList;
        private UIToast _toast;
        private UILoadingOverlay _loading;
        private UIConfirmDialog _confirmDialog;
        private UINetImage _netImage;
        private int _redPointCount;

        public override void OnInit()
        {
            ConfigureDemoCanvas();
            BuildUI();
            _virtualList.SetItemRenderer(RefreshListItem);
            _virtualList.SetDataCount(120, resetPosition: true);
            GameApp.RedPoint.AddListener("demo", OnDemoRedPointChanged);
            Debug.Log("[SGFCoreDemo] Demo form initialized.");
        }

        public override void OnOpen(params object[] args)
        {
            SGFCoreIntegrationDemoData data = args != null && args.Length > 0
                ? args[0] as SGFCoreIntegrationDemoData
                : null;

            _statusText.text = data != null
                ? $"Config: {data.ConfigText}"
                : "Config: <missing>";

            if (data != null && !string.IsNullOrWhiteSpace(data.NetImageFileUrl))
            {
                _netImage.Load(data.NetImageFileUrl);
            }

            _toast.Show("SGFCore demo UI opened.");
            Debug.Log("[SGFCoreDemo] Demo form opened.");
        }

        public override void OnDestroyUI()
        {
            GameApp.RedPoint.RemoveListener("demo", OnDemoRedPointChanged);
        }

        private void BuildUI()
        {
            RectTransform root = transform as RectTransform;
            Stretch(root);

            GameObject panel = CreateUIObject("Panel", root);
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.08f, 0.10f, 0.13f, 0.96f);
            RectTransform panelRect = panel.transform as RectTransform;
            Stretch(panelRect, 44f, 44f, 44f, 44f);

            _titleText = CreateText("Title", panelRect, "SGFCore Integration Demo", 28, TextAnchor.MiddleLeft);
            SetRect(_titleText.rectTransform, new Vector2(24f, -18f), new Vector2(620f, 44f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            _statusText = CreateText("Status", panelRect, "Config loading...", 16, TextAnchor.MiddleLeft);
            SetRect(_statusText.rectTransform, new Vector2(24f, -66f), new Vector2(900f, 34f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            _redPointText = CreateText("RedPoint", panelRect, "RedPoint: 0", 18, TextAnchor.MiddleCenter);
            SetRect(_redPointText.rectTransform, new Vector2(-180f, -24f), new Vector2(160f, 40f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            _redPointText.color = new Color(1f, 0.36f, 0.28f);

            CreateButton(panelRect, "ToastButton", "Toast", new Vector2(24f, -116f), () => _toast.Show("Toast queue message " + DateTime.Now.ToString("HH:mm:ss")));
            CreateButton(panelRect, "LoadingButton", "Loading", new Vector2(144f, -116f), () => StartCoroutine(ShowLoadingRoutine()));
            CreateButton(panelRect, "ConfirmButton", "Confirm", new Vector2(264f, -116f), ShowConfirm);
            CreateButton(panelRect, "RedPointButton", "Red +1", new Vector2(384f, -116f), AddRedPoint);
            CreateButton(panelRect, "CloseButton", "Close", new Vector2(504f, -116f), () => GameApp.UI.CloseUI(SerialId));

            CreateVirtualList(panelRect);
            CreateNetImage(panelRect);
            CreateToast(panelRect);
            CreateLoading(panelRect);
            CreateConfirmDialog(panelRect);
        }

        private void ConfigureDemoCanvas()
        {
            Canvas canvas = GetComponent<Canvas>();
            UIRoot uiRoot = UIRoot.Instance;
            if (canvas != null && uiRoot != null && uiRoot.UICamera != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = uiRoot.UICamera;
                canvas.planeDistance = 1f;
            }
        }

        private void CreateVirtualList(RectTransform parent)
        {
            GameObject scrollObj = CreateUIObject("VirtualList", parent);
            RectTransform scrollRectTransform = scrollObj.transform as RectTransform;
            SetRect(scrollRectTransform, new Vector2(24f, -176f), new Vector2(430f, 360f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            Image scrollImage = scrollObj.AddComponent<Image>();
            scrollImage.color = new Color(0.05f, 0.06f, 0.08f, 1f);
            ScrollRect scrollRect = scrollObj.AddComponent<ScrollRect>();

            GameObject viewport = CreateUIObject("Viewport", scrollRectTransform);
            RectTransform viewportRect = viewport.transform as RectTransform;
            Stretch(viewportRect);
            viewport.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.08f);
            viewport.AddComponent<Mask>().showMaskGraphic = false;

            GameObject content = CreateUIObject("Content", viewportRect);
            RectTransform contentRect = content.transform as RectTransform;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0f, 1f);
            contentRect.anchoredPosition = Vector2.zero;

            GameObject item = CreateUIObject("ItemTemplate", contentRect);
            RectTransform itemRect = item.transform as RectTransform;
            itemRect.sizeDelta = new Vector2(390f, 44f);
            Image itemImage = item.AddComponent<Image>();
            itemImage.color = new Color(0.16f, 0.20f, 0.28f, 1f);
            item.AddComponent<SGFCoreIntegrationDemoItemView>();
            Text label = CreateText("Label", itemRect, "Item", 16, TextAnchor.MiddleLeft);
            Stretch(label.rectTransform, 14f, 0f, 14f, 0f);

            scrollRect.content = contentRect;
            scrollRect.viewport = viewportRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            _virtualList = scrollObj.AddComponent<UIVirtualList>();
        }

        private void CreateNetImage(RectTransform parent)
        {
            Text label = CreateText("NetImageLabel", parent, "UINetImage (local file URL)", 16, TextAnchor.MiddleLeft);
            SetRect(label.rectTransform, new Vector2(490f, -176f), new Vector2(320f, 30f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            GameObject imageObj = CreateUIObject("NetImage", parent);
            RectTransform rect = imageObj.transform as RectTransform;
            SetRect(rect, new Vector2(490f, -216f), new Vector2(180f, 180f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            Image image = imageObj.AddComponent<Image>();
            image.color = new Color(0.12f, 0.14f, 0.18f, 1f);
            _netImage = imageObj.AddComponent<UINetImage>();
        }

        private void CreateToast(RectTransform parent)
        {
            GameObject toastObj = CreateUIObject("Toast", parent);
            RectTransform rect = toastObj.transform as RectTransform;
            SetRect(rect, new Vector2(0f, 74f), new Vector2(420f, 48f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            toastObj.AddComponent<Image>().color = new Color(0.02f, 0.02f, 0.025f, 0.88f);
            toastObj.AddComponent<CanvasGroup>();
            Text text = CreateText("Text", rect, string.Empty, 16, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 18f, 4f, 18f, 4f);
            _toast = toastObj.AddComponent<UIToast>();
        }

        private void CreateLoading(RectTransform parent)
        {
            GameObject loadingObj = CreateUIObject("LoadingOverlay", parent);
            RectTransform rect = loadingObj.transform as RectTransform;
            Stretch(rect);
            loadingObj.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            loadingObj.AddComponent<CanvasGroup>();
            Text text = CreateText("Message", rect, "Loading...", 24, TextAnchor.MiddleCenter);
            SetRect(text.rectTransform, new Vector2(0f, 28f), new Vector2(400f, 48f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            GameObject fillObj = CreateUIObject("ProgressFill", rect);
            Image fill = fillObj.AddComponent<Image>();
            fill.color = new Color(0.24f, 0.74f, 0.95f, 1f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            SetRect(fill.rectTransform, new Vector2(0f, -24f), new Vector2(260f, 8f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _loading = loadingObj.AddComponent<UILoadingOverlay>();
        }

        private void CreateConfirmDialog(RectTransform parent)
        {
            GameObject dialog = CreateUIObject("ConfirmDialog", parent);
            RectTransform rect = dialog.transform as RectTransform;
            SetRect(rect, new Vector2(0f, 0f), new Vector2(420f, 220f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            dialog.AddComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f, 0.98f);
            CreateText("Title", rect, "Confirm", 24, TextAnchor.MiddleCenter).rectTransform.anchoredPosition = new Vector2(0f, -30f);
            Text message = CreateText("Message", rect, "Message", 16, TextAnchor.MiddleCenter);
            SetRect(message.rectTransform, new Vector2(0f, -92f), new Vector2(360f, 70f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            CreateButton(rect, "ConfirmButton", "OK", new Vector2(84f, 34f), null, new Vector2(0.5f, 0f));
            CreateButton(rect, "CancelButton", "Cancel", new Vector2(-84f, 34f), null, new Vector2(0.5f, 0f));
            _confirmDialog = dialog.AddComponent<UIConfirmDialog>();
            dialog.SetActive(false);
        }

        private void RefreshListItem(int index, GameObject item)
        {
            SGFCoreIntegrationDemoItemView itemView = item.GetComponent<SGFCoreIntegrationDemoItemView>();
            if (itemView != null)
            {
                itemView.Refresh(index);
            }
        }

        private IEnumerator ShowLoadingRoutine()
        {
            _loading.Show("Loading overlay test...");
            for (int i = 0; i <= 20; i++)
            {
                _loading.SetProgress(i / 20f);
                yield return new WaitForSecondsRealtime(0.04f);
            }
            _loading.Hide();
            _toast.Show("Loading completed.");
        }

        private void ShowConfirm()
        {
            _confirmDialog.Configure(
                "ConfirmDialog",
                "This dialog is built by SGFCore demo code.",
                onConfirm: () => _toast.Show("Confirmed."),
                onCancel: () => _toast.Show("Canceled."),
                confirmText: "OK",
                cancelText: "Cancel");
        }

        private void AddRedPoint()
        {
            _redPointCount++;
            GameApp.RedPoint.SetCount("demo.mail.unread", _redPointCount);
        }

        private void OnDemoRedPointChanged(RedPointSnapshot snapshot)
        {
            _redPointText.text = $"RedPoint: {snapshot.Count}";
        }

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Text CreateText(string name, Transform parent, string text, int fontSize, TextAnchor alignment)
        {
            GameObject go = CreateUIObject(name, parent);
            Text label = go.AddComponent<Text>();
            label.text = text;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = Color.white;
            return label;
        }

        private static Button CreateButton(RectTransform parent, string name, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClick, Vector2? anchor = null)
        {
            GameObject go = CreateUIObject(name, parent);
            RectTransform rect = go.transform as RectTransform;
            SetRect(rect, anchoredPosition, new Vector2(104f, 38f), anchor ?? new Vector2(0f, 1f), anchor ?? new Vector2(0f, 1f));
            Image image = go.AddComponent<Image>();
            image.color = new Color(0.18f, 0.42f, 0.70f, 1f);
            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            Text buttonText = CreateText("Text", rect, label, 15, TextAnchor.MiddleCenter);
            Stretch(buttonText.rectTransform);
            return button;
        }

        private static void Stretch(RectTransform rect, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void SetRect(RectTransform rect, Vector2 anchoredPosition, Vector2 size, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }
    }
}
