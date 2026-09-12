# 像素冒险：裂隙之刃 — 优化落地计划

> **读者**：产品 + 程序。  
> **对照评审**：[`ARCHITECTURE_REVIEW.md`](./ARCHITECTURE_REVIEW.md)（问题编号 P0/P1/P2 以评审为准）。  
> **原则**：保住当前战斗手感；先关门再迁表；大拆类只在「第二种战斗编排」真出现时做。  
> **本 PR（#6）**：Phase 0–3 已落地（评审 + 止血 + 数值真源 + 装备门闩）。  
> **Phase 4 PR**：引导 / 正式刷怪合流（HP 进表、埋伏走 SpawnWave 参数、TutorialRules）。  
> **Phase 5 PR**：按需减税（玩家技能元数据表、开战单入口、战斗单例禁自动 new、AttrOwnerKind 收口）。  
> **Phase 6 PR**：抽出 WavePlanner / SkillCastService；玩家技能战斗数离 Ally SO（表种子 1:1）。平台 SDK 仍推迟。

---

## 总原则

1. **不发明倍率**。战斗数走 Cook 表；`GameConfig` 只留镜头 / UI / 手感钳制。
2. **引导与正式关共用刷怪入口**，导演只编排节拍。
3. **装备掷骰 ≠ 战斗生效**。未映射词条不进包。
4. **一次只开一扇门**。每阶段可独立合入、可回滚。
5. **禁止**改 `*.prefab`、批量改产品名。`BattleManager` 只做编排，刷怪/施法已抽到 WavePlanner / SkillCastService（Phase 6）。

---

## Non-goals（全程不做）

- 一次性拆解 `BattleUI` / `AutoGameInitializer`（BM 刷怪/施法已在 Phase 6 抽出）。
- 把全部 `GameConfig` 迁进 CSV。
- 删光 Equip / Monster / Skill ScriptableObject（表尚未 100% 覆盖）。
- 重写天赋树、存档格式、上 ECS / Addressables。
- 实现微信云存、真广告、联机。
- 改任何 `*.prefab` 或运行时改用户手摆布局坐标。
- 复活商人 / 诅咒 / 锻造完整玩法。
- 为对称而合并三套能量（先写清规则）。
- 引导可视化时间轴大工程。
- **Phase 5 不做**：伤害 / 能量公式 / 刷怪数量 / VFX 调参；不拆 BattleManager；不接真 SDK。

---

## Phase 0 — 文档闸门（本 PR 文档）

**目标**：合评审、立读数优先级，避免后面各 PR 各写各的真源。

| 闸门 | 内容 | 状态 |
|------|------|------|
| 战斗读数优先级 | 职业/怪物/射程/波次 = Cook 表；手感/镜头/UI = `GameConfig`；SO 仅兼容 | 见下节 |
| 词条落地清单 | 已映射 / 仅展示 / 未做 | 见下节 |
| 能量一句话 | 盟友受击按 `finalDamage/MaxHp` 回主动技能量；满条自动；无击杀/时间涨蓝 | 见下节 |
| 合入评审 | `ARCHITECTURE_REVIEW.md` 为问题真源，本文件为排期真源 | 本 PR |

### 0.1 战斗读数优先级（短表）

