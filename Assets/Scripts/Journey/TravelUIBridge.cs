using Hiking.UIUX;
using UnityEngine;

namespace Hiking.Journey
{
    // Connects the self-contained TravelUI prefab to the journey without putting
    // game state or save rules into the reusable UIUX assembly.
    public sealed class TravelUIBridge : MonoBehaviour
    {
        const string ViewportScaleKey = "Hiking.ViewportScale";
        GameFlowController flow;
        TravelHudController hud;
        ResourceAllocationBinding binding;
        ResourceAllocationPanel panel;
        SettingsNotebookController settings;
        AllocationFrameDock dock;
        WindowsTransparentWindow transparentWindow;
        FixedWidgetDrag hudDrag, notebookDrag;
        RectTransform frameRoot;
        int presetSeeds, presetWater;

        public bool IsPointOpen => panel != null && panel.IsOpen && panel.Mode == AllocationMode.Point;

        public void Initialize(GameFlowController owner)
        {
            flow = owner;
            binding = FindFirstObjectByType<ResourceAllocationBinding>();
            if (binding == null)
            {
                Debug.LogError("GameScene is missing the TravelUI prefab.", this);
                return;
            }
            hud = binding.GetComponentInChildren<TravelHudController>(true);
            panel = binding.Panel;
            settings = binding.GetComponentInChildren<SettingsNotebookController>(true);
            dock = panel.GetComponent<AllocationFrameDock>();
            transparentWindow = FindFirstObjectByType<WindowsTransparentWindow>();
            frameRoot = transform.Find("FrameRoot") as RectTransform;
            var frame = frameRoot != null ? frameRoot.Find("GameFrame") as RectTransform : null;
            if (dock != null) dock.SetFrame(frame);

            presetSeeds = Mathf.Clamp(flow.Config.defaultPlacementSeeds, 0, 3);
            presetWater = Mathf.Clamp(flow.Config.defaultPlacementWater, 0, 3);
            binding.Submitted += OnSubmitted;
            panel.EditingEnded += OnEditingEnded;
            hud.ExitRequested.AddListener(OnExitRequested);
            hud.SeedDetailsRequested.AddListener(binding.OpenPreset);
            hud.WaterDetailsRequested.AddListener(binding.OpenPreset);

            hudDrag = hud.GetComponent<FixedWidgetDrag>();
            notebookDrag = settings.GetComponentInChildren<FixedWidgetDrag>(true);
            if (hudDrag != null) RegisterDrag(hudDrag);
            if (notebookDrag != null) RegisterDrag(notebookDrag);

            float scale = Mathf.Clamp(PlayerPrefs.GetFloat(ViewportScaleKey, 1f), .7f, 1f);
            ApplyViewportScale(scale);
            settings.SetViewportSnapshot(scale, .7f, 1f);
            settings.ViewportScaleRequested += OnViewportScaleRequested;
            AudioListener.volume = settings.MasterVolume;
            settings.MasterVolumeChanged += OnMasterVolumeChanged;
            flow.UI.SetBgmVolume(settings.BgmVolume);
            settings.BgmVolumeChanged += flow.UI.SetBgmVolume;
        }

        void RegisterDrag(FixedWidgetDrag drag)
        {
            drag.DragStarted += OnDragStarted;
            drag.DragEnded += OnDragEnded;
        }

        void OnDragStarted() => transparentWindow?.BeginFloatingWidgetDrag();
        void OnDragEnded() => transparentWindow?.EndFloatingWidgetDrag();

        void OnViewportScaleRequested(float scale)
        {
            ApplyViewportScale(scale);
            PlayerPrefs.SetFloat(ViewportScaleKey, scale);
        }

        void ApplyViewportScale(float scale)
        {
            if (transparentWindow != null) transparentWindow.SetViewportScale(scale);
            else if (frameRoot != null) frameRoot.localScale = Vector3.one * scale;
        }

        void OnMasterVolumeChanged(float volume) => AudioListener.volume = volume;

        public void OpenSettings() => settings?.Open();

