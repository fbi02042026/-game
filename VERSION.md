# PixelAdventureTown 版本记录

恢复某版：

```bash
git checkout v0.3.1
# 或
git show v0.3.1:VERSION.md
```

---

## v0.3.1 — 2026-08-11

里程碑：全量资源入库 + 宝箱通关结算流程。

### 本版提交范围
- 补交 v0.3.0 漏掉的 Epic Toon FX / Battle 场景 `box`·`chuansongmen` 等项目改动
- 通关改为：宝箱 open1→open2 → 金币飞入资源条 → 三选一装备 UI → 开 chuansongmen → 走进后弹选关

### 通关结算
- `StageClearRewardDirector`：驱动 `WorldRoot/box` 与 `chuansongmen`
- `StageClearEquipUI`：三卡并排；选中后同槽已装备显示在下方；底部「装备/替换」「丢弃」
- 未选中装备折金：`rarity * 5 * (1+star)`
- 走进传送门后：`ChapterMapUI.ShowAfterBattle` / 章节二选一

### 恢复提示
若只要旧「走 EndPoint 自动拿装备」逻辑：`git checkout v0.3.0`

---

## v0.3.0 — 2026-08-11

里程碑：Town 三场景流程 + 战斗刷怪/特效/HUD 稳定化。

### 场景与启动
- Boot / Town / Battle 三场景分工；仅 Battle 切场景走 Loading
- `GameSceneGate`：非 Battle 不硬跑战斗初始化；非 Boot 不自动进 Town
- Play 默认从当前打开场景启动（可用菜单强制从 Boot）
- Town 页 `ITownPage` 预加载，底栏切页无 Instantiate 延迟
- 共享资源条/底栏复用（`TownSharedChrome` / `MainBottomNav` / `ResourceBar`）

### 战斗核心
- 修复 Singleton 重复实例销毁整棵 GameRoot 导致「不刷怪」
- 首波刷怪硬性兜底（Update 轮询 + EnsureAtLeastOneWave）
- 怪物攻击频率下调：`MONSTER_ATK_SPEED_MUL=0.65`，间隔加长；以后调高该倍率即可
- 远程射程/索敌加大；攻击表支持 Melee / Bow / Ranged(Orb)
- 传送门分帧激活 + 开战预热 `PortalAnimator`，减轻清场卡顿
- CameraFollow 防 NaN 污染

### 战斗特效 / HUD
- 我方/敌方 VFX 路径严格分离，禁止静默回退我方箭
- 飞行物：水平直线、匀速；大小跟预制体；弓箭发射点略下移
- SPUM 按武器套装选弓/法/近战攻击动画
- `BattleSideHud`：连杀 + 下一波倒计时（右上角）

### 城镇 UI
- 酒馆等功能页、字体规则（fusion-pixel / PixelFont）
- 装备图标迁至 `Assets/Art/UI/Icons/EquipIcons`

### 主要调参入口（恢复/微调时优先看）
| 项 | 位置 |
|---|---|
| 怪物攻速倍率 | `GameConfig.MONSTER_ATK_SPEED_MUL` |
| 攻击间隔 | `MONSTER_*_ATK_INTERVAL` |
| 弓箭发射点下移 | `BattleVFXSystem.bowFireYOffset` |
| 子弹速度 | `BattleVFXSystem.projectileSpeed` |
| 攻击方式表 | `Resources/Config/MonsterAttackStyle.txt` |
