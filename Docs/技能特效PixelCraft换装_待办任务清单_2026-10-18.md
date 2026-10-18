# 技能特效 Pixel Craft 换装 · 待办任务清单（2026-10-18）

> 状态：**侦查完成，未动任何文件**。等主人拍板映射表后派活。
> 关联提交：`9ab94dd2`（2026-09-15，17 个技能特效"临时配装"）、`4baa59f6`（ready.prefab 入库）。

## 一、现状盘点（2026-10-18 实证）

| 组 | 数量 | 当前特效来源 | 像素风？ |
|---|---:|---|---|
| 普攻共用套（刀光/弓/法球/治疗，Ally+Enemy） | 8 | Pixel Craft VFX URP | ✅ |
| 局内抽卡技能（flame_burst 等 17 个） | 17 | Pixel Craft VFX URP | ✅ |
| 雷击奥义 leiji（`BattleVFXSystem.cs:565` 路径加载） | 1 | Pixel Craft VFX URP | ✅ |
| **基础 6 玩家技能**（`ally_heal/shield/atk_up/atk_speed/crit_up/thunder`） | 6 | **ETFX（Epic Toon FX，3D 卡通带光照/Mesh）** | ❌ |
| **佣兵技能** SK001~SK020 | 20 | **ETFX（材质深拷进 `_Deps/`）** | ❌ |
| **怪物技能** mon_slam_multi / mon_magic_burst | 2 | **ETFX** | ❌ |
| **iron_wall 铁壁**（抽卡池成员） | 1 | **无特效**——`GetSkillVfxPrefab` 找不到文件 → 掉到 `ResolveSkillVfxKit` 关键词兜底 → 放**近战刀光**（防御技放刀光，语义错误） | ❌ |
| ready.prefab | 1 | ETFX 孤儿，全库无引用（无代码/无配表读它） | ❌ |

**根因**：`9ab94dd2` 提交信息与文档（`Docs/玩家技能冷却制改版_2026-09-15.md` §6.1）都写明"选 Pixel Craft、弃 ETFX（与 2D 像素基调冲突）"，
但执行脚本 `Tools/copy_etfx_skill_vfx_deep.py` 实际从 **Epic Toon FX** 拷（MAP 里全是 `Combat/Magic/...` 路径）。
→ 基础 6 + 佣兵 20 + 怪物 2 共 **28 个**是"拿错包的临时配装"，与同批 17 个抽卡技能、普攻套、leiji 风格割裂。

## 二、目标

28 个 ETFX 特效全部换成 Pixel Craft VFX URP 同语义单次播放版；补上 iron_wall；
ready.prefab 与 `_Deps/` 去留报主人定。**零代码改动**（特效按技能 id 走文件名路径解析）。

## 三、安全前提（已逐条验证）

1. `Assets/Resources/Config/Skills/**/*.asset` 的 `vfxPrefab` **全是 `{fileID: 0}`**（未按 GUID 引用）→ 覆盖 prefab 文件**零断链**。
2. `git status` 对 `Assets/Resources/VFX/Skills/` **干净**，28 个目标文件全部已入库 → 随时可 `git checkout` 回滚。
3. 解析链：`SkillRegistry.GetSkillVfxPrefab(id)` → `Resources.Load<GameObject>("VFX/Skills/{side}/{id}")`，
   side 由 `ResolveSkillVfxFolder` 定（`mon_*`→Monster、`SK*`→Merc、其余→Ally）。丢对文件名即生效。
4. 先例：17 个抽卡技能的 .prefab 是拷贝 + **全新 guid**（与原包内 guid 不同，已比对验证），内部材质/纹理仍指向原包 → 原包不能删。
5. ⚠ 铁律 22 风险：28 个目标是 `9ab94dd2` 脚本批产，但**若主人这一个月里手调过其中某个**，覆盖前说一声，跳过手调过的。

## 四、映射表（28 替换 + 1 新增）

选型规则：只取 `*_Color_Static` / `*_Color` 单次播放版（§6.1 定，不要 Loop/Bunch/Movement 版）；
不带动画版本号后缀、不加 `_Vertical_Billboard`（17 个先例全用平版）。

### 基础 6 玩家技能（Ally/）

