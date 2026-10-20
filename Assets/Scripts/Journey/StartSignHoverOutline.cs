using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hiking.Journey
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button), typeof(Image))]
    public sealed class StartSignHoverOutline : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] GameUIController uiController;
        [SerializeField, HideInInspector] Material silhouetteMaterial;

        Button button;
        Image signImage;
        RectTransform signRect;
        Image silhouetteImage;
        RectTransform silhouetteRect;
        Outline outline;
        bool hovered;

        void Awake()
        {
            button = GetComponent<Button>();
            signImage = GetComponent<Image>();
            signRect = (RectTransform)transform;
            if (silhouetteMaterial == null) return;

            var silhouette = new GameObject("HoverWhiteOutline", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            silhouetteRect = (RectTransform)silhouette.transform;
            silhouetteRect.SetParent(signRect.parent, false);
            silhouetteRect.SetSiblingIndex(signRect.GetSiblingIndex());
            SyncVisual();
            silhouetteImage = silhouette.GetComponent<Image>();
            silhouetteImage.sprite = signImage.sprite;
            silhouetteImage.preserveAspect = signImage.preserveAspect;
            silhouetteImage.material = silhouetteMaterial;
            silhouetteImage.raycastTarget = false;
            outline = silhouette.GetComponent<Outline>();
            outline.effectColor = Color.white;
            outline.useGraphicAlpha = false;
            SyncWidth();
            silhouette.SetActive(false);
        }

        void LateUpdate()
        {
            if (silhouetteImage == null) return;
            SyncVisual();
            SyncWidth();
            bool visible = hovered && button.IsInteractable() && Width > 0f;
            if (silhouetteImage.gameObject.activeSelf != visible)
                silhouetteImage.gameObject.SetActive(visible);
        }

        void OnDisable()
        {
            hovered = false;
            if (silhouetteImage != null) silhouetteImage.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (silhouetteImage != null) Destroy(silhouetteImage.gameObject);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            if (button != null && button.IsInteractable() && Width > 0f && silhouetteImage != null)
                silhouetteImage.gameObject.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            if (silhouetteImage != null) silhouetteImage.gameObject.SetActive(false);
        }

        float Width => uiController != null ? Mathf.Max(0f, uiController.startSignHoverOutlineWidth) : 0f;

        void SyncVisual()
        {
            if (silhouetteRect.anchorMin != signRect.anchorMin) silhouetteRect.anchorMin = signRect.anchorMin;
            if (silhouetteRect.anchorMax != signRect.anchorMax) silhouetteRect.anchorMax = signRect.anchorMax;
            if (silhouetteRect.pivot != signRect.pivot) silhouetteRect.pivot = signRect.pivot;
            if (silhouetteRect.sizeDelta != signRect.sizeDelta) silhouetteRect.sizeDelta = signRect.sizeDelta;
            if (silhouetteRect.anchoredPosition3D != signRect.anchoredPosition3D)
                silhouetteRect.anchoredPosition3D = signRect.anchoredPosition3D;
            if (silhouetteRect.localRotation != signRect.localRotation) silhouetteRect.localRotation = signRect.localRotation;
            if (silhouetteRect.localScale != signRect.localScale) silhouetteRect.localScale = signRect.localScale;
            if (silhouetteImage != null && silhouetteImage.sprite != signImage.sprite)
                silhouetteImage.sprite = signImage.sprite;
        }

        void SyncWidth()
        {
            float width = Width;
            var distance = new Vector2(width, width);
            if (outline.effectDistance != distance) outline.effectDistance = distance;
        }
    }
}
