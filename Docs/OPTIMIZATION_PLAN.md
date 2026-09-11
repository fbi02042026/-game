# 像素冒险：裂隙之刃 — 优化落地计划

> **读者**：产品 + 程序。  
> **对照评审**：[`ARCHITECTURE_REVIEW.md`](./ARCHITECTURE_REVIEW.md)（问题编号 P0/P1/P2 以评审为准）。  
> **原则**：保住当前战斗手感；先关门再迁表；大拆类只在「第二种战斗编排」真出现时做。  
> **本 PR（#6）**：Phase 0 文档闸门 + **Phase 1 低风险止血已落地**。Phase 2–5 只写计划，不在本 PR 改战斗数值。

---

## 总原则

1. **不发明倍率**。战斗数走 Cook 表；`GameConfig` 只留镜头 / UI / 手感钳制。
2. **引导与正式关共用刷怪入口**，导演只编排节拍。
3. **装备掷骰 ≠ 战斗生效**。未映射词条不进包。
4. **一次只开一扇门**。每阶段可独立合入、可回滚。
5. **禁止**改 `*.prefab`、批量改产品名、拆 `BattleManager` God-object（除非 Phase 5 有第二种战斗模式硬需求）。

---

## Non-goals（全程不做）

- 一次性拆解 `BattleManager` / `BattleUI` / `AutoGameInitializer`。
- 把全部 `GameConfig` 迁进 CSV。
- 删光 Equip / Monster / Skill ScriptableObject（表尚未 100% 覆盖）。
- 重写天赋树、存档格式、上 ECS / Addressables。
- 实现微信云存、真广告、联机。
- 改任何 `*.prefab` 或运行时改用户手摆布局坐标。
- 复活商人 / 诅咒 / 锻造完整玩法。
- 为对称而合并三套能量（先写清规则）。
- 引导可视化时间轴大工程。
- **本 PR 不做**：伤害 / 能量公式 / 刷怪数量 / VFX 调参（Phase 2–4 才允许动这些，且须手感对照）。

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
| 章节属性倍率 | 目标：表（如 `chapter_theme_map` 扩列） | `GameConfig.CHAPTER_STAT_SCALE[]` 8 个数 | Phase 2 |
| 波次构成 | `stage_spawn` + 将来 `wave_slot` | `wave_slot` **未启用**（空表，代码奇偶回退） | Phase 2 填或继续禁用 |
| 关卡类型抽取 | `stage_roller_weights` + `StageRoller` | 仅普通/精英/休息/Boss | 保持；勿复活商人关 |
| 引导波次 | `tutorial_battle` | 表有步进；执行仍嵌 BM | Phase 4 |
| 装备词条数值 | `equip_attr_ranges` + 部位池 | 掷得出 30+，生效约 12 | Phase 3 |
| 天赋 | `TalentDefs` C# | 可继续硬编码直到要热更 | 非本计划必做 |
| 玩家技能 | 目标：CSV 如佣兵 | `PlayerSkillDefs` + Ally SO | Phase 5 |
| 佣兵技能 | `merc_skills` | 已表驱动 | 保持 |

### 0.2 装备词条落地清单（Phase 3 闸门，此处只记账）

**已映射进 `AttrType`（可进 Recalc）**：`ATK` `DEF` `HP` `MS` `CRIT_RATE` `ATK_SPD` `RANGE` `DODGE` `LIFE_STEAL` `ELE_DMG`(→FireDamage) `CRIT_DMG` `DMG_RED`(→DEF%)。

**表有、战斗未落地（Phase 3 起禁止进包）**：`HP_REGEN` `HEAL` `REFLECT` `BLEED` `POISON` `BURN` `SLOW` `RICOCHET` `PURIFY` `AURA` `STATIC_DMG` `LOW_HP_DMG` `RANGE_DMG` `TAUNT` `ARMOR_BREAK` `STUN_DUR` `KNOCK_BACK` `PIERCE` `CONTROL_RES` `ANTI_CRIT` 等。

**稀有度**：现状走 `HiddenLevelSystem`；`equip_rarity_rules` 的关卡段权重 `WeightForStage` 已写未用。Phase 3 必须二选一，禁止双轨。

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

## Phase 2 — 数值真源（独立 PR）

对应 P0-3、P1-3。

- `AttrSystem`：职业表存在时跳过 `GameConfig.BASE_*` 预写，**最终数字与现在一致**（先对表再改写入顺序）。
- `CHAPTER_STAT_SCALE` 迁表，数值一字不改。
- `wave_slot`：填第一章 **或** 保持未启用并在加载日志写清；禁止半填半回退却不声明。
- `AttrOwnerKind`（Player/Merc/Monster）替代 `Hero.Instance.attr == this`。
- 验收：第一章首关与引导战伤害/攻速/射程与 Phase 1 基线对照（允许浮点误差，不允许体感跳变）。

---

## Phase 3 — 装备掷骰 vs 生效（独立 PR）

对应 P0-4。

- 未映射词条不得写入 `EquipInstance` / 不得进 Recalc。
- 稀有度单一真源（隐藏等级 **或** 关卡段权重）。
- 新词条流程：先加 `AttrType` + 一处结算，再开放表权重。
- `rift_equip_gen_steps` 要么接线要么继续未启用，禁止「Cook 了当正式流程」。

---

## Phase 4 — 引导 / 正式刷怪合并（独立 PR，手测最重）

对应 P0-2。

- 教程怪 HP 档进 `tutorial_battle`，删除 `ApplyTutorialMonsterTuning` 随机盖血。
- 埋伏/夹击改为 `SpawnWave` 参数（锚点、forcedTarget），不是 BM 第二套刷怪。
- `TutorialRules` 规则包收拢 `IsTutorialRun` 散点（禁佣兵、禁清关、交战距离等）。
- **此后禁止**再往 BM 核心循环加新的 `IsTutorialRun` 分支；禁止再加 BM 技能 `if (id == SK0xx)`。

---

## Phase 5 — 按需（有产品需求再开）

对应 P0-1、P1-1、P1-4、P1-5、P1-6。

- 玩家技能 CSV 化（对齐佣兵 `MercSkillTable`）。
- 抽出 `WavePlanner` / `SkillCast`：**仅当第二种战斗编排**（新模式/活动本）需要时。
- 平台只改 Bridge，业务调用点不动；Release 广告不得模拟成功。
- UI 不初始化战斗（`BattleUI.Awake` 后备入口收掉）。
- Singleton getter 禁止为战斗必需系统自动 `new`。

---

## 建议 PR 切片

| PR | 内容 | 依赖 |
|----|------|------|
| **#6（本 PR）** | 评审 + 本计划 + Phase 1 代码 | — |
| 下一 PR | Phase 2 数值真源（无手感变化） | #6 |
| 再下一 | Phase 3 装备门闩 | Phase 2 建议先合（Attr 更干净） |
| 再下一 | Phase 4 引导刷怪 | 须完整手测引导 |
| 按需 | Phase 5 单项 | 产品点名 |

---

## 每 PR 验收清单

发战斗相关 PR 时至少勾：

- [ ] **引导全程**：城镇开场 → 选职开战 → 摇杆教学 → 清波 → 宝箱埋伏 → 拿剑 → 救佣兵 → 撤离回城，无卡死、无重复气泡。
- [ ] **第一章 1-0 手感对照**：怪量、出手节奏、射程、受击回能、掉落三选一与上一正式版体感一致（本 Phase 1 不得改这些）。
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
