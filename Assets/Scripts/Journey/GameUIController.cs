using System;
using UnityEngine;
using UnityEngine.UI;
namespace Hiking.Journey
{
    public class GameUIController : MonoBehaviour
    {
        GameFlowController flow;
        public GameObject startPanel, journeyPanel, panoramaPanel;
        [Header("终局与长卷浏览（布局及按钮事件保存在 Canvas）")]
        public Button previewPanoramaButton, continueButton, exitAppreciationButton,
            returnToTitleButton, scrollLeftButton, scrollRightButton;
        public Text panoramaHint;
        public Image foxOutsideFrame;
        [Header("全景画框与浏览进度（布局保存在 Canvas）")]
        public GameObject panoramaBrowsingDecoration;
        public Image panoramaProgressFill;
        public RectTransform panoramaProgressFox;
        public Text panoramaProgressPercent;
        public GameObject panoramaCompletionControls;
        public Button panoramaBrowseReturnButton;
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
        [Header("木牌投放设置（布局保存在 Canvas）")]
        public GameObject signEditorPanel;
        public RectTransform signEditorRect;
        public Text signWaterValue, signSeedValue;
        public Button signWaterMinus, signWaterPlus, signSeedMinus, signSeedPlus,
            signConfirm, signCancel;
        public bool SignEditorOpen => signEditorPanel != null && signEditorPanel.activeSelf;
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
            if (completeVisit) flow.CompletePlacementVisit();
        }
        public void ShowSignEditor(PlacementSlot slot)
        {
            if (signEditorPanel == null) return;
            signEditorPanel.SetActive(true);
            PositionSignEditor(slot);
        }
        public void HideSignEditor()
        {
            if (signEditorPanel != null) signEditorPanel.SetActive(false);
        }
        void PositionSignEditor(PlacementSlot slot)
        {
            if (slot?.Sign == null || signEditorRect == null) return;
            var canvasRect = GetComponent<RectTransform>();
            Vector2 screen = slot.Sign.ScreenPosition(flow.Camera.WorldCamera);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null,
                out Vector2 local);
            Vector2 half = signEditorRect.rect.size * .5f;
            signEditorRect.anchoredPosition = new Vector2(
                Mathf.Clamp(local.x, canvasRect.rect.xMin + half.x + 8, canvasRect.rect.xMax - half.x - 8),
                Mathf.Clamp(local.y, canvasRect.rect.yMin + half.y + 8, canvasRect.rect.yMax - half.y - 8));
        }
        public Text status, hint, toast, departText, modeText;
        public Button startButton, modeButton, depart, returnButton;
        public Image progress;
        [HideInInspector] public Button returnToTravelerButton;
        [Tooltip("开始界面的「继续游戏」，仅在存在存档时显示")]
        public Button loadGameButton;
        [Tooltip("旅途中的「保存并退出」")]
        public Button saveQuitButton;
        [Tooltip("离线结算进度提示，结算期间才显示")]
        public Text loadingText;
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
            ClosePlacementPrompt(false); CloseExitPrompt(); HideSignEditor();
            if (signWaterMinus != null) signWaterMinus.onClick.AddListener(() => flow.Placement.AdjustPlan("water", -1));
            if (signWaterPlus != null) signWaterPlus.onClick.AddListener(() => flow.Placement.AdjustPlan("water", 1));
            if (signSeedMinus != null) signSeedMinus.onClick.AddListener(() => flow.Placement.AdjustPlan("seed", -1));
            if (signSeedPlus != null) signSeedPlus.onClick.AddListener(() => flow.Placement.AdjustPlan("seed", 1));
            if (signConfirm != null) signConfirm.onClick.AddListener(flow.Placement.ConfirmPlan);
            if (signCancel != null) signCancel.onClick.AddListener(flow.Placement.CancelEditor);
            if (depart != null) depart.gameObject.SetActive(false);
            if (modeButton != null)
            {
                modeButton.gameObject.SetActive(true);
                modeButton.onClick.AddListener(CycleMode);
            }
            if (mode != JourneyMode.Quick) mode = JourneyMode.Normal;
            if (returnToTravelerButton != null) returnToTravelerButton.gameObject.SetActive(false);
            if (loadGameButton != null) loadGameButton.onClick.AddListener(ContinueGame);
            if (saveQuitButton != null) saveQuitButton.onClick.AddListener(SaveAndQuit);
            foreach (var label in panoramaPanel.GetComponentsInChildren<Text>(true))
                if (label.text.Contains("旅途完成")) { panoramaTitle = label; break; }
            if (scrollLeftButton != null)
                scrollLeftButton.GetComponent<ScrollHoldButton>()?.Configure(flow, -1);
            if (scrollRightButton != null)
                scrollRightButton.GetComponent<ScrollHoldButton>()?.Configure(flow, 1);
            if (foxOutsideFrame != null && flow.Traveler.walkFrames != null &&
                flow.Traveler.walkFrames.Length > 0)
                foxOutsideFrame.sprite = flow.Traveler.walkFrames[0];
            RefreshModeLabel();
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
        public void ReturnToStart() => flow.ReturnToStart();
        public void ReturnToTitle() => SaveAndQuit();
        // ---- 存档入口：策划要求提供「继续游戏」和「保存并退出」----
        // 直接关闭窗口也保存，那条在 GameFlowController 里（OnApplicationQuit / OnApplicationPause）。
        public void ContinueGame() { if (flow != null) flow.LoadGame(); }
        public void SaveAndQuit()
        {
            if (flow == null) return;
            if (!flow.SaveNow()) { Notify("保存失败，请重试"); return; }
            // 按确认：退到开始菜单，不退进程。玩家不用重启程序就能开新局；
            // 退出后回到开始界面，存档还在，所以「继续游戏」能接着玩。
            flow.ReturnToStart();
        }
        public void PreviewPanorama() => flow.PreviewPanorama();
        public void PrimaryCompletionAction()
        {
            if (flow.IsPreviewing) { flow.ExitPreview(); return; }
            if (!flow.IsFinalized) flow.TryFinalize();
            else flow.EnterAppreciation();
        }
        public void ContinueJourney() => flow.ContinueJourney();
        public void ExitAppreciation() => flow.ExitAppreciation();
        public void SelectMaterial(string id) { }
        void LateUpdate()
        {
            if (flow == null) return;
            var session = flow.Session;
            startPanel.SetActive(session.Phase == JourneyPhase.Start);
            journeyPanel.SetActive(!flow.IsSettlingOffline && !flow.IsDebugMode && !flow.IsPreviewing &&
                flow.Activity != JourneyActivity.Complete && session.Phase != JourneyPhase.Start);
            if (previewPanoramaButton != null) previewPanoramaButton.interactable = !IsModalOpen;
            // 没有存档时不显示「继续游戏」，免得点了没反应
            if (loadGameButton != null) loadGameButton.gameObject.SetActive(flow.HasSave());
            if (loadingText != null)
            {
                bool settling = flow.IsSettlingOffline;
                if (loadingText.gameObject.activeSelf != settling) loadingText.gameObject.SetActive(settling);
                if (settling) loadingText.text = "正在结算离线时间… " + (flow.OfflineProgress01 * 100f).ToString("0") + "%";
            }
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
            bool finished = flow.Activity == JourneyActivity.Complete;
            bool previewing = flow.IsPreviewing;
            bool appreciating = flow.IsAppreciating;
            bool browsing = flow.IsBrowsingPanorama;
            panoramaPanel.SetActive((finished || previewing) && !flow.IsEnteringAppreciation);
            if (panoramaBrowsingDecoration != null)
                panoramaBrowsingDecoration.SetActive(browsing);
            if (panoramaCompletionControls != null)
                panoramaCompletionControls.SetActive(!browsing);
            if (panoramaBrowseReturnButton != null)
                panoramaBrowseReturnButton.gameObject.SetActive(previewing);
            if (browsing)
            {
                float position = flow.Camera.ScrollProgress01;
                if (panoramaProgressFill != null)
                    panoramaProgressFill.rectTransform.anchorMax = new Vector2(position, 1f);
                if (panoramaProgressFox != null)
                {
                    var anchor = new Vector2(position, .5f);
                    panoramaProgressFox.anchorMin = anchor;
                    panoramaProgressFox.anchorMax = anchor;
                }
                if (panoramaProgressPercent != null)
                    panoramaProgressPercent.text = $"{position * 100f:0}%";
            }
            if (panoramaTitle != null) panoramaTitle.text =
                previewing ? $"第 {flow.LevelIndex} 段旅途 · 全景预览" :
                appreciating ? $"第 {flow.LevelIndex} 段旅途 · 长卷欣赏" :
                flow.IsFinalized ? $"第 {flow.LevelIndex} 段旅途已定稿保存" :
                "保存失败 · 请重试";
            var returnLabel = returnButton.GetComponentInChildren<Text>();
            if (returnLabel != null) returnLabel.text = previewing ? "返回旅途" : !flow.IsFinalized ? "重试保存" :
                flow.HasAppreciated ? "再次欣赏" : "欣赏本次旅途";
            returnButton.gameObject.SetActive(!appreciating && !previewing);
            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(finished && !appreciating);
                continueButton.interactable = flow.IsFinalized;
                var label = continueButton.GetComponentInChildren<Text>();
                if (label != null) label.text = flow.HasAppreciated ? "继续旅途" : "开启下一段旅途";
            }
            if (returnToTitleButton != null) returnToTitleButton.gameObject.SetActive(previewing);
            if (exitAppreciationButton != null) exitAppreciationButton.gameObject.SetActive(appreciating);
            if (scrollLeftButton != null)
            {
                scrollLeftButton.gameObject.SetActive(flow.IsBrowsingPanorama);
                scrollLeftButton.interactable = flow.Camera.CanScrollLeft;
            }
            if (scrollRightButton != null)
            {
                scrollRightButton.gameObject.SetActive(flow.IsBrowsingPanorama);
                scrollRightButton.interactable = flow.Camera.CanScrollRight;
            }
            if (foxOutsideFrame != null) foxOutsideFrame.gameObject.SetActive(flow.IsBrowsingPanorama);
            if (panoramaHint != null) panoramaHint.text = previewing ?
                "点击箭头前进一站 · 按住连续浏览 · 返回旅途继续行走" : appreciating ?
                "点击箭头前进一站 · 按住连续浏览 · 松开停止" :
                flow.IsFinalized ? "本段地图与资源已保存 · 可欣赏长卷或直接继续" :
                "未完成保存：" + flow.SaveError;
            string activity = flow.Activity == JourneyActivity.AwaitingPlacement ? "投放点停留" : flow.Activity == JourneyActivity.BetweenStations ? "前往下一站" :
                flow.Activity == JourneyActivity.Complete ? "旅途完成" : "自动行走";
            if (flow.Map != null) status.text = $"第 {session.StationIndex + 1} / {session.StationCount} 站 · {flow.CurrentStation.ThemeName} · 第 {flow.CurrentTileIndex + 1} / 12 块 · {activity}" +
                (flow.Map.Weather.IsRaining || flow.Map.Weather.RainAppliedThisStep ?
                    $" · 降雨中（{flow.Map.Weather.RainStartTile + 1}～{flow.Map.Weather.RainEndTile + 1} 格）" : "");
            progress.rectTransform.anchorMax = new Vector2(flow.Traveler.Progress01, 1);
            UpdateMaterials(materialButtons, false);
            var editingSlot = flow.Placement.EditingSlot;
            if (editingSlot != null && SignEditorOpen)
            {
                PositionSignEditor(editingSlot);
                signWaterValue.text = flow.Placement.EditedWater.ToString();
                signSeedValue.text = flow.Placement.EditedSeeds.ToString();
                signWaterMinus.interactable = flow.Placement.EditedWater > 0;
                signWaterPlus.interactable = flow.Placement.EditedWater < 3;
                signSeedMinus.interactable = flow.Placement.EditedSeeds > 0;
                signSeedPlus.interactable = flow.Placement.EditedSeeds < 3;
            }
            if (hint != null) hint.text = "点击木牌调整水与种子的预设投放量；狐狸到点停留后自动投放";
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
                view.label.text = material.displayName + " × " +
                    (material.id == "water" ? flow.Session.Amount("water").ToString("0.##") : stock.ToString());
                if (view.button.targetGraphic != null) view.button.targetGraphic.color = view.normalColor;
            }
        }
        public void Notify(string message)
        {
            if (toast == null) return; toast.text = message; toastUntil = Time.unscaledTime + 2.5f;
        }
    }
}