| 数据 | 唯一真源（目标态） | 现状 | 迁出阶段 |
|------|-------------------|------|----------|
| 玩家职业底（HP/ATK/DEF/间隔/暴击） | `player_job_base_stats` | 表已覆盖；`GameConfig.BASE_*` 仍先写入再被盖 | Phase 2 |
| 攻击距离 | `attack_range` → `AttackRangeTable` | 已收口；职业表「攻击距离」列仅对照 | 保持 |
| 武器攻速/射程 Kind | 目标：表；现状 `WeaponCombatTable` 硬编码 | 有武器时再乘 Kind 比值 | Phase 2 后（不本 PR） |
| 怪物属性 | `monster_stats` 优先，SO 兜底 | 另叠 `GameConfig.MONSTER_*` 与章节数组 | Phase 2 |
| 章节属性倍率 | `chapter_stat_scale`（1:1 旧数组） | 已迁表；缺表回退 `ChapterStatScaleTable.Fallback` | **Phase 2 完成** |
| 波次构成 | `stage_spawn` + `wave_slot` 奇偶近战/远程 | `wave_slot` 已填 slot0–15 奇偶（spriteIndex=0 加权）；`monsterTotal=0` 明确=公式 | **Phase 2 完成** |
| 关卡类型抽取 | `stage_roller_weights` + `StageRoller` | 仅普通/精英/休息/Boss | 保持；勿复活商人关 |
| 引导波次 | `tutorial_battle` | 步进 + HP 档进表；导演只点 `QueueTutorialStep`；埋伏/夹击是 SpawnWave 参数 | **Phase 4 完成** |
| 装备词条数值 | `equip_attr_ranges` + `IsCombatLanded` | 只抽已映射 12 种进包；未落地不占词条位 | **Phase 3 完成** |
| 天赋 | `TalentDefs` C# | 可继续硬编码直到要热更 | 非本计划必做 |
| 玩家技能 | Cook `player_skills`（元数据+战斗数） | 表优先；Ally SO 仅 VFX | **Phase 6 完成** |
| 佣兵技能 | `merc_skills` | 已表驱动 | 保持 |

### 0.2 装备词条落地清单（Phase 3 闸门，此处只记账）

**已映射进 `AttrType`（可进 Recalc）**：`ATK` `DEF` `HP` `MS` `CRIT_RATE` `ATK_SPD` `RANGE` `DODGE` `LIFE_STEAL` `ELE_DMG`(→FireDamage) `CRIT_DMG` `DMG_RED`(→DEF%)。

**表有、战斗未落地（Phase 3 起禁止进包）**：`HP_REGEN` `HEAL` `REFLECT` `BLEED` `POISON` `BURN` `SLOW` `RICOCHET` `PURIFY` `AURA` `STATIC_DMG` `LOW_HP_DMG` `RANGE_DMG` `TAUNT` `ARMOR_BREAK` `STUN_DUR` `KNOCK_BACK` `PIERCE` `CONTROL_RES` `ANTI_CRIT` 等。

**稀有度（Phase 3 已定）**：裂缝掉落只走 `HiddenLevelSystem`。`WeightForStage` 关卡段权重保持 Obsolete、不接线。SO 回退路径才用 `EquipDropRules`。

### 0.3 能量一句话（运行时口径）

> 玩家/佣兵**主动技能量**只在盟友**受击**时按 `finalDamage / MaxHp` 增加；满条自动释放（引导可锁点击）。击杀、出手、时间**不加蓝**。怪物技能条与雷击奥义是另外的系统；雷击总开关关闭。自动战斗按钮**未开放**。

---

## Phase 1 — 低风险止血（本 PR 实现）

对应评审：P0-5 死开关、P1-1 技能兜底、P1-2 注释漂移、P2-1/P2-3/P2-4。

| 项 | 做法 | 手感 |
|----|------|------|
| 自动战斗 | 运行时隐藏按钮；点击兜底 Toast「未开放」；**不接线**自动 AI | 不变 |
| 死能量常量 | 删除始终为 0 的 `ENERGY_PER_KILL` / `SECOND` / `ON_ATTACK` / `ON_HIT` | 受击回能不变 |
| 缺技能配置 | `ResolveSkill` 失败打 Error 并拒绝释放，不再静默 AOE×2.5 | 已配置技能不变 |
| ChapterManager 头注释 | 对齐 StageRoller 四类现抽 | 无运行时变化 |
| 表卫生 | `wave_slot` / `rift_equip_gen_steps` / `player_jobs` 标明「未启用」；死表可移出 Cooker | 空表回退逻辑不变 |
| 废弃 `Managers/SceneManager.cs` | 零引用则删除（切景只走 `GameSceneManager`） | 无 |

**本阶段不做**：迁 `BASE_*`、填 `wave_slot` 数据、改引导刷怪、改装备进包规则。

---

## Phase 2 — 数值真源（本 PR 已实现）

对应 P0-3、P1-3。**约束：最终 Recalc 数字与改前一致。**

