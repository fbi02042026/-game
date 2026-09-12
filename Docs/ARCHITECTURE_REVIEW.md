# 像素冒险：裂隙之刃 — 架构评审

> **范围**：只读审查 `main` 现况（约 v0.3.5 后战斗手感/表驱动一轮）。不改玩法、不改预制体、不批量改名。  
> **读者**：产品 + 程序。结论按「现在卡什么 / 扩内容会卡什么 / 先别动什么」排列。  
> **证据日期**：2026-09-11，基于仓库当前 `main`。  
> **落地计划**：[`OPTIMIZATION_PLAN.md`](./OPTIMIZATION_PLAN.md)（Phase 1–3 已合 main；Phase 4 引导刷怪合流已合；Phase 5 按需减税已在后续 PR 落地可安全项，WavePlanner/SDK 仍按需）。

---

## 1. 执行摘要

主闭环（Boot→Town→Battle→结算回城）和 CSV→Cook→`.bytes` 管线已经立住，近期职业攻、射程、裂隙装备、受击回能、引导分波都在往「表驱动、少发明倍率」靠。  
真正会咬人的不是缺功能，而是 **战斗状态、刷怪、技能、引导、结算全挤在 `BattleManager`（约 3676 行）**，引导用几十处 `IsTutorialRun` 插进正式管线。  
数值真源仍是「表 + `GameConfig` 常量 + ScriptableObject + C# 静态表」四套并存；装备表有 30+ 词条，战斗只落地约 12 种。  
能量有三套（受击回蓝 / 怪物自充 / 雷击击杀充能），自动战斗按钮未接线。  
扩章节、新职业、新词条、新战斗模式时，**先画边界再加内容**，不要先拆大类。

---

## 2. 现状分层（便于对照后面条目）

```
Boot / PersistentRoot     SaveSystem, ConfigManager, GameSceneManager, Story/Tutorial
Town（切页不切场景）       TownHubController + Adventure/Tavern/Talent/Character
Battle                    AutoGameInitializer → GameRoot 上一打 Singleton
        ┌───────────────┬───────────────┬───────────────┐
        │ BattleManager │ UnitBase/*    │ Tables/SO     │
        │ 波次/能量/结算 │ 普攻/AI/VFX   │ Cook + Config │
        └───────────────┴───────────────┴───────────────┘
联网                      CloudSave / RewardedAd / 微信 — 全是 Stub
```

| 层 | 主要入口 | 体量印象 |
|----|----------|----------|
| 场景流 | `GameSceneManager` + `GameSceneGate` | 三场景分工清楚 |
| 战斗编排 | `BattleManager` | ~3676 行 God-object |
| 单位 | `UnitBase` / `Hero` / `Mercenary` / `Monster` | 1282 / 454 / 830 / 1637 |
| 引导/剧情 | `TutorialDirector` / `StoryDirector` / `Chapter1Story` | 引导 ~1011 行，与 BM 双向耦合 |
| 数据 | `GameDataCooker` → `GameTableStore` + SO/`GameConfig`/`TalentDefs` | 已 Cook 约 29 张表 |
| 成长/装备 | `SaveData` + `RiftEquipGenerator` + `GridBackpackSystem` | 裂隙表已立，落地不全 |
| UI | `BattleUI` / `AdventureUI` / `TalentUI` | 各约 1600–2059 行 |
| 平台 | `Assets/Scripts/Platform/*` | 无真实 SDK |

---

## 3. 值得保留（先别拆）

这些已经在干活，扩内容应**顺着用**，不要为了「更干净」重写。

1. **三场景 + Town 内切页**  
   `GameSceneManager` / `GameSceneGate` 注释与实现一致：只有进 Battle 走 Loading；Town 页预加载。这是当前流程稳定性的底座。

2. **CSV → Cook → `Resources/Data/Tables` + Editor 回退源表**  
   `GameDataCooker.CookAll`、`GameTableStore`、`ContentPaths` 让加新表成本低。明文/加密双模式和 `ConfigFingerprint` 思路正确。