| 目标文件 | 技能 | 源预制体（Pixel Craft VFX URP/VFX/…） | 备选 |
|---|---|---|---|
| ally_heal.prefab | 治愈之泉 | `Heal/Particles/VFX_Heal_01_Color_Static.prefab` | — |
| ally_shield.prefab | 圣盾壁垒 | `Shield/Particles/VFX_Shield_01_Color_Static.prefab` | — |
| ally_atk_up.prefab | 战意爆发 | `Level Up/Particles/VFX_Level_Up_01_Color_Static.prefab` | — |
| ally_atk_speed.prefab | 疾风架势 | `Projectile Wind/Particles/VFX_Projectile_Wind_01_Color_Static.prefab` | `Dust/…/VFX_Dust_01_Color_Static.prefab`（若风条像"飞行物"就换尘） |
| ally_crit_up.prefab | 致命专注 | `Critical Attack/Particles/VFX_Critical_Attack_01_Color.prefab` | — |
| ally_thunder.prefab | 天雷裁决 | `Lightning/Particles/VFX_Lightning_01_Color_Static.prefab` | — |

### 佣兵 20 技（Merc/）

| 目标文件 | 技能 | 源预制体 | 备选 |
|---|---|---|---|
| SK001.prefab | 疾风斩（物攻单体） | `Sword Slash/…/VFX_Sword_Slash_01_Color_Static.prefab` | — |
| SK002.prefab | 割裂（流血被动） | `Impact Sparks/…/VFX_Impact_Sparks_01_Color_Static.prefab` | Flipbook `Elements/Impact/VFX_Impact_Blood_01.prefab`（流血更贴，跨包） |
| SK003.prefab | 横扫千军（前排扇形） | `Sword Slash/…/VFX_Sword_Slash_01_Color_Static.prefab` | — |
| SK004.prefab | 治愈之光 | `Heal/…/VFX_Heal_01_Color_Static.prefab` | — |
| SK005.prefab | 烈焰斩（火+溅射） | `Fireball/…/VFX_Fireball_Impact_01_Color_Static.prefab` | — |
| SK006.prefab | 格挡（受击被动） | `Impact Sparks/…/VFX_Impact_Sparks_01_Color_Static.prefab` | Shield（挡下=泡） |
| SK007.prefab | 坚守（防御+嘲讽） | `Shield/…/VFX_Shield_01_Color_Static.prefab` | — |
| SK008.prefab | 圣光庇护（全体盾） | `Shield/…/VFX_Shield_01_Color_Static.prefab` | — |
| SK009.prefab | 钢铁意志（低血减伤） | `Shield/…/VFX_Shield_01_Color_Static.prefab` | — |
| SK010.prefab | 神圣庇佑（无敌+治疗） | `Respawn/…/VFX_Respawn_01_Color_Static.prefab` | Shield |
| SK011.prefab | 自愈（每秒回复） | `Heal/…/VFX_Heal_01_Color_Static.prefab` | — |
| SK012.prefab | 狂怒（低血加攻） | `Critical Attack/…/VFX_Critical_Attack_01_Color.prefab` | Level Up |
| SK013.prefab | 净化之光 | `Heal/…/VFX_Heal_01_Color_Static.prefab` | — |
| SK014.prefab | 生命绽放 | `Heal/…/VFX_Heal_01_Color_Static.prefab` | — |
| SK015.prefab | 群体治愈 | `Heal/…/VFX_Heal_01_Color_Static.prefab` | — |
| SK016.prefab | 裂隙冲击（全体魔法） | `Magic Impact/…/VFX_Magic_Impact_01_Color_Static.prefab` | Explosion Big |
| SK017.prefab | 魔力涌动（魔伤被动） | `Projectile Burst/…/VFX_Projectile_Burst_01_Color_Static.prefab` | — |
| SK018.prefab | 威慑凝视（降攻） | `Poison Debuff/…/VFX_Poison_Debuff_01_Color_Static.prefab` | — |
| SK019.prefab | 破甲侵蚀（降防被动） | `Poison Debuff/…/VFX_Poison_Debuff_01_Color_Static.prefab` | — |
| SK020.prefab | 凋零印记（DoT+死爆） | `Poison Debuff/…/VFX_Poison_Debuff_01_Color_Static.prefab` | Explosion Small（死亡爆炸那一下） |

### 怪物 2 技（Monster/）

