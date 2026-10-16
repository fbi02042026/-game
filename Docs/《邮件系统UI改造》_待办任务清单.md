# 邮件系统 UI 改造 · 待办任务清单

## 现状（2026-10-16）
- 当前邮件系统只有数据层 `MailSystem.cs` + `MailEntry`，UI 只有一个入口：`GuildHallUI.OnMailClicked()` 点击后**一键领完所有未读邮件**并 Toast，没有列表、没有详情、没有已读管理。
- 红点 `RedDot.Mail` 已按 `MailSystem.UnclaimedCount() > 0` 刷新。
- 用户提供了参考图（邮件列表窗、信件详情弹窗）和 `Y:/WorkBuddy/2026-10-15-17-57-37/表情贴纸_裁切/表情包/` 共 11 张贴纸，希望邮件支持文字 + 图片 + 物品 + 表情包。

## 目标
把主界面「信件」入口做成**邮件列表 → 信件详情**两级弹窗：
1. 列表窗：不区分未领取/已领取标签，每行不显示领取按钮，左下角不要全选；底部保留「删除已读」「一键领取」。
2. 详情窗：羊皮纸风格，显示标题、正文（可含表情包）、可选配图、物品图标与数量、底部单按钮（公告类只有「确定」，奖励类显示「领取」/「已领取」）。
3. 主界面信件按钮从「一键领取」改为「打开邮件列表」。

## 任务清单