3. **射程已收口到 `AttackRangeTable`**  
   注释写明 `player_job_base_stats` 的「攻击距离」列仅对照。`Hero.RecalcAttr` 用职业表默认射程，有武器再按 `WeaponCombatTable` Kind 覆盖——这条链比半年前清晰。

4. **职业基础属性走 `player_job_base_stats`**  
   `PlayerJobBaseStats.ApplyToAttr` 覆盖 HP/ATK/DEF/暴击/间隔；`AttrSystem` 在 Hero+有表时不再叠「力量×2」。符合「不要发明倍率」的产品口径。

5. **统一伤害出口 `DamageFormula`**  
   普攻/技能都走 Build → Crit → DEF。引导已不再走假伤（`UnitBase.Attack` 固定 `openingHit = false`）。

6. **正式刷怪已有表挂钩**  
   `StageSpawnTable` + `stage_spawn.csv`（第一章首关 9 怪可改表）；`StageRoller` 现抽普通/精英/休息/Boss，与 `ChapterManager` 占位图分离——比旧「开局排死商人/诅咒」更可扩展。

7. **引导有独立表 `tutorial_battle.csv`**  
   步进 count/精灵/佣兵/HP 档表驱动；导演只点 `QueueTutorialStep`，埋伏是 SpawnWave 参数（Phase 4）。

8. **佣兵技能已表驱动**  
   `merc_skills.csv` → `MercSkillTable` 运行时合成 `SkillConfig`，比玩家技能更接近目标态。

9. **VFX 按武器 Kit 选文件夹**  
   `BattleVFXSystem.PlayAttackKit` 近战/弓/法球/治疗分离，禁止远程静默回退我方箭。手感规则已落地，不要再改成写死刀光。

10. **存档形态务实**  
    `SaveData` 用 List↔Dictionary 迁就 JsonUtility；本地 `player.dat` + bak。微信云存未接，业务层已通过 Bridge 隔离。

---

## 4. 问题清单

### P0 — 现在就在疼，或加下一章/下一模式会立刻疼

#### P0-1 `BattleManager` 同时是状态机、刷怪器、技能器、结算器和引导适配层

- **症状**：单文件约 3676 行、无 `#region`。开战、波次、佣兵、能量、教程刷怪 API、过场、Update 清场、玩家/佣兵技能 if 链、通关宝箱、撤离、金币本、废弃商人关全在一类。
- **位置**：`Assets/Scripts/Managers/BattleManager.cs`（职责从 L11 场景引用到 L3616 特殊关）。
- **现在**：改手感/波次/引导任何一项都要在同一文件里绕开十几处旗标，回归面是「整局战斗」。
- **以后**：新章节脚本战、新战斗模式（自动、昼夜、活动本）、新技能类型都会继续往这里加 `if`。
- **方向**：不要一次拆完。下次改刷怪或引导时，**顺手抽出** `IWavePlanner`（正式表 vs 引导队列）和 `SkillCastService`（从 BM L2777–3062 挪出）。BM 只留「本局状态 + 调用」。

#### P0-2 引导特例泄漏进核心战斗循环

- **症状**：`IsTutorialRun` 在 BM 内约 30+ 处：跳过正式佣兵/首波兜底/下一波 Banner、改交战距离与 stagger、覆盖怪 HP、换精灵选择、禁第一章结局、撤离走另一套结算。`BattleUI` 再锁技能点击、藏难度标签。`TutorialDirector.BattleRoutine` 直接调 `QueueTutorialWave` / `SpawnTutorialFlankAmbush` / `SpawnTutorialAmbushAround`。
- **位置**：  
  - BM L42–46 旗标；L273–294 开战；L920–934 `ApplyTutorialMonsterTuning`（普通 8–13 HP）；L1033 清空正式波；L2183+ 表步进；L541–792 教程刷怪 API。  
  - `TutorialDirector.cs` L342+ `BattleRoutine`。  
  - `BattleUI` 教程 HUD / `AllowBattleSkillClick`。
