using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hiking.Journey
{
    public sealed class UiSoundFeedback : MonoBehaviour
    {
        float lastHoverTime = -10f;
        Button suppressedClick;

        public void Initialize(Button clickSuppressedButton = null)
        {
            suppressedClick = clickSuppressedButton;
            foreach (var button in GetComponentsInChildren<Button>(true))
            {
                if (button.GetComponent<UiSoundMarker>() != null) continue;
                button.gameObject.AddComponent<UiSoundMarker>();
                var trigger = button.gameObject.AddComponent<EventTrigger>();
                var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enter.callback.AddListener(_ => PlayHover(button));
                trigger.triggers.Add(enter);
                var click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
                click.callback.AddListener(_ => PlayClick(button));
                trigger.triggers.Add(click);
            }
        }

        void PlayHover(Button button)
        {
            if (!button.IsInteractable()) return;
            float cooldown = GameAudio.Instance != null && GameAudio.Instance.Catalog != null ?
                GameAudio.Instance.Catalog.hoverCooldownSeconds : .08f;
            if (Time.unscaledTime - lastHoverTime < cooldown) return;
            lastHoverTime = Time.unscaledTime;
            GameAudio.Instance?.Play("ui_hover", .9f);
        }

        void PlayClick(Button button)
        {
            if (button == suppressedClick || !button.IsInteractable()) return;
            GameAudio.Instance?.Play("ui_click");
        }
    }

    // Marker prevents duplicate EventTrigger components when the UI is initialized again.
    internal sealed class UiSoundMarker : MonoBehaviour { }
}
