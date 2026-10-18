using UnityEngine;
using UnityEngine.EventSystems;

namespace Hiking.UIUX
{
    // Drag the widget body only. Selectables retain their own pointer interactions.
    [DisallowMultipleComponent]
    public sealed class FixedWidgetDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private RectTransform target;
        private RectTransform parent;
        private Vector2 previous;
        private bool dragging;
        public void OnBeginDrag(PointerEventData data)
        {
            dragging = false;
            if (data.button != PointerEventData.InputButton.Left) return;
            var hit = data.pointerPressRaycast.gameObject;
            if (hit && hit.GetComponentInParent<UnityEngine.UI.Selectable>(true)) return;
            target = transform as RectTransform;
            parent = target ? target.parent as RectTransform : null;
            dragging = parent && RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,
                data.position, data.pressEventCamera, out previous);
        }
        public void OnDrag(PointerEventData data)
        {
            if (!dragging || !parent) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, data.position,
                data.pressEventCamera, out var point)) return;
            target.anchoredPosition += point - previous;
            previous = point;
        }
        public void OnEndDrag(PointerEventData data) => dragging = false;
        private void OnDisable() => dragging = false;
    }
}
