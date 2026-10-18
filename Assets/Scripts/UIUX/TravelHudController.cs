using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Hiking.UIUX
{
    public sealed class TravelHudController : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ICanvasRaycastFilter
    {
        public RectTransform Header;
        public TravelCameraLens CameraLens;
        public RectTransform CameraTopButton;
        public RectTransform Pattern;
        public RectTransform HeaderContent;
        public RectTransform Resources;
        public RectTransform Operations;
        public RectTransform[] FrameParts;
        public CanvasGroup ResourceInput;
        public CanvasGroup OperationInput;
        public UnityEngine.UI.Text StationLabel;
        public UnityEngine.UI.Text SeedLabel;
        public UnityEngine.UI.Text WaterLabel;
        public UnityEvent PresetRequested = new UnityEvent();
        public UnityEvent SettingsRequested = new UnityEvent();
        public UnityEvent ExitRequested = new UnityEvent();
        public UnityEvent SeedDetailsRequested = new UnityEvent();
        public UnityEvent WaterDetailsRequested = new UnityEvent();
        [SerializeField] private float exitDelay = 0.25f;
        [SerializeField] private float enterDelay = 0.08f;
        private float timeline;
        private float pointerChangedAt;
        private bool pointerInside;
        private bool keepOpen;
        private bool? preview;
        public float Timeline => timeline;
        private void Awake()
        {
            if (!GetComponent<FixedWidgetDrag>()) gameObject.AddComponent<FixedWidgetDrag>();
        }
        public void SetWidgetScale(float scale) => transform.localScale = Vector3.one * Mathf.Max(0.01f, scale);
        public void ResetToClosed() { timeline=0; Apply(); }

        private void OnEnable() { timeline = 0; pointerInside = false; preview = null; Apply(); }
        private void OnDisable() { preview = null; keepOpen = false; pointerInside = false; }
        private void Update()
        {
            bool target = preview ?? (keepOpen || pointerInside);
            bool waiting = !preview.HasValue && !keepOpen && Time.unscaledTime - pointerChangedAt < (pointerInside ? enterDelay : exitDelay);
            if (!waiting) timeline = Mathf.MoveTowards(timeline, target ? TravelHudMotion.Duration : 0f, Time.unscaledDeltaTime);
            Apply();
        }
        public void OnPointerEnter(PointerEventData data) { pointerInside = true; pointerChangedAt = Time.unscaledTime; }
        public void OnPointerExit(PointerEventData data) { pointerInside = false; pointerChangedAt = Time.unscaledTime; }
        public void SetKeepOpen(bool value) { keepOpen = value; pointerChangedAt = Time.unscaledTime; }
        public void SetPreview(bool value) { preview = value; }
        public void ResumeHover() { preview = null; pointerChangedAt = Time.unscaledTime; }
        public void ShowPreset() { PresetRequested.Invoke(); }
        public void ShowSettings() { SettingsRequested.Invoke(); }
        // The host owns saving and quitting; the widget only requests exit.
        public void RequestExit() { ExitRequested.Invoke(); }
        public void ShowSeedDetails() { SeedDetailsRequested.Invoke(); }
        public void ShowWaterDetails() { WaterDetailsRequested.Invoke(); }
        public void SetValues(int station, int total, int seed, int water)
        {
            StationLabel.text = "第 " + station.ToString("00") + " / " + total + " 站";
            SeedLabel.text = seed.ToString(); WaterLabel.text = water.ToString();
        }
        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (CameraTopButton && CameraTopButton.gameObject.activeInHierarchy &&
                RectTransformUtility.RectangleContainsScreenPoint(CameraTopButton, screenPoint, eventCamera)) return true;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, screenPoint, eventCamera, out Vector2 p)) return false;
            p.y = -p.y;
            Vector3 phase = TravelHudMotion.Evaluate(timeline);
            float x = Mathf.Lerp(288, 0, phase.x);
            if (new Rect(x, 0, 532-x, 244).Contains(p)) return true;
            if (phase.y > 0 && new Rect(22, 244, 488, Mathf.Max(0, Mathf.Lerp(72,236,phase.y)+164-244)).Contains(p)) return true;
            return phase.z > 0 && new Rect(26, 400, 480, Mathf.Max(0, Mathf.Lerp(248,392,phase.z)+144-400)).Contains(p);
        }
        private void Apply()
        {
            if (!Header) return;
            Vector3 phase = TravelHudMotion.Evaluate(timeline);
            float left = Mathf.Lerp(288, 0, phase.x), width = 532-left;
            if (CameraLens) CameraLens.Apply(phase.x);
            if (CameraTopButton) CameraTopButton.anchoredPosition = new Vector2(left + 19, 28);
            Header.anchoredPosition = new Vector2(left,0); Header.sizeDelta = new Vector2(width,244);
            Pattern.anchoredPosition = new Vector2(-left,0);
            HeaderContent.anchoredPosition = new Vector2(-left,0);
            float[] xs = {0,38,width-38,width}, ys = {0,38,206,244};
            int part=0;
            for(int row=0;row<3;row++) for(int col=0;col<3;col++)
            {
                if(row==1 && col==1) continue;
                RectTransform rect=FrameParts[part++];
                rect.anchoredPosition=new Vector2(xs[col],-ys[row]);
                rect.sizeDelta=new Vector2(xs[col+1]-xs[col],ys[row+1]-ys[row]);
            }
            Resources.gameObject.SetActive(phase.y>0);
            Operations.gameObject.SetActive(phase.z>0);
            Resources.anchoredPosition=new Vector2(22,-Mathf.Lerp(72,236,phase.y));
            Operations.anchoredPosition=new Vector2(26,-Mathf.Lerp(248,392,phase.z));
            ResourceInput.interactable=ResourceInput.blocksRaycasts=phase.y>=1;
            OperationInput.interactable=OperationInput.blocksRaycasts=phase.z>=1;
        }
    }
}
