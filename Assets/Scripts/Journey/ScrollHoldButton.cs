using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hiking.Journey
{
    // A quick press moves one station. Holding a pointer scrolls until release or an edge.
    public sealed class ScrollHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] int direction = 1;
        [SerializeField, Min(.1f)] float holdDelay = .28f;
        GameFlowController flow;
        Button button;
        bool pressed, held;
        float pressedAt;

        public void Configure(GameFlowController owner, int scrollDirection)
        {
            flow = owner;
            direction = scrollDirection < 0 ? -1 : 1;
            button = GetComponent<Button>();
        }
        public void OnPointerDown(PointerEventData eventData)
        {
            if (flow == null || !flow.IsBrowsingPanorama || button == null || !button.interactable) return;
            pressed = true; held = false; pressedAt = Time.unscaledTime;
        }
        public void OnPointerUp(PointerEventData eventData)
        {
            if (pressed && !held && flow != null && flow.IsBrowsingPanorama)
                flow.Camera.PanScrollBySection(direction);
            pressed = false; held = false;
        }
        void OnDisable() { pressed = false; held = false; }
        void Update()
        {
            if (!pressed || flow == null || !flow.IsBrowsingPanorama || button == null || !button.interactable) return;
            if (Time.unscaledTime - pressedAt < holdDelay) return;
            held = true;
            flow.Camera.PanScroll(direction * flow.Map.ViewWidth *
                Mathf.Max(.1f, flow.Config.panoramaScrollStationsPerSecond) * Time.unscaledDeltaTime);
        }
    }
}