- **现在**：引导手感靠「正式 Init 后再盖一层低血」和协程叙事，正式关公式测不准引导，引导改完也测不准正式关。
- **以后**：第一章中段再加「剧本波」、活动教学关、第二职业教学，只会复制第三条刷怪轨道。
- **方向（Phase 4 已落地）**：`TutorialDirector` 只编排节拍；刷怪走 `QueueTutorialStep` → 同一 `SpawnWave`（锚点 / L/R / forcedTarget）。`IsTutorialRun` 身份仍在，战斗循环旗标收进 `TutorialRules`。未 100% 拔掉身份判断（HUD/撤离/爽点）。

#### P0-3 战斗数值仍有多层真源，表不是唯一出口

- **症状**：产品要「正式表驱动、不发明倍率」，但运行时仍叠：  
  1. `GameConfig.BASE_HP=200 / BASE_ATTACK=30` 先写入 `AttrSystem`，再被职业表盖成 1500/17 等；  
  2. 怪物：`GameConfig.MONSTER_*` + `MONSTER_HP_GLOBAL_MUL=0.6` + 章节数组 + 难度 + `WeaponCombatTable` 精英/Boss TTK + `monster_stats` 表覆盖 SO；  
  3. 攻速：职业表间隔 → 武器 Kind 硬编码再乘比值（`Hero.cs` L130–136）；敌方再乘 `MONSTER_ATK_SPEED_MUL` / `PROJECTILE_ATK_SPEED_MUL`；  
  4. `stage_spawn.csv` 里 `monsterTotal=0` 仍走 `GameConfig` 公式；`wave_slot.csv` **零数据行**，正式关回退奇偶近战/远程。
- **位置**：`GameConfig.cs`（约 911 行常量）、`AttrSystem.cs` L31–71 / L157–159、`PlayerJobBaseStats.cs`、`Monster.Init`、`WeaponCombatTable.cs`、`stage_spawn.csv`、`wave_slot.csv`。
- **现在**：调「第一章爽感」要同时改表、常量和 BM 教程盖血；容易出现「表改了没生效」。
- **以后**：第 2–8 章用 `CHAPTER_STAT_SCALE[]` 硬编码 8 个倍率，加第 9 章或改主题必须改代码；波次构成无法按关填表。
- **方向**：立一份「战斗读数优先级」短表（建议：职业/怪物/射程/波次 = Cook 表；手感钳制/镜头/UI = `GameConfig`）。填 `wave_slot` 或删掉空表以免假象。章节倍率迁到已有 `chapter_theme_map` 一类表，而不是加长 C# 数组。

#### P0-4 装备：表能掷出的词条，战斗大多吃不到

- **症状**：`equip_attr_ranges` + `RiftEquipTables.MapCnAttrToId` 有生命回复、治疗、反弹、流血、毒、点燃等 30+ ID；`RiftEquipGenerator.TryResolveAttrType` **只映射约 12 种**到 `AttrType`，其余静默丢弃。`AttrType` 枚举本身没有这些字段。  
  稀有度：表里有按关卡段权重（`W1to5`…），`WeightForStage` **已写未调用**；实际走 `HiddenLevelSystem.GetRarityWeights`。  
  SO 回退路径另有 `EquipDropRules` + `EquipRollCeiling`，Rift 主路径不走 Ceiling。  
  `rift_equip_gen_steps` 已 Cook，**无运行时读取**。
