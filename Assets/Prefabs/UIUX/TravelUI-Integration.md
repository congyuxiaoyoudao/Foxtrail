# Travel UI 接入说明

独立演示中将 `TravelUI.prefab` 放在场景根层级。它自带 Screen Space Overlay Canvas、CanvasScaler 和 GraphicRaycaster。场景保留一个 EventSystem，使用项目现有 InputSystemUIInputModule；总预制体不重复创建 EventSystem。

正式接入 kevin/build 由组装方完成：透明窗口只收集游戏 Canvas 层级的可见 Graphic。需将 UI 内容放入该 Canvas 下与 FrameRoot 同级的独立层，保持原有像素尺寸，处理多余 Canvas，避免受 FrameRoot 缩放影响。当前交付不自动挂接、不修改 GameScene、不关闭旧弹窗。

内部保留 TravelHUD、SettingsNotebook 与 ResourceAllocationPanel 三个嵌套预制体。SettingsRequested 已绑定同一实例内的 SettingsNotebookController.Open。设置默认关闭，打开后全屏遮罩拦截下层点击，关闭按钮恢复正常操作。设置显示时，相机可按原 hover 规则收起。

## 尺寸规则

- HUD 自身缩放从 0.75 改为 0.5，宽高均为原来的三分之二。
- 总 Canvas 使用 Constant Pixel Size，固定 scaleFactor=1.264911，对齐本次调整前 1920×1080 演示中的显示比例。
- 设置面板缩放为 0.75（上一版 0.5 的 1.5 倍），文字、滑条和侧边标签一起等比例缩放；窗口改变只影响居中位置和遮罩尺寸，不改变设置与 HUD 的像素大小。
- 视窗滑条仅发出 `SettingsNotebookController.ViewportScaleRequested(float scale)`，默认演示范围 0.7～1，初始化显示 1.00×；下限不是策划硬限制，由接入方通过 SetViewportSnapshot 配置。组装方以同一倍率同步调整外框、内部游戏显示与相机视口；不得缩放设置页/HUD，也不得用它改变桌面透明窗口分辨率。
- `SetViewportSnapshot(scale, minimum, maximum)` 回填当前倍率与范围，不发事件、不存档；最终持久化由接入方负责。初始化也不发缩放请求。
- 设置页正文空白处、HUD 非控件区域可拖动；按钮和滑条不启动窗口拖动。关闭重开保留本次位置，跨会话位置保存由接入方处理。
- `SetNotebookScale(float)` 与 `TravelHudController.SetWidgetScale(float)` 仅供程序配置尺寸；玩家没有缩放手柄或滚轮缩放入口。拖动不改变尺寸。

## 验证

左上角电源按钮已替换为 `camera-exit.png`，显示区域裁去透明留白，尺寸为 60×36.54 UI 单位，保持素材比例；随首层向左展开一起移动。按钮支持点击及悬停/按下反馈。

退出接口：订阅 `TravelHudController.ExitRequested`。UI 只发出请求，不调用退出、不保存游戏、不修改旧弹窗。组装方负责退出确认、保存及最终退出流程；可在 Inspector 绑定，也可运行时 AddListener/RemoveListener。请勿在同一实例上重复绑定退出处理。

打开 `Assets/Scenes/ShiyuanUISence.unity`，悬停相机，点击设置，切换视窗/音量页，关闭后再次打开。演示场景提供点位和模拟库存入口，供检查 UI 交互；不作为正式游戏状态来源。

视窗请求、拖动和投放生命周期需按交付检查验证；实际 Windows 透明区域、拖动边界和组装后的显示仍需成品验收。音量事件尚未接入游戏 AudioMixer。全屏设置遮罩会阻止下层交互，组装时需确认它对透明桌面区域的影响。

## 提交

提交四个预制体、使用的 UIUX 运行脚本及 asmdef、实际依赖的 Fonts/SettingsNotebook/TravelHUD/ResourceAllocation 美术与材质/Shader、相关测试、整合演示场景、本说明及相应 .meta。本次不交付历史生成工具和旧窗口分辨率逻辑。

不要提交 Library、临时截图、外部实验目录、个人配置、.vsconfig 或本机 Funplay package 的 manifest/packages-lock 变更。不要只提交 TravelUI.prefab 而漏掉三个嵌套预制体。无需修改 GameScene；没有修改构建场景列表。

## 组装方接口速查

- `TravelHudController.SetValues`：回填站点、种子、水分；`ExitRequested`：接入保存/确认/退出。
- `ResourceAllocationBinding.SetPresetSnapshot` / `OpenPreset` / `OpenPoint`：提供预设、点位计划及实际允许上限，最小值为 0。
- `Submitted`：保存计划，不立即消耗资源；预设传播跳过独立设置点位。
- `Panel.RefreshLimits`：刷新上限；`EditingStarted` / `EditingEnded`：对接游戏编辑状态，避免编辑期间自动投放。
- `AllocationFrameDock.SetFrame`：绑定游戏外框，确认/取消后保留收回动画，不立即隐藏 UI。
- `ViewportScaleRequested`：同时缩放外框与内部游戏；HUD 和设置页保持独立固定尺寸。
- `MasterVolumeChanged` / `OperationVolumeChanged` / `BgmVolumeChanged`：接入音频系统。当前设置页使用 ShiyuanUI 音量 PlayerPrefs，需与正式设置保存统一，并在初始化时读取对应音量属性应用到混音器。
- 正式组装协调 Canvas、EventSystem、透明窗口点击区域和旧弹窗入口；本次不改 GameScene。
