using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Hiking.Journey
{
    /// <summary>A canvas widget that can be moved independently of the game frame.</summary>
    public sealed class FloatingWidgetController : MonoBehaviour, IPointerDownHandler,
        IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] Color hoverColor = new Color(.34f, .54f, .42f, .98f);

        RectTransform widget;
        Image background;
        Color normalColor;
        WindowsTransparentWindow transparentWindow;
        bool hovered;
        bool dragging;

        void Awake()
        {
            widget = GetComponent<RectTransform>();
            background = GetComponent<Image>();
            if (background != null) normalColor = background.color;
        }

        public void OnPointerDown(PointerEventData eventData) { }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            UpdateColor();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            UpdateColor();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            dragging = true;
            transparentWindow = FindFirstObjectByType<WindowsTransparentWindow>();
            if (transparentWindow != null) transparentWindow.BeginFloatingWidgetDrag();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging || widget == null || !(widget.parent is RectTransform parent)) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,
                    eventData.position, eventData.pressEventCamera, out Vector2 current) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,
                    eventData.position - eventData.delta, eventData.pressEventCamera,
                    out Vector2 previous)) return;
            widget.anchoredPosition += current - previous;
            ClampToCanvas();
        }

        public void OnEndDrag(PointerEventData eventData) => StopDragging();

        void LateUpdate()
        {
            if (dragging && (Mouse.current == null || !Mouse.current.leftButton.isPressed))
                StopDragging();
            ClampToCanvas();
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused) StopDragging();
        }

        void OnDisable()
        {
            StopDragging();
            hovered = false;
            UpdateColor();
        }

        void StopDragging()
        {
            if (!dragging) return;
            dragging = false;
            if (transparentWindow != null) transparentWindow.EndFloatingWidgetDrag();
            transparentWindow = null;
        }

        void UpdateColor()
        {
            if (background != null) background.color = hovered ? hoverColor : normalColor;
        }

        void ClampToCanvas()
        {
            if (widget == null || !(widget.parent is RectTransform parent)) return;
            Rect bounds = parent.rect;
            Rect own = widget.rect;
            Vector2 anchor = new Vector2(
                Mathf.Lerp(bounds.xMin, bounds.xMax, widget.anchorMin.x),
                Mathf.Lerp(bounds.yMin, bounds.yMax, widget.anchorMin.y));
            float minX = bounds.xMin + own.width * widget.pivot.x - anchor.x;
            float maxX = bounds.xMax - own.width * (1f - widget.pivot.x) - anchor.x;
            float minY = bounds.yMin + own.height * widget.pivot.y - anchor.y;
            float maxY = bounds.yMax - own.height * (1f - widget.pivot.y) - anchor.y;
            Vector2 position = widget.anchoredPosition;
            widget.anchoredPosition = new Vector2(
                Mathf.Clamp(position.x, minX, maxX),
                Mathf.Clamp(position.y, minY, maxY));
        }
    }
}
