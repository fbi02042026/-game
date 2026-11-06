# SDK 统一开关规范

> **生效**：2026-11-06 主人拍板「把 taptapsdk 做成开关，以后还会接其他的 sdk 到时候都按开关做」。
> **适用**：本项目全部第三方 SDK（TapTap、微信、广告、支付、统计、崩溃等），无例外。
> **配套文档**：`Docs/接入第三方SDK情况说明.md` 管「合规备案」（谁、收集什么、何时读）；本规范管「工程接入方式」（怎么开、怎么关）。**新增 SDK 两份都要改**。
> **本次交付范围**：仅本规范文档入库；代码改造清单见第五节，另起提交由 C# 子模型落地。

---

## 一、现状与根因

TapTap 其实已有开关雏形，只是被写死成「常开」，而且业务侧没跟上来：

| 环节 | 位置 | 现状 |
|---|---|---|
| 开关宏 | `ProjectSettings/ProjectSettings.asset:1250-1252` | Android 组 `scriptingDefineSymbols` 为空；编辑器里默认不带 `TAPSDK_ENABLED` |
| 恒开点 | `Assets/Editor/CliAndroidBuild.cs:52-56` | 每次出包无条件 `AddDefine(baseDefines, TapDefine)`；第 90 行注释明写「TAPSDK_ENABLED 不回滚：Android 出包固定带 TapTap」 |
| SDK 调用 | `Assets/Scripts/Core/TapTapBootstrap.cs`（103/175/188/267/304/336/353 行）、`Assets/Scripts/Core/GameAnalytics.cs`（54/94/115/130/149/367/382 行） | 全部已包在 `#if TAPSDK_ENABLED` 内，**这部分本身合规**，关宏不会编译失败 |
| 业务侧 | `Assets/Scripts/UI/LoginUI.cs:194-212, 229, 562`、`Assets/Scripts/UI/TapTapRatePopup.cs:25-35`、`Assets/Scripts/Managers/BattleManager.Settlement.cs:2336`、`Assets/Scripts/UI/TownHubController.cs:81` | 无开关判断。只关宏的话登录按钮仍在、点了会刷 `[TapTap] 未定义 TAPSDK_ENABLED` 的 LogError，章末好评链路仍会走 |
| 包依赖 | `Packages/manifest.json:13-15` | `com.taptap.sdk.core/login` 4.10.9 + EDM 内嵌包；2026-11-06 拍板常驻，SDK 包相关改动不入库 |

**结论**：编辑器默认关、命令行出包恒开，两处真值不一致；且只关宏「等于没关」——玩家仍看得到入口并吃到报错。因此统一成「一个真值、三层生效、所有 SDK 同规格」。

三层关系（自顶向下）：菜单勾选 → 写入 define 宏（唯一真值）→ 一支驱动 `#if` 编译期守卫、一支驱动 `SdkFeatures` 运行时开关 → 共同决定业务表现（登录 / 埋点 / 好评）。包依赖在最底下常驻，不随开关增删。

---

## 二、规范正文（硬规则）

### 2.1 唯一真值 = Scripting Define Symbol

- 每个 SDK 一个全局唯一宏，命名 `XXX_ENABLED`（现例 `TAPSDK_ENABLED`）。
- 宏按平台组写在 `ProjectSettings/ProjectSettings.asset` 的 `scriptingDefineSymbols` 里；该文件**入库**，换机 `git pull` 后状态天然一致。
- `Tools/SDK/…` 菜单只是读写宏的界面，**不得**用 EditorPrefs / ScriptableObject / 独立 json 另存一份状态——两套真值必然打架，命令行出包也无法复现。
- 默认值随登记表；TapTap 首版 = **开**，保持现状线上行为不变。

### 2.2 注册表唯一真源

- `Assets/Editor/SdkToggles.cs`（Editor-only）：一条记录 = Id / Define 宏名 / 生效平台组 / 默认值 / 菜单名。
- 新 SDK 接入**只加一条记录**，不改构建逻辑、不改菜单代码。
- 菜单带勾选态（validate 函数）与「打印当前开关状态」，照 `Assets/Editor/UiSpriteRefGuard.cs:78-90` 的现成先例。

### 2.3 编译期守卫

- 第三方 SDK 的类型 / API **只准**出现在 `#if XXX_ENABLED` 块内；业务代码一律只引用项目内 Facade，禁止直接点名 SDK 类型。
- 一个 SDK 一个 Facade（现例：`TapTapBootstrap` 管初始化与登录、`GameAnalytics` 管埋点），将来换 SDK 只改这一层。
- 关闭态下全工程必须零 `CS0246`（找不到 SDK 程序集）——这是开关能成立的前提。