| 项 | 状态 | 做法 | 手感 |
|----|------|------|------|
| 玩家 BASE_* 预写 | 完成 | `OwnerKind==Player` 且职业表有数据时，`InitBaseDict` 直接 `TryWriteCombatBases`，不先写 `BASE_HP/ATK` | 最终 ATK/HP 同前（仍叠体质派生与装备） |
| `CHAPTER_STAT_SCALE` | 完成 | 新表 `chapter_stat_scale.csv`（1.0/1.3/1.6/1.7/1.4/2.0/2.4/2.8）；`GetChapterStatScale` 只读表 | 章节怪属性倍率不变 |
| `wave_slot` | 完成 | slot 0–15 奇偶 Melee/Ranged，`spriteIndex=0` 走原加权；≥16 仍代码回退 | 近战/远程交替与加权不变 |
| `stage_spawn` 的 0 | 完成 | 注释写明 `0=GameConfig.GetStageMonsterTotal`；仅 1-0 固定 9 | 不填具体总数，避免冻随机 |
| `AttrOwnerKind` | 完成 | `UnitBase.Awake` 绑定 Player/Merc/Monster；派生不再问 `Hero.Instance` | 仅玩家跳过力量→ATK |

验收：角色页 ATK/HP 与开战 Recalc 同职业+装备应与改前一致；第一章 1-0 清场时间同一量级。

---

## Phase 3 — 装备掷骰 vs 生效（本 PR 已实现）

对应 P0-4。

| 项 | 状态 | 做法 |
|----|------|------|
| 未映射不进包 | 完成 | `RiftEquipTables.IsCombatLanded`；抽池只含已映射 ID |
| 稀有度单源 | 完成 | 裂缝 = `HiddenLevelSystem`；`WeightForStage` Obsolete |
| 新词条闸门 | 完成 | 须先加 `AttrType` + 结算，再写入 `TryResolveCombatAttr` |
| `rift_equip_gen_steps` | 保持未启用 | 不 Cook、不读取 |

**有意行为**：以前抽到毒/回复会占词条位再丢掉；现在改抽已落地属性，同品质装备可能多 0–2 条已映射词条。已映射词条的数值公式未改。

---

## Phase 4 — 引导 / 正式刷怪合并（本 PR）

对应 P0-2。**约束：波次人数、L/R 进场、HP 档、错峰、受击回能、伤害公式与改前一致。**

| 项 | 状态 | 做法 | 手感 |
|----|------|------|------|
| 教程怪 HP 档 | 完成 | `tutorial_battle` 增 `hpMin/hpMax/eliteHpMin/eliteHpMax`（8/13、25/36，整型开区间，再乘 `MONSTER_HP_GLOBAL_MUL`）。表有值才盖血；BM 不再写死 8–13 / 25–36 | 普通约 8–12、精英 25–35（×0.6）同前 |
| 埋伏/夹击合流 | 完成 | `WaveData` 增加锚点 / `bilateralEnter` / `aroundAnchor` / `forcedTarget` / `staggerOverride`；`CoSpawnWaveMonsters` 走 `SpawnMonsterOffscreenEnter`。宝箱/佣兵 L/R、普通波右侧、埋伏 stagger=0 | 进场方向与错峰同前 |
| `TutorialRules` | 完成（有意未 100%） | 禁佣兵预召、禁清关 Banner、禁自动首波、交战距离/间距/stagger、禁第一章结局、撤离/成就/连杀 HUD 等收进规则包。`IsTutorialRun` = `Rules.Active` | 仅搬家，行为不变 |
| 导演只编排节拍 | 完成 | `TutorialDirector` 调 `QueueTutorialStep(order)`；不再读 count / 自造刷怪数学 / 发明 HP | 节拍顺序不变 |

**本阶段仍保留（下阶段再收，勿在本 PR 硬拆）**

- `IsTutorialRun` 身份判断：导演启动、设置撤离、雷击过场、`BeginTutorialPowerFantasy`。
- `BattleUI` 教程 HUD / 技能点击锁（仍问 `TutorialDirector`）。
- `AllowMonsterMapEnter` 运行时窗口（节拍冻帧，不是刷怪轨）。
- 救援佣兵放置 `SpawnTutorialMercAt`（节拍：放 NPC，不是怪波数学）。

- **此后禁止**再往 BM 核心循环加新的 `IsTutorialRun` 分支（新旗标加 `TutorialRules`）；禁止再加 BM 技能 `if (id == SK0xx)`。

---

## Phase 5 — 按需减税（本 PR 已落地可安全项）