- **位置**：`RiftEquipTables.cs` L215–252；`RiftEquipGenerator.cs` L87–133、L247–282；`AttrType.cs`；`ConfigManager.GetRandomEquipInstances`。
- **现在**：掉落 UI 可能展示/生成与战斗不一致；策划改范围表以为「减伤/回复」已进战斗。
- **以后**：防具/饰品设计文档（`Docs/像素冒险：裂隙之刃_防具与饰品系统设计_V1.0.md`）无法按表落地，只能继续堆特殊 case。
- **方向**：先出「词条落地清单」：已映射 / 仅展示 / 未做。新词条 = 先加 `AttrType` + 战斗一处结算，再开放表权重。稀有度二选一：隐藏等级 **或** 关卡段权重，不要两套并存。

#### P0-5 技能资源规则三套并行，和产品文案不一致

- **症状**：  
  - **玩家/佣兵主动技**：`BattleManager.playerSkillEnergy` / `mercSkillEnergy[]`，盟友**受击**按 `finalDamage/MaxHp` 回充（`UnitBase.TakeDamage` L1206–1209）。击杀/出手/时间常量仍在 BM L196–201，全是 `0` 的死常量。  
  - **怪物技**：`Monster._skillEnergy`，时间 + 普攻 +0.15。  
  - **雷击奥义**：`HeroThunderUltimate` 击杀充能；总开关 `GameConfig.THUNDER_ULT_ENABLED = false`。  
  `PlayerSkillDefs` 注释写「手动点击」；运行时 `PlayerSkillPassive` 能量满**自动放**，教程也教自动放。`isAutoBattle` 仅 UI 翻转，**战斗逻辑零读取**。
- **位置**：`UnitBase.cs` L1190–1211；`BattleManager` 能量 API / `TryUsePlayerSkill`；`PlayerSkillDefs.cs` L1–7 vs `PlayerSkillPassive`；`BattleUI.ToggleAutoBattle`。
- **现在**：调「受击回能手感」只动 UnitBase 一处是对的；但策划文档/技能设计仍写手动，测试与引导文案容易打架。
- **以后**：新技能（冷却型 vs 能量型）、佣兵手动槽、自动战斗模式会对着三套充能各写一套。
- **方向**：写清一条运行时规则（建议维持现状：**能量=受击占比；满条自动；无时间/击杀涨蓝**）。新资源类型用策略对象挂单位上，不要再在 BM 加常量。自动战斗要么接线，要么隐藏按钮。

---

### P1 — 现在能玩，扩展时会交税

#### P1-1 玩家技能 / 职业 / 天赋仍是 C# 或 SO，和佣兵表不同步

- **症状**：玩家 6 技能在 `PlayerSkillDefs.All`；数值/特效在 `Resources/Config/Skills/Ally/*.asset`。`player_jobs.csv` **未进 Cooker**，展示数据在 `PlayerJobDefs`。天赋整树在 `TalentDefs` 静态构造；旧 `TalentConfig` SO 仅兜底非 L/C/R。  
  BM `ResolveSkill` 找不到配置时 **默认 AOE ×2.5**（L2952–2964），缺表会变成 silently 强力技能。
- **位置**：`PlayerSkillDefs.cs`、`SkillRegistry.cs`、`TalentDefs.cs`、`GameDataCooker.cs`（无 `player_jobs`）。
- **现在**：改技能文案/解锁章要改代码发版。  
- **以后**：每加一个主动技或转职，都要改 Defs + SO + BM if（佣兵侧已有 SK015/SK007 分支）。
- **方向（Phase 5 已落地元数据）**：`player_skills.csv` + `PlayerSkillTable`；缺配置拒绝释放（不再静默 `ally_heal` / ×2.5）。战斗数值仍走 Ally SO。天赋可继续 C#，直到要做「热更天赋树」。新技能禁止 BM `if (id==SKxxx)`（GATE 注释，SkillCast 仍推迟）。

#### P1-2 章节图注释与实现、特殊关废弃物