### 2.4 运行时出口

- `Assets/Scripts/Core/SdkFeatures.cs`：每个 SDK 一个由 `#if` 生成的只读 `const bool`，自身不引用任何 SDK 类型。
- 所有 SDK 专属 UI、触发点、置位 / 消费点一律判它；**禁止各处自己散写 `#if`**（散着写必漏，TapTap 这次就是反例）。

### 2.5 构建脚本不代管

- `CliAndroidBuild.BuildApk` 删掉无条件注入 SDK 宏的语句，只保留 `SPOTLIGHT_BUILD` 这类与 SDK 无关的构建宏处理（含原有的构建后回滚）。
- 出包前把每个 SDK 宏的 ON/OFF 打进日志。**缺宏不自动补、不报错中断**——不兜底，要让人看见。
- 其余平台组（Standalone / WeixinMiniGame）同理按登记表各自维护，禁止硬编码。

### 2.6 包依赖常驻

- `Packages/manifest.json` 里的 SDK 依赖与 EDM 内嵌包**不随开关增删**（2026-11-06 拍板 TapSDK 常驻；SDK 包相关改动不入库）。
- 开关只控制「编译是否带 SDK 调用」与「业务是否走 SDK 专属流程」，不碰 UPM 依赖，不动 gradle / 原生依赖。

### 2.7 关闭态行为（fail-closed，且静默）

- 关掉后：隐藏 SDK 专属入口、不创建 / 不初始化 Bootstrap、不弹渠道弹窗、清掉跨场景残留标记、不刷「未定义宏」的 LogError。
- 这是**开关的预期行为，不是兜底**；真实异常（初始化抛错、配置缺失、节点找不到）仍按项目纪律 `Debug.LogError`，不许一起吞掉。
- 账号身份类持久化键（如 `Assets/Scripts/Systems/SaveSystem.cs:55-58` 的 `TapTapGuestId`）清档时照旧保留，不因开关删除。

### 2.8 合规闸门不受开关影响

- 任何新增 SDK 的初始化仍必须在「用户已同意隐私政策」之后（`Docs/接入第三方SDK情况说明.md` 第 6 行、两段式启动）。开或关都不改这条。

---

## 三、新 SDK 接入四步（照抄）

1. 登记表加一条：Id / Define / 平台组 / 默认值 / 菜单名；同步把默认宏写进 `ProjectSettings.asset` 对应平台组。
2. 建 Facade：SDK API 全关进 `#if XXX_ENABLED`，对外只暴露项目自有方法。
3. `SdkFeatures` 加一个只读开关；所有专属 UI 与触发点判它，关闭态按 2.7 收敛。
4. 追加条目到 `Docs/接入第三方SDK情况说明.md`；核对关闭态编译与运行，出包日志确认宏状态。

---

## 四、当前登记表

| Id | Define | 平台组 | 默认值 | 状态 |
|---|---|---|---|---|
| TapTap | `TAPSDK_ENABLED` | Android | 开 | 规范已定，代码待落地 |

---

## 五、【任务清单】首次落地改造（本次提交不含代码改动）