对应 P0-1（门闩）、P1-1、P1-3 尾巴、P1-4、P1-5。P1-6 / 大拆类仍按需。

| 项 | 状态 | 做法 | 手感 |
|----|------|------|------|
| 玩家技能元数据 CSV | **完成** | `player_skills.csv` Cook → `PlayerSkillTable`；`PlayerSkillDefs` 缺表回退 Fallback（与表 1:1）。战斗数值/VFX 仍走 Ally SO（`allyConfigId`）。未知 id / 缺 Ally 配置：Error + 拒绝释放，不再静默 `ally_heal` | 已配置技能不变 |
| 缺配置拒绝 | **保持** | `ResolveSkill` 失败返回 null（Phase 1）；`GetPlayerSkillId` 去掉 `DefaultPlayerSkillId` 静默回退 | 同左 |
| BM 技能 `if (id==SKxxx)` | **门闩（未抽执行器）** | 抽出通用 SkillCast 会碰 SK007/008/010/015/018 历史分支，本阶段不加行为。`TryCastMercActiveSkill` / `MercSkillTable` 加 PHASE5 GATE：禁止再加新 ID 分支 | 不变 |
| 开战单入口 | **完成** | `BattleUI.Awake` 不再调 `AutoGameInitializer.Initialize`；开战只走场景组件 Awake。`TryStartNewRunOnce` 仍防重入 | 无 HUD 改动 |
| 战斗单例禁自动 new | **完成** | `ICombatBoundSingleton`：BM / SkillSystem / SkillRegistry / BattleVFX / CombatJuice / DamageText / MonsterSpriteLoader / PoolManager / HeroThunderUltimate。找不到 → null + Error。Boot/城镇（Save/Config/MercenaryManager 等）仍可 getter 创建 | 装配后行为不变 |
| AttrOwnerKind 尾巴 | **完成** | `RecalcAllAttr` 仅 Player 叠存档/天赋/传说；城镇角色页无 Hero 时 `new AttrSystem(Player)` | 开战 Recalc 同前；城镇面板更接近职业表 |
| WavePlanner / SkillCast | **Phase 6 完成** | 见下节 | — |
| 平台 Bridge / 真广告 | **推迟** | 不上线微信前不改业务调用点 | — |

**本阶段仍禁止**：改 `*.prefab`、改 `GameConfig` 战斗倍率、新 BM `IsTutorialRun` / 新技能 if。

---

## Phase 6 — WavePlanner / SkillCast / 玩家技能离 SO（本 PR）

对应评审 P0-1 抽出、P1-1 战斗数离 Ally SO。**约束：伤害公式、受击回能、刷怪人数/左右/stagger、教程 HP 档、VFX Kit、Ch1-0 与引导手感 1:1。**

| 项 | 状态 | 做法 | 手感 |
|----|------|------|------|
| WavePlanner | **完成** | 正式 `SetupNormal/Elite/Boss` 与引导 `QueueTutorialStep` 都铺 `WaveData`，出怪仍走同一 `SpawnWave` / `CoSpawnWaveMonsters`（参数：bilateralEnter / aroundAnchor / staggerOverride / forcedTarget）。导演只点步进 | 人数/L-R/错峰/HP 档同前 |
| SkillCastService | **完成** | 玩家/佣兵施放从 BM 迁出。佣兵分派 `MercSkillExecutor`（Category / TargetType / Formula），无 `if (id==SKxxx)` | SK007/008/010/015/018 现网数不变 |
| 玩家技能离 Ally SO | **完成** | `player_skills` 增战斗列（skillType/attackKit/倍率/AOE/Buff/治疗/energyCost）。`SkillRegistry.Get` 先合成表配置；Ally SO 只拷 `vfxPrefab`。缺表回退 Fallback（与 CSV 1:1，种子见下） | 6 技能有效数 1:1 |
| BM 变薄 | **完成** | 状态机 / 胜负 / 能量 / 过场仍在 BM；刷怪与施放只接线 | 无 |

**战斗数种子（从现网 Ally SO 读一次，禁止发明）：**

