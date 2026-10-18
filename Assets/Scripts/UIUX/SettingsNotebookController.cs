using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Hiking.UIUX
{
    public sealed class SettingsNotebookController : MonoBehaviour
    {
        private const string MasterKey = "ShiyuanUI.MasterVolume";
        private const string OperationKey = "ShiyuanUI.OperationVolume";
        private const string BgmKey = "ShiyuanUI.BgmVolume";

        [Header("Notebook")]
        [SerializeField] private CanvasGroup overlay;
        [SerializeField] private RectTransform book;
        [SerializeField] private RectTransform windowTab;
        [SerializeField] private RectTransform audioTab;
        [SerializeField] private RawImage windowSelectedArtwork;
        [SerializeField] private RawImage windowInactiveArtwork;
        [SerializeField] private RawImage audioSelectedArtwork;
        [SerializeField] private RawImage audioInactiveArtwork;
        [SerializeField] private CanvasGroup windowPage;
        [SerializeField] private CanvasGroup audioPage;
        [SerializeField] private Text windowTitle;
        [SerializeField] private Text audioTitle;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button windowTabButton;
        [SerializeField] private Button audioTabButton;

        [Header("Typography")]
        [SerializeField] private Font notebookFont;
        [SerializeField] private Font accentFont;
        [SerializeField] private Text notebookHeading;
        [SerializeField] private Text bgmCaption;
        [SerializeField] private FontStyle headingStyle = FontStyle.Normal;
        [Range(24, 72)] [SerializeField] private int headingSize = 42;

        [Header("Settings")]
        [SerializeField] private Slider windowSlider;
        [SerializeField] private Text windowValue;
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Text masterValue;
        [SerializeField] private Slider operationSlider;
        [SerializeField] private Text operationValue;
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Text bgmValue;
        [SerializeField] private float minimumViewportScale = 0.7f;
        [SerializeField] private float maximumViewportScale = 1f;
        public event Action<float> ViewportScaleRequested;
        public float ViewportScale => Mathf.Lerp(minimumViewportScale, maximumViewportScale, windowSlider.value);
        [Range(0.3f, 1.2f)] [SerializeField] private float notebookScale = 0.75f;
        [SerializeField] private float transitionSeconds = 0.22f;

        public event Action<float> MasterVolumeChanged;
        public event Action<float> OperationVolumeChanged;
        public event Action<float> BgmVolumeChanged;

        public float MasterVolume => masterSlider.value;
        public float OperationVolume => operationSlider.value;
        public float BgmVolume => bgmSlider.value;

        private Coroutine transition;

        private void Awake()
        {
            book.localScale = Vector3.one * notebookScale;
            // Decorative artwork ignores raycasts. Give the body its own hit surface
            // so real pointer events can reach FixedWidgetDrag through the hierarchy.
            var dragSurface = book.GetComponent<UnityEngine.UI.Image>();
            if (!dragSurface)
            {
                dragSurface = book.gameObject.AddComponent<UnityEngine.UI.Image>();
                dragSurface.color = Color.clear;
            }
            dragSurface.raycastTarget = true;
            if (!book.GetComponent<FixedWidgetDrag>()) book.gameObject.AddComponent<FixedWidgetDrag>();
            ApplyTypography();
            closeButton.onClick.AddListener(Close);
            windowTabButton.onClick.AddListener(SelectWindow);
            audioTabButton.onClick.AddListener(SelectAudio);
            windowSlider.onValueChanged.AddListener(SetWindowSize);
            masterSlider.onValueChanged.AddListener(SetMasterVolume);
            operationSlider.onValueChanged.AddListener(SetOperationVolume);
            bgmSlider.onValueChanged.AddListener(SetBgmVolume);
            // The host owns frame size; initialization never emits a resize request.
            windowSlider.SetValueWithoutNotify(1f);
            masterSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(MasterKey, 1f));
            operationSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(OperationKey, 1f));
            bgmSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(BgmKey, 1f));
            RefreshValues();
            ShowPage(false, true);
            overlay.gameObject.SetActive(false);
        }

        public void Open()
        {
            overlay.gameObject.SetActive(true);
            overlay.alpha = 1f;
            overlay.blocksRaycasts = true;
            overlay.interactable = true;
            ShowPage(false, true);
        }

        public void Close()
        {
            if (transition != null) StopCoroutine(transition);
            transition = null;
            overlay.blocksRaycasts = false;
            overlay.interactable = false;
            overlay.gameObject.SetActive(false);
            PlayerPrefs.Save();
        }

        public void SelectWindow() => ShowPage(false, false);
        public void SelectAudio() => ShowPage(true, false);

        public void ApplyTypography()
        {
            if (overlay == null) return;
            if (notebookFont != null)
            {
                foreach (Text label in overlay.GetComponentsInChildren<Text>(true))
                    label.font = notebookFont;
            }

            if (notebookHeading != null)
            {
                notebookHeading.fontStyle = headingStyle;
                notebookHeading.fontSize = headingSize;
            }

            if (accentFont == null) return;
            if (bgmCaption != null) bgmCaption.font = accentFont;
            if (windowValue != null) windowValue.font = accentFont;
            if (masterValue != null) masterValue.font = accentFont;
            if (operationValue != null) operationValue.font = accentFont;
            if (bgmValue != null) bgmValue.font = accentFont;
        }

        public void SetWindowSize(float value)
        {
            windowSlider.SetValueWithoutNotify(Mathf.Clamp01(value));
            windowValue.text = ViewportScale.ToString("0.00", CultureInfo.InvariantCulture) + "×";
            ViewportScaleRequested?.Invoke(ViewportScale);
        }

        public void SetViewportSnapshot(float scale, float minimum, float maximum)
        {
            minimumViewportScale = Mathf.Max(0.01f, minimum);
            maximumViewportScale = Mathf.Max(minimumViewportScale, maximum);
            windowSlider.SetValueWithoutNotify(Mathf.InverseLerp(minimumViewportScale, maximumViewportScale, scale));
            windowSlider.interactable = maximumViewportScale > minimumViewportScale;
            windowValue.text = ViewportScale.ToString("0.00", CultureInfo.InvariantCulture) + "×";
        }

        // Programmatic sizing only; no player resize handle or wheel binding.
        public void SetNotebookScale(float scale)
        {
            notebookScale = Mathf.Max(0.01f, scale);
            book.localScale = Vector3.one * notebookScale;
        }

        public void SetMasterVolume(float value)
        {
            PlayerPrefs.SetFloat(MasterKey, value);
            masterValue.text = Mathf.RoundToInt(value * 100f) + "%";
            MasterVolumeChanged?.Invoke(value);
        }

        public void SetOperationVolume(float value)
        {
            PlayerPrefs.SetFloat(OperationKey, value);
            operationValue.text = Mathf.RoundToInt(value * 100f) + "%";
            OperationVolumeChanged?.Invoke(value);
        }

        public void SetBgmVolume(float value)
        {
            PlayerPrefs.SetFloat(BgmKey, value);
            bgmValue.text = Mathf.RoundToInt(value * 100f) + "%";
            BgmVolumeChanged?.Invoke(value);
        }

        private void RefreshValues()
        {
            windowValue.text = ViewportScale.ToString("0.00", CultureInfo.InvariantCulture) + "×";
            masterValue.text = Mathf.RoundToInt(masterSlider.value * 100f) + "%";
            operationValue.text = Mathf.RoundToInt(operationSlider.value * 100f) + "%";
            bgmValue.text = Mathf.RoundToInt(bgmSlider.value * 100f) + "%";
        }

        private void ShowPage(bool audio, bool immediate)
        {
            if (transition != null) StopCoroutine(transition);
            transition = null;
            CanvasGroup incoming = audio ? audioPage : windowPage;
            CanvasGroup outgoing = audio ? windowPage : audioPage;
            outgoing.alpha = 0f;
            outgoing.interactable = false;
            outgoing.blocksRaycasts = false;
            incoming.gameObject.SetActive(true);
            outgoing.gameObject.SetActive(false);
            incoming.interactable = true;
            incoming.blocksRaycasts = true;

            // The selected side tab projects farther from the notebook edge.
            UpdateTabArtwork(windowTab, windowSelectedArtwork, windowInactiveArtwork, !audio);
            UpdateTabArtwork(audioTab, audioSelectedArtwork, audioInactiveArtwork, audio);
            windowTitle.color = audio ? new Color(0.38f, 0.40f, 0.33f) : new Color(0.23f, 0.31f, 0.22f);
            audioTitle.color = audio ? new Color(0.23f, 0.31f, 0.22f) : new Color(0.38f, 0.40f, 0.33f);

            RectTransform pageRect = (RectTransform)incoming.transform;
            if (immediate || !gameObject.activeInHierarchy)
            {
                incoming.alpha = 1f;
                pageRect.anchoredPosition = Vector2.zero;
            }
            else
            {
                incoming.alpha = 0f;
                pageRect.anchoredPosition = new Vector2(0f, -18f);
                transition = StartCoroutine(AnimatePage(incoming, pageRect));
            }
        }

        private static void UpdateTabArtwork(RectTransform tab, RawImage selectedArtwork, RawImage inactiveArtwork, bool selected)
        {
            float width = selected ? 155f : 100f;
            float height = selected ? 40f : 43f;
            tab.sizeDelta = new Vector2(width, height);
            tab.anchoredPosition = new Vector2(360f + width * 0.5f, tab.anchoredPosition.y);
            RectTransform caption = tab.GetComponentInChildren<Text>(true).rectTransform;
            caption.anchoredPosition = new Vector2(selected ? 13f : 11f, caption.anchoredPosition.y);
            if (selectedArtwork != null) selectedArtwork.gameObject.SetActive(selected);
            if (inactiveArtwork != null) inactiveArtwork.gameObject.SetActive(!selected);
            tab.GetComponent<Button>().targetGraphic = selected ? selectedArtwork : inactiveArtwork;
        }

        private IEnumerator AnimatePage(CanvasGroup page, RectTransform pageRect)
        {
            float elapsed = 0f;
            while (elapsed < transitionSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, transitionSeconds));
                t = 1f - (1f - t) * (1f - t);
                page.alpha = t;
                pageRect.anchoredPosition = new Vector2(0f, Mathf.Lerp(-18f, 0f, t));
                yield return null;
            }
            page.alpha = 1f;
            pageRect.anchoredPosition = Vector2.zero;
            transition = null;
        }
    }
}
