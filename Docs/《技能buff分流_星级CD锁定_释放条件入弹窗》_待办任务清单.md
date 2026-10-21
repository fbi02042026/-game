# 《技能 Buff 分流 · 星级 CD 锁定 · 释放条件入弹窗》待办任务清单

> 2026-10-21 主人三条指令：① 战意爆发法系吃不到，要按伤害类型分流；② 升星后 CD 应该不变；③ 检查释放条件是否有问题 + 把释放条件加进技能弹窗信息。
> 状态：**已落地（7 个代码文件，2026-10-21）**。未起团结编辑器编译（改动集中且小），**待主人进编辑器实测**；未提交，等「提交」指令。

---

## 一、现状与调用链（已逐行核实）

### 技能定位
- `battle_surge` 战意爆发（AtkBuff，ally_atk_up）：CSV 攻击 +50% / 持续 8s / CD 18s，身边 ≥2 怪自动放
- `deadly_focus` 致命专注（CritBuff，ally_crit_up）：CSV 暴击 ×1.25 / 持续 8s / CD 18s，身边 ≥1 怪自动放
- 运行时真源 = `player_skills.csv`（仅 6 行，6 个原始技能全在）；`PlayerSkillDefs.Fallback` 只作缺表兜底，**battle_surge 的 Fallback 数值（+30%）与 CSV（+50%）不一致，以 CSV 为准**
- 扩充池 24 个技能（war_banner / iron_wall 等）当前**不在 `PlayerSkillDefs.All`**（既无 CSV 行也无 Fallback 条目）→ 抽卡池实际只有 6 个原始技能（DraftPool.CollectNewSkills 从 All 选）。本清单不碰此事，仅记录备查

### 链路 1：玩家 Buff 技能释放（T1 根因）
```
PlayerSkillPassive.TickAutoCast（每帧，全局 GCD 早退闸）
  → BattleManager.FindReadySkillSlot（Skills.cs:43-72：CD 好 + IsTriggerMet + 蓝够，槽序优先）
  → SkillCastService.TryUsePlayerSkillSlot（:39-142）
      isBuff = true → 不进 SkillSystem.UseSkill（:106 `!isBuff && UseSkill`）
      → ExecuteAllySkillFallback（:356）→ ApplyTeamBuff（:461-507）
          buffAttr == Attack/Defense/CritRate && duration>0 → u.attr.ApplyTimedBuff(...)
          （hero + 全体在场佣兵各吃一份，:488-491）
```
- 表里 `battle_surge` 的 `buffAttr = Attack`，`PlayerSkillTable.BuildRuntimeConfig:302` 直通无分流
- `SkillSystem.ExecuteBuff:250` **有**按 `caster.IsMagicDamageDealer()` 分流 MagicAttack 的逻辑——但玩家 Buff 不走这条路，分流形同虚设
- 伤害链路证实白加：`DamageFormula.BuildAttackRaw:62-77` 魔法单位读 `GetAttr(MagicAttack)`；`AttrSystem.GetAttr:270` → `ApplyTimedBuff(type, v):355-364` **MagicAttack 落 default 原样返回** → 法师魔攻永远吃不到战意爆发

### 链路 2：星级成长（T2 根因）
```
RunDraftDirector.BuildRunSkill:574-613
  :584  float cdMul = SkillDraftMeta.StarCooldownMul(star);      // 每星 -6%，封顶 0.6
  :593  active.cooldown = Mathf.Max(0.6f, active.cooldown * cdMul);
  → ★5 时 18s → 12.4s
同算式副本：DraftRewardDetailUI.cs:403（升星卡详情，注释承诺「与 BuildRunSkill 同源算式」）
```

### 链路 3：弹窗文案（T4 根因）
```
SkillSelectUI（角色界面技能页）descText = PlayerSkillDefs.FormatDetail(def)（SkillSelectUI.cs:117，hover 路径 :111 同口）
  FormatDetail（PlayerSkillDefs.cs:586-590）= "{numbers}。冷却 {cd} 秒。{useHint}。"
  useHint 两套口径：CSV 6 行 =「…自动释放」模糊版；Fallback 6 个 =「…手动点击」过期版（纯 CD 制后从没更新）
```

