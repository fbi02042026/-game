# 《点击地点集结与 Boss 红圈躲避》待办任务清单

> 状态：**点击移动 + 红圈改造已落地（未提交）**；疾风窗口按主人 2026-11-09 拍板「先不修改」冻结。
> 本机无引擎，只做了静态核对（换行 / 花括号 / 调用点 / diff check），**编译与真机手感请主人在团结 2026.3.62t16 跑一次**。
> 定稿日期：2026-11-09 ｜ 基线提交：`632561fa`（工作区有 46 个改动，未提交）
> 依据：AGENTS.md §2.4（活干一半必交清单）、§4（不兜底 / 最小增量 / 不动美术排版 / 出错只报位置）

---

## 一、现状（改造前）

### 1.1 已有可复用（逐条带位置）

| 能力 | 位置 | 说明 |
|---|---|---|
| 手动移动底层 | `Hero.cs:373-401`、`Hero.cs:420-478` | `SetManualMove` / `ClearManualMove` / `BeginAutoAcquireOnRelease` 完整保留；手动分支含 X 位移、车道 Y 钳制、走路动画、朝向、视野钳制、边走边打 |
| 战场空白区点击 | `BattleManager.Skills.cs:476-488` | `IsBattlefieldTap()` 已按鼠标 / 触摸分别用 `IsPointerOverGameObject` 过滤 HUD |
| 点击范围框（Y） | `BattleUI.prefab:19798 m_Name: walk`、父 `:20606 map`；`BattleLaneBounds.cs:14, 214-263` | 主人摆的「地图上能走的那块地面」矩形，投影到世界算车道上下界，**唯一真源**；可视化标记 `BattleLaneArea`（`:321-364`） |
| 点击范围框（X） | `BattleManager.Waves.cs:1016-1034` | `GetBattleVisibleX(out minX,out maxX,pad)` = 当前相机视野 ± pad；相机不动则该区间固定 |
| Boss 红圈（已完整） | `Monster.Skills.cs:135-137`、`680-717`、`833-861`；`Monster.Boss.cs:43` | 圆心锁定、半透呼吸放大、波纹扩散；注释明确「预警期跑出圈即真躲开」；阶段切换圈半径 4.5 |
| 预警时长 | `GameConfig.Combat.cs:818-820, 853` | Boss 一阶段 3s / 二阶段 2.2s / 精英盾 3.2s（走 CombatTuningTable） |
| 引导开关 | `TutorialDirector.cs:41-48` `IsTutorialBattle`、`:55-59` `GuideCovers`；`BattleManager.State.cs:55` `UnitsCanAct` | 引导冻场时 `UnitsCanAct=false`（`TutorialDirector.Battle.cs:69` 等） |
| 全队列表 / 阵型 | `BattleManager.cs:153`（hero 入 `allyUnits`）、`:520/:605/:689`（佣兵）；`UnitCrowd.cs:162-175`、`Mercenary.AI.cs:414-470` | `allyUnits` 含英雄，故红圈伤害能打到英雄；佣兵按 `GetMercDesiredCombatX` 排队形 |

### 1.2 阻塞项：疾风窗口 UI 根本没显示（**只报位置，未改；主人 11-09 拍板「先不修改」**）

