# 投放面板接入

`ResourceAllocationPanel.prefab` 是共用视图，已作为嵌套预制体放入 `TravelUI.prefab`。
继续使用 `ShiyuanUISence.unity`，没有新增场景。现有相机的预设事件连接
`ResourceAllocationBinding.OpenPreset()`。设置界面保持位于面板上层。
从相机打开设置时取消投放草稿并关闭投放面板，避免两个编辑面板重叠操作。

## 数据流

- 用 `SetPresetSnapshot(seed, water, seedMaximum, waterMaximum)` 提供当前预设与上限。
  默认快照为 1 / 1，上限 3 / 3；接入方可修改上下文上限，快照并非存档。
- 点位入口调用 `OpenPoint(pointId, seed, water, seedHeld, waterHeld)`。
  外部先决定使用点位独立值还是全局预设，再将结果传入。
- 订阅 `ResourceAllocationBinding.Submitted` 获取 `AllocationResult`：模式、点位 ID、
  种子和水分整数值。外部负责保存并回传最新快照。
- 动态库存变化时，点位模式调用 `Panel.RefreshLimits(seedHeld, waterHeld)`。
  下调上限只限制临时草稿，不会自动提交。0 上限禁用对应滑条，显示 0 份。
- `Panel.Open` 可传入自定义标题与单行说明。两种模式最小值都是 0。
- `Panel.Cancelled` 可用于外部关闭通知。取消只丢弃草稿，不修改外部状态。
- 重复打开同一模式、同一点位不关闭也不重置草稿；切换到其他点位会载入新草稿。

不在 UI 中实现扣除资源、到达检测、点位独立状态存档或预设传播。
确认表示提交计划，不表示立即消耗。不要把 20 写死在面板中。

## kevin/build 接入提醒（由组装方执行）

- 当前统一定位方式：`AllocationFrameDock` 已保存在投放面板预制体中；组装方调用 `SetFrame(RectTransform)` 提供游戏外框。两种入口均从外框左上角向上展开，木牌下边缘贴住外框上沿，左边缘对齐左侧木框内沿。ArtworkBounds 排除外框透明留白，InnerLeft 指定左内沿；可按正式素材校准。木牌宽度固定为外框 ArtworkBounds 有效显示宽度的三分之一，整体等比例随外框缩放；展开时间为 0.24 秒。动效为整块木牌保持比例向上滑动，通过运行时 SlideAperture 的 RectMask2D 在外框上沿裁切；关闭时沿原路向下滑回。动效不做纵向拉伸或透明度渐变。
- 展开期间暂不允许操作滑条/按钮，结束后恢复；同一入口重复点击不重播动效。取消/确定发出结果后反向收回，收回完成才隐藏，期间禁用交互且不重复提交；宿主收到结果后不要直接销毁/隐藏面板。外框移动/缩放时每帧更新连接位置及投放 UI 比例。玩家不能单独拖动或缩放投放木牌；设置页和悬浮组件仍保持各自固定尺寸。本组件不拖动或缩放游戏外框。
- `DemoGameFrame` 仅属于现有 `ShiyuanUISence`，使用正式外框素材，760×475 UI 单位，可拖拽以检查跟随；不在 TravelUI 或投放面板预制体内。正式 Frame 引用默认留空，由组装方赋值。宿主应约束外框位置，给外框上方预留至少一个木牌高度的可见空间；不自动翻到下方，也不单独缩小木牌。

- 订阅 `Panel.EditingStarted(mode, pointId)` 和 `EditingEnded(mode, pointId)`：单点编辑时保持游戏 EditingSlot/暂停该点投放倒计时，结束时释放。确认事件在结束事件之前发送；取消、切换目标、禁用面板根对象均结束旧编辑。重复同一入口不重复发送开始事件。
- `Panel.PositionAboveLeft` 为旧的手动定位接口，启用 FrameDock 时不要同时调用，以免两套定位覆盖。当前采用上述统一外框连接方式，UI 不自行查找场景对象。
- 单点 OpenPoint 的最后两项必须传入游戏实际允许的上限。当前游戏 SetPlan/RestorePlan 限制 3，不能只传库存 20 却让保存端截断。未来规则放开时由接入方同步调整。
- 旧 SignPlacementEditor 保持原样。组装方负责两套入口交接，避免同时显示，并确保同点重复点击不会在游戏侧重置草稿。
- 接入方负责预设传播时跳过 HasCustomPlan/Committed 点位、保存预设及点位计划、更新库存快照。UI 不调用 TryPlace、不修改游戏配置资产。
- 提交预制体、依赖素材/.meta、UIUX 脚本和本说明；当前没有自动接入 GameScene，不能把演示效果当作已完成正式投放接入。

## 演示与美术

`AllocationDemoControls` 仅存在于整合场景，模拟两个点位、库存切换和内存保存，
不属于 TravelUI 预制体。正式接入时由真实游戏状态与场景气泡取代。
原图复制在 `Assets/Arts/UIUX/ResourceAllocation`；Sprite 以矩形区域排除留白，
不改动 Downloads 原图。资源徽章继续引用 TravelHUD 图像。
沿用现有 nishiki-teki 字体和固定像素 Canvas。新面板的左上锚点可在 Inspector 调整；
演示场景已放入可拖动外框；正式游戏外框引用仍由组装方传入。

提交应包括新脚本、预制体、美术/Sprite 及 .meta、修改后的 TravelUI 和整合场景。
不提交 Library 截图、个人配置或本机 MCP package 变更。
