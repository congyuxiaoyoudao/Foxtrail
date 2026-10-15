using UnityEngine;

namespace Hiking.Journey
{
    // World decoration attached to a route slot. The actual plan and settlement live in PlacementSlot.
    public sealed class PlacementSignVisual : MonoBehaviour
    {
        // Background plants are 0 and the fox is 10: the sign sits between them.
        const int BoardSortingOrder = 5;
        SpriteRenderer board;
        SpriteRenderer waterBubble;
        SpriteRenderer seedBubble;
        SpriteRenderer warning;
        TextMesh waterText;
        TextMesh seedText;
        GameObject warningMark;
        Camera worldCamera;
        bool hidden;
        bool themedBoard;

        public void Configure(RingMapSettings settings)
        {
            var sprite = settings.fallbackPlantSprite;
            var font = settings.tileLabelFont;
            themedBoard = settings.placementSignSprite != null;
            if (themedBoard)
            {
                var sign = settings.placementSignSprite;
                float height = .88f;
                board = MakePanel("WoodenBoard", sign, Color.white,
                    new Vector3(0, height * .5f, 0),
                    new Vector2(height * sign.bounds.size.x / Mathf.Max(.001f, sign.bounds.size.y), height), BoardSortingOrder);
            }
            else
            {
                MakePanel("Post", sprite, new Color(.42f, .27f, .14f),
                    new Vector3(0, .22f, 0), new Vector2(.10f, .50f), BoardSortingOrder - 2);
                MakePanel("BoardBorder", sprite, new Color(.38f, .25f, .14f),
                    new Vector3(0, .56f, -.01f), new Vector2(.64f, .39f), BoardSortingOrder - 1);
                board = MakePanel("WoodenBoard", sprite, new Color(.73f, .53f, .29f),
                    new Vector3(0, .56f, -.02f), new Vector2(.57f, .32f), BoardSortingOrder);
                MakeText("BoardMark", "•", font, new Vector3(0, .54f, -.03f), .21f,
                    new Color(.32f, .22f, .12f), BoardSortingOrder + 1);
            }

            waterBubble = MakePanel("WaterBubble", sprite, new Color(.80f, .92f, .98f),
                new Vector3(-.39f, 1.02f, -.04f), new Vector2(.70f, .36f), 22);
            MakePanel("WaterBubbleTail", sprite, new Color(.80f, .92f, .98f),
                new Vector3(-.39f, .82f, -.04f), new Vector2(.13f, .13f), 21)
                .transform.localRotation = Quaternion.Euler(0, 0, 45);
            waterText = MakeText("WaterValue", "", font, new Vector3(-.39f, 1.01f, -.05f), .13f,
                new Color(.16f, .34f, .48f), 23);
            seedBubble = MakePanel("SeedBubble", sprite, new Color(.88f, .94f, .73f),
                new Vector3(.39f, 1.02f, -.04f), new Vector2(.70f, .36f), 22);
            MakePanel("SeedBubbleTail", sprite, new Color(.88f, .94f, .73f),
                new Vector3(.39f, .82f, -.04f), new Vector2(.13f, .13f), 21)
                .transform.localRotation = Quaternion.Euler(0, 0, 45);
            seedText = MakeText("SeedValue", "", font, new Vector3(.39f, 1.01f, -.05f), .13f,
                new Color(.28f, .39f, .19f), 23);
            warning = MakePanel("WarningBubble", sprite, new Color(1f, .86f, .47f),
                new Vector3(0, 1.39f, -.06f), new Vector2(.29f, .31f), 25);
            warningMark = MakeText("WarningMark", "!", font, new Vector3(0, 1.37f, -.07f), .21f,
                new Color(.60f, .21f, .12f), 26).gameObject;
            warning.gameObject.SetActive(false);
            warningMark.SetActive(false);
        }

        SpriteRenderer MakePanel(string name, Sprite sprite, Color color, Vector3 position,
            Vector2 size, int sortingOrder)
        {
            var child = new GameObject(name).transform;
            child.SetParent(transform, false);
            child.localPosition = position;
            var renderer = child.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            Vector2 spriteSize = sprite != null ? sprite.bounds.size : Vector2.one;
            child.localScale = new Vector3(size.x / Mathf.Max(.001f, spriteSize.x),
                size.y / Mathf.Max(.001f, spriteSize.y), 1);
            return renderer;
        }

        TextMesh MakeText(string name, string text, Font font, Vector3 position,
            float size, Color color, int sortingOrder)
        {
            var child = new GameObject(name).transform;
            child.SetParent(transform, false);
            child.localPosition = position;
            var label = child.gameObject.AddComponent<TextMesh>();
            label.text = text;
            label.fontSize = 48;
            // TextMesh 的 characterSize 已经是世界尺寸；再除以 fontSize 会让数字几乎不可见。
            label.characterSize = size * .25f;
            label.anchor = TextAnchor.MiddleCenter;
            label.color = color;
            if (font != null)
            {
                label.font = font;
                label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            label.GetComponent<MeshRenderer>().sortingOrder = sortingOrder;
            return label;
        }

        public void SetPlan(PlacementSlot slot)
        {
            waterBubble.gameObject.SetActive(true);
            seedBubble.gameObject.SetActive(true);
            waterText.text = $"水 {slot.PlannedWater}";
            seedText.text = $"种 {slot.PlannedSeeds}";
        }

        public void Frame(Camera camera, PlacementSlot slot, bool hovered)
        {
            if (hidden || camera == null) return;
            worldCamera = camera;
            transform.rotation = camera.transform.rotation;
            board.color = themedBoard
                ? (hovered ? new Color(1f, .9f, .68f) : Color.white)
                : (hovered ? new Color(.96f, .75f, .39f) : new Color(.73f, .53f, .29f));
            bool showWarning = slot.WarningAtArrival;
            warning.gameObject.SetActive(showWarning);
            warningMark.SetActive(showWarning);
        }

        public bool Contains(Vector2 pointer, bool warningOnly = false)
        {
            if (hidden || worldCamera == null) return false;
            var center = warningOnly ? warning.transform.position : board.transform.position;
            Vector3 screen = worldCamera.WorldToScreenPoint(center);
            if (screen.z <= 0) return false;
            Vector3 edge = worldCamera.WorldToScreenPoint(center + worldCamera.transform.right *
                (warningOnly ? .26f : .42f));
            Vector3 top = worldCamera.WorldToScreenPoint(center + worldCamera.transform.up *
                (warningOnly ? .25f : (themedBoard ? .46f : .34f)));
            return Mathf.Abs(pointer.x - screen.x) <= Mathf.Abs(edge.x - screen.x) &&
                Mathf.Abs(pointer.y - screen.y) <= Mathf.Abs(top.y - screen.y);
        }

        public Vector2 ScreenPosition(Camera camera) =>
            camera == null ? Vector2.zero : camera.WorldToScreenPoint(transform.position +
                camera.transform.up * 1.15f);

        public void Hide() { hidden = true; gameObject.SetActive(false); }
    }
}
