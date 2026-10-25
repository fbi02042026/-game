# 《TapADN 激励视频接入》待办任务清单

> 2026-10-25 立项。主人拍板：**先在 TapTap 端接 TapADN（聚合）激励视频，接好后默认不开广告，数据稳定后再考虑微信小游戏转化**。
> 本文档只给改造点和验收标准，不写完整代码；代码交给子模型（C# → GLM 5.3 Flash）落地。

---

## 一、现状

### 1.1 平台侧

- 游戏已上架 TapTap（「像素冒险：裂隙之刃」，正式签名 keystore 在 `Keys/`）。
- TapADN（Dirichlet）后台**尚未开通**：未注册媒体、未建推广位、未拿 Media ID / Media Key / Space ID。

### 1.2 项目内已有广告骨架（接 SDK 是「灌进现有桥」，不是从零搭）

| 文件 | 现状 |
|---|---|
| `Assets/Scripts/Platform/RewardedAdBridge.cs` | 激励视频桥，`ShowRewarded(placement, Action<bool>)` 接口已定；`HasRealSdk=false` 时走本地模拟；Spotlight 守卫已内置 |
| `Assets/Scripts/Platform/SpotlightBuild.cs` | 聚光灯参赛包合规开关（`SPOTLIGHT_BUILD` 宏），生效时强制 `HasRealSdk=false` 禁广告 |
| `Assets/Scripts/Systems/ResourceAdRewards.cs` | 体力/金币加号补给位；**广告分支 2026-09-21 整体注释停用**（聚光灯参赛包不得含广告），恢复步骤写在文件尾两处注释块；当前口径=前 2 次免费+第 3/4 次 50/100 钻 |
| `Assets/Scripts/Managers/BattleManager.Death.cs` → `TryReviveHeroByDiamond()` | 复活扣费唯一出口；注释已预留接广告做法（外层套 `ShowRewarded("revive", ...)`，扣费行删除，`AD_SLOT_REVIVE_DIAMOND` 转价值锚点） |
| `Assets/Scripts/Core/GameConfig.Progression.cs` | 广告总开关 `ADS_ENABLED_BEFORE_SPOTLIGHT` 已注释停用，恢复说明在位 |
| `Assets/Scripts/UI/GuildHallUI.cs` | 加号按钮当前绑定钻石路径 `TryClaimGold / TryClaimStamina` |

### 1.3 关键历史拍板（本任务必须延续）

- 2026-09-21：**聚光灯参赛包不得出现任何广告**（该铁律不因本次接入作废）。
- 2026-10-24：复活先按扣钻石上线，接广告时按 `TryReviveHeroByDiamond` 注释口径替换。
- 2026-10-25：战斗失败弹窗用主人手摆预制钮（失败节点「确定」=复活 /「取消」=返回，运行时只补 Button 接线）——复活入口 UI 以此为准。

---

## 二、目标

1. TapTap 正式包内置 TapADN **聚合** SDK（主人 2026-10-25 确认选聚合，非直连），仅接**激励视频**一种广告形式。
2. **接好 ≠ 开**：代码集成完毕后总开关默认关闭，商店在售包行为与现在完全一致；主人拍板后才在后台转「正式」+ 开开关 + 出新包计费。
3. 广告调用全部走 `RewardedAdBridge` 单一出口，玩法代码不碰具体 SDK（为将来微信小游戏换实现留口子）。
4. 发奖严格以核验结果为准（`RewardVerified` 且 `IsVerified=true` 才回调 success，关闭/跳过不发奖）。

---

## 三、任务清单

### T1【后台·主人操作】开通 Dirichlet 平台并创建媒体/推广位

- 做法：TapTap 账号登录 ssp.dirichlet.cn → 关联开发者厂商 → 签协议 → 新建媒体（包名必须与 `ProjectSettings` 的 Bundle Identifier **完全一致含大小写**）→ 新建推广位 1 个：类型=激励视频，状态=**测试**。
- 同时在聚合后台开通穿山甲、优量汇广告源账号参数。
- 产出：Media ID / Media Key / Media Name / Space ID（交客户端填配置）。
  - ⚠ 2026-10-25 主人提供过一串 **API Token**（约 130 字符）——那是平台「账户信息→Token」的**报表接口凭证**（给第三方拉数据用），**不是 SDK 初始化参数**；SDK 只认 Media ID / Media Key / Space ID 三个。Token 属敏感信息：不进仓库、不进文档、不进提交说明，只留在本地私人配置。