| # | 状态 | 方向 | 改造点 | 验收 | 依赖 | 风险 |
|---|---|---|---|---|---|---|
| **T1** | ⬜ 待定 | 数据模型扩展 | `Assets/Scripts/Core/SaveData.cs` 的 `MailEntry` 新增字段：`bool read`、`string sender`、`MailType type`（Reward/Announcement）、`string detailImagePath`、结构化附件 `List<MailAttachment>`（type/amount/iconOverride）。为兼容旧溢出邮件保留现有 `gold/diamond/...` 字段，读档时无新字段走默认值。 | 旧存档不炸；新邮件可带发件人、类型、配图、结构化附件。 | — | 字段改名/迁移破坏旧存档。 |
| **T2** | ⬜ 待定 | MailSystem API 补全 | `Assets/Scripts/Systems/MailSystem.cs` 新增：`Enqueue(...)` 富邮件入队重载、`MarkAsRead(mailId)`、`DeleteRead()`（**只删 `read==true && claimed==true` 的邮件**，已读未领取的不删）、`ClaimAll()`（一键领取所有未领并汇总提示）。`TryClaim` 兼容旧 flat 资源字段，同时支持新 `attachments`。 | API 覆盖列表/详情全部操作；一键领取后红点熄灭；删除已读只删已领的、不误删未领奖励。 | T1 | — |
| **T3** | ⬜ 待定 | 美术资源导入 | ① 邮件窗体美术**已就位** `Assets/Resources/Art/UI/邮件/`：`_0010_底框.png`(窗体框)、`_0005_关闭.png`(关闭)、`_0006_一键领取.png`/`_0007_删除已读.png`(底部按钮)、`_0008_已领取邮件.png`/`_0009_未领取邮件.png`(信封开闭)、`_0000_已领取.png`(已领印章)、`_0001_礼物.png`(礼物)、`_0002_垃圾桶.png`(删除)、`_0003_对钩.png`(勾)、`_0004_方框.png`(框)、`_0000s_0002_信件.png`(羊皮纸详情底)；② **贴纸仍在外部分享目录**，需拷到 `Assets/Resources/Art/UI/Stickers/` 并带 `.meta`（11 张：生气/高兴/惊讶/调皮/傲娇/白眼/轻视/害羞/哭泣/无语/疑问）。 | 列表/详情窗体美术可直接引用；贴纸 `Resources.Load<Sprite>("Art/UI/Stickers/生气")` 可加载。 | — | 贴纸需从 `Y:/WorkBuddy/2026-10-15-17-57-37/表情贴纸_裁切/表情包/` 拷入项目（用脚本批量拷 + 生成 hex32 .meta）。 |
| **T4** | ⬜ 待定 | 预制体搭建 | 新建 `Assets/Resources/Prefabs/UI/MailListWindow.prefab`（Canvas、遮罩、窗体底图、标题「邮件」、关闭按钮、ScrollView、底部「删除已读」「一键领取」）、`MailListItem.prefab`（背景、信封图标、标题、发件人、时间、奖励预览图标区、红点）、`MailDetailWindow.prefab`（Canvas、遮罩、羊皮纸、关闭按钮、标题、正文区、可选配图、表情包行、物品区、底部按钮）。 | 三个预制体在编辑器里可见、节点命名与子模型代码绑定约定一致。 | T3 | 必须按「以预制体效果为准」原则，代码只绑定不覆盖几何。 |
| **T5** | ⬜ 待定 | 邮件列表 UI 脚本 | 新建 `Assets/Scripts/UI/MailListUI.cs`：参考 `DailyLoginUI` 的 `Show()/Ensure()` 静态入口，`Build()` 按预制体节点名绑定；`Refresh()` 从 `MailSystem.GetInbox()` 生成列表项；点击行调 `MailDetailUI.Show(mailId)`。 | 打开列表显示全部邮件；无标签、无行内领取按钮、无全选；点击行进详情。 | T1,T2,T4 | ScrollView 内容少时不需要虚拟化。 |
| **T6** | ⬜ 待定 | 列表项脚本 | 新建 `Assets/Scripts/UI/MailListItemUI.cs`：`SetData(MailEntry)` 刷新信封开闭、标题、发件人、时间、奖励预览图标、红点/已读灰态；点击事件上报给 `MailListUI`。 | 每行数据与视觉一致；未读显示红点，已读变灰/去红点。 | T1,T4 | 时间格式走统一工具，不要手写 `DateTime`。 |
| **T7** | ⬜ 待定 | 信件详情 UI 脚本 | 新建 `Assets/Scripts/UI/MailDetailUI.cs`：`Show(mailId)` 打开；解析正文中的 `[sticker:xxx]` 标签并在正文下方生成贴纸图；显示 `detailImagePath` 配图、物品图标与数量；底部按钮：公告类只显示「确定」，奖励类未领显示「领取」、已领显示「已领取」；领取成功后刷新列表红点。 | 公告只确定，奖励可领取，表情包正常显示，关闭后列表状态刷新。 | T1,T2,T3,T4 | 正文行内插入图片较复杂，**建议贴纸不内嵌、统一放正文下方一行**，待确认。 |
| **T8** | ⬜ 待定 | 主入口切换 | `Assets/Scripts/UI/GuildHallUI.cs`：`OnMailClicked()` 改为 `MailListUI.Show()`；原一键领取逻辑保留在列表底部按钮调用 `MailSystem.ClaimAll()`。 | 点击主界面信件按钮打开邮件列表，不再直接领完。 | T5 | 需同步改 `OnMailClicked` 命名或注释，避免误导。 |
| **T9** | ⬜ 待定 | 红点/刷新 | `RedDot.RefreshCommon()` 已刷新 `Mail`；`MailSystem` 所有变更（领取/删除/新增）后调用 `RedDot.RefreshCommon()`。`MailListUI`/`MailDetailUI` 打开/关闭时触发刷新。 | 主界面信件红点与未读数量一致。 | T2,T7 | — |
| **T10** | ⬜ 待定 | 测试验证 | ① 旧溢出邮件仍能正常领取；② 新建富邮件（带图、带贴纸、带多资源）流程走通；③ 一键领取/删除已读/单条领取/只读公告全部验证；④ 微信小游戏真机无 Resources.Load 卡顿。 | 全流程无报错，视觉与参考图一致。 | T1-T9 | 本机无引擎，CS 编译与真机需老师在项目内验证。 |

## 待确认（✅ 2026-10-16 11:56 主人拍板：②③④⑤ 全部按建议方案执行——与已落地实现一致，无需改码）
1. **美术源图**：用户截图是带内容的效果图，需要**空的邮件列表窗深蓝框**、**空的信件详情羊皮纸框**、**信封开/闭图标**、**底部按钮底图**的源文件（PNG/PSD）。如果暂时没有，子模型先用纯色块兜底，但会明确标记为占位。
   → 实际进展：窗体美术已就位于 `Assets/Resources/Art/UI/邮件/`（见 T3），UI 已直接引用真底图；未用纯色块占位。