| 玩家 id | allyConfigId | skillType | attackKit | 有效数 |
|---------|--------------|-----------|-----------|--------|
| heal_spring | ally_heal | Buff | Heal | healPercent=0.3，aoe=6，cd=12 |
| holy_barrier | ally_shield | Buff | None | Defense+35%（现网 ApplyTeamBuff，不是独立护盾条），dur=5，aoe=4，cd=18 |
| battle_surge | ally_atk_up | Buff | None | Attack+30%，dur=8，aoe=6，cd=18 |
| gale_stance | ally_atk_speed | Buff | None | AttackSpeed+35%，dur=6，aoe=6，cd=15 |
| deadly_focus | ally_crit_up | Buff | None | CritRate+25%，dur=8，aoe=6，cd=18 |
| thunder_verdict | ally_thunder | AOE | None | ATK×3，aoe=8，cd=25 |

energyCost=1：满条消耗（与现网受击回能、满条自动一致）。VFX 仍按 `allyConfigId` 找 `Resources/VFX/Skills/Ally/{id}` 与 attackKit。

**佣兵历史分支 → 表字段（现网数）：**

| 现网 ID | 表依据 | 执行 |
|---------|--------|------|
| SK015 等恢复+全体 | Category=恢复 + 全体 | 群体治疗 atk×表倍率（缺省 1.3；SK015 表 120%） |
| 恢复+单体 | Category=恢复 | 单体治疗 |
| SK007 | 自身 + 防御 +「防御 +35%」 | ApplySelfDefBuff(duration 表=5) |
| SK008 | 全体 +「生命上限 × 10%」 | 护盾 10% / 6s |
| SK010 | 全体 + 公式含「治疗」 | 群体治疗 atk×50%（仍不接无敌） |
| SK018 | 法术 +「目标攻击」 | Fear 8s + fallback |

---

## 建议 PR 切片

| PR | 内容 | 依赖 |
|----|------|------|
| **#6** | 评审 + 本计划 + Phase 1–3 | 已合 main `45ad6101` |
| **#7** | Phase 4 引导刷怪合流 | 已合 main `e7d4c56b` |
| **#8** | Phase 5 按需减税 | 已合 main `f591f814` |
| **本 PR** | Phase 6 WavePlanner + SkillCast + 玩家技能离 SO | 手测引导 + Ch1-0 + 六个玩家技能各放一次 |
| 按需 | 真 SDK | 接微信/广告时 |

---

## 每 PR 验收清单

发战斗相关 PR 时至少勾：

- [ ] **引导全程**：城镇开场 → 选职开战 → 摇杆教学 → 清波 → 宝箱埋伏 → 拿剑 → 救佣兵 → 撤离回城，无卡死、无重复气泡。
- [ ] **第一章 1-0 手感对照**：怪量、出手节奏、射程、受击回能、掉落三选一与上一正式版体感一致（Phase 1–2 不得改这些）。
- [ ] **职业面板**：同职业同装备的 ATK/HP 与开战 Recalc 一致（Phase 2）。
- [ ] **无新 `IsTutorialRun` 散点**（Phase 4 之后为硬门闩；Phase 1–3 也不要新增）。
- [ ] **无新 BM 技能 if**（Phase 4 之后为硬门闩）。
- [ ] **无 prefab / 产品名批量改**。
- [ ] **无 `GameConfig` 战斗倍率调参**（除非该 PR 标题写明且附对照表）。
- [ ] 自动战斗：按钮不可见或点了只 Toast「未开放」，战斗 AI 与现在一致。
- [ ] 故意缺技能配置：打 Error、不放技能、能量不扣（Phase 1+）。

---

## Phase 1 实现对照（便于 review）

| 文件 | 改动 |
|------|------|
| `BattleUI.cs` | 运行时隐藏 `autoButton`；点击兜底 Toast「未开放」 |
| `BattleManager.cs` | 删死常量；`ResolveSkill` 失败返回 null；开战强制 `isAutoBattle=false` |
| `ChapterManager.cs` | 仅文件头注释 |
| `GameDataCooker.cs` | 不再 Cook `rift_equip_gen_steps` |
| `wave_slot.csv` / `rift_equip_gen_steps.csv` / `player_jobs.csv` | 表头「未启用」 |
| `GameDataTableTools.cs` | 种子生成保留「未启用」注释 |
| `Managers/SceneManager.cs` | 删除（零引用；切景只走 `GameSceneManager`） |