- **症状**：`ChapterManager` 文件头仍写「商人/附魔/诅咒/休息每章 1–2 个、随机分配」。实现已是：占位 Normal+末关 Boss，类型由 `StageRoller` 现抽（仅四类）。BM 底部 `[Obsolete] LoadMerchantStage` 等仍在。`SPECIAL_STAGES_PER_CHAPTER` 仍在 `GameConfig`。
- **位置**：`ChapterManager.cs` L6–38、L94–110；`StageRoller.cs`；BM L3616+。
- **现在**：读注释的人会按旧规则做关卡表。  
- **以后**：恢复商人/诅咒若同时改注释、Roller、BM、UI，会和「未开放 Toast」再打一架。
- **方向**：改文件头注释对齐现状；特殊关未做前不要复活 BM 旧方法。新章优先填 `chapter_branch*` / `stage_roller_weights` / `stage_spawn`。

#### P1-3 `AttrSystem` ↔ `Hero.Instance` 循环，属性不能当纯数据测

- **症状**：`ApplyPlayerJobBaseIfAny` / 跳过力量派生都靠 `Hero.Instance.attr == this`。佣兵/怪物共用 `AttrSystem`，靠「不是 Hero」避开职业表。`GameConfig.IsOpeningStage()` 再读 ChapterManager+BattleManager。
- **位置**：`AttrSystem.cs` L157–178；`GameConfig.cs` 开局关判断。
- **现在**：引导开局总攻曾被力量×2 抬高，才加了这处特例——说明循环已经制造过数值事故。  
- **以后**：第二主角、训练场、无 Hero 的结算预览会再踩。
- **方向（Phase 2 + Phase 5）**：`AttrOwnerKind` 已绑定；`RecalcAllAttr` 仅 Player 叠存档/天赋/传说，不再问 `Hero.Instance`。

#### P1-4 Singleton 自动 `new` + PersistentRoot / GameRoot 双挂

- **症状**：`Singleton<T>.Instance` 找不到就新建 DontDestroyOnLoad。Story/Tutorial 在 Boot 的 PersistentRoot 和 Battle 的 GameRoot **各挂一份**，靠 Awake 销毁重复组件。`AutoGameInitializer` ~1171 行，BattleUI.Awake 还会后备初始化。
- **位置**：`Singleton.cs` L12–45；`AutoGameInitializer.cs`；`BootManager`。
- **现在**：偶发「空引用后冒出一个裸单例、没有场景引用」。  
- **以后**：多战斗模式/重进战斗时，生命周期更难推理。
- **方向（Phase 5 已落地 getter）**：`ICombatBoundSingleton` 找不到 → null + Error，禁止 new 空物体。Story/Tutorial 仍挂 PersistentRoot；城镇 MercenaryManager 等保持可自动创建。

#### P1-5 UI 单体与战斗双向耦合

- **症状**：`BattleUI` ~2059 行（含嵌套格子类），约 22 处 `BattleManager.Instance`；Awake 触发战斗初始化。`AdventureUI` / `DialogueUI` / `TalentUI` 同等体量。Editor 里还有一批 PrefabGenerator（与「运行时不改用户预制体」并存）。
- **位置**：`Assets/Scripts/UI/BattleUI.cs`；`Assets/Editor/*PrefabGenerator*.cs`。
- **现在**：改 HUD 容易碰到开战时序。  
- **以后**：新战斗 HUD（昼夜、天气、第二种操作）会继续堆进 BattleUI。
- **方向（Phase 5 开战单入口）**：`BattleUI.Awake` 不再调用 `AutoGameInitializer.Initialize`。生成器只用于缺失预制体的脚手架，不覆盖手摆 UI。

#### P1-6 平台层全 Stub，业务已按「有云/有广告」写了分支

- **症状**：`CloudSaveBridge` 写本地镜像，微信开关直接 `onDone(false)`。`RewardedAdBridge` 连 Release 也可能模拟成功。无真实网络对战。
- **位置**：`Assets/Scripts/Platform/*`。
- **现在**：演示版够用。  
- **以后**：接真 SDK 时应只换 Bridge，但要先清掉「Release 也当成功」的广告路径，避免经济漏洞。
- **方向**：不上线微信前不要改业务调用点；接 SDK 时加环境开关（Spotlight 已有禁云/禁广告先例）。