- 依赖：无。**阻塞 T3**。

### T2【客户端·GLM】导入 TapADN 聚合 Unity SDK 并灌进 RewardedAdBridge ✅ 代码已施工（2026-10-25 15:3x），待真机验收

- 已完成：
  1. 主人已导入 `dirichlet_mediation_unity_5.3.1.6.unitypackage`（`Assets/DirichletMediation` + `Assets/Plugins/Android/DirichletMediation/libs` 全套 AAR，Editor 依赖解析器自动接管 gradle）；**官方 Samples 示例目录已按要求删除**。
  2. 新增 `Platform/TapAdConfig.cs`（MediaId=1112901 / MediaKey（只在本文件）/ MediaName / RewardVideoSpaceId=1072474，唯一真源）。
  3. 新增 `Platform/TapAdBridge.cs`（真 SDK 封装：懒初始化、`ShowRewardVideoAutoAd`、`IsVerified=true` 才回 true；所有 SDK 引用关在 `#if UNITY_ANDROID && !UNITY_EDITOR`，编辑器零依赖）。
  4. `RewardedAdBridge.HasRealSdk` 默认改 true → 真机走 TapAdBridge，编辑器回落模拟；Spotlight 守卫保留。API 签名已逐一对过绑定源码（Init/Builder/监听接口五成员）。
  5. **ProjectSettings `AndroidMinSdkVersion` 22→24**（穿山甲硬要求，⚠ 新包不再支持 Android 5.x/6.x 设备，TapTap 可装机范围收窄——已报主人）。
- 编辑器编译 0 error CS（SDK 引用在编辑器侧被条件编译剥离，**真机编译/运行时才真正链接**）。
- 待验收：出 Android 测试包真机装 → 按官方流程把测试设备加入后台「测试工具」→ 触发广告点位（见 T3）看测试广告拉起、核验回调、发奖。
- 依赖：T1 已完成（MediaId/SpaceId/Key 齐）。

### T3【客户端·GLM】恢复/接线三个广告点位（走桥单一出口，含开关守卫）✅ 阶梯已施工（2026-10-25 14:47 拍板版），待真机验收

- 做法（全部在开关=false 时保持现有钻石路径不变）：
  1. **复活**：按 `TryReviveHeroByDiamond` 注释口径——外层套 `RewardedAdBridge.ShowRewarded("revive", ok => { if (ok) { DoReviveHero(); 收界面; } })`；是否保留「钻石复活」作为广告失败/无广告时的备用路径，**待主人拍板**（见待确认 ①）。
  2. **体力/金币加号**：按 `ResourceAdRewards` 文件尾注释块恢复广告分支；是「替换钻石路径」还是「并存选择」，**待主人拍板**（见待确认 ②）。
  3. **结算双倍**（新增候选位）：`BattleManager.Settlement.cs` 结算处，看广告=奖励×2；是否上线**待主人拍板**（见待确认 ③）。
- 约束：不动 prefab / UI 排版；复活入口 UI 以 2026-10-25 手摆预制钮方案为准；每个点位一个 placement 字符串（`revive` / `stamina_plus` / `gold_plus` / `settle_double`），禁字符串散落多处。
- 验收：开关=false 时三处行为与当前线上包完全一致；开关=true + 测试广告时点位流程走通、拒绝展示/激励未完成时不发奖。

### T4【合规·必须】隐私协议披露 + 同意后初始化

- 做法：
  1. 游戏内隐私政策文本补充 TapADN SDK 披露段（SDK 名称：TapADN SDK；主体：上海艾得蒽数字科技有限公司；采集信息类型照抄官方《SDK 合规使用说明》披露示例；隐私政策链接 https://ssp.dirichlet.cn/docs/agreement ）。
  2. SDK 初始化必须放在用户同意隐私协议**之后**（首次启动未同意 → 不初始化、不请求广告）。
- 待确认 ④：项目当前**有无**首次启动隐私同意弹窗？若无需先补（涉及 UI，另立小任务）。
- 验收：真机首启未同意协议时抓包/日志确认无 TapADN 网络请求；同意后初始化正常。