2. **删除已读的范围**：「删除已读」是否只删已读且已领取的邮件？若允许删除已读但未领取的奖励邮件，玩家可能误删。建议：**已读且已领取** 或 **已读的公告类** 才可删；奖励类必须等领取后才可被删除。请老师拍板。
   → ✅ 拍板「是」：已实现于 `MailSystem.DeleteRead()`（公告已读即可删；奖励类 read && claimed 才删）。
3. **表情包显示方式**：推荐不在正文行内插图片（需要 TMP 或复杂 Layout），而是把 `[sticker:生气]` 这类标签从正文移除，在正文下方用 HorizontalLayout 排成一排贴纸。是否接受？
   → ✅ 拍板「可以」：已实现于 `MailDetailUI.RebuildStickers`（正文下方横排，≤5 张）。
4. **奖励类邮件已读后是否保留**：领取后邮件变「已领取」状态，是继续显示在列表里（参考图中有「已领取」行）还是删除？参考图显示保留，建议保留并显示灰态/已领取标签。
   → ✅ 拍板「继续在列表等玩家手动删」：已实现于 `MailListUI.ApplyRowVisual`（灰态+信封开+印章；删除只走「删除已读」按钮）。
5. **邮件列表排序**：按时间倒序（最新的在最上）？默认如此，除非老师另有要求。
   → ✅ 拍板「最新在最上面」：已实现于 `MailListUI.RebuildRows`（createTime 倒序）。

## 施工红线
- 子模型分工：`T1/T2/T7/T8` 等 C# 逻辑/bug 修复 → GLM 5.3 Flash 或 Step-5-Preview；无 Shader 需求，不启用 DeepSeek。
- 不改无关系统：只动邮件相关数据/UI，不动战斗、数值、天赋、装备体系。
- 旧存档兼容：`MailEntry` 新字段必须给默认值，不能破坏现有 `mailInbox` 反序列化。
- 资源 / 代码 / `.meta` 全部入库；新增贴纸拷入项目后必须带 `.meta`。
- 本机无团结编辑器，只做静态核对与任务拆解，编译与真机验证需老师在项目内跑。

## 实施进展（2026-10-16 主人说「我要完整的功能」后落地，未提交）
- ✅ **已实施：T1 / T2 / T3 / T5 / T6 / T7 / T8 / T9**（T10 编译+真机待主人跑）。
  - T1/T2：`MailSystem.cs` —— `MailEntry` 新增 `read/sender/type/detailImagePath/attachments`（旧档默认值兼容），`MailType`/`MailAttachment` 新类型；`TryClaim` 支持附件走上限；新 API `Enqueue/MarkAsRead/DeleteRead/ClaimAll`。
  - T3：11 张贴纸已拷入 `Assets/Resources/Art/UI/Stickers/` 并生成 hex32 `.meta`（脚本 `.workbuddy/tmp_copy_stickers.py`）。
  - T5-T7：`Assets/Scripts/UI/MailListUI.cs` + `Assets/Scripts/UI/MailDetailUI.cs`，纯运行时 Build（DDOL + `UICanvasSetup.ApplyPopup` 600/610），底图引用已就位美术。
  - T8：`GuildHallUI.OnMailClicked()` → `MailListUI.Show()`。
- ⚠️ **T4 变通**：本机无法验证手写 prefab YAML → 未建预制体，UI 用**代码 Build + 已就位底图**（_0010_底框 / _0005_关闭 / _0006_一键领取 / _0007_删除已读 / _0008_/_0009_信封 / _0000_已领取 / _0000s_0002_羊皮纸）。布局常量集中在 `MailListUI.cs` / `MailDetailUI.cs` 顶部，真机与底图对位有偏差时报界面名+方向即可单点调；美术精修预制体做好后按本清单 T4 的节点名约定接入，代码不覆盖几何。
- **待确认 ②③④⑤ 按清单建议值落地**（老师不认可随时单点改）：删除范围=公告已读即可删、奖励类必须已领（`MailSystem.DeleteRead`）；贴纸=正文下方横排最多 5 张（`MailDetailUI.RebuildStickers`）；已领取邮件保留列表灰态+印章（`MailListUI.ApplyRowVisual`）；排序=时间倒序（`MailListUI.RebuildRows`，时间格式 `FormatTime` 私有实现——项目暂无统一时间工具）。
- 提醒：`MailListUI.cs.meta` 为编辑器生成的 56 字符 guid，按 2026-10-15 口径照常提交不纠结（脚本运行时 AddComponent，不拖预制体，无影响）。
