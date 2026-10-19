using System;
using UnityEngine;
using UnityEngine.UI;
namespace Hiking.Journey
{
    public class GameUIController : MonoBehaviour
    {
        GameFlowController flow;
        public GameObject startPanel, panoramaPanel;
        [Header("开始界面木牌")]
        [InspectorName("悬停白色描边宽度"), Min(0f)]
        public float startSignHoverOutlineWidth = 4f;
        [Header("终局与长卷浏览（布局及按钮事件保存在 Canvas）")]
        public Button continueButton, exitAppreciationButton,
            returnToTitleButton, scrollLeftButton, scrollRightButton;
        public Image foxOutsideFrame;
        [Header("全景画框与浏览进度（布局保存在 Canvas）")]
        public GameObject panoramaBrowsingDecoration;
        [InspectorName("全景进度条整体位移")]
        [Tooltip("同时移动进度条背景、填充、百分比和狐狸装饰；播放时修改立即生效")]
        public Vector2 panoramaProgressOffset;
        public Image panoramaProgressFill;
        public RectTransform panoramaProgressFox;
        public Text panoramaProgressPercent;
        public Button panoramaBrowseReturnButton;
        [Header("背景音乐")]
        public AudioClip titleBgm, plainBgm, snowMountainBgm, desertBgm;
        [Min(.1f)] public float bgmCrossfadeSeconds = 1.5f;
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
        public bool SignEditorOpen => travelUI != null && travelUI.IsPointOpen ||
            signEditorPanel != null && signEditorPanel.activeSelf;
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
            if (travelUI != null)
            {
                travelUI.OpenPoint(slot);
                return;
            }
            if (signEditorPanel == null) return;
            signEditorPanel.SetActive(true);
            PositionSignEditor(slot);
        }
        public void HideSignEditor()
        {
            if (travelUI != null) travelUI.ClosePoint();
            if (signEditorPanel != null) signEditorPanel.SetActive(false);
        }
        void PositionSignEditor(PlacementSlot slot)
        {
            if (slot?.Sign == null || signEditorRect == null) return;
            var canvasRect = signEditorRect.parent as RectTransform;
            if (canvasRect == null) return;
            Vector2 screen = slot.Sign.ScreenPosition(flow.Camera.WorldCamera);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null,
                out Vector2 local);
            Vector2 half = signEditorRect.rect.size * .5f;
            signEditorRect.anchoredPosition = new Vector2(
                Mathf.Clamp(local.x, canvasRect.rect.xMin + half.x + 8, canvasRect.rect.xMax - half.x - 8),
                Mathf.Clamp(local.y, canvasRect.rect.yMin + half.y + 8, canvasRect.rect.yMax - half.y - 8));
        }
        public Button startButton;
        [HideInInspector] public Button returnToTravelerButton;
        [Tooltip("开始界面的「继续游戏」，仅在存在存档时显示")]
        public Button loadGameButton;
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
        TravelUIBridge travelUI;
        JourneyBgmPlayer bgmPlayer;
        Text panoramaTitle;
        public void Initialize(GameFlowController owner)
        {
            flow = owner;
            ClosePlacementPrompt(false); CloseExitPrompt(); HideSignEditor();
            bgmPlayer = new JourneyBgmPlayer(gameObject);
            travelUI = gameObject.AddComponent<TravelUIBridge>();
            travelUI.Initialize(owner);
            if (signWaterMinus != null) signWaterMinus.onClick.AddListener(() => flow.Placement.AdjustPlan("water", -1));
            if (signWaterPlus != null) signWaterPlus.onClick.AddListener(() => flow.Placement.AdjustPlan("water", 1));
            if (signSeedMinus != null) signSeedMinus.onClick.AddListener(() => flow.Placement.AdjustPlan("seed", -1));
            if (signSeedPlus != null) signSeedPlus.onClick.AddListener(() => flow.Placement.AdjustPlan("seed", 1));
            if (signConfirm != null) signConfirm.onClick.AddListener(flow.Placement.ConfirmPlan);
            if (signCancel != null) signCancel.onClick.AddListener(flow.Placement.CancelEditor);
            if (returnToTravelerButton != null) returnToTravelerButton.gameObject.SetActive(false);
            if (loadGameButton != null) loadGameButton.onClick.AddListener(ContinueGame);
            foreach (var label in panoramaPanel.GetComponentsInChildren<Text>(true))
                if (label.text.Contains("旅途完成")) { panoramaTitle = label; break; }
            if (scrollLeftButton != null)
                scrollLeftButton.GetComponent<ScrollHoldButton>()?.Configure(flow, -1);
            if (scrollRightButton != null)
                scrollRightButton.GetComponent<ScrollHoldButton>()?.Configure(flow, 1);
            if (signEditorPanel != null) signEditorPanel.SetActive(false);
        }
        public void StartJourney() => flow.StartJourney();
        public void OpenSettings() => travelUI?.OpenSettings();
        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        public void ReturnToTitle()
        {
            if (flow == null) return;
            // Completed journeys already live in the archive; SaveNow rejects this phase.
            if (flow.IsFinalized) flow.ReturnToStart();
            else SaveAndQuit();
        }
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
        public void ExitPanoramaPreview() => flow?.ExitPreview();
        public void ContinueJourney()
        {
            if (flow.IsAppreciating) flow.ExitAppreciation();
            flow.ContinueJourney();
        }
        public void StartNewJourneyFromPanorama()
        {
            if (flow == null || !flow.IsFinalized) return;
            ContinueJourney();
        }
        public void ReturnToMenuFromPanorama()
        {
            if (flow == null || !flow.IsFinalized) return;
            flow.ReturnToStart();
        }
        public void ApplyTravelPreset() => travelUI?.ApplyPresetToMap();
        public void SyncTravelPreset() => travelUI?.SyncPresetFromMap();
        public void SetBgmVolume(float volume) => bgmPlayer?.SetVolume(volume);
        public void ExitAppreciation() => flow.ExitAppreciation();
        void LateUpdate()
        {
            if (flow == null) return;
            bgmPlayer.SetTarget(CurrentBgm(), bgmCrossfadeSeconds);
            bgmPlayer.Tick(Time.unscaledDeltaTime);
            var session = flow.Session;
            startPanel.SetActive(session.Phase == JourneyPhase.Start);
            // 没有存档时不显示「继续游戏」，免得点了没反应
            if (loadGameButton != null) loadGameButton.interactable = flow.HasSave();
            if (loadingText != null)
            {
                bool settling = flow.IsSettlingOffline;
                if (loadingText.gameObject.activeSelf != settling) loadingText.gameObject.SetActive(settling);
                if (settling) loadingText.text = "正在结算离线时间… " + (flow.OfflineProgress01 * 100f).ToString("0") + "%";
            }
            bool finished = flow.Activity == JourneyActivity.Complete;
            bool previewing = flow.IsPreviewing;
            bool appreciating = flow.IsAppreciating;
            bool browsing = flow.IsBrowsingPanorama;
            panoramaPanel.SetActive(finished || previewing);
            if (panoramaBrowsingDecoration != null)
            {
                panoramaBrowsingDecoration.SetActive(browsing);
                if (panoramaBrowsingDecoration.transform is RectTransform progressGroup)
                    progressGroup.anchoredPosition = panoramaProgressOffset;
            }
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
                flow.IsFinalized ? $"第 {flow.LevelIndex} 段旅途 · 旅途完成" :
                "保存失败";
            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(finished && flow.IsFinalized);
                continueButton.interactable = flow.IsFinalized;
            }
            if (returnToTitleButton != null)
                returnToTitleButton.gameObject.SetActive(finished && flow.IsFinalized);
            if (exitAppreciationButton != null) exitAppreciationButton.gameObject.SetActive(false);
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
            var editingSlot = flow.Placement.EditingSlot;
            if (editingSlot != null && signEditorPanel != null && signEditorPanel.activeSelf)
            {
                PositionSignEditor(editingSlot);
                signWaterValue.text = flow.Placement.EditedWater.ToString();
                signSeedValue.text = flow.Placement.EditedSeeds.ToString();
                signWaterMinus.interactable = flow.Placement.EditedWater > 0;
                signWaterPlus.interactable = flow.Placement.EditedWater < 3;
                signSeedMinus.interactable = flow.Placement.EditedSeeds > 0;
                signSeedPlus.interactable = flow.Placement.EditedSeeds < 3;
            }
        }
        public void Notify(string message)
        {
            if (!string.IsNullOrEmpty(message)) Debug.Log(message, this);
        }
        AudioClip CurrentBgm()
        {
            if (flow.Map == null || flow.Activity == JourneyActivity.Complete) return titleBgm;
            var station = flow.CurrentStation;
            if (station == null) return titleBgm;
            // Match the ground/near landscape when weather changes a station's visible theme.
            switch (station.MiddleTheme)
            {
                case StationTheme.Snow: return snowMountainBgm;
                case StationTheme.Desert: return desertBgm;
                default: return plainBgm;
            }
        }
    }

    // Two looping sources overlap so scene and station changes never cut the old track abruptly.
    internal sealed class JourneyBgmPlayer
    {
        readonly AudioSource[] sources = new AudioSource[2];
        readonly float[] gains = new float[2];
        readonly float[] startGains = new float[2];
        AudioClip target;
        float volume = 1f;
        float elapsed;
        float fadeSeconds = 1f;
        int incoming = -1;

        public JourneyBgmPlayer(GameObject host)
        {
            for (int i = 0; i < sources.Length; i++)
            {
                var source = host.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = true;
                source.spatialBlend = 0f;
                source.volume = 0f;
                sources[i] = source;
            }
        }

        public void SetVolume(float value)
        {
            volume = Mathf.Clamp01(value);
            ApplyVolumes();
        }

        public void SetTarget(AudioClip clip, float seconds)
        {
            if (clip == target) return;
            target = clip;
            incoming = -1;
            if (clip != null)
            {
                for (int i = 0; i < sources.Length; i++)
                    if (sources[i].clip == clip && sources[i].isPlaying) incoming = i;
                if (incoming < 0)
                {
                    incoming = gains[0] <= gains[1] ? 0 : 1;
                    sources[incoming].Stop();
                    sources[incoming].clip = clip;
                    gains[incoming] = 0f;
                    sources[incoming].volume = 0f;
                    sources[incoming].Play();
                }
            }
            for (int i = 0; i < sources.Length; i++) startGains[i] = gains[i];
            elapsed = 0f;
            fadeSeconds = Mathf.Max(.1f, seconds);
        }

        public void Tick(float deltaTime)
        {
            elapsed = Mathf.Min(fadeSeconds, elapsed + Mathf.Max(0f, deltaTime));
            float t = elapsed / fadeSeconds;
            t = t * t * (3f - 2f * t);
            for (int i = 0; i < sources.Length; i++)
            {
                gains[i] = Mathf.Lerp(startGains[i], i == incoming ? 1f : 0f, t);
                if (elapsed >= fadeSeconds && i != incoming && sources[i].isPlaying)
                    sources[i].Stop();
            }
            ApplyVolumes();
        }

        void ApplyVolumes()
        {
            for (int i = 0; i < sources.Length; i++) sources[i].volume = gains[i] * volume;
        }
    }
}