| # | 文件 / 位置 | 做法 | 验收 | 依赖 / 风险 |
|---|---|---|---|---|
| 1 | 新增 `Assets/Editor/SdkToggles.cs` | SDK 注册表（首条 = TapTap）+ `Tools/SDK/…` 勾选菜单（读写对应平台组 define，validate 打勾）+ 状态打印菜单 | 勾选 / 取消后 `ProjectSettings` 对应组宏随之增删，菜单勾选态与宏一致 | 先做，2/3/4 依赖它；Editor-only |
| 2 | 新增 `Assets/Scripts/Core/SdkFeatures.cs` | 运行时只读开关出口，每个 SDK 一个 `#if` 生成的 `const bool`，不引用 SDK 类型 | 关宏时编译零 `CS0246`；业务侧只认这个类 | 无 SDK 依赖，换机 / 微信组都安全 |
| 3 | `Assets/Editor/CliAndroidBuild.cs:14-16, 52-56, 87-97` | 删无条件 `AddDefine`；保持现有 SDK 宏不动，只处理 `SPOTLIGHT_BUILD`；构建前打印各 SDK 宏 ON/OFF | 出包日志能看到 `TAPSDK_ENABLED=ON/OFF`；开与关各出一次 APK 均成功 | 最小改动，不动其它构建参数；关宏不自动补、不报错中断 |
| 4 | `ProjectSettings/ProjectSettings.asset:1251` | Android 组填入 `TAPSDK_ENABLED`（默认开），入库跨机一致 | `git pull` 后新机器无需额外配置即与当前行为一致 | 改 YAML 前备份，只动这一行，别碰 WeixinMiniGame 组 |
| 5 | `Assets/Scripts/UI/LoginUI.cs:194-212, 216, 229, 562` | `TapLoginClosed` 常量改读 `SdkFeatures.TapTapEnabled`：关时隐藏 `TapLoginButton`、不调 `OnClickTapLogin`；`:562` 的 `Ensure()` 同样判开关 | 关宏进 Play：登录页无 Tap 按钮、Console 无 `[TapTap]` 报错；开宏时按钮与登录流程与现在完全一致 | 复用现有隐藏分支，不动预制体、不动按钮美术 |
| 6 | `Assets/Scripts/UI/TapTapRatePopup.cs:25-35` | `ConsumePendingPrompt` 并列加「SDK 关 → 清残留 `PendingPrompt` 直接返回」，与既有 `TAPTAP_RATE_ENABLED` 各管一件事 | 章末通关置位后回城不弹、无残留 | `BattleManager.Settlement.cs:2336` 置位点不改，最小增量 |
| 7 | `Assets/Scripts/Core/TapTapBootstrap.cs:153-159`（可选） | `#else` 分支降噪：开关主动关闭时静默走游客身份，不 LogError「未定义」 | 关闭态零噪音 | 按 2.7 定性，属预期行为不是兜底 |
| 8 | 追加 `Docs/接入第三方SDK情况说明.md` | 在 TapTap 条目注明「接入方式遵循 `Docs/SDK统一开关规范.md`」 | 两份文档互相指向，新增 SDK 时不会漏 | 与本规范同批或随后一次落地，勿改合规正文 |

**落地注意**：提交本规范时，工作区已有 `CliAndroidBuild.cs`、`LoginUI.cs`、`ProjectSettings.asset`、`manifest.json` 等他人 / 旧会话的未提交改动。落地时必须按点最小增量修改，**禁止整文件覆盖、禁止顺手 `git add -A`**。

---

## 六、验收（两种状态都要过）

1. **开 = 现状**：登录按钮在、点击真调 SDK、埋点正常上报、章末好评链路按 `TAPTAP_RATE_ENABLED` 原样执行。
2. **关 = 取消菜单勾选**：编辑器零编译错误；Play 模式登录页无 Tap 入口、无 `[TapTap]` 报错；章末不弹好评、无残留标记。
3. 两种状态各跑一次 `Tools/PixelAdventureTown/一键测试`（入口 `Assets/Editor/OneClickTest.cs:17`），全绿。
4. 开与关各出一次 APK，出包日志的 ON/OFF 与包内实际行为对得上。
5. 换机 `git pull` 后默认仍是开，无需额外配置。

---

## 七、红线 / 不动清单

- **不动**：`Packages/manifest.json` 及 `packages-lock.json`（包常驻、改动不入库）、`GameAnalytics.cs` 现有宏结构（已合规）、`SaveSystem.cs:55-58` 的 `TapTapGuestId` 键、`ADS_ENABLED` 与 `TAPTAP_RATE_ENABLED` 两个既有常量（各管广告与好评，禁止合并进 SDK 开关）。
- **禁止**：为了「让 SDK 关掉也能跑」而写运行时兜底 / 复制资源补路径；禁止把开关状态散落到多个文件；禁止把 56 字符 guid 修补、美术排版、无关重构夹带进落地提交。
- **纪律**：落地代码只改清单内点位，不顺手优化；提交前跑 `Tools/EnsureMeta.py`，不跑 `CheckDanglingRefs.py`；提交 + 推送只走 `git push origin main`。

---

## 八、待确认项（本次提交时未拍板，落地前须主人确认）

| # | 问题 | 未确认前的处置 |
|---|---|---|
| 1 | 开关真值放 `ProjectSettings` define（推荐，入库跨机一致）还是 EditorPrefs 菜单（本机即改即生效、不入库） | 按本规范 2.1 的 define 口径写文档；**落地前需一句话确认** |
| 2 | 默认值保持「开」（推荐，行为不变）还是改成关 | 登记表暂记「开」 |
| 3 | 关掉后登录按钮「直接隐藏」（推荐，与现有 `TapLoginClosed` 分支一致）还是「置灰提示暂未开放」 | 清单第 5 条暂按隐藏写 |
| 4 | `Assets/Scripts/UI/LoginUI.cs:15-18` 的隐私协议页目前指向 TapTap 渠道页，是否随开关切换 | **不动**，保持现状，待主人明确 |

> 以上 4 项未经主人点头，不得据此改动线上行为；规范正文与任务清单按推荐口径先入库，供落地时按确认结果微调。
