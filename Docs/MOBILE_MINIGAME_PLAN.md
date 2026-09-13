# 像素冒险：裂隙之刃 — 玩法修正 + 聚光灯优先 + 多端优化方案

> **对照文档**：[`ARCHITECTURE_REVIEW.md`](./ARCHITECTURE_REVIEW.md)、[`OPTIMIZATION_PLAN.md`](./OPTIMIZATION_PLAN.md)（Phase 0–6 已合 main）。
> **证据日期**：2026-09-12，编辑器 2022.3.62t12（Tuanjie 1.10.0），`Assets/Scripts` 262 个文件 / 7.4 万行。
> **排期原则（已定）**：**先冲 TapTap 聚光灯，完成后再做微信小游戏端与手机端。**
> **铁律不变**：不改 `*.prefab`、不新增散落 `IsTutorialRun` / 平台 `if`、战斗数值改动必须在本文写明并附对照。

---

## 0. 排期与优先级总览

| 阶段 | 内容 | 时机 |
|------|------|------|
| **S1** | 玩法修正 + 打出可提交包（keystore / 锁竖屏 / 版本号）+ 真机冒烟 | **聚光灯之前** |
| **S2** | 报名、建游戏页、开发者日志、商店资料、传包开 TapPlay、过稳定性 | 聚光灯提交期（运营为主） |
| **S3** | 小游戏端 + 手机端：包体瘦身 → 战斗帧止血 → 平台层 → 出包闭环 | **聚光灯之后**，按 §4 顺序逐项做 |

---

## 1. 本次已落地（玩法修正，S1）

### 1.1 引导关「实际怪比表里多」= 真 bug（已修）

`WavePlanner.QueueTutorialWaveCore` 的补刷判据被**同帧缓存**骗了：

```
:154  aliveBefore = CountAliveMonsters()     ← 建立本帧缓存 = 0
:155  TrySpawnTutorialWave(...)              ← 刷主波（协程按 stagger 陆续出怪）
:157  if (CountAliveMonsters() == 0)         ← 仍读本帧缓存 0 → 误判"没刷出来"
:161/:166  EmergencySpawn*(n)                ← 再补一整波，数量翻倍
```

实测当量：6→8、7→10、8→12。缓存实现 `BattleManager.cs:1238`（只按 `Time.frameCount` 失效，刷怪后未失效）。

| 文件:行号 | 改法 |
|-----------|------|
| `WavePlanner.cs:157` | `== 0` → `<= aliveBefore`（主波一只都没刷出来才补刷） |
| `BattleManager.cs` | 新增 `InvalidateAliveMonsterCache()`；`SpawnMonster` 入列后、`OnMonsterDead` 时调用 |

副作用：`ForceSpawn`（正式关首波，`:411/:428` 的 `after <= 0`）此前同样读到脏值而多补兜底波，一并被修掉。

### 1.2 回滚「引导关 −30%」

上一轮以为是数值问题，实为上述 bug。已回滚：`TutorialRules.MonsterCountMul` 引导侧 `0.7f → 1.0f`。
**引导关人数真源 = `tutorial_battle.csv`（6/7/7/7/8，共 35 只），代码不再叠乘。**
`ScaleMonsterCount()` 保留（正式关恒 1 透传），临时调难度才动 `MonsterCountMul`。

### 1.3 玩家攻击速度 −20%（仅 Hero）

- 新常量 `GameConfig.PLAYER_ATTACK_SPEED_MUL = 0.8f`
- 应用点 `UnitBase.GetAttackCooldown()`（唯一换算点），在 `this is Hero` 分支乘
- **不用 `isAlly`**，否则连带佣兵一起变慢。佣兵、怪物节奏不变
- 与武器种族系数、连杀加速、被动攻速乘法叠加；`UnitAnimation.PlayAttack` 的锁自动跟随冷却，无需另改

### 1.4 全部弹道 −20% + 速度真实化

六种弹道（敌方普攻 / 我方普攻 / Bow / Orb / 怪物技能 / 玩家技能）**全部汇入** `BattleVFXSystem.ProjectileFlightCoroutine`（`SkillSystem:147/181` 两条路径最终也调 `PlaySkillProjectile` → 同一协程），故一处生效。

- 新常量 `GameConfig.PROJECTILE_SPEED_GLOBAL_MUL = 0.8f`，乘在 `:388` 的速度上
- **去掉 `maxFlightTime`（1.2s）截断**：它把怪物弹道（speedMul 0.196 / 0.138）顶在上限，降速对它无效、敌我不对称。现在 `duration = max(minFlightTime, distance / speed)`，即真实的「每秒移动 N 个单位」
- 弹道仍朝**发射瞬间**的目标点直线飞行、不追踪（保持现状）

### 1.5 残影改为与攻速技能绑定（不单独出现）

先修一个现存 bug：`ApplyTeamBuff` 只用 `attr.AddAttr()` 永久写值，`gale_stance` 表里声明的 `duration=6f` **从未被消费** → 放一次 +35% 攻速永久生效，且全项目没有 buff 容器可查。

