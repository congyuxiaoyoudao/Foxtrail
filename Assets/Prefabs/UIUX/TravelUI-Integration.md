# Travel UI 接入说明

独立演示中将 `TravelUI.prefab` 放在场景根层级。它自带 Screen Space Overlay Canvas、CanvasScaler 和 GraphicRaycaster。场景保留一个 EventSystem，使用项目现有 InputSystemUIInputModule；总预制体不重复创建 EventSystem。

`GameScene.unity` 已放入 TravelUI 预制体实例，使用独立 Overlay Canvas 保持 HUD 和设置页的固定像素尺寸。`TravelUIBridge` 将站点、库存、预设与点位投放、退出、视窗缩放及总音量接入游戏。透明窗口将 TravelUI 的可见 Graphic 计入可点击区域。

内部保留 TravelHUD、SettingsNotebook 与 ResourceAllocationPanel 三个嵌套预制体。SettingsRequested 已绑定同一实例内的 SettingsNotebookController.Open。设置默认关闭，打开后全屏遮罩拦截下层点击，关闭按钮恢复正常操作。设置显示时，相机可按原 hover 规则收起。

## 尺寸规则

- HUD 自身缩放从 0.75 改为 0.5，宽高均为原来的三分之二。
- 总 Canvas 使用 Constant Pixel Size，固定 scaleFactor=1.264911，对齐本次调整前 1920×1080 演示中的显示比例。
- 设置面板缩放为 0.75（上一版 0.5 的 1.5 倍），文字、滑条和侧边标签一起等比例缩放；窗口改变只影响居中位置和遮罩尺寸，不改变设置与 HUD 的像素大小。
- 视窗滑条仅发出 `SettingsNotebookController.ViewportScaleRequested(float scale)`，GameScene 使用 0.7～1 的范围。`TravelUIBridge` 按同一倍率调整 FrameRoot，游戏相机视口跟随画框；设置页/HUD 和桌面透明窗口分辨率不变。
- `SetViewportSnapshot(scale, minimum, maximum)` 回填当前倍率与范围，不发事件；GameScene 通过 `Hiking.ViewportScale` PlayerPrefs 保存当前倍率。
- 设置页正文空白处、HUD 非控件区域可拖动；按钮和滑条不启动窗口拖动。关闭重开保留本次位置，跨会话位置保存由接入方处理。
- `SetNotebookScale(float)` 与 `TravelHudController.SetWidgetScale(float)` 仅供程序配置尺寸；玩家没有缩放手柄或滚轮缩放入口。拖动不改变尺寸。

## 验证

左上角电源按钮已替换为 `camera-exit.png`，显示区域裁去透明留白，尺寸为 60×36.54 UI 单位，保持素材比例；随首层向左展开一起移动。按钮支持点击及悬停/按下反馈。

退出接口：`TravelHudController.ExitRequested` 只发出请求。GameScene 的 `TravelUIBridge` 在标题页退出程序，在旅途中保存并返回标题页，完成时先保存作品再返回标题页。

打开 `Assets/Scenes/ShiyuanUISence.unity`，悬停相机，点击设置，切换视窗/音量页，关闭后再次打开。演示场景提供点位和模拟库存入口，供检查 UI 交互；不作为正式游戏状态来源。

视窗请求、拖动和投放生命周期需在 GameScene 实机检查；实际 Windows 透明区域、拖动边界和组装后的显示仍需成品验收。总音量已接到 AudioListener；项目尚无操作音效/BGM 的 AudioMixer 分组，因此两路独立音量仍只保存在 UI 设置中。全屏设置遮罩会阻止下层交互。

## 提交

提交四个预制体、使用的 UIUX 运行脚本及 asmdef、实际依赖的 Fonts/SettingsNotebook/TravelHUD/ResourceAllocation 美术与材质/Shader、相关测试、整合演示场景、本说明及相应 .meta。本次不交付历史生成工具和旧窗口分辨率逻辑。

不要提交 Library、临时截图、外部实验目录、个人配置、.vsconfig 或本机 Funplay package 的 manifest/packages-lock 变更。不要只提交 TravelUI.prefab 而漏掉三个嵌套预制体。GameScene 使用 TravelUI 实例；没有修改构建场景列表。

## 组装方接口速查

- `TravelHudController.SetValues`：回填站点、种子、水分；`ExitRequested`：接入保存/确认/退出。
- `ResourceAllocationBinding.SetPresetSnapshot` / `OpenPreset` / `OpenPoint`：提供预设、点位计划及实际允许上限，最小值为 0。
- `Submitted`：保存计划，不立即消耗资源；预设传播跳过独立设置点位。
- `Panel.RefreshLimits`：刷新上限；`EditingStarted` / `EditingEnded`：对接游戏编辑状态，避免编辑期间自动投放。
- `AllocationFrameDock.SetFrame`：绑定游戏外框，确认/取消后保留收回动画，不立即隐藏 UI。
- `ViewportScaleRequested`：同时缩放外框与内部游戏；HUD 和设置页保持独立固定尺寸。
- `MasterVolumeChanged` / `OperationVolumeChanged` / `BgmVolumeChanged`：接入音频系统。当前设置页使用 ShiyuanUI 音量 PlayerPrefs，需与正式设置保存统一，并在初始化时读取对应音量属性应用到混音器。
- GameScene 已连接 Canvas、EventSystem、透明窗口点击区域和点位编辑入口。