### T5【埋点】补齐广告漏斗数据

- 做法：`Analytics.AdSlotClick` 已有点击埋点；补「广告可展示 / 展示成功 / 激励完成 / 失败原因」四类事件（placement 维度），供开广告后看渗透率与留存影响。
- 验收：后台能按点位区分四类数据。

### T6【上线·主人拍板时机】转正式 + 出计费包

- 做法：真机全量回归 → 后台媒体/推广位 测试→**正式** + 开关开启 → 正式出包。出包按 AGENTS.md §4.5：`bundleVersion` 语义递增 + `AndroidBundleVersionCode` **必须 +1**；签名与上一包一致。
- 验收：商店新包生效后 TapADN 后台 T+1 报表出现收益数据。

---

## 四、待确认项 → 【2026-10-25 14:47 主人全部拍板，已施工】

| # | 问题 | 拍板结果 |
|---|---|---|
| ① | 复活：广告复活上线后，钻石复活保留吗？ | **每日阶梯**：前 2 次复活=看广告，第 3 次 50 钻、第 4 次 100 钻，**再死亡只能返回**（不弹复活选择） |
| ② | 体力/金币加号：广告与钻石怎么排？ | **每日 6 档**：前 2 次免费 → 2 次广告 → 50 钻 → 100 钻，之后封顶（金币/体力同规则） |
| ③ | 结算双倍要不要做？ | **保留不做**（本次不施工） |
| ④ | 隐私同意弹窗现状？ | 已有：腾讯版隐私协议 + TapTap 上线接了 Tap 的隐私协议；TapADN 披露段在 T4 补文本即可 |
| ⑤ | 聚光灯参赛包与广告包共存？ | **聚光灯已结束，不用再关**；2026-09-21「参赛包不得含广告」铁律作废，以后以商业广告版为主 |

### 施工口径（已落地，待主人切回团结编译验收）

- 总开关 `GameConfig.ADS_ENABLED`（**默认 false**）：false = 广告档折叠，金币/体力维持「2 免费+50/100 钻、每日 4 次」、复活维持「10 钻/次」——与当前线上包行为**完全一致**；SDK 真机验收通过、主人拍板后才翻 true 切广告版阶梯。
- 新档位真源：`ResourceAdRewards.ResolveClaimKind`（金币/体力共用唯一出口）+ 新文件 `ReviveQuota.cs`（复活阶梯 + 每日计数，存档字段 `reviveDayKey`/`reviveClaimCount`）。
- 广告调用全部走 `RewardedAdBridge` 单一出口（placement：`revive` / `stamina_plus` / `gold_plus`）；复活广告异步回调由 `_reviveChoiceOpen`/`_reviveAdPending` 双守卫防诈尸/防重复拉起；失败弹窗按钮文案广告档显示「看广告复活」。
- ⚠ 已报主人：复活确认钮上的钻石 icon 是主人手摆的，广告档显示「看广告复活」时 icon 不匹配——换图或留观，等主人看效果定。

---

## 五、施工红线

1. ~~聚光灯参赛包仍不得含任何广告~~ →【2026-10-25 主人拍板：聚光灯已结束，本条作废】以后以商业广告版为主；`SpotlightBuild` 代码保留不动（`SPOTLIGHT_BUILD` 宏不打就不生效）。
2. 🔴 总开关默认 false：接完 SDK 的包在上主人拍板前，行为必须与当前线上包**完全一致**。
3. 发奖只认 `IsVerified=true`；广告关闭/跳过/加载失败一律不发奖，且失败要有可见提示（不许静默兜底）。
4. 广告调用唯一出口 = `RewardedAdBridge`；禁止业务代码直接 import TapADN API。
5. 编辑器内测不出真实广告（SDK 是 Noop 桥）；一切真实验收以 **Android 真机**为准，排查用真机原生日志（HTTP 400 类问题先对齐初始化参数与官方 Demo）。
6. 不动 prefab / UI 排版；复活 UI 走 2026-10-25 手摆预制钮方案，运行时只补接线。
7. ID/Key 属敏感信息：只进配置文件，不硬编码散落、不进文档、不进提交说明。
8. 平时私自不提交；主人说「提交」= commit+push 一次做完；新拖的 SDK 文件连 `.meta` 一起提交，跑 `Tools/EnsureMeta.py`。