| 文件:行号 | 改法 |
|-----------|------|
| `SkillCastService.cs` | 新增静态计时 `_teamAtkSpdBuffUntil` / `_teamAtkSpdMul` 与 `IsTeamAttackSpeedBuffActive` / `GetTeamAttackSpeedMul()`；攻速类增益（percent + duration>0）**不再走 AddAttr**，改记到期时间 |
| `UnitBase.GetAttackCooldown` | `isAlly` 分支乘 `GetTeamAttackSpeedMul()`，到期自动回到 1 |
| `KillComboAfterimage.LateUpdate` | 门控换成 `IsTeamAttackSpeedBuffActive`；**删掉连杀倍率门控与 `IsMoving()`**（策划确认：攻速技能期间无条件出现）；间隔与透明度改固定值 |

效果：有残影 ⇔ 正在吃攻速 buff。副作用：gale_stance 由「永久 +35%」变为「6 秒 +35%」，是修 bug 的必然结果。

### 1.6 上一轮已落地（保留）

- `GameConfig.SHOW_MONSTER_STACK_LABEL = false`：关闭怪物重叠 ×N 角标（每个角标本要 5 个 TextMesh），`UnitCrowd.TickMonsterOverlapStacks` 早退、零分配
- 修 ×N 误判根因：原 `MonsterFootprintsOverlap` **只比 X 不看车道 Y**，上下两排错开的怪被并成「一簇」。判定已加 Y 轴，需要时把开关改回 `true` 即可
- 簇统计静态缓冲化（原每 0.12s 分配 List / int[] / 2×Dictionary / 2 个闭包）

---

## 2. 目标与硬约束

| 端 | 目标 | 硬约束 |
|----|------|--------|
| 手机端 | 中低端机（骁龙 6 系 / 4GB）战斗稳定 30fps | 已 `Application.targetFrameRate = 30`（`GamePerf.cs:13`），内存峰值 < 600MB |
| 微信小游戏 | 首屏 < 5s、战斗 30fps、主包 ≤ 4MB | `WeChatMiniGameConfig.MainPackageSoftLimitMb = 4`；WebGL 无多线程、GC 抖动放大 3–5 倍；`PlayerPrefs` 映射 `wx.setStorage`（约 10MB 配额） |

两端共用一套代码，平台差异只出现在 `Assets/Scripts/Platform/`，业务代码禁止 `UNITY_ANDROID` / `UNITY_WEBGL` 分支。

---

## 3. 架构审查结论（S3 用）

**加分项（别误伤）**：战斗主循环无 LINQ、无每帧 `Physics2D.Overlap*`、无 `OnTriggerStay2D`；日志已收敛到 `GamePerf`；引导/正式关共用 `SpawnWave` 单一轨。

### P0 — 小游戏端必崩

| # | 位置 | 问题 | 修法 |
|---|------|------|------|
| P0-1 | `BattleVFXSystem.cs:581/233/275/559/604/623/737` | 每次播放特效连调 8–10 次 `GetComponentsInChildren`，每次命中都触发 | 按 prefab 实例 ID 缓存组件数组 |
| P0-2 | `BattleVFXSystem.cs:392` | 弹道 `Instantiate` + `Destroy` + 每次 `StartCoroutine`，绕过对象池 | 弹道入池，改常驻 Update 驱动 |
| P0-3 | `KillComboAfterimage.cs` | 每次残影新建 1+N 个 GameObject 再销毁 | 残影池化（本次已动此文件，S1 顺手做） |
| P0-4 | `MonsterHealthBar.cs:369→245` | LateUpdate 每帧改 `anchorMax`，N 只怪 = N 次 Canvas 重建 | ratio 变化 < 0.5% 跳过 |
| P0-5 | `Assets/Art` 813.7MB、`Assets/Resources` 88MB 整包 | 对照主包 4MB 高两个数量级，Resources 无法分包 | 见 §4 第 1 步 |

### P1 — 手机端掉帧

`BattleManager.Update:1332` 五段 O(N) 扫描降频；`BattleUI:1697` 每帧插值写 Text（改整数变化才写）；`UnitAnimation:631` 攻击期间每帧 `ReapplyWeaponVisuals`；`SkillSystem:48` / `MercPassiveRunner:204` 每帧 `new List`；`BattleSideHud:289` 倒计时每帧拼串；`BattleHeadTalkUI:194`、`CameraFollow:203` LateUpdate `GetComponent`；`Monster:1212` 池复用时 `Resources.Load` 无缓存。

### 平台与发布

| 维度 | 现状 | 结论 |
|------|------|------|
| 平台抽象 | `Platform/` 仅 4 个文件，**全工程零 `RuntimePlatform` 分支** | 需 `IPlatformServices` |
| 广告 | `RewardedAdBridge` 无 SDK 仍返回 true | 可白嫖刷新，上架必被拒 → 改 false |
| 安全区 | 全库 `SafeArea` 零命中；四向 autorotate 全开 | 加 `SafeAreaPanel` + 锁竖屏 |
| 帧率 | 全局硬锁 30，不分档 | 按平台下发 60/45/30 |
| 构建 | `CliAndroidBuild` 只有 APK/Windows；`WeChatShipChecklist` 只是弹窗文本；keystore 空、`bundleVersionCode` 恒 1 | 补小游戏 CLI + 真校验 + 版本号自增 |
| 存档 | 同步 File IO + 整包镜像单个 `PlayerPrefs` key | 小游戏端需分片存储 |