| 位置 | 原文 / 事实 | 判断 |
|---|---|---|
| `Assets/Resources/Prefabs/Battle/BattleUI.prefab` | 全文件搜不到「能量槽」「能量」「抬手」节点，只有 `walk` / `map` | 2026-10-28 说好摆进预制体的节点组不在这个预制体里 |
| `GaleWindowHud.cs:31-36` | `FindDeepChildIgnoreCase(transform, "能量槽")` → null → `Debug.LogError(...节点缺失...)` → `return` | 三个引用全为 null |
| `GaleWindowHud.cs:54` / `:63` | `if (_galeEnergyRoot == null) return;` / `if (_galeEnergyRoot == null \|\| !activeSelf) return;` | 进度条、抬手动画全部静默不显示 |
| `GaleWindowHud.cs:44-46` | 只找「抬手」Animator，且是窗口期持续播放 | **没有「点击瞬间」的手势反馈**，与主人要的「手势点击动画」不是一回事 |
| `BattleManager.Waves.cs:1094` | `TickGaleWindow();` 每帧照常调度 | 状态机没停，只是 UI 不显示 |
| `BattleManager.Skills.cs:453` | `EndGaleWindow` 无条件 `TriggerTeamAttackSpeedBuffFromLoadout(...)` | 「不点不亏」照给攻速 buff |
| `KillComboAfterimage.cs:35` | 残影只在攻速增益期间出现 | 主人看到的「自动重影 + 攻速加快」＝**窗口后台空转结算**，不是旧自动释放 |
| `BattleManager.Skills.cs:62` | 自动释放池已 `continue` 跳过 `gale_stance` | 改造不是没做，是卡在 UI 节点 |

**结论**：窗口 4s 开窗→倒计时→结算这条链在跑，但 UI 层因找不到预制体节点全部 return；没点也上攻速 → 视觉上就是「还是自动」。按纪律只报位置，等主人确认节点在哪 / 是否改运行时建树，**不私自造节点、不复制资源兜底**。

---

## 二、目标（主人 2026-11-09 口径）

1. 战斗中点击地面某点 → 我方**全体**（英雄 + 佣兵）移动到该点。
2. **首人到达**后 **2 秒**才恢复自动索敌；**到达该地时有敌人则立即**恢复索敌（2026-11-09 主人拍板「按首人到达」）。
3. 点击区域＝已有范围框（`walk` 的 Y + `GetBattleVisibleX` 的 X），**只在摄像机范围内可点，点击不导致相机移动 / 推图**。
4. **引导期间禁止点击**。
5. 玩法目的：躲 Boss 红圈，多一种操作玩法；**不点不亏**，不点战斗照旧。

---

## 二·五、本轮已落地（2026-11-09，未提交）

| 项 | 文件 | 做法 |
|---|---|---|
| 集结状态机（新增） | `Assets/Scripts/Combat/BattleRally.cs`（新建，团结已生成 `.meta`） | 吃点击、屏幕点→世界落点、首人到达 2 秒驻留、到达后有敌人立即恢复、打断/清档复位 |
| 输入挂点 | `BattleManager.Waves.cs:1093` | `BattleRally.Tick()` 挂 `Update`，复用同一早退闸 |
| 落点范围 | `BattleRally.cs` → `GetBattleVisibleX`（X 越界忽略）+ `BattleLaneBounds.ClampLaneOffset`（Y 钳边界） | 相机不动、不推图；可行走区仍只有 `BattleLaneBounds` 一个出口 |
| 引导禁点 | `BattleRally.cs:46` | `bm.IsTutorialRun`（与 `IsTutorialBattle` 同口径，无 `FindObjectOfType` 开销） |
| 英雄走位 | `Hero.cs:421-495` | 复用被停用的 `SetManualMove` 手动分支；集结期间不索敌不出手；到点站立不滑步 |
| 佣兵全体 | `Mercenary.AI.cs:194` | `EnsureAiPolicy()` 后加优先分支，敌方佣兵（竞技场机器人）不参与 |
| 清档复位 | `NewGameReset.cs:36` | `BattleRally.Reset()` |
| 红圈二维判定 | `Monster.Skills.cs:830` `ApplySkillDamage(..., Vector3 center)` | `dx² + (dy/0.35)² > r²` 才免伤；Y 按扁椭圆视觉比例折算，演出与判定同位 |
| 双圈 | `Monster.Skills.cs:667-692` `CreateTelegraphDisc(..., bool isProgress)` / `CreateTelegraphPair` | 浅色范围圈（alpha 0.28）第一秒就全尺寸；深色进度圈（alpha 0.85）从缩放 0 线性放大到满范围，用时＝预警时长；层级 11/12/13 |
| 分类与时长 | `GameConfig.Combat.cs` 新常量 | 精英 ≤2.6 / 3 秒，Boss ≥4.5 / 4 秒；表 `aoeRadius` 仍作基准只夹上下限 |
| Boss 阶段切换 | `Monster.Boss.cs:37-88` | 同步双圈 + 4 秒 + 二维结算 |
| 落点动效 | `BattleRally.cs` 动效段 + 主人摆的 `BattleUI / 集结 / {旗子, 圈}` | 按名字绑定（兼容 prefab 的 Unicode 转义名）；**圈由大变小**缩到预制体当前大小（2.4→1 倍）、旗子从上落下 120 单位，两者同一进度 **0.18s 同时到位**；整组 **2 秒后隐藏**；进战斗首帧自动隐藏、两个 Image 的 `raycastTarget` 运行时关掉（不挡点击）；找不到节点 LogError 不兜底 |

