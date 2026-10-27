# 《冒险界面UI适配与引导》_待办任务清单

> 创建：2026-10-27（主人真机截图反馈）
> 范围：冒险界面（AdventureUI）引导期布局/适配问题 + 切后台后引导丢失。
> 性质：已静态定位根因，本清单只给「改造点 + 验收标准」，编码交子模型落地（GLM/Step-5 走识图修 bug；Shader/数学不在本次范围）。
> 红线：以主人预制体为准，运行时改布局只动锚点/偏移/显隐，不复制资源兜底（AGENTS.md §4）。

---

## 现状
主人真机截图（Clipboard_2026-10-27）显示冒险界面（章节选择）引导期有 5 处问题：
1. 引导时主界面任务栏会「收回」且偏低，疑似未适配安全区；
2. 冒险界面左侧「副本类型标签」（主线/精英/迷宫/每日/活动）整列不见了；
3. 右侧章节标题与顶条资源条重叠；
4. 顶条应只显示「钻石 + 体力」，其余（金币/天赋石）隐藏；
5. 在该界面切出去打字、等一会回来，引导气泡消失。

---

## 任务 1：冒险界面顶条只显示「钻石 + 体力」，隐藏金币/天赋石
- **现象**：冒险页顶条显示 金币/钻石/天赋石/体力 四格；主人要求只留钻石 + 体力。
- **根因**：`AdventureUI.ShowPage`（AdventureUI.cs:283-298）调 `TownSharedChrome.RaiseSharedChrome(hall)` 只是把**主界面共享 TopBar** 提到最前，沿用 `ResourceStrip` 的 `Mode.Main`（四格全显，ResourceStrip.cs:21、:272-287 `SetMode`）；没有按页切显隐。
- **改造点**：
  1. `ResourceStrip.Mode` 增 `DiamondStamina`（钻石+体力）；`SetMode` 增分支：金币/天赋石隐藏、钻石显示；体力列对该模式保持可见（注意 `Show()` 现无条件 `SetStaminaVisible(false)`，:196 — 冒险走 raised 共享条不进 Show，故体力需显式可见）。
  2. `TownSharedChrome.RaiseSharedChrome` 增参数 `ResourceStrip.Mode desiredMode = Mode.Main`，内部 `SetAsLastSibling` 后调 `ResourceStrip.SetMode(desiredMode)`（新增公开静态方法，复用现有私有 `SetMode` 逻辑：只切显隐 + `ApplyRightAlign`，不动几何）。
  3. `AdventureUI.ShowPage` 调 `RaiseSharedChrome(hall)` → 传 `ResourceStrip.Mode.DiamondStamina`；`HidePage` 还原 `ResourceStrip.SetMode(Mode.Main)`（回城/切其它页恢复四格）。
- **验收**：进冒险页顶条仅见「钻石 + 体力」两格、右对齐贴顶；回城/切角色/酒馆恢复四格；角色页 GoldDiaTalent、招募 GoldDia 不受影响。
- **风险**：`ResourceStrip` 是全局单例、`_root`/`_stamina` 跨页共享；务必成对 Show/Release 或 ShowPage/HidePage 还原，避免脏状态泄漏到下一页。

## 任务 2：冒险界面右侧章节标题与顶条资源条重叠
- **现象**：右侧章节标题（如「第一章 边境小镇」金色横幅）被顶条压住/重叠。
- **根因**：`AdventureUI.Fit.cs` 的 `FitAdventureTopBelowResourceStrip`（:1042-1073）只把「Top」（主线冒险横幅）和「LeftSidebar」两块整体下移 `shift` 到资源条之下；**章节标题节点 `RightContent/<MainNode>/TitleBar` 没被同 `shift` 处理**，仍停预制体贴顶位 → 资源条（SafeAreaFitter 已下移）压住它。
- **改造点**：在 `FitAdventureTopBelowResourceStrip` 末尾，对 `right.Find(MainNodeName + "/TitleBar")` 也 `ApplyTopNodeShift(titleBar, shift)`（复用现有 :1076 `ApplyTopNodeShift`）。注意 `right.Find` 直接找不到（TitleBar 挂在 主线 下）；restore 分支（:115-120）要对应补 `RestoreAdventureBase(titleBar)`。
- **验收**：真机瘦屏/微信端，章节标题整条在资源条正下方、间隙一致，不再被压。
- **风险**：标准屏 `ApplyAdventureFit` 提前 return，TitleBar 走 restore 分支不改动；需确认 restore 分支已含 TitleBar（当前 :115-120 只 restore Top/WorldMapBtn）。