---

### P2 — 整洁度 / 死代码，不挡内容

| ID | 症状 | 位置 | 建议 |
|----|------|------|------|
| P2-1 | `Managers/SceneManager.cs` 同步 LoadScene，无引用 | 该文件 | 标 Obsolete 或删除（单独小 PR） |
| P2-2 | `TutorialPowerFantasy` 仍设置，伤害函数已 Obsolete 且无调用 | BM L44/L541；`GameConfig.RollOpeningAllyHitDamage` | 删旗标或重新定义「爽点=换武器+低血怪」 |
| P2-3 | 能量死常量 `ENERGY_PER_KILL` 等全 0 | BM L196–201 | 删，避免误调 |
| P2-4 | `wave_slot` / `rift_equip_gen_steps` / `player_jobs.csv` 空转或未 Cook | 表目录 + Cooker | 接线或移出 Cook 清单并在表头写「未启用」 |
| P2-5 | 佣兵遗留 `Skills/Merc/SK*.asset` 与表并存 | Resources | 表稳定后停用 SO |
| P2-6 | `AllowMonsterMapEnter` 等引导冻帧旗标偏战斗内核 | BM L35–39 | 随 P0-2 收进「本局规则包」即可 |

---

## 5. 现在 ↔ 以后：建议先画的边界（5 条）

1. **引导 vs 正式刷怪**  
   正式关：`StageSpawnTable` → `BuildCombatWaves` → `SpawnWave`。  
   引导：同一 `SpawnWave`，波次列表由 `tutorial_battle` 生成；埋伏/夹击是 **Spawn 参数**（锚点、forcedTarget），不是 BM 第二套协程刷怪。  
   `TutorialDirector` 禁止再 `new` 血量随机数。

2. **装备掷骰 vs 战斗生效**  
   掷骰只负责：槽、品质、词条 ID、数值（`equip_*` 表）。  
   生效只负责：`AttrBonusData` → `AttrSystem` / 专有系统（毒、反弹）。  
   未映射 ID 不许进玩家背包实例（或进包但标「未实装」且不进 Recalc）。禁止 SO 回退与 Rift 两套稀有度同时对玩家可见。

3. **能量 / 技能资源**  
   主动技：受击占比充能、满条自动、点头像可选（引导用开关）。  
   怪物技：保留独立条，但公式进 `monster_stats` 或技能表。  
   雷击：保持关闭直到产品要「击杀充能」第二条资源；届时不要复用 `playerSkillEnergy`。

4. **职业成长 vs 装备成长**  
   职业固定底（`player_job_base_stats`，无账号等级）已写在类注释里。  
   成长走隐藏等级（掉落品质）+ 装备词条 + 天赋。  
   不要把力量/智力派生重新叠回玩家攻击（已踩过坑）。

5. **章节扩展默认路径**  
   新章：主题表 + `CHAPTER` 倍率表 + `stage_spawn` 行 + `chapter_branch`。  
   新关类型：先加 `StageRoller` 权重和独立 UI，再碰 BM。  
   新战斗模式（金币本已是先例）：`ChapterManager` 开独立入口，**复用** LoadStage/刷怪，不要复制 BM。

6. **表 Cook 纪律**  
   新战斗数：先 CSV + Cooker 一行 + `*Table.EnsureLoaded`，禁止先写 `GameConfig` 再补表。  
   `GameConfig` 只留：分辨率、镜头、车道、UI 层级、手感钳制、尚未表化的安全阀（并在常量旁写「待迁表」）。

7. **技能执行**  
   配置（ID、倍率、CD、VFX）在表/SO；执行在 `SkillSystem` / `MercSkillCaster`。  
   BM 内 `if (id == SK015)` 视为债务：新技能只加表行 + 通用执行器，不再加分支。