---

## 二、任务清单

### T1 战意爆发 buffAttr 按伤害类型分流（法系生效）

**改造点（两处，缺一不可）**：

1. `Assets/Scripts/Combat/SkillCastService.cs` `ApplyTeamBuff` 的 `ApplyTimed` 局部函数（:483-487）：
   `buffAttr == AttrType.Attack` 时按**承受单位自身** `u.IsMagicDamageDealer()` 换键 `AttrType.MagicAttack`（法师 hero 吃魔攻倍率、物理佣兵吃物攻倍率，混合队正确）。Defense/CritRate 无物魔之分，不动。
2. `Assets/Scripts/Systems/AttrSystem.cs` 补 MagicAttack 计时层（五处成对，漏一处 = buff 残留跨关）：
   - :292-294 字段区加 `_buffMagicAtkMul / _buffMagicAtkUntil / _buffMagicAtkTotal`（挨着 `_buffAtk*` 声明）
   - :306-312 `ApplyTimedBuff(type, value, isPercent, duration)` switch 加 `case AttrType.MagicAttack`
   - :355-364 `ApplyTimedBuff(type, raw)` 读值加 case
   - :343-353 `CollectActiveTimedBuffs` 加输出
   - :316-324 `ClearTimedBuffs` 加清零

**UI 零改动**：`BuffHudUI.StyleOf:101`（「魔」紫牌）/ `IconOf:61`（魔攻图）已就绪，数据出口就是 CollectActiveTimedBuffs。

**验收**：法师放战意爆发 → 战斗力右侧亮「魔」牌 + 数字倒计时 + 20% 闪烁 → 普攻与技能伤害按 `+50%×星级` 结算 → 8s 到期恢复；战士 / 物理佣兵行为零变化（「攻」牌照旧）。

**风险**：`SkillSystem.ExecuteBuff:250` 写的是 `BattleManager.tempBuffs`（永久层）不是 ApplyTimedBuff，两条路径不冲突；改完后临时打点 `Debug.Log("[BuffDebug]...永久分支")` 不再被法师战意触发（应改走计时层）。

---

### T2 升星后 CD 锁定不变

**改造点**：

1. `Assets/Scripts/Systems/RunDraftDirector.cs:584`：星级 CD 倍率恒 1（摘掉 `StarCooldownMul` 乘法）。**保留亲和 `cdMul *= AffinityCooldownMul`（:588）**——亲和是职业匹配奖励，不是升星成长（见 Q1）。
2. `Assets/Scripts/UI/DraftRewardDetailUI.cs:403`：同改，维持「与 BuildRunSkill 同源算式」的注释承诺。其 :443-448 冷却对比行**不用动**：`CompareLine:513` 数值相等时只显示当前值，不会出现「18 → 18」。
3. `Assets/Scripts/Config/SkillDraftMeta.cs:105 / 134-139`：`StarCooldownStep` / `StarCooldownMul` 方法**保留不删**（防漏改编译断裂），加注释「2026-10-21 主人拍板：星级不再减 CD，仅供回退」。

**验收**：把某技能 ★1 → ★3 → HUD 冷却总时长（`GetPlayerSkillCooldownTotal`）与 1 星同值；升星卡详情「冷却」行无箭头；伤害 / 增益幅度 / 持续时间仍按星级成长（StarDamageMul / StarBuffMul / StarDurationMul 不动）。

**已知行为变化（主人主动要求）**：满星技不再「CD 更短」，只强在数值。

---

### T3 释放条件检查（分析结论 + 修点）

**链路已核实**：`PlayerSkillPassive.Triggers:91-99`（6 技能全登记）、`IsTriggerMet:105-121`（别名解析正确）、`CountNearbyMonsters:123-137`（Hero 为中心 9 单位）、`FindReadySkillSlot` 槽序优先级——主干无缺陷。发现问题 4 个：