## 任务 3：冒险界面左侧「副本类型标签」不显示
- **现象**：左侧 主线/精英/迷宫/每日/活动 标签整列缺失。
- **根因（待真机确认，二选一或叠加）**：
  - a) 层级/遮挡：预制体里 `LeftSidebar` 在 `RightContent` 之前（下层）；瘦屏 `FitAdventureSidebarAboveMap`（:474）才把它抬到 RightContent 之上；标准屏 restore 分支 `RestoreAdventureSidebarOrder`（:486）放回原兄弟序 → 若地图背景/RightContent 盖住左侧宽 170 区域，标签被压在图下不可见。
  - b) 下移过量：`FitAdventureTopBelowResourceStrip` 的 `shift` 在微信端（胶囊预留 44 + 安全区 + 额外 20，SafeAreaFitter.cs:306/312）可能很大，整列下移过多贴到底部导航或屏外。
- **改造点（确认根因后二选一）**：
  - 若是 a：让 `LeftSidebar` 始终位于 RightContent 之上（瘦屏抬层逻辑对标准屏也生效，但保留 DetailPanel 压住其底部）；或确认地图背景不覆盖左 170 列。
  - 若是 b：对 `shift` 做下限钳制（`Mathf.Max(shift, 允许最小下移)`），保证首颗标签最终 y 不越过底部导航顶边。
- **验收**：真机进冒险页左侧标签完整可见、可点；切主线/无限/活动模式切换正常。
- **待确认**：需主人真机发一张「左侧标签缺失」的层级截图，或我方 dump `LeftSidebar` 兄弟序 + `ModeBtn_*` 的 rect/世界角，确认是压在图下还是移出屏。

## 任务 4：引导提示切后台/等待后自动消失
- **现象**：冒险页（引导中）切出去打字、等一会回来，引导气泡没了。
- **根因**：`TutorialHintUI.Show(text, highlight, autoHideSeconds=8f)`（:330）设 `_hideAt = Time.unscaledTime + autoHideSeconds`（:364）；`Update`（:461-474）每帧判断 `Time.unscaledTime >= _hideAt` 就 `Hide()`。App 切后台再回来时 `Time.unscaledTime` 已累加（或首帧 deltaTime 巨大），autoHide 触发 → 引导条消失、该步失去指引。
- **改造点**：`TutorialHintUI` 加 `OnApplicationFocus(false)/OnApplicationPause(true)` 记录 `_bgAt = Time.unscaledTime` 并暂存 `_hideAt`；`OnApplicationFocus(true)/OnApplicationPause(false)` 时把 `_hideAt += (Time.unscaledTime - _bgAt)`（后台时长补回计时器），即「只在聚焦时倒计时」。
- **验收**：切出 30s+ 回来引导气泡仍在、指引不断；该引导步仍可被完成；聚焦时正常超时仍生效（不应永久不消失，硬引导 ShowHard 本就 -1 永显）。
- **风险**：`TutorialHintUI` 是 `DontDestroyOnLoad`，OnApplicationFocus 直接挂它身上即可；软提示默认 8s（部分调用传 5f）聚焦超时后仍应正常隐藏。

## 任务 5：引导期主界面任务栏「收回 + 偏低」未适配
- **现象**：引导时主界面（镇）任务栏/底栏有收起感、且位置偏低，疑似未适配安全区。
- **根因（待确认）**：
  - 底栏 `MainBottomNav` 经 `TownSharedChrome.RaiseSharedChrome`（TownSharedChrome.cs:62-76）只给**顶条**挂了 `SafeAreaFitter.EnsureTopOffset`（:54），**底栏没有任何底部安全区内缩**（`SafeAreaFitter` 有 `Edge.Bottom`/`BottomInsetUnits` 但 RaiseSharedChrome 未调用）→ 带手势条/Home 指示的机型底栏贴到系统条、显得「低」。
  - 引导期（FirstRunGuide 硬引导）四周 Dim 遮罩盖住底栏非目标区，视觉上像「收回」。
