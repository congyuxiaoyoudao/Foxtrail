using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hiking.Journey
{
    // The layout is authored under GameScene/Canvas/FrameRoot/JourneyTutorial.
    public sealed class JourneyTutorialOverlay : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] Image blackOverlay;
        [SerializeField] Text message;
        [SerializeField] Text continueHint;
        [SerializeField] TutorialArrowGraphic arrow;

        static readonly string[] Messages =
        {
            "点击木牌，单独设置本点的投放量",
            "展开旅途手记\n随时查看当前持有的种子和水分",
            "设置预设，沿途自动投放。\n只对未单独设置的点位生效，\n不会重设已经单独设置的点位。"
        };

        GameFlowController flow;
        TravelUIBridge travelUI;
        int step;
        int shownFrame;
        bool shownThisRun;

        public void Initialize(GameFlowController owner)
        {
            flow = owner;
            travelUI = owner.UI.GetComponent<TravelUIBridge>();
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
            travelUI?.PreviewTutorialHud(false);
            gameObject.SetActive(false);
            flow.ResumeAfterTutorial();
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

            var corners = new Vector3[4];
            message.rectTransform.GetWorldCorners(corners);
            Vector3 origin = step == 0 ? corners[0] : corners[2];
            Vector2 start = RectTransformUtility.WorldToScreenPoint(null, origin);
            var rect = arrow.rectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, start, null,
                    out Vector2 localStart) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, target, null,
                    out Vector2 localEnd)) return;
            arrow.SetEndpoints(localStart, localEnd);
        }
    }
}
