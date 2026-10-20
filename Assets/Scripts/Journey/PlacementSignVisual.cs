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
        Transform signRoot;
        Transform waterHint;
        Transform seedHint;
        GameObject warningMark;
        Camera worldCamera;
        RingMapSettings sourceSettings;
        bool hidden;
        bool themedBoard;
        bool themedWater;
        bool themedSeed;

        public void Configure(RingMapSettings settings)
        {
            sourceSettings = settings;
            signRoot = new GameObject("SignRoot").transform;
            signRoot.SetParent(transform, false);
            var sprite = settings.fallbackPlantSprite;
            var font = settings.tileLabelFont;
            var numberFont = settings.placementSignNumberFont != null
                ? settings.placementSignNumberFont : font;
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

            var labelColor = new Color32(0x85, 0x4f, 0x39, 0xff);
            themedWater = settings.placementWaterSignSprite != null;
            themedSeed = settings.placementSeedSignSprite != null;
            waterBubble = MakePanel("WaterBubble", themedWater ? settings.placementWaterSignSprite : sprite,
                themedWater ? Color.white : new Color(.80f, .92f, .98f),
                new Vector3(-.39f, 1.02f, -.04f), new Vector2(.70f, .36f), 22);
            SpriteRenderer waterTail = null;
            if (!themedWater)
                waterTail = MakePanel("WaterBubbleTail", sprite, new Color(.80f, .92f, .98f),
                    new Vector3(-.39f, .82f, -.04f), new Vector2(.13f, .13f), 21);
            if (waterTail != null)
                waterTail.transform.localRotation = Quaternion.Euler(0, 0, 45);
            waterText = MakeText("WaterValue", "", numberFont,
                new Vector3(themedWater ? -.21f : -.39f, 1.01f, -.05f),
                (themedWater ? .17f : .13f) * settings.placementSignNumberTextScale,
                labelColor, 23);
            waterHint = GroupParts("WaterHint", new Vector3(-.39f, 1.02f, 0f),
                waterBubble.transform, waterText.transform, waterTail != null ? waterTail.transform : null);
            seedBubble = MakePanel("SeedBubble", themedSeed ? settings.placementSeedSignSprite : sprite,
                themedSeed ? Color.white : new Color(.88f, .94f, .73f),
                new Vector3(.39f, 1.02f, -.04f), new Vector2(.70f, .36f), 22);
            SpriteRenderer seedTail = null;
            if (!themedSeed)
                seedTail = MakePanel("SeedBubbleTail", sprite, new Color(.88f, .94f, .73f),
                    new Vector3(.39f, .82f, -.04f), new Vector2(.13f, .13f), 21);
            if (seedTail != null)
                seedTail.transform.localRotation = Quaternion.Euler(0, 0, 45);
            seedText = MakeText("SeedValue", "", numberFont,
                new Vector3(themedSeed ? .57f : .39f, 1.01f, -.05f),
                (themedSeed ? .17f : .13f) * settings.placementSignNumberTextScale,
                labelColor, 23);
            seedHint = GroupParts("SeedHint", new Vector3(.39f, 1.02f, 0f),
                seedBubble.transform, seedText.transform, seedTail != null ? seedTail.transform : null);
            bool themedWarning = settings.placementExclamationSprite != null;
            warning = MakePanel("WarningBubble", themedWarning ? settings.placementExclamationSprite : sprite,
                themedWarning ? Color.white : new Color(1f, .86f, .47f),
                new Vector3(0, 1.39f, -.06f), themedWarning ? new Vector2(.11f, .39f) : new Vector2(.29f, .31f), 25);
            if (!themedWarning)
                warningMark = MakeText("WarningMark", "!", font, new Vector3(0, 1.37f, -.07f), .21f,
                    labelColor, 26).gameObject;
            warning.gameObject.SetActive(false);
            if (warningMark != null) warningMark.SetActive(false);
            ApplyScales();
        }

        void Update() => ApplyScales();

        Transform GroupParts(string name, Vector3 center, params Transform[] parts)
        {
            var group = new GameObject(name).transform;
            group.SetParent(signRoot, false);
            group.localPosition = center;
            foreach (var part in parts)
                if (part != null) part.SetParent(group, true);
            return group;
        }

        void ApplyScales()
        {
            if (sourceSettings == null) return;
            if (signRoot != null)
            {
                float signScale = Mathf.Max(.1f, sourceSettings.placementSignBoardScale);
                if (!Mathf.Approximately(signRoot.localScale.x, signScale))
                    signRoot.localScale = Vector3.one * signScale;
            }
            ApplyHintScale();
        }

        void ApplyHintScale()
        {
            if (sourceSettings == null || waterHint == null || seedHint == null) return;
            float scale = Mathf.Max(.1f, sourceSettings.placementSignHintScale);
            if (Mathf.Approximately(waterHint.localScale.x, scale)) return;
            waterHint.localScale = Vector3.one * scale;
            seedHint.localScale = Vector3.one * scale;
        }

        SpriteRenderer MakePanel(string name, Sprite sprite, Color color, Vector3 position,
            Vector2 size, int sortingOrder)
        {
            var child = new GameObject(name).transform;
            child.SetParent(signRoot, false);
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
            child.SetParent(signRoot, false);
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
            waterText.text = themedWater ? slot.PlannedWater.ToString() : $"水 {slot.PlannedWater}";
            seedText.text = themedSeed ? slot.PlannedSeeds.ToString() : $"种 {slot.PlannedSeeds}";
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
            if (warningMark != null) warningMark.SetActive(showWarning);
        }

        public bool Contains(Vector2 pointer, bool warningOnly = false)
        {
            if (hidden || worldCamera == null) return false;
            var center = warningOnly ? warning.transform.position : board.transform.position;
            Vector3 screen = worldCamera.WorldToScreenPoint(center);
            if (screen.z <= 0) return false;
            float signScale = signRoot != null ? signRoot.localScale.x : 1f;
            Vector3 edge = worldCamera.WorldToScreenPoint(center + worldCamera.transform.right *
                (warningOnly ? .26f : .42f) * signScale);
            Vector3 top = worldCamera.WorldToScreenPoint(center + worldCamera.transform.up *
                (warningOnly ? .25f : (themedBoard ? .46f : .34f)) * signScale);
            return Mathf.Abs(pointer.x - screen.x) <= Mathf.Abs(edge.x - screen.x) &&
                Mathf.Abs(pointer.y - screen.y) <= Mathf.Abs(top.y - screen.y);
        }

        public Vector2 ScreenPosition(Camera camera) =>
            camera == null ? Vector2.zero : camera.WorldToScreenPoint(transform.position +
                camera.transform.up * (1.15f * (signRoot != null ? signRoot.localScale.x : 1f)));

        public void Hide() { hidden = true; gameObject.SetActive(false); }
    }
}