- **P1（必修，实打实 bug）**：`battle_surge` 触发 = 身边 ≥2 怪（:96），而 CSV useHint 明写「精英/Boss 战或大量小怪时自动释放」——**单 Boss / 单精英战只有 1 只怪 → 战意爆发永远放不出**，恰好错过最该开的窗口（deadly_focus 阈值 1 反而成立）。
  **修法**：`NearbyMonstersAtLeast` 判定增加「场上存在精英或 Boss（遍历 `bm.monsters` 判 `m.IsEliteWave || m.IsBossUnit`，Monster.cs:73-74 公开属性）即视为满足」；battle_surge 阈值保持 2（小怪场景不变），Boss/精英场景放行。gale_stance / deadly_focus 阈值已是 1，不受影响。
- **P2（设计问题，待主人拍板，见 Q3）**：deadly_focus ≥1 怪 = 战斗中几乎恒开（CD 18s / 持续 8s，覆盖率 44%+，怪露面就放），与 useHint「Boss 战或精英怪出现时」不符。是否收紧为「仅精英/Boss 在场才放」？牵涉战力节奏，不动，等指令。
- **P3（小口，建议修）**：`CountNearbyMonsters` 没有 `GameConfig.IsInCombatViewport` 过滤——屏幕外 / 刚刷出的怪也计数，与 AOE 索ET（`SkillSystem.cs:308`）口径不一致。补一行过滤。
- **P4（非 bug，仅记录）**：触发满足 → 释放前怪已死 → buff 空放。与 2026-10-18 主人拍板「治疗/增益/护盾目标是友方，不受无目标限制」同口径，不改。

---

### T4 释放条件写进技能弹窗信息

**改造点**：

1. `Assets/Scripts/Systems/PlayerSkillPassive.cs` 新增 `public static string TriggerText(string skillId)`——按 `Triggers` 表生成中文精确条件（**唯一真源**，杜绝文案与代码两处漂移）：
   - `HpBelow` → 「自身血量 ≤{0}% 时自动释放」
   - `AnyAllyHpBelow` → 「任意队友血量 ≤{0}% 时自动释放」
   - `NearbyMonstersAtLeast` → 「身边 {半径} 内有 {n} 名敌人时自动释放」（P1 修完后追加「（含精英/Boss）」）
   - 未登记 → 返回 null，调用方兜底
   - 阈值 / 半径从 `Trigger` 实例字段读，**不写死数字**（改表即改文案）
2. `Assets/Scripts/Config/PlayerSkillDefs.cs` `FormatDetail:586-590`：改为 `{numbers}。冷却 {cd} 秒。{释放条件}。`——优先 `PlayerSkillPassive.TriggerText(def.id)`，null 时回退 `def.useHint`。
3. 依赖方向：Config → Systems 静态互引，同程序集（Assets/Scripts 无 asmdef），TriggerText 只在 UI 展示期调用、不进静态构造，无初始化顺序风险。

**验收**：角色界面点开技能 → 致命专注显示「暴击率 ×1.25，持续 8 秒。冷却 18 秒。身边 9 内有 1 名敌人（含精英/Boss）时自动释放。」；治愈之泉显示「任意队友血量 ≤80% 时自动释放」；文案数字与 Triggers 表逐项一致。

---

## 三、待确认项（Q）

| # | 问题 | 影响 |
|---|---|---|
| Q1 | 亲和减 CD（×0.85）保留还是连坐去掉？ | T2 默认保留（职业匹配奖励 ≠ 升星成长），主人一句话可改 |
| Q2 | Fallback 6 处 useHint「手动点击」过期字样是否顺手改「自动释放」？ | T4 兜底路径；CSV 6 技已有精确条件不受影响 |
| Q3 | P2 致命专注是否收紧为「仅精英/Boss 窗口」？ | 牵涉 DPS 节奏与战力计算，未动 |
| Q4 | T1 后法师战意爆发的 BuffHud 亮「魔攻」图（紫）——10-12 主人给的七张图里有这张，OK 吗？ | 纯展示确认 |

