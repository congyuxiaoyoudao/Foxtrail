using UnityEngine;

namespace Hiking.UIUX
{
    // Host injects the frame. No scene search, game-state access or frame resizing.
    [DisallowMultipleComponent]
    public sealed class AllocationFrameDock : MonoBehaviour
    {
        public RectTransform Frame;
        [Tooltip("Visible wood bounds within the frame texture (normalized, bottom-left origin). Excludes transparent margins.")]
        public Rect ArtworkBounds = new Rect(.03f, .09f, .95f, .80f);
        [Tooltip("Left inner edge of the frame as a fraction of texture width.")]
        public float InnerLeft = .085f;
        private const float WidthRatio = 1f / 3f;
        // Authored board ends 15 UI units above the 270-unit panel rectangle.
        private const float BoardBottomInset = 15f;
        [Min(0)] public float RevealSeconds = 0.24f;
        private ResourceAllocationPanel panel;
        private RectTransform surface;
        private CanvasGroup group;
        private Vector3 baseScale;
        private Vector2 shownPosition;
        private float slideDistance;
        private float elapsed;
        private bool closing;
        private readonly Vector3[] corners = new Vector3[4];
        public void SetFrame(RectTransform frame) { Frame = frame; FollowFrame(); }
        private void Awake()
        {
            panel = GetComponent<ResourceAllocationPanel>();
            surface = (RectTransform)panel.Surface.transform;
            baseScale = surface.localScale;
            // A fixed aperture ends at the frame's wood edge. Move the intact
            // artwork behind it instead of squeezing its text, icons and buttons.
            var clip = new GameObject("SlideAperture", typeof(RectTransform), typeof(UnityEngine.UI.RectMask2D));
            var clipRect = (RectTransform)clip.transform;
            clipRect.SetParent(transform, false);
            clipRect.anchorMin = clipRect.anchorMax = clipRect.pivot = new Vector2(0, 1);
            const float padding = 20f;
            clipRect.anchoredPosition = new Vector2(-padding, padding);
            clipRect.sizeDelta = new Vector2(surface.rect.width + padding * 2,
                surface.rect.height - BoardBottomInset + padding);
            var authoredPosition = surface.anchoredPosition;
            surface.SetParent(clipRect, false);
            surface.anchoredPosition = authoredPosition + new Vector2(padding, -padding);
            shownPosition = surface.anchoredPosition;
            slideDistance = clipRect.rect.height;
            group = surface.GetComponent<CanvasGroup>();
            if (!group) group = surface.gameObject.AddComponent<CanvasGroup>();
        }
        private void OnEnable()
        {
            panel.EditingStarted += Reveal;
            panel.Closing += Retract;
        }
        private void OnDisable()
        {
            panel.EditingStarted -= Reveal;
            panel.Closing -= Retract;
            ResetVisual(default, null);
        }
        private void Reveal(AllocationMode mode, string point)
        {
            elapsed = 0;
            closing = false;
            FollowFrame();
            ApplyReveal(0);
        }
        private void Retract() { closing = true; group.interactable = false; }
        private void ResetVisual(AllocationMode mode, string point)
        {
            surface.localScale = baseScale;
            surface.anchoredPosition = shownPosition;
            group.alpha = 1;
            group.interactable = true;
        }
        private void LateUpdate()
        {
            if (!panel.Surface.activeSelf) return;
            FollowFrame();
            elapsed = Mathf.Clamp(elapsed + (closing ? -1 : 1) * Time.unscaledDeltaTime, 0, RevealSeconds);
            ApplyReveal(RevealSeconds <= 0 ? 1 : Mathf.Clamp01(elapsed / RevealSeconds));
            if (closing && (elapsed <= 0 || RevealSeconds <= 0))
            {
                panel.FinishClose();
                closing = false;
                ResetVisual(default, null);
            }
        }
        private void ApplyReveal(float t)
        {
            float eased = 1 - Mathf.Pow(1 - t, 3);
            surface.localScale = baseScale;
            surface.anchoredPosition = shownPosition + Vector2.down * (slideDistance * (1 - eased));
            group.alpha = 1;
            group.interactable = !closing && t >= 1;
        }
        private void FollowFrame()
        {
            if (!Frame) return;
            var rect = (RectTransform)transform;
            var parent = rect.parent as RectTransform;
            var canvas = GetComponentInParent<Canvas>();
            var frameCanvas = Frame.GetComponentInParent<Canvas>();
            if (!parent || !canvas || !frameCanvas) return;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var frameCamera = frameCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : frameCanvas.worldCamera;
            var fr = Frame.rect;
            var left = Frame.TransformPoint(new Vector3(fr.xMin + fr.width * ArtworkBounds.xMin,
                fr.yMin + fr.height * ArtworkBounds.yMax, 0));
            var right = Frame.TransformPoint(new Vector3(fr.xMin + fr.width * ArtworkBounds.xMax,
                fr.yMin + fr.height * ArtworkBounds.yMax, 0));
            var screen = RectTransformUtility.WorldToScreenPoint(frameCamera, left);
            var rightScreen = RectTransformUtility.WorldToScreenPoint(frameCamera, right);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, camera, out var frameLeft);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, rightScreen, camera, out var frameRight);
            float scale = Vector2.Distance(frameLeft, frameRight) * WidthRatio / Mathf.Max(.001f, rect.rect.width);
            rect.localScale = Vector3.one * scale;
            rect.GetWorldCorners(corners);
            var current = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            var desired = frameLeft + (frameRight - frameLeft) * ((InnerLeft - ArtworkBounds.xMin) / ArtworkBounds.width)
                - Vector2.up * (BoardBottomInset * scale);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, current, camera, out var previous);
            rect.anchoredPosition += desired - previous;
        }
    }
}
