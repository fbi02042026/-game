# 《Boss 长条冲撞攻击》待办任务清单

> 状态：**主体已落地（未提交）**；本机无团结引擎，只做了静态核对（括号 / 符号唯一性 / EnsureMeta / Guardrails G1–G9 全过），**编译与真机手感请主人在团结 2026.3.62t16 跑一次**。
> 定稿日期：2026-11-28 ｜ 基线提交：`41a72b2e`（工作区还有大量他人未提交改动，本任务只动自己那 8 个文件 + 2 个新文件）
> 依据：AGENTS.md §2.4（活干一半必交清单）、§4（不兜底 / 最小增量 / 出错只报位置）

---

## 一、主人拍板口径（2026-11-28）

1. 攻击形态：**地面长条预警 + 长条内填充从 0 涨到 100 → Boss 沿长条冲出去**。
2. 目标：**石灵**（forest_110，第1章小 Boss，近战肉盾）。前期 Boss 只有 1 个技能（砸地 **或** 冲撞），不叠两套；后期 Boss 两个都有（本次不动后期 Boss，见待办）。
3. 方向：**预警瞬间锁定玩家方向**，之后玩家换道 / 绕后能真躲开，冲刺中不再追踪。
4. 判定：**只打冲刺路径实际撞到的**（矩形走廊，逐帧扫，一人一次）。
5. 命中效果：**击退 + 眩晕**；眩晕头顶挂转圈星星（`Resources/VFX/buff/xuanyun`，主人 11-26 加的资源）。

---

## 二、本次已落地（逐条带位置）

| 项 | 文件 / 位置 | 做法 |
|---|---|---|
| 新技能配置 | `Assets/Resources/Config/Skills/Monster/mon_charge.asset`（新，guid `b425cfeb8d0740779c0e5635216953ac`） | 近战 AOE 型，damageMultiplier 2.2，aoeRadius 8（长度提示用） |
| 专属分配 | `SkillRegistry.cs:26-30` + `:195-212` | 新增 `MonsterChargeSkillId`；`GetMonsterSkillId` 仅对 `template.id == "forest_110"` 且近战返回它，其余 Boss / 精英不动 |
| 调参常量 | `GameConfig.Combat.cs:1169-1185` | `BOSS_CHARGE_LENGTH / HALF_HEIGHT / SPEED_MUL / STUN_SEC`、`CHARGE_LANE_RANGE_ALPHA / PROGRESS_ALPHA`，走 `CombatTuningTable.Get` |
| 调参表 | `combat_tuning.csv` 尾 3 行 + `.bytes` 已用 `Tools/sync_table_bytes.py` 同步 | 长度 9 / 半高 0.75 / 速度倍率 4.5 |
| 长条图元 | `Monster.Skills.cs` `StartChargeLaneTelegraph` / `CreateChargeLaneBar` / `ChargeLaneWhite` / `SyncChargeLaneFill` | 代码生成 1×1 白图，不新增美术；浅红满长度底条（order 11）+ 深红填充条（order 12），alpha .28/.85 |
| 进度同源 | `Monster.Skills.cs` `AdvanceHpSkillCharge` | 条内填充与头顶条共用 `_skillEnergy`，3 秒涨满；临门一脚对长条同样生效（最后 16% 染色 + 轻震） |
| 不重复铺条 | `Monster.Skills.cs` `SyncSkillChargeBar` / `TryReleaseChargedSkill` | 认 `_chargeLaneFill` 为「已铺」，槽满当帧放招，不再补第二遍预警 |
| 冲刺演出 | `Monster.Skills.cs` `CoBossChargeDash` | 沿锁定方向 Lerp 位移（`GameConfig.SetWorldPosition` 同步刚体）；速度 = 移速×倍率；冻场暂停不越权出伤；`finally` 清标志 + 销毁长条 |
| 命中判定 | `Monster.Skills.cs` `ApplyChargeHits` | 矩形走廊：X 在已冲过区间 ±0.35、Y 在半高内；`HashSet` 一人一次；`TakeDamage`（自带击退）+ `ApplyStun` |
| 释放分流 | `Monster.Skills.cs` `UseActiveSkill` | 冲撞有长条时直接进冲刺协程，不再进红圈 / 跳砸协程；远程 / 其他技能路径逐字不变 |
| AI 闸门 | `Monster.AI.cs:203-223` | `_bossCharging` 进演出闸门与超时自愈，防冲刺中被 AI 抢位置 / 协程强停后站桩 |
| 阶段互斥 | `Monster.Boss.cs:33` | 冲撞攒槽 / 冲刺中不起二阶段红圈，避免两套预警叠地 |
| 头顶星星 | `UnitBase.AI.cs` `ApplyStun` / `ShowStunStars` / `ClearStunStars`、`UnitBase.Damage.cs` `ResetForReuse` | 所有眩晕统一挂 `Resources/VFX/buff/xuanyun`（按路径加载，缺资源 LogError 不兜底）；粒子层对齐战斗层；到期 / 回池统一清理 |

---

## 三、任务清单（剩余 / 验收）

### T1 真机编译与手感验收（主人执行）
- 步骤：团结打开工程 → 进第1章石灵关 → 打到掉血触发技能。
- 验收：① 头顶条从 0 涨 3 秒，同时地面长条内填充同步涨满；② 涨满当帧 Boss 沿线冲，不再等红圈；③ 玩家 / 佣兵站走廊内被打退 + 头顶转圈星星 + 停手，走廊外（上下换道）不掉血；④ 剧情冻场时长条与槽都不涨、不越权出伤；⑤ 冲刺完长条消失、Boss 正常恢复 AI，不站桩；⑥ 被眩晕者头顶星星到期自动消失。
- 风险：🔴 粒子 prefab 是 3D 粒子，代码已把 renderer 提到战斗层，但**粒子发射朝向/大小是否符合像素风需真机看一眼**，不对再调 prefab（不写运行时补偿）。

### T2 后期 Boss 双技能（未做，等主人指定名单）
- 现状：`Monster` 只有单个 `_skillId`（`Monster.Skills.cs:10`），没有第二技能槽。
- 目标：后期 Boss 砸地 + 冲撞两个都有，按血量档或 CD 轮换。
- 做法提示：新增 `_skillId2` + 第二套充能 / 轮换出口；`GetMonsterSkillId` 只决定首发技能，第二技能另起分配表；**不要**靠改 `_skillId` 在运行中切换（会让头顶通知 / VFX / 判定口径串味）。
- 依赖：主人先给「哪些后期 Boss 要双技能」名单，再动工。

### T3 通用冲撞判定精度（可选优化）
- 现状：判定用 `transform.position`（逻辑体），与攻击距离真源 `GetCombatX`（身体节点）口径不同；两者通常相差挤位偏移 < 0.1，走廊 ±0.35 已覆盖。
- 若真机出现「看着在条里却没打到」，统一改读 `GetCombatX` / `UnitBase.GetCombatY`，不要各写一份。

---

## 四、施工红线

1. **不动美术排版**：粒子 prefab 本体的朝向 / 大小若不合适，报主人，不私自改序列值。
2. **不兜底**：`Resources/VFX/buff/xuanyun` 缺失时 LogError 并跳过，不造替代特效。
3. **不提交**：本项目按主人指令才提交推送；本次只落地未提交。
4. **避开他人改动**：工作区有 50+ 个他人改动文件，本任务只碰上文 8 个 .cs + 2 个新文件 + combat_tuning 两处；后续接手勿整仓 stash / reset。