        void OnExitRequested()
        {
            if (flow.Map == null) flow.UI.QuitGame();
            else if (flow.Activity == JourneyActivity.Complete)
            {
                if (!flow.IsFinalized) flow.TryFinalize();
                if (flow.IsFinalized) flow.ReturnToStart();
                else flow.UI.Notify("作品保存失败，请重试");
            }
            else flow.UI.SaveAndQuit();
        }

        public void OpenPoint(PlacementSlot slot)
        {
            if (binding == null || slot == null) return;
            flow.Placement.GetPlanLimits(slot, out int seedLimit, out int waterLimit);
            binding.OpenPoint(slot.slotId, slot.PlannedSeeds, slot.PlannedWater,
                seedLimit, waterLimit);
        }

        public void ClosePoint()
        {
            if (IsPointOpen) panel.Cancel();
        }

        void OnSubmitted(AllocationResult result)
        {
            if (result.Mode == AllocationMode.Preset)
            {
                presetSeeds = result.Seed;
                presetWater = result.Water;
                ApplyPresetToMap();
                if (flow.Map != null) flow.SaveNow();
                return;
            }

            var slot = flow.Placement.EditingSlot;
            if (slot == null || slot.slotId != result.PointId) return;
            slot.SetPlan(result.Water, result.Seed);
            flow.SaveNow();
        }

        void OnEditingEnded(AllocationMode mode, string pointId)
        {
            if (mode != AllocationMode.Point) return;
            var slot = flow.Placement.EditingSlot;
            if (slot != null && slot.slotId == pointId) flow.Placement.CancelEditor();
        }

        public void ApplyPresetToMap()
        {
            if (flow.Map == null) return;
            foreach (var station in flow.Map.stations)
                foreach (var slot in station.slots)
                    slot.ApplyPreset(presetWater, presetSeeds);
        }

        public void SyncPresetFromMap()
        {
            if (flow.Map == null) return;
            foreach (var station in flow.Map.stations)
                foreach (var slot in station.slots)
                    if (!slot.HasCustomPlan && !slot.Committed)
                    {
                        presetWater = slot.PlannedWater;
                        presetSeeds = slot.PlannedSeeds;
                        return;
                    }
        }

        void LateUpdate()
        {
            if (flow == null || binding == null) return;
            int station = flow.Map != null ? flow.Session.StationIndex + 1 : 0;
            hud.SetValues(station, flow.Session.StationCount,
                flow.Session.Stock("seed"), flow.Session.Stock("water"));
            // A preset describes future stops, so it may exceed today's stock.
            binding.SetPresetSnapshot(presetSeeds, presetWater, 3, 3);
            if (IsPointOpen)
            {
                var slot = flow.Placement.EditingSlot;
                if (slot == null || slot.slotId != panel.PointId) panel.Cancel();
                else
                {
                    flow.Placement.GetPlanLimits(slot, out int seeds, out int water);
                    panel.RefreshLimits(seeds, water);
                }
            }
        }

        void OnDestroy()
        {
            PlayerPrefs.Save();
            if (binding != null) binding.Submitted -= OnSubmitted;
            if (panel != null) panel.EditingEnded -= OnEditingEnded;
            if (hud != null)
            {
                hud.ExitRequested.RemoveListener(OnExitRequested);
                hud.SeedDetailsRequested.RemoveListener(binding.OpenPreset);
                hud.WaterDetailsRequested.RemoveListener(binding.OpenPreset);
            }
            if (settings != null)
            {
                settings.ViewportScaleRequested -= OnViewportScaleRequested;
                settings.MasterVolumeChanged -= OnMasterVolumeChanged;
                settings.BgmVolumeChanged -= flow.UI.SetBgmVolume;
            }
            if (hudDrag != null) UnregisterDrag(hudDrag);
            if (notebookDrag != null) UnregisterDrag(notebookDrag);
        }

        void UnregisterDrag(FixedWidgetDrag drag)
        {
            drag.DragStarted -= OnDragStarted;
            drag.DragEnded -= OnDragEnded;
        }
    }
}