## 三、任务清单（改造点 + 验收）

### T1 可行走区补「点是否在框内」出口
- 文件：`Assets/Scripts/Combat/BattleLaneBounds.cs`（在 `ClampLaneOffset` 附近最小增量加一个 public 判定）
- 做法：复用 `TryReadWalkRange`（`:214`）算 Y 世界区间，X 走 `BattleManager.GetBattleVisibleX`；对外只给一个 `ContainsWorldPoint(Vector3)` 之类的布尔出口。
- 验收：框内点返回 true、框外（含 HUD 下方、屏外）返回 false；`map` 展开动画期间结果与可视化标记一致。
- 依赖：无。风险：🔴 别处**不许再算一遍**（该类是唯一真源）；fail closed：读不到 walk 直接 LogError 返回 false，不静默放行。

### T2 点击输入与闸门
- 文件：新增 `Assets/Scripts/Combat/BattleRallyInput.cs`（或挂 `BattleManager` 的 partial），调用点 `BattleManager.Waves.cs:1079 Update`
- 做法：复用 `IsBattlefieldTap`（`BattleManager.Skills.cs:476`）的过滤口径；追加闸门：`TutorialDirector.IsTutorialBattle` / `GuideCovers`、`UnitsCanAct`、`BattleLootMode.Active`、`_stageCleared`、`PortalWalkMode`、任意弹窗打开态。
- 验收：引导 15 步全程点击无任何反应；抽卡 / 结算 / 死亡 / 剧情冻场期间不响应；点技能槽 / 背包 / 闪避按钮不响应。
- 依赖：T1。风险：疾风窗口期（`BattleManager.Skills.cs:409-413`）同一次点击会「多打一次 + 集结」双响应，需明确互斥或并行（见待确认 7）。

### T3 屏幕点 → 世界落点
- 文件：同 T2；换算参照 `BattleLaneBounds.cs:269-274 ToGroundPlaneY`
- 做法：正交相机 `ScreenToWorldPoint`，z 取 `unitRoot.position.z`；Y 用 `BattleLaneBounds.ClampLaneOffset` 钳、X 用 `GetBattleVisibleX(0.35f)` 钳。**目标点必须落在当前相机视野内**，因此 `CameraFollow`（`CameraFollow.cs:181-231`）不会因点击产生滚动。
- 验收：点屏幕最左 / 最右，落点被钳在视野内且不出现相机位移；真机刘海 / 不同宽高比下不越界。
- 依赖：T1、T2。风险：🔴 相机仍在按原逻辑跟随英雄；若英雄被推到视野边缘要确认不会慢慢滚镜头。

### T4 集结状态机
- 文件：新增 `Assets/Scripts/Combat/BattleRallyState.cs`（纯状态，不碰渲染）
- 字段：落点、是否激活、全队到达时刻、恢复索敌时刻（到达 +2s）、打断条件。
- 做法：再次点击＝改落点重置计时；`UnitsCanAct=false` / 死亡 / 战斗结束 / 引导开始 / 弹窗打开 → 立即清状态并恢复自动。
- 验收：连续点两次不叠加状态；任一打断条件触发后 1 帧内恢复原有自动索敌。
- 依赖：T2。风险：🔴 状态字段须进 `NewGameReset.ApplyAll()`（T8），否则清档后残留。

