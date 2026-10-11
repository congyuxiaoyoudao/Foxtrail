using System;
using UnityEngine;
using UnityEngine.UI;
namespace Hiking.Journey
{
    public class GameUIController : MonoBehaviour
    {
        GameFlowController flow;
        public GameObject startPanel, journeyPanel, panoramaPanel;
        [Header("地块调试（布局及按钮事件保存在 Canvas）")]
        public GameObject debugPanel;
        public Text debugStatus;
        // Retain old serialized references while the scene migrates to automatic travel.
        [HideInInspector] public GameObject exitPrompt;
        [HideInInspector] public Text exitPromptText;
        [HideInInspector] public Button confirmExit, cancelExit;
        public bool ExitPromptOpen => false;
        public void CloseExitPrompt() { if (exitPrompt != null) exitPrompt.SetActive(false); }
        [Header("是否投放")]
        public GameObject placementDecision;
        public Text placementDecisionText;
        public Button choosePlacement, skipPlacement;
        [Header("材料与数量")]
        public GameObject placementPrompt;
        public Text placementPromptText;
        public Button confirmPlacement, cancelPlacement, quantityMinus, quantityPlus;
        public InputField quantityInput;
        public MaterialButton[] placementMaterialButtons;
        public bool PlacementPromptOpen => placementPrompt != null && placementPrompt.activeSelf;
        public bool PlacementDecisionOpen => placementDecision != null && placementDecision.activeSelf;
        public bool IsModalOpen => PlacementDecisionOpen || PlacementPromptOpen;
        public void ShowPlacementDecision()
        {
            if (!flow.Placement.IsAtSlot(flow.Placement.PendingSlot)) return;
            placementPrompt.SetActive(false); placementDecision.SetActive(true);
        }
        public void ShowPlacementPrompt()
        {
            if (!flow.Placement.IsAtSlot(flow.Placement.PendingSlot)) return;
            placementDecision.SetActive(false); placementPrompt.SetActive(true);
        }
        public void ClosePlacementPrompt(bool completeVisit = true)
        {
            if (placementPrompt != null) placementPrompt.SetActive(false);
            if (placementDecision != null) placementDecision.SetActive(false);
            flow.Placement.ClearSelection();
            if (completeVisit) flow.CompletePlacementVisit();
        }
        public Text status, hint, toast, departText, modeText;
        public Button startButton, modeButton, depart, returnButton;
        public Image progress;
        [HideInInspector] public Button returnToTravelerButton;
        [Serializable]
        public class MaterialButton
        {
            public string materialId;
            public Button button;
            public Text label;
            public Color normalColor = new Color(.17f, .29f, .31f);
            public Color selectedColor = new Color(.28f, .49f, .43f);
        }
        public MaterialButton[] materialButtons;
        float toastUntil;
        Text panoramaTitle;
        public JourneyMode mode = JourneyMode.Normal;
        public void Initialize(GameFlowController owner)
        {
            flow = owner;
            confirmPlacement.onClick.AddListener(() => { CommitQuantity(quantityInput.text); flow.Placement.ConfirmPlacement(); });
            cancelPlacement.onClick.AddListener(ShowPlacementDecision);
            choosePlacement.onClick.AddListener(() => flow.Placement.ChooseToPlace());
            skipPlacement.onClick.AddListener(() => flow.Placement.SkipPlacement());
            quantityMinus.onClick.AddListener(() => flow.Placement.AdjustQuantity(-1));
            quantityPlus.onClick.AddListener(() => flow.Placement.AdjustQuantity(1));
            quantityInput.onEndEdit.AddListener(CommitQuantity);
            foreach (var view in placementMaterialButtons)
            {
                var id = view.materialId; view.button.onClick.AddListener(() => flow.Placement.Select(id));
            }
            ClosePlacementPrompt(false); CloseExitPrompt();
            if (depart != null) depart.gameObject.SetActive(false);
            if (modeButton != null)
            {
                modeButton.gameObject.SetActive(true);
                modeButton.onClick.AddListener(CycleMode);
            }
            if (mode != JourneyMode.Quick) mode = JourneyMode.Normal;
            if (returnToTravelerButton != null) returnToTravelerButton.gameObject.SetActive(false);
            foreach (var label in panoramaPanel.GetComponentsInChildren<Text>(true))
                if (label.text.Contains("旅途完成")) { panoramaTitle = label; break; }
            RefreshModeLabel();
        }
        void CommitQuantity(string text)
        {
            flow.Placement.SetQuantity(int.TryParse(text, out int value) ? value : 1);
            quantityInput.SetTextWithoutNotify(flow.Placement.SelectedQuantity.ToString());
        }
        public void StartJourney() => flow.StartJourney(mode);
        public void StartDebugMode() => flow.StartDebugMode();
        public void StepDebugMap() => flow.StepDebugMap();
        public void DebugPanLeft() { if (flow.IsDebugMode) flow.Camera.PanDebug(-4 * flow.Map.TileWidth); }
        public void DebugPanRight() { if (flow.IsDebugMode) flow.Camera.PanDebug(4 * flow.Map.TileWidth); }
        public void Depart() { }
        public void CycleMode()
        {
            if (flow == null || flow.Session.Phase != JourneyPhase.Start) return;
            mode = mode == JourneyMode.Quick ? JourneyMode.Normal : JourneyMode.Quick;
            RefreshModeLabel();
        }
        void RefreshModeLabel()
        {
            if (modeText == null) return;
            string name = mode == JourneyMode.Quick ? "开发快速" : "默认模式";
            modeText.text = $"{name}（点击切换）\n移动 {flow.Config.MoveSeconds(mode):0.##} 秒 · 站间 {flow.Config.TransitionSeconds(mode):0.##} 秒";
            if (modeButton != null && modeButton.targetGraphic != null)
                modeButton.targetGraphic.color = mode == JourneyMode.Quick ? new Color(.28f,.49f,.43f) : new Color(.17f,.29f,.31f);
        }
        public void ReturnToStart() { if (flow.IsPreviewing) flow.ExitPreview(); else flow.ReturnToStart(); }
        public void PreviewPanorama() => flow.PreviewPanorama();
        public void SelectMaterial(string id) => flow.Placement.Select(id);
        void LateUpdate()
        {
            if (flow == null) return;
            var session = flow.Session;
            startPanel.SetActive(session.Phase == JourneyPhase.Start);
            journeyPanel.SetActive(!flow.IsDebugMode && !flow.IsPreviewing && session.Phase != JourneyPhase.Start && session.Phase != JourneyPhase.Panorama);
            if (debugPanel != null) debugPanel.SetActive(flow.IsDebugMode);
            if (flow.IsDebugMode)
            {
                if (debugStatus != null)
                {
                    var weather = flow.Map.Weather;
                    int interval = Mathf.Max(1, flow.Map.sourceSettings.rainCheckInterval);
                    string rain = weather.IsRaining || weather.RainAppliedThisStep ?
                        $"降雨 {weather.RainStartTile + 1}～{weather.RainEndTile + 1} 格 · 剩余 {weather.RemainingSeconds} 秒 · 本场 {weather.EventRain:0.00}" :
                        $"下次天气检查 {interval - flow.Map.SimulationStep % interval} 秒后";
                    debugStatus.text = $"地块调试 · 第 {flow.DebugStepCount} 步 · 共 {flow.Map.stations.Length * RingMapSettings.TilesPerStation} 块\n" +
                        rain + " · 拖动 / 滚轮 / ← → 浏览全图";
                }
            }
            panoramaPanel.SetActive(flow.IsPreviewing || session.Phase == JourneyPhase.Panorama);
            if (panoramaTitle != null) panoramaTitle.text = flow.IsPreviewing ? "全景预览" : "旅途完成 · 全景观察";
            var returnLabel = returnButton.GetComponentInChildren<Text>();
            if (returnLabel != null) returnLabel.text = flow.IsPreviewing ? "返回旅途" : "返回开始";
            string activity = flow.Activity == JourneyActivity.AwaitingPlacement ? "等待投放选择" : flow.Activity == JourneyActivity.BetweenStations ? "前往下一站" :
                flow.Activity == JourneyActivity.Complete ? "旅途完成" : "自动行走";
            if (flow.Map != null) status.text = $"第 {session.StationIndex + 1} / {session.StationCount} 站 · {flow.CurrentStation.ThemeName} · 第 {flow.CurrentTileIndex + 1} / 12 块 · {activity}" +
                (flow.Map.Weather.IsRaining || flow.Map.Weather.RainAppliedThisStep ?
                    $" · 降雨中（{flow.Map.Weather.RainStartTile + 1}～{flow.Map.Weather.RainEndTile + 1} 格）" : "");
            progress.rectTransform.anchorMax = new Vector2(flow.Traveler.Progress01, 1);
            UpdateMaterials(materialButtons, false);
            UpdateMaterials(placementMaterialButtons, PlacementPromptOpen);
            var slot = flow.Placement.PendingSlot;
            if (PlacementDecisionOpen && slot != null)
                placementDecisionText.text = $"已到达第 {slot.tileIndex + 1} 块投放点\n是否投放材料？\n不投放则立即继续前行";
            if (PlacementPromptOpen && slot != null)
            {
                int quantity = flow.Placement.SelectedQuantity;
                var selected = flow.Material(flow.Placement.SelectedMaterialId);
                var tile = slot.station.Tiles[slot.tileIndex];
                bool hasSuitablePlant = flow.Map.sourceSettings.plantCatalog != null &&
                    flow.Map.sourceSettings.plantCatalog.SuitableFor(slot.station.theme).Length > 0;
                string action = selected == null ? "请选择材料" : flow.Session.Stock(selected.id) == 0 ? "库存不足，可返回选择不投放" :
                    selected.id == "water" ? tile.SurfaceWater >= TileState.SurfaceCapacity ? "地表水已满" :
                        $"投水 {quantity} 份：增加地表水，上限 35" :
                    !hasSuitablePlant ? "当前主题没有适宜生长的植物" :
                    quantity > tile.FreePlantCapacity ? $"地块最多还能接收 {tile.FreePlantCapacity} 颗种子" :
                    $"投种 {quantity} 份：待萌发种子最多存活 30 秒";
                placementPromptText.text = $"第 {slot.tileIndex + 1} 块 · 地表水 {tile.SurfaceWater:0.0} · 已投种子 {slot.SeedCount} · 待萌发 {tile.SeedCount}\n{action}";
                if (!quantityInput.isFocused) quantityInput.SetTextWithoutNotify(quantity.ToString());
                quantityMinus.interactable = quantity > 1;
                quantityPlus.interactable = quantity < flow.Session.Stock(flow.Placement.SelectedMaterialId);
                confirmPlacement.interactable = flow.Placement.CanConfirm;
            }
            if (hint != null) hint.text = "自动前行 · 白框为投放点 · 灰蓝色为过渡块 · 下方为地表水，框内为已投种子";
            if (toast != null && Time.unscaledTime > toastUntil) toast.text = "";
        }
        void UpdateMaterials(MaterialButton[] views, bool available)
        {
            if (views == null) return;
            foreach (var view in views)
            {
                var material = flow.Material(view.materialId); int stock = flow.Session.Stock(view.materialId);
                view.button.interactable = available && material != null && stock > 0;
                if (material == null) { view.label.text = "未配置材料"; continue; }
                bool selected = available && flow.Placement.SelectedMaterialId == material.id;
                view.label.text = (selected ? "✓ " : "") + material.displayName + " × " +
                    (material.id == "water" ? flow.Session.Amount("water").ToString("0.##") : stock.ToString());
                if (view.button.targetGraphic != null) view.button.targetGraphic.color = selected ? view.selectedColor : view.normalColor;
            }
        }
        public void Notify(string message)
        {
            if (toast == null) return; toast.text = message; toastUntil = Time.unscaledTime + 2.5f;
        }
    }
}
