using System;
using Hiking.UIUX;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hiking.Journey
{
    /// <summary>A screen-sized transparent Windows window with a movable, fixed-size game frame.</summary>
    public sealed class WindowsTransparentWindow : MonoBehaviour
    {
        const int FrameWidth = 1152;
        const int FrameHeight = 720;
        const int GwlStyle = -16;
        const int WsCaption = 0x00c00000;
        const int WsThickFrame = 0x00040000;
        const uint SwpNoZOrder = 0x0004;
        const uint SwpNoActivate = 0x0010;
        const uint SwpFrameChanged = 0x0020;
        const int RegionOr = 2;
        const int ScreenEdgeMargin = 16;
        const uint MonitorDefaultToNearest = 2;
        const int SmXVirtualScreen = 76;
        const int SmYVirtualScreen = 77;
        const int SmCxVirtualScreen = 78;
        const int SmCyVirtualScreen = 79;
        // Alpha >= 51 in the 1485x994 GameFrame source occupies this bounding box.
        // Keep a rectangle to avoid artifacts when the native region changes during a drag.
        static readonly Rect FramePaintedBounds = Rect.MinMaxRect(
            36f / 1485f, 51f / 994f, 1470f / 1485f, 924f / 994f);
        const float GameplayDragThreshold = 5f;

        [StructLayout(LayoutKind.Sequential)]
        struct Margins
        {
            public int left;
            public int right;
            public int top;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct NativeRect
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct NativePoint
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MonitorInfo
        {
            public int size;
            public NativeRect monitor;
            public NativeRect work;
            public uint flags;
        }

        delegate bool EnumWindowsCallback(IntPtr window, IntPtr parameter);

        [DllImport("user32.dll")]
        static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("kernel32.dll")]
        static extern uint GetCurrentProcessId();

        [DllImport("user32.dll")]
        static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        static extern bool GetClientRect(IntPtr window, out NativeRect rectangle);

        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr window, out NativeRect rectangle);

        [DllImport("user32.dll")]
        static extern bool ClientToScreen(IntPtr window, ref NativePoint point);

        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr window, int index);

        [DllImport("user32.dll", SetLastError = true)]
        static extern int SetWindowLong(IntPtr window, int index, int value);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y,
            int width, int height, uint flags);

        [DllImport("user32.dll")]
        static extern int GetSystemMetrics(int index);

        [DllImport("user32.dll")]
        static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

        [DllImport("dwmapi.dll")]
        static extern int DwmExtendFrameIntoClientArea(IntPtr window, ref Margins margins);

        [DllImport("gdi32.dll")]
        static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);

        [DllImport("gdi32.dll")]
        static extern int CombineRgn(IntPtr destination, IntPtr source1, IntPtr source2, int mode);

        [DllImport("gdi32.dll")]
        static extern bool DeleteObject(IntPtr handle);

        [DllImport("user32.dll", SetLastError = true)]
        static extern int SetWindowRgn(IntPtr window, IntPtr region, bool redraw);

        readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        readonly List<Graphic> widgetGraphics = new List<Graphic>();
        readonly List<Graphic> widgetScratch = new List<Graphic>();
        readonly Vector3[] regionCorners = new Vector3[4];
        IntPtr windowHandle;
        Image frameImage;
        RectTransform frameRoot;
        Camera clearCamera;
        bool draggingFrame;
        bool pendingGameplayDrag;
        bool draggingFloatingWidget;
        Vector2 lastPointer;
        bool windowConfigured;
        bool hasStartupMonitor;
        NativeRect startupMonitor;
        int virtualDesktopLeft;
        int virtualDesktopTop;
        bool regionErrorLogged;
        bool expandedRegionWhileDragging;
        float viewportScale = 1f;
        float lastFittedViewportScale;
        int lastRegionHash;
        SceneLayoutSettings sceneLayout;
        RectTransform titleBackgroundClip;
        RectTransform titleBackground;
        RectTransform titleStartPanel;
        Vector3 titleBackgroundSceneLocalPosition;
        int lastFittedScreenWidth;
        int lastFittedScreenHeight;
        float lastFittedImageWidth;
        float lastFittedImageHeight;

        public static RectTransform ActiveFrameRoot { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void CreateForWindowsPlayer()
        {
#if UNITY_STANDALONE_WIN
            var controller = new GameObject(nameof(WindowsTransparentWindow));
            DontDestroyOnLoad(controller);
            controller.AddComponent<WindowsTransparentWindow>();
#endif
        }

        void Awake()
        {
            Application.runInBackground = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SetupScene();
        }

        void Start()
        {
#if !UNITY_EDITOR
            StartCoroutine(ConfigureWindow());
#endif
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            ActiveFrameRoot = null;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) => SetupScene();

        void SetupScene()
        {
            if (clearCamera != null) Destroy(clearCamera.gameObject);
            ActiveFrameRoot = null;
            frameRoot = null;
            draggingFrame = false;
            pendingGameplayDrag = false;
            draggingFloatingWidget = false;
            expandedRegionWhileDragging = false;
            lastFittedViewportScale = 0f;
            titleBackgroundClip = null;
            titleBackground = null;
            titleStartPanel = null;
            lastFittedScreenWidth = lastFittedScreenHeight = 0;
            lastFittedImageWidth = lastFittedImageHeight = 0f;
            sceneLayout = FindFirstObjectByType<MapProvider>()?.ringSettings?.sceneLayout;

            // The world camera renders only inside its inset viewport. Clear the entire
            // backbuffer first so the untouched area also has zero alpha.
            var clearObject = new GameObject("Transparent Backbuffer Clear");
            clearCamera = clearObject.AddComponent<Camera>();
            clearCamera.depth = -100;
            clearCamera.clearFlags = CameraClearFlags.SolidColor;
            clearCamera.backgroundColor = Color.clear;
            clearCamera.cullingMask = 0;
            clearCamera.allowHDR = false;
            clearCamera.allowMSAA = false;
            clearCamera.rect = new Rect(0, 0, 1, 1);
            clearObject.AddComponent<UniversalAdditionalCameraData>().renderType = CameraRenderType.Base;

            frameImage = null;
            var canvas = FindFirstObjectByType<GameUIController>()?.GetComponent<Canvas>();
            if (canvas == null) return;
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null) scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            // FrameRoot is authored in the scene. Keep a fallback for older scenes.
            frameRoot = canvas.transform.Find("FrameRoot") as RectTransform;
            if (frameRoot == null)
            {
                var rootObject = new GameObject("FrameRoot", typeof(RectTransform));
                frameRoot = (RectTransform)rootObject.transform;
                frameRoot.SetParent(canvas.transform, false);
                string[] framedElements = { "StartPanel", "GameFrame", "PanoramaPanel",
                    "LoadingText", "SignPlacementEditor" };
                foreach (string name in framedElements)
                {
                    var element = canvas.transform.Find(name);
                    if (element != null) element.SetParent(frameRoot, false);
                }
            }
            frameRoot.anchorMin = frameRoot.anchorMax = frameRoot.pivot = new Vector2(.5f, .5f);
            frameRoot.sizeDelta = new Vector2(FrameWidth, FrameHeight);
            frameRoot.anchoredPosition = Vector2.zero;
            // Widgets left directly under Canvas render above the movable frame.
            frameRoot.SetAsFirstSibling();
            ActiveFrameRoot = frameRoot;
            Canvas.ForceUpdateCanvases();

            var frame = frameRoot.Find("GameFrame");
            if (frame != null) frameImage = frame.GetComponent<Image>();
            FitFrameToScreen();
            if (windowConfigured && hasStartupMonitor)
                CenterStartupLayout();
            lastRegionHash = 0;

            var startPanel = frameRoot.Find("StartPanel") as RectTransform;
            var background = startPanel != null ? startPanel.Find("Background") as RectTransform : null;
            if (background == null) return;
            titleStartPanel = startPanel;
            titleBackgroundSceneLocalPosition = background.localPosition;

            var clipObject = new GameObject("Title Background Clip", typeof(RectTransform), typeof(RectMask2D));
            var clip = (RectTransform)clipObject.transform;
            clip.SetParent(startPanel, false);
            titleBackgroundClip = clip;
            Rect viewport = FramedViewport;
            clip.anchorMin = viewport.min;
            clip.anchorMax = viewport.max;
            // Extend the title art beneath the lower frame rail so rounded
            // mask coordinates cannot expose the transparent window between them.
            clip.offsetMin = new Vector2(0f, -8f);
            clip.offsetMax = Vector2.zero;
            background.SetParent(clip, false);
            titleBackground = background;
            PositionTitleBackground();
            clip.SetAsFirstSibling();
            if (windowConfigured) UpdateWindowRegion(true);
        }

        IEnumerator ConfigureWindow()
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (windowHandle == IntPtr.Zero && Time.realtimeSinceStartup < deadline)
            {
                windowHandle = FindPlayerWindow();
                if (windowHandle == IntPtr.Zero) yield return null;
            }

            if (windowHandle == IntPtr.Zero)
            {
                Debug.LogError("Windows player window was not found; transparent frame is unavailable.");
                yield break;
            }

            // A single transparent backbuffer spans the virtual desktop. Unity's
            // pointer coordinates can then follow the frame across monitor edges.
            int desktopLeft = GetSystemMetrics(SmXVirtualScreen);
            int desktopTop = GetSystemMetrics(SmYVirtualScreen);
            int screenWidth = GetSystemMetrics(SmCxVirtualScreen);
            int screenHeight = GetSystemMetrics(SmCyVirtualScreen);
            if (screenWidth <= 0 || screenHeight <= 0)
            {
                Debug.LogError("Could not read the virtual desktop bounds.");
                yield break;
            }
            var initialMonitor = MonitorFromWindow(windowHandle, MonitorDefaultToNearest);
            var initialMonitorInfo = new MonitorInfo { size = Marshal.SizeOf<MonitorInfo>() };
            hasStartupMonitor = initialMonitor != IntPtr.Zero &&
                GetMonitorInfo(initialMonitor, ref initialMonitorInfo);
            if (hasStartupMonitor) startupMonitor = initialMonitorInfo.monitor;
            virtualDesktopLeft = desktopLeft;
            virtualDesktopTop = desktopTop;
            // Remove the native frame before requesting a virtual-desktop backbuffer,
            // so Windows does not reserve room for a title bar around that size.
            int initialStyle = GetWindowLong(windowHandle, GwlStyle);
            SetWindowLong(windowHandle, GwlStyle, initialStyle & ~(WsCaption | WsThickFrame));
            if (Screen.width != screenWidth || Screen.height != screenHeight ||
                Screen.fullScreenMode != FullScreenMode.Windowed)
                Screen.SetResolution(screenWidth, screenHeight, FullScreenMode.Windowed);

            // Wait for Unity's backbuffer before expanding the native borderless window.
            while ((Screen.width != screenWidth || Screen.height != screenHeight) &&
                   Time.realtimeSinceStartup < deadline) yield return null;
            if (Screen.width != screenWidth || Screen.height != screenHeight)
            {
                Debug.LogError($"Could not set the player resolution to {screenWidth}x{screenHeight}.");
                yield break;
            }

            // Unity can recreate its HWND when changing the backbuffer size.
            windowHandle = FindPlayerWindow();
            if (windowHandle == IntPtr.Zero)
            {
                Debug.LogError("Windows player window was lost while changing resolution.");
                yield break;
            }

            int style = GetWindowLong(windowHandle, GwlStyle);
            SetWindowLong(windowHandle, GwlStyle, style & ~(WsCaption | WsThickFrame));
            if ((GetWindowLong(windowHandle, GwlStyle) & (WsCaption | WsThickFrame)) != 0)
            {
                Debug.LogError($"Could not change the player window style: {Marshal.GetLastWin32Error()}");
                yield break;
            }
            if (!SetWindowPos(windowHandle, IntPtr.Zero, desktopLeft,
                    desktopTop, screenWidth, screenHeight,
                    SwpNoZOrder | SwpNoActivate | SwpFrameChanged))
            {
                Debug.LogError($"Could not size the virtual-desktop player window: {Marshal.GetLastWin32Error()}");
                yield break;
            }

            var margins = new Margins { left = -1 };
            int result = DwmExtendFrameIntoClientArea(windowHandle, ref margins);
            if (result != 0) Debug.LogError($"Could not extend DWM glass into the player window: 0x{result:X8}");

            windowConfigured = true;
            FitFrameToScreen();
            if (hasStartupMonitor) CenterStartupLayout();
            UpdateWindowRegion(true);
        }

        void CenterStartupLayout()
        {
            CenterFrameOnMonitor(startupMonitor, virtualDesktopLeft, virtualDesktopTop);
            Rect monitorScreen = new Rect(
                startupMonitor.left - virtualDesktopLeft,
                Screen.height - (startupMonitor.bottom - virtualDesktopTop),
                startupMonitor.right - startupMonitor.left,
                startupMonitor.bottom - startupMonitor.top);
            var hud = FindFirstObjectByType<TravelHudController>(FindObjectsInactive.Include);
            if (hud != null) PlaceWidgetOnMonitor(hud.transform as RectTransform, monitorScreen);
            var notebook = FindFirstObjectByType<SettingsNotebookController>(FindObjectsInactive.Include);
            var notebookDrag = notebook != null ?
                notebook.GetComponentInChildren<FixedWidgetDrag>(true) : null;
            if (notebookDrag != null)
                PlaceWidgetOnMonitor(notebookDrag.transform as RectTransform, monitorScreen);
            foreach (var floating in FindObjectsByType<FloatingWidgetController>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                PlaceWidgetOnMonitor(floating.transform as RectTransform, monitorScreen);
        }

        static void PlaceWidgetOnMonitor(RectTransform widget, Rect monitorScreen)
        {
            var parent = widget != null ? widget.parent as RectTransform : null;
            if (parent == null) return;
            Vector2 anchor = (widget.anchorMin + widget.anchorMax) * .5f;
            Vector2 desktopPoint = new Vector2(Screen.width * anchor.x,
                Screen.height * anchor.y);
            Vector2 monitorPoint = new Vector2(
                monitorScreen.xMin + monitorScreen.width * anchor.x,
                monitorScreen.yMin + monitorScreen.height * anchor.y);
            var canvas = widget.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent, desktopPoint, camera, out var from) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent, monitorPoint, camera, out var to)) return;
            widget.anchoredPosition += to - from;
        }

        void CenterFrameOnMonitor(NativeRect monitor, int desktopLeft, int desktopTop)
        {
            var parent = frameRoot != null ? frameRoot.parent as RectTransform : null;
            if (parent == null) return;
            Canvas.ForceUpdateCanvases();
            Vector2 screenCenter = new Vector2(
                (monitor.left + monitor.right) * .5f - desktopLeft,
                Screen.height - ((monitor.top + monitor.bottom) * .5f - desktopTop));
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent, screenCenter, null, out var localCenter)) return;
            frameRoot.anchoredPosition = ClampFrameToScreen(localCenter - parent.rect.center);
        }

        static IntPtr FindPlayerWindow()
        {
            uint processId = GetCurrentProcessId();
            IntPtr active = GetActiveWindow();
            if (active != IntPtr.Zero && IsWindowVisible(active) &&
                BelongsToProcess(active, processId) && GetClientRect(active, out var activeRect) &&
                activeRect.right > activeRect.left && activeRect.bottom > activeRect.top) return active;

            IntPtr best = IntPtr.Zero;
            long largestArea = 0;
            EnumWindows((candidate, unused) =>
            {
                if (!IsWindowVisible(candidate) || !BelongsToProcess(candidate, processId) ||
                    !GetClientRect(candidate, out var rectangle)) return true;
                long area = (long)(rectangle.right - rectangle.left) * (rectangle.bottom - rectangle.top);
                if (area > largestArea)
                {
                    best = candidate;
                    largestArea = area;
                }
                return true;
            }, IntPtr.Zero);
            return best;
        }

        static bool BelongsToProcess(IntPtr window, uint processId)
        {
            GetWindowThreadProcessId(window, out uint owner);
            return owner == processId;
        }

        void Update()
        {
            if (!Application.isFocused || frameRoot == null || frameImage == null ||
                Mouse.current == null)
            {
                if (draggingFrame) FinishDragging();
                pendingGameplayDrag = false;
                if (draggingFloatingWidget) EndFloatingWidgetDrag();
                return;
            }

            var mouse = Mouse.current;
            Vector2 pointer = mouse.position.ReadValue();
            if (pendingGameplayDrag)
            {
                if (draggingFloatingWidget)
                {
                    pendingGameplayDrag = false;
                    return;
                }
                if (!mouse.leftButton.isPressed)
                {
                    pendingGameplayDrag = false;
                    return;
                }
                if ((pointer - lastPointer).sqrMagnitude <
                    GameplayDragThreshold * GameplayDragThreshold) return;
                pendingGameplayDrag = false;
                draggingFrame = true;
                ExpandRegionForDragging();
            }
            if (draggingFrame)
            {
                if (!mouse.leftButton.isPressed)
                {
                    FinishDragging();
                    return;
                }
                Vector2 next = frameRoot.anchoredPosition + pointer - lastPointer;
                // Round before clamping so the last pixel cannot escape the screen.
                next = new Vector2(Mathf.Round(next.x), Mathf.Round(next.y));
                frameRoot.anchoredPosition = ClampFrameToScreen(next);
                lastPointer = pointer;
                return;
            }
            if (draggingFloatingWidget)
            {
                if (!mouse.leftButton.isPressed) EndFloatingWidgetDrag();
                return;
            }
            if (!mouse.leftButton.wasPressedThisFrame || IsOnInteractiveUI(pointer)) return;
            if (IsOnVisibleFrame(pointer))
            {
                draggingFrame = true;
                ExpandRegionForDragging();
            }
            else if (IsOnGameplayViewport(pointer)) pendingGameplayDrag = true;
            else return;
            lastPointer = pointer;
        }

        void LateUpdate()
        {
            FitFrameToScreen();
            if (titleBackgroundClip != null)
            {
                Rect viewport = FramedViewport;
                if (titleBackgroundClip.anchorMin != viewport.min ||
                    titleBackgroundClip.anchorMax != viewport.max)
                {
                    titleBackgroundClip.anchorMin = viewport.min;
                    titleBackgroundClip.anchorMax = viewport.max;
                    PositionTitleBackground();
                }
            }
            if (windowConfigured && !expandedRegionWhileDragging) UpdateWindowRegion(false);
        }

        Rect FramedViewport => sceneLayout != null ? sceneLayout.FramedViewport :
            SceneLayoutSettings.DefaultFramedViewport;

        public void SetViewportScale(float scale)
        {
            viewportScale = Mathf.Clamp(scale, .7f, 1f);
            FitFrameToScreen();
        }

        void PositionTitleBackground()
        {
            if (titleBackgroundClip == null || titleBackground == null ||
                titleStartPanel == null) return;
            // The clipping rectangle may be offset from the scene's center.
            // Keep the artwork at its original StartPanel position after reparenting.
            titleBackground.position =
                titleStartPanel.TransformPoint(titleBackgroundSceneLocalPosition);
        }

        void FitFrameToScreen()
        {
            if (frameRoot == null || frameImage == null) return;
            var image = frameImage.rectTransform;
            // The illustrated frame extends above and below its 1152x720 layout
            // root. Include those overhangs when fitting on smaller monitors.
            float imageWidth = image.rect.width * Mathf.Abs(image.localScale.x);
            float imageHeight = image.rect.height * Mathf.Abs(image.localScale.y);
            if (Screen.width == lastFittedScreenWidth && Screen.height == lastFittedScreenHeight &&
                Mathf.Approximately(imageWidth, lastFittedImageWidth) &&
                Mathf.Approximately(imageHeight, lastFittedImageHeight) &&
                Mathf.Approximately(viewportScale, lastFittedViewportScale)) return;
            float outerWidth = Mathf.Max(FrameWidth, imageWidth);
            float outerHeight = Mathf.Max(FrameHeight, imageHeight);
            float scale = Mathf.Min(1f,
                (Screen.width - ScreenEdgeMargin * 2f) / outerWidth,
                (Screen.height - ScreenEdgeMargin * 2f) / outerHeight);
            scale = Mathf.Max(.1f, scale);
            frameRoot.localScale = Vector3.one * (scale * viewportScale);
            frameRoot.anchoredPosition = ClampFrameToScreen(frameRoot.anchoredPosition);
            lastFittedScreenWidth = Screen.width;
            lastFittedScreenHeight = Screen.height;
            lastFittedImageWidth = imageWidth;
            lastFittedImageHeight = imageHeight;
            lastFittedViewportScale = viewportScale;
        }

        void ExpandRegionForDragging()
        {
            if (expandedRegionWhileDragging || !windowConfigured || windowHandle == IntPtr.Zero ||
                !GetWindowRect(windowHandle, out var bounds)) return;
            IntPtr region = CreateRectRgn(0, 0, bounds.right - bounds.left,
                bounds.bottom - bounds.top);
            if (region == IntPtr.Zero) return;
            // Keep one stable region while dragging; repeatedly reshaping the
            // native window can leave stale pixels in the DWM composition.
            if (SetWindowRgn(windowHandle, region, true) == 0)
            {
                DeleteObject(region);
                return;
            }
            expandedRegionWhileDragging = true;
        }

        void FinishDragging()
        {
            draggingFrame = false;
            if (!draggingFloatingWidget) RestoreRegionAfterDrag();
        }

        public void BeginFloatingWidgetDrag()
        {
            draggingFloatingWidget = true;
            ExpandRegionForDragging();
        }

        public void EndFloatingWidgetDrag()
        {
            draggingFloatingWidget = false;
            if (!draggingFrame) RestoreRegionAfterDrag();
        }

        void RestoreRegionAfterDrag()
        {
            if (!expandedRegionWhileDragging) return;
            expandedRegionWhileDragging = false;
            UpdateWindowRegion(true);
        }

        void UpdateWindowRegion(bool force)
        {
            if (windowHandle == IntPtr.Zero || frameImage == null || frameRoot == null ||
                !GetWindowRect(windowHandle, out var windowRect)) return;

            // Window regions are measured from the native window's top-left, while
            // Unity UI screen coordinates start at the client's bottom-left.
            var clientOrigin = new NativePoint();
            if (!ClientToScreen(windowHandle, ref clientOrigin)) return;
            int offsetX = clientOrigin.x - windowRect.left;
            int offsetY = clientOrigin.y - windowRect.top;
            RectInt frameBounds = PaintedFrameBounds(
                WindowBounds(frameImage.rectTransform, offsetX, offsetY));
            int hash = frameBounds.GetHashCode();
            var canvas = frameRoot.parent;
            widgetGraphics.Clear();
            for (int i = 0; i < canvas.childCount; i++)
            {
                var widget = canvas.GetChild(i) as RectTransform;
                if (widget == null || widget == frameRoot || !widget.gameObject.activeInHierarchy)
                    continue;
                // A widget's root can stretch across Canvas for layout. Use its
                // actual visible UI graphics so empty layout areas stay clickable
                // on the Windows desktop.
                widgetScratch.Clear();
                widget.GetComponentsInChildren(false, widgetScratch);
                foreach (var graphic in widgetScratch)
                    if (VisibleRegionGraphic(graphic)) widgetGraphics.Add(graphic);
            }
            // TravelUI owns a separate overlay Canvas so its fixed-size HUD and
            // settings notebook do not inherit the movable game frame's scale.
            foreach (var overlay in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (overlay == null || !overlay.isRootCanvas || overlay.transform == canvas ||
                    !overlay.gameObject.activeInHierarchy) continue;
                widgetScratch.Clear();
                overlay.GetComponentsInChildren(false, widgetScratch);
                foreach (var graphic in widgetScratch)
                    if (VisibleRegionGraphic(graphic)) widgetGraphics.Add(graphic);
            }
            // FrameRoot belongs to the painted GameFrame and camera opening.
            // Its child graphics must not turn transparent artwork padding into
            // a rectangular desktop hit area. Independent overlay widgets above
            // are still included in their own regions.
            hash = unchecked(hash * 31 + widgetGraphics.Count);
            foreach (var graphic in widgetGraphics)
                hash = unchecked(hash * 31 + WindowBounds(graphic.rectTransform,
                    offsetX, offsetY).GetHashCode());
            if (!force && hash == lastRegionHash) return;

            IntPtr region = CreateRectRgn(0, 0, 0, 0);
            if (region == IntPtr.Zero) return;
            AddRegionRect(region, frameBounds.xMin, frameBounds.yMin,
                frameBounds.xMax, frameBounds.yMax);
            foreach (var graphic in widgetGraphics)
            {
                RectInt bounds = WindowBounds(graphic.rectTransform, offsetX, offsetY);
                AddRegionRect(region, bounds.xMin, bounds.yMin, bounds.xMax, bounds.yMax);
            }

            // Windows owns the region after a successful SetWindowRgn call.
            // Region changes occur at drag start/end, not for each drag pixel.
            if (SetWindowRgn(windowHandle, region, true) == 0)
            {
                if (!regionErrorLogged)
                {
                    Debug.LogError($"Could not update the transparent window region: {Marshal.GetLastWin32Error()}");
                    regionErrorLogged = true;
                }
                DeleteObject(region);
                return;
            }
            regionErrorLogged = false;
            lastRegionHash = hash;
        }

        static bool VisibleRegionGraphic(Graphic graphic) => graphic != null &&
            graphic.enabled && graphic.gameObject.activeInHierarchy && graphic.color.a > .01f &&
            (!(graphic is Text label) || !string.IsNullOrWhiteSpace(label.text));

        static RectInt PaintedFrameBounds(RectInt imageBounds)
        {
            int left = imageBounds.xMin + Mathf.FloorToInt(imageBounds.width * FramePaintedBounds.xMin);
            int right = imageBounds.xMin + Mathf.CeilToInt(imageBounds.width * FramePaintedBounds.xMax);
            int top = imageBounds.yMin + Mathf.FloorToInt(imageBounds.height * (1f - FramePaintedBounds.yMax));
            int bottom = imageBounds.yMin + Mathf.CeilToInt(imageBounds.height * (1f - FramePaintedBounds.yMin));
            return new RectInt(left, top, right - left, bottom - top);
        }

        static void AddRegionRect(IntPtr region, int left, int top, int right, int bottom)
        {
            if (right <= left || bottom <= top) return;
            IntPtr addition = CreateRectRgn(left, top, right, bottom);
            if (addition == IntPtr.Zero) return;
            CombineRgn(region, region, addition, RegionOr);
            DeleteObject(addition);
        }

        RectInt WindowBounds(RectTransform rect, int offsetX, int offsetY)
        {
            rect.GetWorldCorners(regionCorners);
            Vector2 lowerLeft = RectTransformUtility.WorldToScreenPoint(null, regionCorners[0]);
            Vector2 upperRight = RectTransformUtility.WorldToScreenPoint(null, regionCorners[2]);
            return new RectInt(
                Mathf.FloorToInt(lowerLeft.x) + offsetX,
                Mathf.FloorToInt(Screen.height - upperRight.y) + offsetY,
                Mathf.CeilToInt(upperRight.x) - Mathf.FloorToInt(lowerLeft.x),
                Mathf.CeilToInt(Screen.height - lowerLeft.y) -
                    Mathf.FloorToInt(Screen.height - upperRight.y));
        }

        bool IsOnVisibleFrame(Vector2 pointer)
        {
            var frame = frameImage.rectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(frame, pointer, null, out var local))
                return false;
            Rect rect = frame.rect;
            float u = Mathf.InverseLerp(rect.xMin, rect.xMax, local.x);
            float v = Mathf.InverseLerp(rect.yMin, rect.yMax, local.y);
            if (!rect.Contains(local))
                return false;

            return FramePaintedBounds.Contains(new Vector2(u, v));
        }

        bool IsOnGameplayViewport(Vector2 pointer)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    frameRoot, pointer, null, out var local)) return false;
            Rect rect = frameRoot.rect;
            if (!rect.Contains(local)) return false;
            float u = Mathf.InverseLerp(rect.xMin, rect.xMax, local.x);
            float v = Mathf.InverseLerp(rect.yMin, rect.yMax, local.y);
            return FramedViewport.Contains(new Vector2(u, v));
        }

        Vector2 ClampFrameToScreen(Vector2 position)
        {
            if (frameRoot == null || frameImage == null) return position;

            // Probe the proposed position, then restore it before returning.
            // The image rect contains generous transparent padding, so clamp its
            // painted pixels (including the outer leaves) rather than the rect.
            Vector2 previous = frameRoot.anchoredPosition;
            frameRoot.anchoredPosition = position;
            frameImage.rectTransform.GetWorldCorners(regionCorners);
            Vector2 lowerLeft = RectTransformUtility.WorldToScreenPoint(null, regionCorners[0]);
            Vector2 upperRight = RectTransformUtility.WorldToScreenPoint(null, regionCorners[2]);
            frameRoot.anchoredPosition = previous;

            float width = upperRight.x - lowerLeft.x;
            float height = upperRight.y - lowerLeft.y;
            float left = lowerLeft.x + width * FramePaintedBounds.xMin;
            float right = lowerLeft.x + width * FramePaintedBounds.xMax;
            float bottom = lowerLeft.y + height * FramePaintedBounds.yMin;
            float top = lowerLeft.y + height * FramePaintedBounds.yMax;
            float padding = 0f;
            float correctionX = left < padding ? padding - left :
                right > Screen.width - padding ? Screen.width - padding - right : 0f;
            float correctionY = bottom < padding ? padding - bottom :
                top > Screen.height - padding ? Screen.height - padding - top : 0f;
            if (correctionX == 0f && correctionY == 0f) return position;

            // Convert screen pixels back to the parent's anchored coordinates.
            var parent = frameRoot.parent as RectTransform;
            if (parent == null) return position;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,
                    Vector2.zero, null, out var origin) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,
                    new Vector2(correctionX, correctionY), null, out var corrected))
                return position;
            return position + corrected - origin;
        }

        bool IsOnInteractiveUI(Vector2 pointer)
        {
            if (EventSystem.current == null) return false;
            uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = pointer }, uiHits);
            foreach (var hit in uiHits)
            {
                if (ExecuteEvents.GetEventHandler<IPointerDownHandler>(hit.gameObject) != null ||
                    ExecuteEvents.GetEventHandler<IBeginDragHandler>(hit.gameObject) != null)
                    return true;
            }
            return false;
        }
    }
}
