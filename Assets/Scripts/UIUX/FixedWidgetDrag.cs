using System;
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
        public event Action DragStarted;
        public event Action DragEnded;
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
            if (dragging) DragStarted?.Invoke();
        }
        public void OnDrag(PointerEventData data)
        {
            if (!dragging || !parent) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, data.position,
                data.pressEventCamera, out var point)) return;
            target.anchoredPosition += point - previous;
            previous = point;
            ClampToParent();
        }
        public void OnEndDrag(PointerEventData data) => StopDragging();
        private void OnApplicationFocus(bool focused) { if (!focused) StopDragging(); }
        private void OnDisable() => StopDragging();
        private void StopDragging()
        {
            if (!dragging) return;
            dragging = false;
            DragEnded?.Invoke();
        }
        private void ClampToParent()
        {
            if (!target || !parent) return;
            var parentRect = parent.rect;
            var size = Vector2.Scale(target.rect.size, new Vector2(
                Mathf.Abs(target.localScale.x), Mathf.Abs(target.localScale.y)));
            var anchor = new Vector2(
                Mathf.Lerp(parentRect.xMin, parentRect.xMax, target.anchorMin.x),
                Mathf.Lerp(parentRect.yMin, parentRect.yMax, target.anchorMin.y));
            var min = parentRect.min + Vector2.Scale(size, target.pivot) - anchor;
            var max = parentRect.max - Vector2.Scale(size, Vector2.one - target.pivot) - anchor;
            var position = target.anchoredPosition;
            target.anchoredPosition = new Vector2(
                min.x <= max.x ? Mathf.Clamp(position.x, min.x, max.x) : (min.x + max.x) * .5f,
                min.y <= max.y ? Mathf.Clamp(position.y, min.y, max.y) : (min.y + max.y) * .5f);
        }
    }
}