## Phase 2 实现对照

| 文件 | 改动 |
|------|------|
| `AttrOwnerKind.cs` | 新枚举 Player/Merc/Monster |
| `AttrSystem.cs` | 按 Owner 写职业表基底；派生不再问 Hero |
| `PlayerJobBaseStats.cs` | `TryWriteCombatBases` 直接写基底 |
| `UnitBase.cs` | Awake 绑定 OwnerKind |
| `ChapterStatScaleTable.cs` + `chapter_stat_scale.csv/.bytes` | 章节倍率表（1:1 旧数组） |
| `GameConfig.GetChapterStatScale` | 只读表 |
| `wave_slot.csv/.bytes` | slot0–15 奇偶 Melee/Ranged |
| `stage_spawn.csv/.bytes` | 注释明确 0=公式 |

## Phase 3 实现对照

| 文件 | 改动 |
|------|------|
| `RiftEquipTables.cs` | `IsCombatLanded` / `TryResolveCombatAttr` 单一映射 |
| `RiftEquipGenerator.cs` | 只抽已落地词条；稀有度注释 + `WeightForStage` Obsolete |
| `HiddenLevelSystem.cs` | 标明裂缝稀有度真源 |
| `ConfigManager.cs` | SO 回退才走 `EquipDropRules` |
| `equip_attr_ranges.csv` | 表头闸门说明 |

## Phase 4 实现对照

| 文件 | 改动 |
|------|------|
| `TutorialRules.cs` | 本局规则包 Formal / Tutorial；BM 核心循环读包不读散落 `IsTutorialRun` |
| `TutorialBattleTable.cs` + `tutorial_battle.csv/.bytes` | HP 档列；缺列回退旧 8/13、25/36 |
| `BattleManager.cs` | `QueueTutorialStep`；`WaveData` 刷怪参数；埋伏/夹击走 `SpawnWave`；表驱动盖血 |
| `TutorialDirector.cs` | 只点步进与节拍；救援佣兵仍由导演放置 |
| `BattleSideHud.cs` / `MercBattleBanter.cs` | 连杀隐藏 / 禁闲聊改问规则包 |
| `GameDataTableTools.cs` | 种子表列与现网步进对齐 |

## Phase 5 实现对照

| 文件 | 改动 |
|------|------|
| `player_skills.csv` + `.bytes` | 6 条元数据（与 Fallback 1:1） |
| `PlayerSkillTable.cs` / `PlayerSkillDefs.cs` | Cook 表优先；未知 id → null |
| `SkillRegistry.GetPlayerSkillId` | 缺配置 Error，不回退 `ally_heal` |
| `BattleManager` | ResolvePlayerSkill 无默认 id；佣兵施法 GATE 注释；`ICombatBoundSingleton` |
| `BattleUI` / `AutoGameInitializer` | UI 不再开战；EnsureGameRoot 用 Find 不靠 getter new |
| `Singleton.cs` | 战斗必需禁止自动创建 |
| `AttrSystem` / `CharacterUI` | 存档加成仅 Player；城镇预览绑 Player |

## Phase 6 实现对照

| 文件 | 改动 |
|------|------|
| `WaveData.cs` | 原 BM 嵌套类外提；正式/引导共用 |
| `WavePlanner.cs` | 铺波 + SpawnWave / 屏外进场 / 教程 HP / 精灵挑选（方法体从 BM 原样迁出） |
| `SkillCastService.cs` | 玩家/佣兵施放、治疗/Buff fallback |
| `MercSkillExecutor.cs` | 佣兵主动技按表字段分派 |
| `player_skills.csv/.bytes` | 增战斗列；6 行 1:1 种子自 Ally SO |
| `PlayerSkillTable` / `PlayerSkillDefs` | 解析战斗列；Fallback 带同数；`BuildRuntimeConfig` |
| `SkillRegistry.Get` | 玩家 id / allyConfigId 先走表合成；Ally 只拷 vfxPrefab |
| `MercSkillTable` | 公式/类型解析，去掉 SK007/008/018 if |
| `BattleManager` | 编排器：QueueTutorial* / TryUse* 转发给 Planner / SkillCast |
| `TutorialDirector` | 未改调用点，仍只 `QueueTutorialStep` |
