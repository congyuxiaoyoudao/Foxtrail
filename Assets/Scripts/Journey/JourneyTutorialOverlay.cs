using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hiking.Journey
{
    // The layout is authored under GameScene/Canvas/FrameRoot/JourneyTutorial.
    [DefaultExecutionOrder(1000)]
    public sealed class JourneyTutorialOverlay : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] Image blackOverlay;
        [SerializeField] Text message;
        [SerializeField] Text continueHint;
        [SerializeField] TutorialArrowGraphic arrow;
        [SerializeField] RectTransform gameFrame;

        static readonly string[] Messages =
        {
            "点击木牌，单独设置本点的投放量",
            "展开旅途手记\n随时查看当前持有的种子和水分",
            "小tips:设置预设，沿途自动投放\n生效后,已单独设置的点位资源不受影响"
        };

        GameFlowController flow;
        TravelUIBridge travelUI;
        int step;
        int shownFrame;
        bool shownThisRun;
        Vector3 frameOrigin;

        public void Initialize(GameFlowController owner)
        {
            flow = owner;
            travelUI = owner.UI.GetComponent<TravelUIBridge>();
            if (gameFrame == null && transform.parent != null)
                gameFrame = transform.parent.Find("GameFrame") as RectTransform;
            shownThisRun = false;
            if (blackOverlay != null) blackOverlay.gameObject.SetActive(false);
            gameObject.SetActive(false);
        }

        public bool TryShow()
        {
            if (flow == null || shownThisRun) return false;
            shownThisRun = true;
            step = 0;
            shownFrame = Time.frameCount;
            if (blackOverlay != null) blackOverlay.gameObject.SetActive(true);
            gameObject.SetActive(true);
            ShowStep();
            return true;
        }

        void ShowStep()
        {
            frameOrigin = transform.parent != null ? transform.parent.position : Vector3.zero;
            message.text = Messages[step];
            continueHint.text = "点击画面内任意位置继续";
            travelUI?.PreviewTutorialHud(step > 0);
            UpdateArrow();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left ||
                Time.frameCount == shownFrame) return;
            if (++step < Messages.Length)
            {
                ShowStep();
                return;
            }
            Dismiss();
            flow.ResumeAfterTutorial();
        }

        public void Dismiss()
        {
            travelUI?.PreviewTutorialHud(false);
            gameObject.SetActive(false);
        }

        void OnDisable()
        {
            if (blackOverlay != null) blackOverlay.gameObject.SetActive(false);
        }

        void LateUpdate() => UpdateArrow();

        void UpdateArrow()
        {
            if (arrow == null || message == null || flow == null) return;
            Vector2 target;
            if (step == 0)
            {
                var slots = flow.CurrentStation?.slots;
                if (slots == null || slots.Length == 0 || slots[0].Sign == null) return;
                target = slots[0].Sign.ScreenPosition(flow.Camera.WorldCamera);
            }
            else if (!travelUI.TryGetTutorialTarget(step == 1, out target)) return;

            var bounds = message.rectTransform.parent as RectTransform;
            if (bounds == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    bounds, target, null, out Vector2 localTarget)) return;
            // The HUD stays on the desktop while FrameRoot moves. Cancel that frame motion
            // for label placement; the arrow still uses the HUD's live screen position.
            if (step > 0 && transform.parent != null)
            {
                Vector3 frameTravel = bounds.InverseTransformVector(
                    transform.parent.position - frameOrigin);
                localTarget += new Vector2(frameTravel.x, frameTravel.y);
            }

            Rect safeArea = bounds.rect;
            if (gameFrame != null)
            {
                var frameCorners = new Vector3[4];
                gameFrame.GetWorldCorners(frameCorners);
                Vector3 lowerLeft = bounds.InverseTransformPoint(frameCorners[0]);
                Vector3 upperRight = bounds.InverseTransformPoint(frameCorners[2]);
                float frameWidth = upperRight.x - lowerLeft.x;
                float frameHeight = upperRight.y - lowerLeft.y;
                float insetX = frameWidth * .075f;
                safeArea = Rect.MinMaxRect(lowerLeft.x + insetX,
                    lowerLeft.y + frameHeight * .15f,
                    upperRight.x - insetX, upperRight.y - frameHeight * .13f);
            }
            if (continueHint != null)
            {
                var hint = continueHint.rectTransform;
                hint.anchoredPosition = new Vector2(0f, safeArea.yMin + 30f);
            }

            var label = message.rectTransform;
            float margin = 24f;
            float width = Mathf.Min(step == 0 ? 760f : 640f, safeArea.width - margin * 2f);
            float height = step == 0 ? 105f : step == 1 ? 150f : 180f;
            float left = step == 0 ? localTarget.x - 45f : localTarget.x - width + 80f;
            float centerY = step == 0 ? localTarget.y + 135f : localTarget.y - 145f;
            left = Mathf.Clamp(left, safeArea.xMin + margin,
                safeArea.xMax - width - margin);
            centerY = Mathf.Clamp(centerY,
                safeArea.yMin + height * .5f + 90f,
                safeArea.yMax - height * .5f - margin);
            label.anchorMin = label.anchorMax = new Vector2(.5f, .5f);
            label.pivot = new Vector2(0f, .5f);
            label.sizeDelta = new Vector2(width, height);
            label.anchoredPosition = new Vector2(left, centerY);
            message.alignment = TextAnchor.MiddleLeft;

            Vector3 labelStart = step == 0
                ? new Vector3(Mathf.Min(250f, width * .35f), -height * .43f, 0f)
                : new Vector3(width - 145f, height * .43f, 0f);
            Vector2 start = RectTransformUtility.WorldToScreenPoint(null,
                label.TransformPoint(labelStart));
            var rect = arrow.rectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, start, null,
                    out Vector2 localStart) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, target, null,
                    out Vector2 localEnd)) return;
            Vector2 delta = localEnd - localStart;
            Vector2 control = step == 0
                ? localStart + new Vector2(28f, delta.y * .85f)
                : localStart + new Vector2(delta.x * .15f - 35f, delta.y * .85f);
            arrow.SetCurve(localStart, control, localEnd);
        }
    }
}