- **改造点（确认后）**：
  - 给底栏挂底部避让：新增 `SafeAreaFitter.EnsureBottomOffset`（仿 :331 `EnsureTopOffset`，`edge = Edge.Bottom`、`mode = Offset`），在 `RaiseSharedChrome` 对 `_cachedBottom` 调用。
  - 若「收回」是 Dim 遮罩导致：非 bug（硬引导设计），向主人说明；如需底栏引导中可见，在硬引导步骤把底栏目标纳入挖空或降低遮罩。
- **验收**：带手势条机型底栏上抬到安全区内、不被系统条压；引导中底栏可见且可点（若设计允许）。
- **待确认**：需主人确认「收回」是 Dim 遮罩视觉还是真被 hidden；是否以微信端（胶囊/手势条机型）为主。

---

## 依赖 & 施工顺序
- 任务 1、2 改 `ResourceStrip`/`TownSharedChrome`/`AdventureUI.Fit.cs`，互相独立，可并行。
- 任务 3 依赖真机确认根因（a/b）后再定改造点。
- 任务 4 独立（TutorialHintUI）。
- 任务 5 依赖主人确认「收回」性质后再定。
- 全部改完需真机回归（尤其微信端安全区/胶囊机型）：顶条钻石+体力、章节标题不压、左标签可见、引导切后台不丢。

---

## 实施状态（2026-10-27 续：主人拍板「左侧标签是间隔改动的事，别的改吧」→ 已落地）

> 原清单是架构方案（本会话约定不写完整 C#）。主人确认任务3根因=上次间隔改动、并要求其余一并改掉后，已直接编码落地。

- **任务1 ✅ 已落地**：`ResourceStrip.Mode` 增 `DiamondStamina`；`SetMode` 改 `public static`（钻石恒显；DiamondStamina 隐金币+天赋石；金币加号随金币列显隐）。`AdventureUI.ShowPage` 在 `RaiseSharedChrome` 后调 `SetMode(DiamondStamina)`；`HidePage` 还原 `Mode.Main`。
- **任务2 ✅ 已落地**：`FitAdventureTopBelowResourceStrip` 末尾对 `RightContent/主线`、`RightContent/无限`、`RightContent/活动副本` 三个整列容器也 `ApplyTopNodeShift(shift)`（与 Top/LeftSidebar 同一位移，整列标题↔槽位↔支线岛内部节奏随之整体下移）；restore 分支补这三个容器的 `RestoreAdventureBase`。仅瘦屏路径生效，标准屏无回归。
- **任务3 ✅ 已落地（根因=FitSidebarEvenSpacing 间隔改动）**：`FitSidebarEvenSpacing` 改空实现，以预制体为准、运行时不再重排；`RestoreAdventureSidebarEven` 已就位 no-op 安全。改回旧行为删注释段+还原方法体即可。
- **任务4 ✅ 已落地**：`TutorialHintUI` 增 `_bgLostAt` 字段 + `OnApplicationFocus/OnApplicationPause` + `FreezeHideTimer/ResumeHideTimer`：失焦/暂停冻结 `_hideAt`，回前台把后台时长补回 `_hideAt`（计时从离开处继续）；`Show/Hide` 重置 `_bgLostAt`。
- **任务5 🟡 部分落地**：`MainBottomNav.Awake` 本就自带 `SafeAreaFitter(Edge.Bottom)`；新增 `SafeAreaFitter.EnsureBottomOffset`（仿 `EnsureTopOffset`），`RaiseSharedChrome` 对 `_cachedBottom` 上的 `MainBottomNav` 节点调 `EnsureBottomOffset`（GetComponent 复用已有 fitter，幂等不双倍）。「收回」是否=硬引导 Dim 遮罩仍未确认——若设计如此则非 bug。

### 已知边界（验证时留意）
- 本机无团结编辑器，全部仅静态核对，编译请主人跑。
- 任务2 只移整列容器，**未移 MapRoot（章节地图）**：地图顶仍可能压在资源条下（主人未报此点，属原状）；若真机地图顶被压，需再对 MapRoot 同 shift。
- 任务1 冒险页若叠加打开借走资源条的弹窗（招募/升阶/抽卡），弹窗关后 `Home()` 还原 `Mode.Main`，回冒险页时顶条会短暂四格；常见流程（大厅直进冒险、无顶层弹窗）不受影响。
- 全部改动**未提交**，等主人编辑器编译 + 真机回归。
