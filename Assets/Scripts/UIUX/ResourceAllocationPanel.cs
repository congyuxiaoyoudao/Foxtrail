using System;
using UnityEngine;

namespace Hiking.UIUX
{
    public enum AllocationMode { Preset, Point }

    public readonly struct AllocationResult
    {
        public readonly AllocationMode Mode;
        public readonly string PointId;
        public readonly int Seed;
        public readonly int Water;
        public AllocationResult(AllocationMode mode, string pointId, int seed, int water)
        { Mode = mode; PointId = pointId; Seed = seed; Water = water; }
    }

    // Presentation only: edits a draft, never spends resources or persists point state.
    public sealed class ResourceAllocationPanel : MonoBehaviour
    {
        public GameObject Surface;
        public UnityEngine.UI.Text Heading;
        public UnityEngine.UI.Text Description;
        public UnityEngine.UI.Text SeedValue;
        public UnityEngine.UI.Text WaterValue;
        public UnityEngine.UI.Slider SeedSlider;
        public UnityEngine.UI.Slider WaterSlider;
        public UnityEngine.UI.Image SeedFill;
        public UnityEngine.UI.Image WaterFill;
        public UnityEngine.UI.Button ConfirmButton;
        public UnityEngine.UI.Button CancelButton;
        public event Action<AllocationResult> Confirmed;
        public event Action Cancelled;
        public event Action<AllocationMode, string> EditingStarted;
        public event Action<AllocationMode, string> EditingEnded;
        public event Action Closing;
        private bool closing;
        public bool IsOpen => Surface && Surface.activeSelf && !closing;
        public AllocationMode Mode { get; private set; }
        public string PointId { get; private set; }

        private void Awake()
        {
            SeedSlider.onValueChanged.AddListener(OnValueChanged);
            WaterSlider.onValueChanged.AddListener(OnValueChanged);
            ConfirmButton.onClick.AddListener(Confirm);
            CancelButton.onClick.AddListener(Cancel);
        }
        private void OnDestroy()
        {
            SeedSlider.onValueChanged.RemoveListener(OnValueChanged);
            WaterSlider.onValueChanged.RemoveListener(OnValueChanged);
            ConfirmButton.onClick.RemoveListener(Confirm);
            CancelButton.onClick.RemoveListener(Cancel);
        }
        public void Open(AllocationMode mode, string pointId, int seed, int water,
            int seedMaximum, int waterMaximum, string heading = null, string description = null)
        {
            if (mode == AllocationMode.Point && string.IsNullOrWhiteSpace(pointId))
                throw new ArgumentException("Point mode requires a stable point ID.", nameof(pointId));
            // Repeated clicks on the current entry do not discard an in-progress draft.
            if (IsOpen && Mode == mode && PointId == (mode == AllocationMode.Point ? pointId : null)) return;
            if (IsOpen) Cancel();
            Mode = mode; PointId = mode == AllocationMode.Point ? pointId : null;
            Heading.text = heading ?? (mode == AllocationMode.Preset ? "投放预设" : "本点投放");
            Description.text = description ?? (mode == AllocationMode.Preset
                ? "未单独设置的点位均适用" : "仅本点生效，不随预设改变");
            SetRange(SeedSlider, seedMaximum, seed);
            SetRange(WaterSlider, waterMaximum, water);
            closing = false;
            Surface.SetActive(true);
            RefreshValues();
            EditingStarted?.Invoke(Mode, PointId);
        }
        private void OnDisable() { if (IsOpen) Cancel(); FinishClose(); }
        public void FinishClose() { if (Surface) Surface.SetActive(false); closing = false; }
        private void BeginClose()
        {
            closing = true;
            if (Closing != null) Closing.Invoke();
            else FinishClose();
        }

        // Host supplies the sign/frame anchor in screen pixels; call again when it moves.
        public void PositionAboveLeft(Vector2 anchorScreen, float gapPixels = 12f)
        {
            var rect = (RectTransform)transform;
            var parent = rect.parent as RectTransform;
            var canvas = GetComponentInParent<Canvas>();
            if (!parent || !canvas) return;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var bottomLeft = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            var topRight = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            var size = topRight - bottomLeft;
            var wanted = anchorScreen + new Vector2(-size.x - gapPixels, gapPixels);
            wanted.x = Mathf.Clamp(wanted.x, 0, Mathf.Max(0, Screen.width - size.x));
            wanted.y = Mathf.Clamp(wanted.y, 0, Mathf.Max(0, Screen.height - size.y));
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, bottomLeft, camera, out var oldLocal);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, wanted, camera, out var newLocal);
            rect.anchoredPosition += newLocal - oldLocal;
        }
        public void RefreshLimits(int seedMaximum, int waterMaximum)
        {
            SetRange(SeedSlider, seedMaximum, Mathf.RoundToInt(SeedSlider.value));
            SetRange(WaterSlider, waterMaximum, Mathf.RoundToInt(WaterSlider.value));
            RefreshValues();
        }
        private static void SetRange(UnityEngine.UI.Slider slider, int maximum, int value)
        {
            slider.wholeNumbers = true; slider.minValue = 0;
            slider.maxValue = Mathf.Max(0, maximum);
            slider.SetValueWithoutNotify(Mathf.Clamp(value, 0, Mathf.Max(0, maximum)));
            slider.interactable = maximum > 0;
        }
        private void OnValueChanged(float unused) => RefreshValues();
        public void RefreshValues()
        {
            SeedValue.text = Mathf.RoundToInt(SeedSlider.value) + " 份";
            WaterValue.text = Mathf.RoundToInt(WaterSlider.value) + " 份";
            UpdateRoundedFill(SeedFill, SeedSlider);
            UpdateRoundedFill(WaterFill, WaterSlider);
        }
        private static void UpdateRoundedFill(UnityEngine.UI.Image image, UnityEngine.UI.Slider slider)
        {
            float ratio = slider.maxValue > 0 ? slider.value / slider.maxValue : 0;
            image.enabled = ratio > 0;
            var area = (RectTransform)slider.handleRect.parent;
            float width = area.rect.width * ratio;
            // Below one cap diameter shrink the whole capsule; do not overstate the value.
            float height = Mathf.Min(11f, width);
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            image.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            if (height > 0) image.pixelsPerUnitMultiplier = image.sprite.rect.height / height;
        }
        public void Confirm()
        {
            if (!IsOpen) return;
            var result = new AllocationResult(Mode, PointId,
                Mathf.RoundToInt(SeedSlider.value), Mathf.RoundToInt(WaterSlider.value));
            BeginClose();
            try { Confirmed?.Invoke(result); }
            finally { EditingEnded?.Invoke(result.Mode, result.PointId); }
        }
        public void Cancel()
        {
            if (!IsOpen) return;
            BeginClose();
            var mode = Mode;
            var pointId = PointId;
            try { Cancelled?.Invoke(); }
            finally { EditingEnded?.Invoke(mode, pointId); }
        }
    }
}