---

## 4. S3 优化项执行顺序（聚光灯之后）

| 顺序 | 阶段 | 工期 | 内容 |
|------|------|------|------|
| 1 | **B 资源瘦身（低成本部分）** | 1 天 | 删第三方 Demo 目录、164 个 WAV(149MB) 转压缩、2 段视频(28MB) 外置 |
| 2 | **A 战斗帧止血（剩余项）** | 1–2 天 | 弹道入池、`BattleManager.Update` 降频、P0-1 组件缓存、P0-4 血条降频、日志收敛 |
| 3 | **C 平台硬伤** | 1 天 | 假广告桩改 false、`SafeArea`、锁竖屏、帧率分档 |
| 4 | **B3 Resources 迁移** | 3–5 天 | 88MB → 主包 ≤ 4MB，按目录分批，每批跑通全链路 |
| 5 | **C1/C5 平台接口与存档** | 3–5 天 | `IPlatformServices` + 小游戏端分片存储 |
| 6 | **D 出包闭环** | 2 天 | 小游戏构建 CLI、清单升级为真校验、包体门禁与性能基线进 CI |

### S1 聚光灯前顺带做（低成本、收益可见）

`BattleVFXSystem` 组件缓存、`MonsterHealthBar` 血条降频、HUD 文本只在整数变化时赋值、`Monster` 精灵 `Resources.Load` 加缓存、`KillComboAfterimage` 残影池化。**不做**弹道入池等大改。

---

## 5. 风险与副作用（需实测）

| 项 | 风险 | 处理 |
|----|------|------|
| 弹道 | 去掉上限后怪物弹道飞行时间 1.2s → 约 2.2s，伤害结算推迟、`PROJECTILE_IMPACT_MISS_DIST=0.55` 更容易判 miss → 远程怪隐性变弱 | 实测命中率，必要时回调 `MISS_DIST` 或怪物 `speedMul` |
| 弹道 | 我方远程（游侠）体感变钝 | 与策划确认，必要时给我方保留补偿 |
| 攻速 | 与武器种族系数、连杀加速乘法叠加，极端值冷却可能过短 | 实测 `GetAttackCooldown` 实际值 |
| 残影 | gale_stance 由永久变 6 秒（削弱）；攻速 buff 期间站桩也出残影 | 已知会，属修 bug 的必然结果 |
| 引导关 | 修完 bug 怪量减半，难度明显下降，教学节奏需重验 | 跑引导全程 |
| 编译 | 本机 Unity CLI 因许可证不可用、C# 编译器被安全策略禁止，**无法自动编译** | 每次改完必须在编辑器内确认无 error |

---

## 6. 本次实现对照

| 文件 | 改动 |
|------|------|
| `Scripts/Combat/WavePlanner.cs` | 补刷判据 `<= aliveBefore`；`SpawnMonster` 后失效缓存；两条裸 `Debug.Log` 改 `GamePerf` |
| `Scripts/Managers/BattleManager.cs` | 新增 `InvalidateAliveMonsterCache()`；`OnMonsterDead` 调用它 |
| `Scripts/Combat/TutorialRules.cs` | `MonsterCountMul` 回 1.0 + 注释说明真源与坑 |
| `Scripts/Unit/UnitBase.cs` | Hero 攻速 ×0.8；`isAlly` 乘团队攻速 buff 倍率 |
| `Scripts/Core/GameConfig.cs` | 新增 `PLAYER_ATTACK_SPEED_MUL`、`PROJECTILE_SPEED_GLOBAL_MUL` |
| `Scripts/Systems/BattleVFXSystem.cs` | 弹道速度 ×0.8；去掉 `maxFlightTime` 截断（字段保留并标注废弃） |
| `Scripts/Combat/SkillCastService.cs` | 攻速 buff 改计时倍率 + 静态查询接口 |
| `Scripts/Combat/KillComboAfterimage.cs` | 门控换攻速 buff；删连杀与 `IsMoving` 门控；间隔固定 |

## 7. 验收清单

- [ ] 编辑器内编译无 error
- [ ] 引导关每波实际出怪 = 表内 6/7/7/7/8（共 35），无翻倍；正式关 1-0 怪量与上一版一致
- [ ] 玩家出手间隔 ×1.25；佣兵与怪物节奏不变
- [ ] 六种弹道飞行时间均 ×1.25；最远射程仍能命中；远程怪命中率无明显崩塌
- [ ] gale_stance 只在 6 秒内出残影（站桩也有），技能结束立即停止；连杀不再单独触发残影
- [ ] 引导全程 + 1-0 手感对照通过