---

## 四、施工红线

1. 只改上文点名的文件 + 行号，**禁顺手重构**；AttrSystem 计时层五处必须成对，漏一处 = buff 跨关残留。
2. 不碰预制体 / UI 布局；**不动 player_skills.csv**（数值不动，只改代码链路）。
3. 不删 `StarCooldownMul` 方法本体，只摘调用点。
4. P1 修法只加「精英/Boss 在场即满足」的 OR 条件，不改原阈值语义。
5. 改完静态核对 + 逐段回读，**不起编辑器编译**（改动小且集中），交主人进编辑器实测；提交前跑 `Tools/EnsureMeta.py`（虽无资源改动，走流程）。
6. 按约束：本清单只给改造点与验收标准，C# 交子模型落地。

---

## 五、执行记录（2026-10-21 已落地）

| 项 | 文件 | 改动 |
|---|---|---|
| T1 | `Systems/AttrSystem.cs` | MagicAttack 计时层五处成对：字段（:298）、写（:315）、读（:371）、收集（:360-361）、清除（:326/330） |
| T1 | `Combat/SkillCastService.cs` | `ApplyTeamBuff` 的 `ApplyTimed`（:486-492）按承受单位 `IsMagicDamageDealer()` 换键 MagicAttack |
| T2 | `Systems/RunDraftDirector.cs` | :584 `cdMul = 1f`（亲和 ×0.85 保留，按 Q1 默认项） |
| T2 | `UI/DraftRewardDetailUI.cs` | :403 同改（同源算式承诺） |
| T2 | `Config/SkillDraftMeta.cs` | :105/:134-139 `StarCooldownMul` 保留不删，标注停用仅供回退 |
| T3-P1 | `Systems/PlayerSkillPassive.cs` | `IsSatisfied` 怪群判定加 `HasEliteOrBossOnField()` OR；新增该方法（`monsters` 是 `List<UnitBase>`，先 `is Monster` 强转，口径同 `BattleManager.MonsterDeath.cs:13`） |
| T3-P3 | `Systems/PlayerSkillPassive.cs` | `CountNearbyMonsters` 补 `GameConfig.IsInCombatViewport` 过滤 |
| T4 | `Systems/PlayerSkillPassive.cs` | 新增 `TriggerText(skillId)`：按 Triggers 表生成中文精确条件（血量/怪群两类，阈值从实例字段读） |
| T4 | `Config/PlayerSkillDefs.cs` | `FormatDetail:586-594` 优先 `TriggerText`，null 回退 useHint，tail 为空不再拼「。。」 |

**回读验证**：git diff 逐行核对 7 文件，无越界改动；`StarCooldownMul` 全库零调用点（仅定义+注释）；`FormatDetail` 唯一消费点 = `SkillSelectUI.cs:117`；AttrType.MagicAttack / IsInCombatViewport(UnitBase) / Monster.IsEliteWave/IsBossUnit 签名逐一比对存在。

**附带效果**：useHint 字段退居「未登记技能兜底」（6 个原始技能全部被 TriggerText 取代），Q2 的 Fallback「手动点击」过期文案问题自然消解，不再需要改。

**待主人实测（验收）**：
1. 法师放战意爆发 → 战斗力右侧亮「魔」牌+倒计时+20% 闪烁，普攻/技能伤害 +50%×星级，到期恢复；战士/佣兵零变化；
2. 技能 ★1→★3 → HUD 冷却总时长不变，升星卡详情「冷却」行无箭头；
3. 单 Boss / 单精英战 → 战意爆发能自动放出（以前放不出）；
4. 角色界面技能页 → 文案形如「暴击率 ×1.25，持续 8 秒。冷却 18 秒。身边有敌人（含精英/Boss）时自动释放。」；治愈之泉 = 「任意队友血量 ≤80% 时自动释放」。