| 目标文件 | 技能 | 源预制体 | 备选 |
|---|---|---|---|
| mon_slam_multi.prefab | 多重猛击 | `Impact Sparks/…/VFX_Impact_Sparks_01_Color_Static.prefab` | Dust（重落尘） |
| mon_magic_burst.prefab | 魔法爆裂 | `Magic Impact/…/VFX_Magic_Impact_01_Color_Static.prefab` | — |

### 新增 1 个

| 新文件 | 技能 | 源预制体 |
|---|---|---|
| Ally/iron_wall.prefab（**新建**） | 铁壁（盾盾流 Common） | `Shield/…/VFX_Shield_01_Color_Static.prefab`（与原计划"复用 ally_shield"同款） |

## 五、任务清单

1. [GLM 5.3 Flash] 新建 `Tools/copy_pixelcraft_skill_vfx.py`（单文件，照 `copy_etfx_skill_vfx_deep.py` 的形制）：
   内嵌第四节 MAP（目标相对路径 → 源相对路径）；逐项读源 `.prefab` 文本写到
   `Assets/Resources/VFX/Skills/<目标>.prefab`，**不同时拷源的 .meta**，另写一份新 `.meta`
   （`guid: uuid4().hex`，32 位小写 hex = 合法 hex32，正则 `^[0-9a-f]{32}$`；模板照 ETFX 脚本的 PrefabImporter 段）。
   安全闸：每个目标先 `git ls-files --error-unmatch` 确认已入库（未入库的**跳过并告警**，不覆盖来源不明的本地文件）；
   iron_wall 是新建不受此限。跑完打印：换了几个 / 跳了哪几个 / 回滚命令（`git checkout -- Assets/Resources/VFX/Skills/`）。
   （依赖：无。接口：无新增公开 API。预期输出：29 个 prefab+meta 落位 + 摘要。）
2. [GLM 5.3 Flash] 同脚本加 `--check` 模式：扫 `Resources/VFX/Skills/**/*.prefab`，把每个 prefab 引用的材质 guid
   解析成来源包（Pixel Craft / ETFX / `_Deps` / 其它），打一张表 —— 验收"还有没有 ETFX 残留"用。
   （依赖：任务 1 的 MAP。预期输出：全 Pixel Craft（ready.prefab 除外，见待确认）。）
3. [主人] 跑脚本 → 团结 `Ctrl+R` → 进战斗把 29 个技能各放一次（重点：6 基础技 CD 一到就放、铁壁抽卡拿到后放、
   精英/Boss 关放佣兵技），看风格是否统一、有没有残留/不消失。
4. [主人] 拍板两项（见下）。

架构校验点：
- **零代码**：`SkillRegistry.GetSkillVfxPrefab` / `ResolveSkillVfxFolder` / `BattleVFXSystem.PlaySkillPrefab` 一律不动，
  特效装载链（cfg.vfxPrefab → 路径同名 → 目录兜底 → 共用套）不变。
- 新 guid 只由 `uuid4().hex` 生成且**无任何 GUID 引用方**（§三.1 已验证），不存在"新造 guid 断引用"风险。
- 只单次播放版；prefab 自带音源（Pixel Craft 包内 SFX，flame_burst 已实证有 AudioSource）→ 换包顺带换音。
- 不删原素材包（§三.4）；不动 `other/world/`（传送门是主人手调，铁律 22）。
- 换 prefab 只需 `Ctrl+R` 刷新，不用重启（铁律 27 口径）；只有刷新后还 Missing 才重启。

## 六、待主人拍板

1. **ready.prefab**（ETFX 孤儿，无引用）：删 / 留 / 换成 Pixel Craft 占位？
2. **`_Deps/`**（ETFX 深拷材质+FBX+音效，换完后 28 个全不引用了）：全部删 / 留一阵子观察？
   —— 删了能省包体，但属批量删资源，必须主人点头才动。
3. 映射表里标了"备选"的格子（ally_atk_speed、SK002、SK010、SK012、SK020、mon_slam_multi）口味偏好。

## 七、施工红线

- 不提交不推送（等主人发话）。
- 只覆盖 §四 表内列名的 28 个 + 新建 iron_wall；表外文件一字节不碰。
- 不跑生成器覆盖任何主人手调过的 prefab（铁律 22）——手调过的从 MAP 里划掉。
- `.meta` 只许脚本按 uuid4 新造（本批零引用场景合法）；不许手改其它 meta。