### T5 英雄走目标点
- 文件：`Assets/Scripts/Unit/Hero.cs:403-478`（最小增量，不重写）
- 做法：集结中每帧按「落点 − 当前位置」算方向喂 `SetManualMove`；到达阈值内停。禁止复用 `ClearManualMove` 的「0.25s 后回自动」（`GameConfig.cs:25`），必须等 T4 的 2 秒。
- 验收：点左能逆着推图走；到达后 2 秒内不自动追怪；`Hero.cs:365 HERO_MAX_FRAME_DT` 单帧步长钳制仍生效。
- 依赖：T4。风险：手动分支 `Hero.cs:461-469` 仍会攻击射程内敌人，与「不索敌」不冲突但需确认（待确认 3）。

### T6 全体佣兵集结
- 文件：`Assets/Scripts/Unit/Mercenary.AI.cs:414-470`、`UnitCrowd.cs:162-175`
- 做法：AIUpdate 里加一条**优先于** `FollowHeroOrIdle` 的集结分支，目标 X = 落点 + 该佣兵原有阵型偏移（近战前排 / 远程后排不变）；Y 走车道钳制。场上红圈预警中不得因 `_skillTelegraphing` 之外的逻辑被拉回。
- 验收：英雄 + 全部存活佣兵都到达指定区域；到达后队形不乱、不叠人（`UnitCrowd.ResolveOverlap` 仍生效）。
- 依赖：T4、T5。风险：🔴 若只挪英雄让收绳自然跟随，会出现「英雄已到、佣兵还在路上」，2 秒计时口径失真（待确认 6）。

### T7 到达判定 + 2 秒驻留 + 立即索敌
- 文件：同 T4，判定遍历 `BattleManager.allyUnits`（含 hero，见 `BattleManager.cs:153`）
- 做法（**已落地**）：**任一**存活我方进入落点阈值（按 X，0.35）→ 记到达时刻 → 2 秒后清集结；**到达之后**才判落点 1.2 半径内有无存活怪，有则立即清集结（点击当下就判会导致点怪堆全队不出发）。
- 验收：空地落点＝站满 2 秒再打；怪堆落点＝立刻开打；Boss 红圈预警 2.2~3s 内跑出圈不掉血。
- 依赖：T5、T6。风险：🔴 恢复自动时不得残留 `_manualMove` / `_manualReleaseUntil`，否则会重复踩 `Hero.cs:480-500` 的松手冷却。

### T8 落点反馈 / 手势动画
- 文件：待定（现成件 `Combat/TargetIndicator.cs` 是「攻击目标」专用，不宜直接复用）
- 做法：落点贴地光圈 / 点击手势，走运行时建树；新增 Canvas 必须 `UICanvasSetup.ApplyPopup(canvas, sortOrder)`，禁裸 Overlay。
- 验收：点下立刻有反馈、集结结束消失；不挡射线、不挡单位。
- 依赖：待确认 2（美术）。风险：🔴 缺资源直接 LogError，**禁止复制资源到 `Resources/` 补图 / 写运行时兜底**。

### T9 复位与阶段闸
- 文件：`Assets/Scripts/Core/NewGameReset.cs:21 ApplyAll()`、`BattleManager.State.cs:55`
- 做法：T4 的进程级状态在清档时统一复位，别散着写。
- 验收：清档 → 重进战斗，集结状态为未激活、无残留落点。
- 依赖：T4。风险：无。

### T10 疾风窗口 UI 补齐（阻塞项，单独做）
- 文件：`Assets/Resources/Prefabs/Battle/BattleUI.prefab`、`UI/GaleWindowHud.cs`
- 做法：等主人确认节点位置 / 是否改运行时建树后再定；并补「点击瞬间」手势反馈（现只有窗口期持续抬手）。
- 验收：CD 好 + 身边有怪 → 出现 4s 倒计时能量条；窗口内点空白区出现手势动画并多打一次（×0.8）；不点不出现多打但仍上基础攻速。
- 依赖：待确认 1。风险：🔴 不动美术排版，改前先问。

