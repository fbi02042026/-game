# GUID 完整性：诊断与修复说明

## 〇、第二轮结论更新（2026-09-15 晚，优先级高于下文）

1. **「从 git 还原」已验证不可行**——不是没试，是查干净了：
   - `git fetch --all --prune` 拉下全部远程分支与标签后，
     对 `git rev-list --objects --all` 里的**每一个历史 .meta blob** 建了 (guid → 路径) 索引；
   - 101 个幽灵 guid **命中 0 个**；
   - 另外查了 `git stash`、`git fsck --unreachable` 的悬空对象，同样没有。
   - **结论：这批 guid 对应的 .meta 从未进入过本仓库任何提交。** 文件层面能还原，引用关系无从还原。
2. **`7e526d69`（批量修复 581 个 .meta 的 base64 guid）不是本次元凶。**
   用 `Tools/prefab-analysis/history_health.py` 对比：
   | 提交 | .meta 数 | 悬空 guid |
   |---|---|---|
   | `167e9937`（更早） | 7746 | 202 |
   | `7e526d69^` | 7746 | 202 |
   | `7e526d69` | 7746 | 202 |
   | HEAD | 7746 | 201（修好 1 个） |
   改前改后都是 202 → **这 202 个悬空是长期历史遗留，不是这一次改坏的。**
   （但 `7e526d69` 仍是定时炸弹：它随机化了 581 个 .meta 的身份，见第五节铁律。）
3. **图片文件基本没丢，丢的是「引用关系」。**
   已验证：`Sword_1.prefab` 引用 `e19c4362…`（悬空），而工程里同时存在
   `Assets/Art/UI/Icons/EquipIcons/Sword_1.png`(guid `973d3eac…`)、
   `Assets/Resources/UI/EquipIcons/Sword_1.png`(guid `fa4d91f0…`) 等多个同名图。
   **→ 正确动作是「重绑(rebind)」：把悬空 guid 改成工程里同名/同语义资源的现行 guid，
   而不是「还原」。**
4. **「初始武器」其实没坏。**
   `Assets/Resources/Config/Equips/equip_sword_1.asset` 的 `icon` 指向 `fa4d91f0…` =
   `Assets/Resources/UI/EquipIcons/Sword_1.png`，**引用有效**。
   坏的只是第三方素材包 `Assets/Art/RPG Props and Items 370+/1/Sword_1.prefab`。
5. **「选职业页」也不是 guid 问题。**
   `PlayerJobSelect.prefab` 悬空数为 **0**；职业立绘走运行时
   `PlayerJobDefs.cs: Resources.Load<Sprite>(def.IconResourcePath)` 动态加载，
   若显示异常要查表里的路径字段，用 `Tools/CheckResourcePaths.py` 查，不要去动 guid。

**一句话：剩下的是「语义重绑」的活儿，不是「从 git 回滚」的活儿。**

---

> 本文是 2026-09-15 排查「图片/预制体大面积丢失」的结论沉淀。**动手改 guid 前必读**，
> 否则会重犯历史上「把 base64 改成 hex → 引用全断」的错误（见 `Tools/FixAllCorruptedGuids.ps1` 废弃说明）。

## 一、结论（一句话）

- **本工程用「团结引擎」。团结给新导入资源分配 base64 形态 guid 是正常行为，不是损坏。**
  → **32 位 hex 与 base64 两种 guid 都合法，绝不能互相转换。**
- 这次「图片丢失」的根因不是 guid 被写坏，而是：**预制体/场景里的 sprite 引用指向了一批
  「本工程根本不存在的 guid」（幽灵 guid）**，也就是预制体与美术资源的「身份」对不上。
- **本次事件与我们上一轮「批量把 base64 改 hex」的提交无关**（已用 `git log -S` 逐个验证：
  这些幽灵 guid 从未在本仓库任何 .meta 出现过）。

## 二、证据链