---

## 6. 明确不做（本阶段 Non-goals）

以下 **现在不要重构**。动了收益低、和软著/手摆预制体/正在调的手感冲突。

- 一次性拆解 `BattleManager` / `BattleUI` / `AutoGameInitializer` 成「干净架构」。
- 批量重命名产品名、Docs、场景、预制体节点（裂缝/裂隙并存是历史，单独做文案 PR）。
- 把全部 `GameConfig` 迁进 CSV。
- 删光 Equip/Monster/Skill ScriptableObject（表尚未 100% 覆盖）。
- 重写天赋为表、重写存档格式、上 ECS/Addressables。
- 实现微信云存、真广告、联机战斗。
- 改任何 `*.prefab` 或运行时改用户布局坐标。
- 复活商人/诅咒/锻造完整玩法（需产品档期 + 独立 UI）。
- 为了对称而统一三套能量（先写规则，再谈合并）。
- 引导「完全插件化 / 可视化时间轴」大工程——先收 `IsTutorialRun` 分支即可。

---

## 7. 建议的落地顺序（仍是方向，不是本 PR 实现）

| 顺序 | 动作 | 对应 | 风险 |
|------|------|------|------|
| A | 一页「战斗读数优先级」+ 词条落地清单，贴进策划和 `ContentPaths` 注释 | P0-3, P0-4 | 低，只写文档/表头 |
| B | 自动战斗按钮：接线或隐藏；技能文案改成「满能自动」 | P0-5 | 低 |
| C | 引导刷怪改为填 `tutorial_battle` + 共用 `SpawnWave`；教程怪 HP 进表列 | P0-2 | 中，要手测引导 |
| D | `wave_slot` 填第一章 或 标明未启用；章节倍率进表 | P0-3, P1-2 | 中 |
| E | 装备未映射词条不要进包；稀有度单一真源 | P0-4 | 中，掉落体感会变 |
| F | 玩家技能缺配置失败而不是 ×2.5；新技能禁止 BM 新分支 | P1-1 | 低 |
| G | 抽出 WavePlanner / SkillCast（有实际需求时再做） | P0-1 | 高，需回归全战斗 |

---

## 8. 关键耦合（便于 code review 对照）

```
TutorialDirector  ←→  BattleManager.IsTutorialRun / 教程刷怪 API
BattleManager     →   BattleUI / ChapterManager / StoryProgress / SkillRegistry / ConfigManager
AttrSystem        →   Hero.Instance + SaveSystem + ConfigManager + PlayerJobBaseStats
Hero.RecalcAttr   →   GridBackpackSystem → EquipStatRollup → AttrSystem
                  →   AttackRangeTable / WeaponCombatTable（再盖射程与攻速）
ChapterManager    →   BattleManager.LoadStage
BattleManager     →   ChapterManager.OnStageComplete
BattleUI.Awake    →   AutoGameInitializer → 再绑 BattleUI
SkillRegistry     →   SaveData + 背包 grantSkill + PlayerSkillDefs + MercSkillTable
```

循环里最危险的是 **AttrSystem 问 Hero** 和 **UI 触发战斗初始化**。其余是「编排器知道太多」，用提取服务就能降压。

---

## 9. 结论（给排期用）

架构不是「写崩了」，而是 **演示闭环堆在一个战斗上帝对象上，表驱动做到一半**。  
继续加章、加职业、加词条、加模式时：

- **顺着** Cook 管线、射程单源、职业表、DamageFormula、StageRoller；  
- **拦住** 新的 `IsTutorialRun`、新的 BM 技能 if、新的 `GameConfig` 战斗倍率、新的未映射装备词条；  
- **大拆类留到** 真的要做第二种战斗编排的时候，再按第 7 节 A→G 做。

本文件只评审。代码改动按 [`OPTIMIZATION_PLAN.md`](./OPTIMIZATION_PLAN.md) 分阶段进行。