---

## 四、待确认项（拿不准的报主人，不自己造兜底）

1. 「能量槽 / 能量 / 抬手」节点在哪个预制体？还是要改成运行时建树？（涉及美术排版）
2. 点击落点标记 / 手势动画用现成资源还是新做？有没有现成美术？
3. ✅ 已定：按**首人到达**。驻留期间当前实现为**不索敌、不出手**；若主人希望「站着还能还手」需另拍板。
4. 「落点有敌人」当前取 **1.2 世界单位**（`BattleRally.ENEMY_NEAR_RADIUS`），待真机试手感后定；是否改成攻击射程 / 红圈半径？
5. ✅ 已改成二维判定（`dx² + (dy/0.35)²`）。⚠️ Y 按扁椭圆视觉比例折算，**不是纯欧氏距离**——目的是让「看到的圈」和「挨不挨打」一致（项目 10-19 已踩过「躲了照样挨打」）。若主人要纯圆距离请明说。另：Boss 阶段 2 半径还会 ×1.35（约 6.1），可能大于可走 X 范围，需真机验证躲不躲得掉。
6. 全队集结时阵型：按集结点重排，还是各人保持相对英雄的原有偏移？
7. 疾风窗口期同一次点击：移动与「多打一次」互斥还是并行？（疾风暂缓期间未处理，当前会双响应）
8. ✅ 已落地（主人 11-09 补摆节点 + 10:58 更正口径）：圈**由大变小**缩到预制体当前大小，与旗子下落**同时**完成；0.18s 出完、2 秒消失。三个数值是我按「出来快点」取的默认，可调：`BattleRally.cs` 的 `MARKER_ANIM_SEC=0.18` / `MARKER_FLAG_DROP=120` / `MARKER_RING_FROM_MUL=2.4`，主人试手感后报数我再改。
9. 疾风窗口 UI：`BattleUI.prefab` 里中文节点名是 Unicode 大写转义存的，**节点和组件其实都在**（外层/内层「能量槽」、「能量」是 Filled Image、「抬手」带 Animator）；上轮「节点不存在」的判断是踩了转义的坑，已更正。主人授权改节点命名，但本轮按「先不修改」冻结。真正不显示的原因需主人在 Console 看报哪条 `LogError`，或确认窗口是否压根没开（开局 30s CD 且需携带疾风）。

---

## 五、施工红线

- 只做最小增量，**禁止删掉重建**；不动美术排版，改前先问。
- **不兜底**：取不到节点 / 资源直接 `LogError`，不复制资源到 `Resources/`、不写「缺了才补」的运行时逻辑。
- 可行走区唯一读出口是 `BattleLaneBounds`，别处不许再算一遍；56 字符 guid 不管、不扫、路径加载。
- 界面 Canvas 一律摄像机模式 `UICanvasSetup.ApplyPopup`；弹窗真模态铺满 `raycast=1`。
- 新增进程级状态统一在 `NewGameReset.ApplyAll()` 复位。
- C# 写完本机只能静态核对（括号 / `#if` / 字段），**编译请主人在团结 2022.3.62t16 跑一次**；「改了没生效」先查编译错误。
- 不主动提交；主人说「提交」＝ commit + `git push origin main` 一次做完。
- 活干一半按 AGENTS.md §2.4 更新本清单一起提交。

---

## 六、验收总表

| 场景 | 期望 |
|---|---|
| 不点 | 战斗与现在完全一致（不点不亏） |
| 点空地（框内） | 全队走过去 → 站 2 秒 → 恢复自动索敌；相机不滚动 |
| 点怪堆 | 全队到位立刻开打 |
| Boss 红圈亮起时点圈外 | 全员在 2.2~3s 预警内跑出圈，砸下时不掉血 |
| 点框外 / 屏外 | 落点被钳到框内，无相机位移、无推图 |
| 引导 15 步期间 | 点击完全无反应 |
| 抽卡 / 结算 / 死亡 / 剧情冻场 | 点击无反应，状态被清干净 |
| 清档重开 | 无残留集结状态 |