| 证据 | 说明 |
|---|---|
| `Tools/FixAllCorruptedGuids.ps1`（2026-08-26，已废弃） | 原话：「团结编辑器给它导入的新资源分配 base64 形态 guid，这是引擎行为，不是损坏。本脚本会把 base64 换成随机新 hex …但不会同步改回引用 → 引用悬空 → 预制体白框 / Missing Script（越修越坏）」 |
| `Tools/VerifyGuids.ps1`（已废弃） | 「guid 非 32 位 hex 即损坏」在本工程不成立 |
| `Tools/_guid_cache.json` | 结构为 `guid_to_path` / `path_to_guid`，路径均为 `Y:\PixelAdventureTown\Assets\...` |
| 所有历史修复脚本 | 硬编码 `Y:\PixelAdventureTown`，说明工程曾在 Y: 盘开发，现工作区在 `E:\xiangsumaoxian` |
| `git log -S <幽灵guid> -- '*.meta'` | 全部为空 → 幽灵 guid 在本仓库从未存在 |
| `Tools/orphan-sprite-rebind.json`（上一轮成果） | 上一轮已把约 45 个幽灵 guid 按「用途推断」重绑到当前美术资源 |

**推论**：预制体是在 **Y: 工程状态**下生成/编辑的（引用 Y: 的美术 guid），
工作区换到 `E:\xiangsumaoxian` 后，美术资源的 guid 与之不一致 → 引用悬空。
每轮「同步/拷贝」都可能带进新一批幽灵 guid → 这就是**反复发生**的机制。

## 三、当前受损清单

运行 `python Tools/CheckGuidRefs.py` 可随时复现（报告见 `Tools/guid-audit-report.txt`）：
- 非法 .meta：**0**（base64 视为合法）
- 悬空引用：游戏本体 **14 个文件**、第三方特效包 Demo **140 个文件**

游戏本体（需修）：
`BattleUI`(11) · `MercenaryRecruitPopup`(11) · `SettingsPopup`(10) · `BattleSettlement`(8) ·
`PlayerNamingUI`(5) · `TalentUI`(2) · `AdventureLogUI`(2) · `CharacterUI` · `DialogueUI` ·
`WorldMapPopup` · `BattleStageMap` · `Sword_1` · `Battle.unity`(box 预制体) · `Boot.unity`

> 第三方目录（`Art/Effects/**`、`SPUM/**` 的 demo 场景）的悬空是素材包自带历史问题，
> 不影响游戏本体，默认不阻塞提交。

## 四、已做的修复

1. **`Assets/Scripts/Core/Singleton.cs`**：加固 `Instance` getter。原代码在
   MonoBehaviour 构造函数/字段初始化器里被访问时会抛
   `get_IsPlaying is not allowed to be called from a MonoBehaviour constructor`；
   现改为 try/catch 静默降级返回已有实例（语义不变，仅在非法时机不再报错）。
   - 理想修复仍在**调用点**：不要在字段初始化器里访问 `Xxx.Instance`，挪到 `Awake/Start`。

## 五、防护（根治复发）

- **`Tools/CheckGuidRefs.py`**：正确模型校验器。
  - 接受 hex **与** base64；只报「引用悬空」和「真正非法的 guid」。
  - `--quiet` 供 hook 用；`--strict` 连第三方也算。
- **`Tools/install-git-hooks.py`**：安装 `pre-commit` 钩子，提交前自动跑校验，
  发现悬空引用就**拦截提交**（`git commit --no-verify` 可跳过）。
  已在本工作区安装完成。

## 六、还原方案（图片怎么回来）

幽灵 guid 指向的资源**只存在于原 Y: 工程**，本机无法自动反推，所以：

- **方案 A（最稳）**：在 Unity 里对每个悬空 Image 重新拖入正确 sprite（人力，但零错配）。
- **方案 B（最快）**：若还留有 Y: 工程（或含正确 .meta 的备份），把它的
  `Assets/Art/**/*.meta` 拷进来对齐 → 引用立刻全部解析。
- **方案 C（半自动）**：按「预制体节点 + 相邻文本」启发式推断候选资源，
  生成映射表后由脚本批量重绑 —— 需你逐条复核（存在错配风险）。

## 七、避免复发的铁律

1. **单一真源**：只在一处打开工程（别两处来回拷）。
2. **拷资源必须连 `.meta` 一起拷**：单独拷 png 会让 Unity 重新分配 guid → 引用全断。
3. **绝不 hex↔base64 互转 guid**（无论用脚本还是手改）。
4. 提交前跑 `python Tools/CheckGuidRefs.py`（或用已装的 pre-commit 钩子）。
5. 换机器/换路径后，先跑一次校验再开工。
