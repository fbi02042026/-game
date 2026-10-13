# TapTap 登录 + 数据分析接入 · 实施清单（v2）

> v3 · 2026-10-13 16:26 主人拍板：Q1 位置=可以 / Q2变体=默认圆角矩形 / Q3 底色=纯净白 / Q4 凭据=放代码。
> **本机已落地的部分见第八节「已落地」**；本机无团结编辑器，编译与真机留给家里那台。
> 状态：**代码与配置已在本机落盘，等家里开引擎验证**。

---

## 〇、一个关键发现（决定方案瘦身一半）

**v4 SDK 的数据分析能力「装core 就附送」**：`TapTapEvent`（事件上报 / 账号 ID / 通用属性 / 设备属性）**就定义在 `TapSDK.Core` 命名空间里**，官网《数据分析·客户端接入》明确写「请参考集成指南完成**基础依赖**配置」——**不需要单独的 analytics 模块**。官方 Unity 下载页 4.10.9 的模块清单里也确实**没有「数据分析」这一项**。

> 结论：主人要的「拿到游戏数据」**不需要多装任何包**，装 core 就有。

于是依赖就四个：`core + login + newtonsoft-json + EDM4U`。

---

## 一、现状（本机已核实）

| 项 | 事实 | 来源 |
|---|---|---|
| 包名 | `com.PixelAdventure.RiftBlade` | `Assets/Plugins/Android/AndroidManifest.xml` |
| 签名 | `Keys/pixeladventure-release.jks`（`{inproject}`） | `ProjectSettings.asset:323` |
| minSdk / targetSdk | 22 / 34（TapSDK 要求 ≥21、≤36 ✅） | `ProjectSettings.asset:222-223` |
| 架构 | ARM64 only | `ProjectSettings.asset:319` |
| UPM | `manifest.json` 全官方模块、**零第三方包、零 scopedRegistries** | 实测 |
| 登录页可用位 | `ActionPanel/GuestButton`（当前被 `ApplyPresentation()` 强制隐藏，`LoginUI.cs:111/119`）、`ActionPanel/StartButton`、`ActionPanel/SocialRow`（隐藏） | `LoginUI.cs:108-139`、`.prefab:1090/1132/1349/1427` |
| 结算快照 | `BattleManager.FillSettlementSnapshot()` 已备齐 `Chapter / IsDeath / IsVictory / GoldGained / TalentGained / EquipCount / EnchantStoneDelta / DecomposeMatDelta / StageTitle / MVP` | `BattleManager.cs:3871-3896` |
| 隐私前置 | 两段式启动已落地 | `PrivacySplashActivity.java` |
| 按钮素材 | 已导入 `Assets/Art/UI/Icons/TapTapLogin/`（4 张 @2x，**尚未生成 .meta**） | 本轮新增 |

---

## 二、方案总览（主人要「最好的处理方法」，这是定案）

### 2.1 四件套依赖（UPM 远程，不要本地 unitypackage）

```json
"com.taptap.sdk.core":  "https://github.com/taptap/tapsdk-unity-dist.git?path=/Core#4.10.9",
"com.taptap.sdk.login": "https://github.com/taptap/tapsdk-unity-dist.git?path=/Login#4.10.9",
"com.unity.nuget.newtonsoft-json": "3.2.1",
"com.google.external-dependency-manager": "1.2.179"
```
+ `scopedRegistries` 两段：NPMJS（`com.taptap`）、openupm（`com.google`）。

**为什么不用本地 unitypackage**：会在 `Assets/` 倒几百文件 + 全部 `.meta`，跨机极易出现「A 机有 Assets/TapTap、B 机又按 manifest 装一遍」的重复文件。走 UPM 则 git 里只有两个 JSON 文本，冲突面最小。

### 2.2 分层架构（三个独立，互不牵连）

```
TapTapBootstrap（唯一入口，异步初始化 + 回调）
   ├── 初始化 TapSDK（core+login）
   ├── 埋点上报 TapTapEvent（★ 主角）
   └── 登录态（TapTap 账号 / 游客）
```
- `Assets/Scripts/Sdk/TapTapBootstrap.cs` 单例，负责初始化、串起登录与埋点。
- `Assets/Scripts/Sdk/GameAnalytics.cs` 薄封装，**只暴露语义化方法**（`ReportRunEnd(...)`、`ReportStageEnter(...)`…），内部拼 `TapTapEvent.LogEvent`。业务代码只调这个，**不直接碰 TapSDK 类型** → 将来 SDK 升版/换掉只改一个文件。
- 🔴 **编译守卫**：整体包在 `#if TAPSDK_ENABLED` 里。没装 SDK 的机器（微信端/本机静态核对）也能编译过，不留窟窿。

### 2.3 登录策略：**TapTap 账号为可选，不强制**（单机游戏最优解）

- 登录页两个入口：**TapTap 登录**（官方按钮）+ **游客继续**。
- **游客不用 TapSDK 匿名登录**，用本地生成的 `guestId`（`PlayerPrefs` 存一次 UUID）→ 直接喂 `TapTapEvent.SetUserID()`。
  这样**不登录也能上报数据**（数据分析以设备为主账号），玩家不被一次授权拦住，转化率最高。
- 已登录玩家 → 登录成功立刻 `TapTapEvent.SetUserID(openId)`，此后所有事件自动带账号 ID → 支持「跨设备看同一个人的数据」。
- 登出 → `TapTapLogin.Instance.Logout()` + `TapTapEvent.ClearUser()`。

### 2.4 按钮规范（官方硬性要求，写错会被渠道打回）

官方《使用 TapTap 登录的按钮设计规则》原文：
> 「默认高度为 50 pt，在保证宽高比不变的前提下，可以根据游戏屏幕分配率规则调整按钮的尺寸。」
> 「**该按钮不可更改样式，也不可在标准样式上做任何更改**，如添加描边、渐变、投影、纹理等，均不可调整。」

落地口径：

| 项 | 值 |
|---|---|
| 素材 | 已导入的 `@2x`（540×100），宽高比 **5.4 : 1** |
| Image `sizeDelta` | **270 × 50**（= 官方 @1x 基准，正好适配 720×1280 画布），宽高比不变 ✅ |
| Sprite 导入 | `Pixels Per Unit = 100`（默认）、`Sprite (2D and UI)`、Compression = None（按钮文字要清晰）、Wrap = Clamp |
| 变体 | 在「最大圆角半径 / 默认圆角矩形」里二选一，**禁止纯直角** |
| 禁止 | 加描边/阴影/渐变、遮罩裁切、非等比拉伸 |

**位置建议（需主人确认 Q1）**：复用现有 `ActionPanel/GuestButton` 那个节点（它现在被强制隐藏，正好是空位），改名 `TapLoginButton`，位置对齐 `StartButton` 上方。**预制体具体改法不擅自动**（项目红线：不动美术排版）。

### 2.5 🔴 隐私合规（本项目最敏感的线）

- `TapTapSDK.Init()` 调用时机 **必须晚于隐私同意**。
  现有两段式启动（`PrivacySplashActivity` 同意后才建 UnityPlayer）**天然满足** —— 只要 `Init` 挂在 Unity 侧第一个场景的初始化流程里，**绝不能放进 `PrivacySplashActivity`**。
- TapSDK 会读 AndroidID / 设备型号 / CPU / 网络类型 / 内存 → **TapTap 那份政策页 SDK 表已经写了 TapSDK，接完就名副其实**（之前担心的「声明与实际不符」自动消失）。
- `Assets/Plugins/Android/PrivacySplashActivity.java:34-68` 的 `PRIVACY_TEXT` 摘要「三、第三方 SDK」节**必须补 TapSDK 条目**（渠道核对项）。
- `Docs/接入第三方SDK情况说明.md` 里 TapSDK 从「预留未接入」改为「已接入」。
- 🔴 **绝不采集**：密码、支付信息、精确位置、相册、通讯录。埋点只报玩法数值。

---

## 三、埋点设计（★ 主人的核心诉求，商业项目级）

### 3.1 通用属性（每个事件自动携带）

| 属性 | 来源 |
|---|---|
| `#game_version` | `Application.version`（区分版本对比） |
| `#chapter` | `ChapterManager.currentChapter` |
| `#platform` | 编译期常量（Android） |

### 3.2 账号 / 设备属性（低频变化，优先用属性而不是事件）

| 属性 | 时机 | API |
|---|---|---|
| `#total_runs` | 每次开局 | `TapTapEvent.UserAdd("{\"total_runs\":1}")` |
| `#total_deaths` | 每次阵亡 | `TapTapEvent.UserAdd("{\"total_deaths\":1}")` |
| `#highest_chapter` | 章节推进 | `TapTapEvent.UserUpdate(...)` |
| `#total_login_days` | 每日首次 | `UserAdd("{\"total_login_days\":1}")` |
| `#is_payer` | 首次内购 | `UserUpdate("{\"is_payer\":true}")` |

### 3.3 自定义事件（★ 调优看这里）

事件名一律 `#` 开头（官方规定），属性名也一律 `#` 开头。

| 事件 | 触发点（**已有现成数据，直接取**） | 属性 |
|---|---|---|
| `#run_start` | `BattleManager` 开局 | `#chapter`, `#run_index`, `#gold` |
| `#run_end` | `BattleManager.FillSettlementSnapshot()` 之后（`:3871`，数据**已备齐**） | `#is_death`, `#is_victory`, `#chapter`, `#stage_id`, `#gold_gained`, `#talent_gained`, `#equip_count`, `#stone_delta`, `#decompose_delta`, `#mvp`, `#duration_sec` |
| `#stage_enter` | 进关卡 | `#chapter`, `#stage_id` |
| `#chapter_clear` | 通关 | `#chapter`, `#run_count`, `#death_count` |
| `#draft_pull` | 抽奖（`DraftPool`/`SlotMachineSystem`） | `#pool_type`(定向/随机), `#rarity`, `#gold_cost` |
| `#equip_replace` | 装备替换（`EquipReplaceConfirmUI`） | `#power_delta`, `#same_attr_count` |
| `#talent_pick` | 天赋选择 | `#talent_id`, `#talent_cost` |
| `#guide_step` | 引导 14 步 | `#step`（**调新手漏斗的关键**） |
| `#ad_click` | 未来广告位 | 预留 |

### 3.4 🔴 埋点三条铁律（商业项目，防数据污染）

1. **事件属性必须先在 TapTap 后台「配置 → 事件属性管理」预登记**，否则属性可能被丢。清单里列的每个 `#` 属性，落地时同步到后台。
2. **上报失败必须 `LogError`**，不静默（项目纪律：不兜底）。同时**绝不在上报里带玩家标识以外的个人信息**。
3. **属性值有长度/范围限制**：字符串 ≤ 256 字符，数值 ∈ [-9E15, 9E15]。`StageTitle` 是中文标题 → **上报时用关卡 ID 不用标题**，否则维度爆炸。

### 3.5 这些数据能回答什么（这才是主人的目的）

- 新手 14 步引导每步流失多少 → `#guide_step`
- 哪个章节死亡率最高、卡在哪 → `#run_end.#is_death` 按 `#chapter` 分组
- 定向抽奖值不值（花了钱没出想要的）→ `#draft_pull` 对比 `#pool_type` 与留存
- 装备替换是不是反悔 → `#equip_replace.#power_delta`
- 单局时长分布（在线时长是核心指标）→ `#run_end.#duration_sec`

---

## 四、任务清单（按顺序，每步带验收）

### T1 · 后台配置（主人网页操作）
- `游戏服务 > 应用配置` 开通 **TapTap 登录**。
- 填包名 `com.PixelAdventure.RiftBlade` + **签名 MD5**（不是 SHA1）。
- `游戏服务 > 基础服务` 开通 **数据分析**。
- `数据分析 > 配置 > 事件属性管理` **预登记**第三节全部 `#` 属性。
- 记下 Client ID / Client Token。
- **验收**：MD5 与本地 keystore 一致；属性登记完成。

### T2 · `Packages/manifest.json` 加四件套 + scopedRegistries
**验收**：JSON 合法；`git diff` 仅此一文件。

### T3 · EDM4U 解析（家里那台，有网）
- `Assets > External Dependency Manager > Android Resolver > Force Resolve`。
- 产物（`Assets/Plugins/ExternalDependencyManager/` 等）**必须入库** —— 否则本机拉取后要重新联网解析。
- **验收**：Build Output 无 `mergeDebugJavaResource` 多文件冲突。

### T4 · 新建 `Assets/Scripts/Sdk/TapTapBootstrap.cs`
- 单例；`Init()` 填 `clientId / clientToken / region=CN / screenOrientation=0(竖屏) / enableLog=Development`。
- 挂 `TapTapEventOptions{ channel="TapTap", propertiesJson="{}", overrideBuiltInParameters=false, enableAutoIAPEvent=false }`。
- `TapTapSDK.Init(coreOptions, otherOptions)` → `AddInitCallback` 取异步结果。
- 🔴 **调用点**：Unity 侧第一个场景初始化之后。**严禁**放进 `PrivacySplashActivity`。
- 编译守卫 `#if TAPSDK_ENABLED`。
- **验收**：失败走 `LogError` + 玩家可见提示；游戏仍可正常进入（登录失败不阻塞单机玩法）。

### T5 · 新建 `Assets/Scripts/Sdk/GameAnalytics.cs`
- 语义化薄封装，内部 `TapTapEvent.LogEvent` / `SetUserID` / `UserAdd` / `DeviceUpdate`。
- 宿主静态类，**不做 MonoBehaviour**（与「构造期禁读表」同款约束：不依赖场景生命周期）。
- **验收**：一个战斗流程下来 TapDB 后台能看到完整事件链。

### T6 · 登录流程接入 `LoginUI`
- 复用 `ActionPanel/GuestButton` 节点 → TapTap 登录（官方按钮贴图）。
- 新增/启用游客按钮（本地 UUID，不调 SDK）。
- `LoginUI.cs:108-139` `ApplyPresentation()` 里**不要再无条件隐藏** Tap 登录按钮。
- **验收**：Tap 登录成功拿 `openId` + `nickname` + `avatar`；游客直接进；两者都能开始游戏。

### T7 · 埋点接入（分两批，别一次铺满）
- 第一批（决定能不能用）：`#run_start` / `#run_end` / `#stage_enter` / `#guide_step`。
- 第二批：`#draft_pull` / `#equip_replace` / `#talent_pick` / `#chapter_clear` + 通用/账号属性。
- **验收**：后台漏斗图能画出 14 步引导与章节死亡率。

### T8 · 合规文本同步（必做，渠道核对项）
- `Assets/Plugins/Android/PrivacySplashActivity.java:34-68` `PRIVACY_TEXT`「三、第三方 SDK」补 TapSDK 条目。
- `Docs/接入第三方SDK情况说明.md` TapSDK 由「预留未接入」→「已接入」+ 完整字段。
- **验收**：游戏内摘要与 TapTap 政策页 SDK 表一致。

### T9 · 按钮素材入库收尾（已拷 PNG，未生成 .meta）
- `Assets/Art/UI/Icons/TapTapLogin/` 四张 PNG **等团结引擎导入生成 .meta 后提交**。
- 🔴 引擎可能把 `.meta` guid 写成 56 字符 → 入库前跑 `Tools/ScanGuid56.py`；这四张要拖进 prefab，**必须换回 hex32**。
- **验收**：`git status` 无遗漏 `.meta`；`ScanGuid56.py` 对这四张无告警。

### T10 · 真机验证 + 上传测试
- 出包（keystore 密码可能手输）。
- 验证：隐私弹窗仍最先出现 → Tap 登录通 → 游客通 → 埋点能在后台看到。
- **验收**：TapDB 后台出现 `device_login` 与 `#run_end` 数据。

---

## 五、待确认（不猜，等主人拍板）

> v3 已全部拍板：Q1 可以 / Q2 默认圆角矩形 / Q3 纯净白 / Q4 放代码。
> 剩余执行项见第七节。

| # | 问题 | 结论 |
|---|---|---|
| Q1 | 按钮位置 | ✅ 复用 `ActionPanel` 内、StartButton 下方，运行时构建 |
| Q2 | 按钮变体 | ✅ 默认圆角矩形 |
| Q3 | 按钮底色 | ✅ 纯净白 |
| Q4 | Client ID / Token 放哪 | ✅ 代码顶部常量，集中一处便于轮换 |
| Q5 | 游客 ID 生成 | ✅ `PlayerPrefs` + `System.Guid.NewGuid().ToString("N")`（业务层，不进资源链路） |
| Q6 | `#guide_step` 埋点位置 | ⏳ **待定位**：引导在 `TutorialDirector` / `FirstRunGuide`，需子模型先找真实步骤回调点，**不臆造** |

---

## 六、施工红线

1. 🚫 **不动** `PrivacySplashActivity` 的两段式启动逻辑（164014 的根治方案，动一处全盘失效）。
2. 🚫 **TapSDK 初始化严禁早于隐私同意**。
3. 🚫 **埋点不上报任何个人信息**（无密码/支付/位置/相册/通讯录）；中文标题一律转关卡 ID。
4. 🚫 事件属性**必须先在 TapTap 后台预登记**再上报。
5. 🚫 不用本地 unitypackage 接 SDK（见 2.1）。
6. 🚫 不做静默兜底：SDK 异常一律 `LogError` + 明确提示；但**登录失败不得阻塞单机游戏**（这是设计，不是兜底）。
7. 🚫 不擅自动 `LoginUI.prefab` 美术排版。
8. 改 `Packages/manifest.json` 后 `packages-lock.json` 一并入库（它**已入库**，实测）。
9. 活没干完必须更新本清单并一起提交。

---

## 七、跨机同步（家里接完，这边拉取行不行）

**能。** 核心三条：
1. 家里走 **UPM**（git 里只有 `manifest.json` + `packages-lock.json` 两个文本，均已入库）。
2. **EDM4U 解析产物入库**（T3）。
3. 这边拉后：**重启引擎** → `ScanGuid56.py` → `CheckDanglingRefs.py --strict` + `EnsureMeta.py`（**后台跑**）→ **手拷 `Keys/pixeladventure-release.jks`**（`.gitignore:144` 不入库）。

---

## 八、已落地（本机 2026-10-13 16:00~16:30）

> 本机**无团结编辑器**（`C:/Program Files/Tuanjie/` 下只有 Tuanjie AI，没有 Hub/Editor），
> 故**只能改代码与配置**，编译 / EDM4U 解析 / 真机验证必须在**家里那台**执行。

### 8.1 已改文件

| 文件 | 改动 |
|---|---|
| `Packages/manifest.json` | 加 4 个依赖 + `scopedRegistries`（NPMJS / openupm两段），JSON 已校验合法 |
| `Assets/Scripts/Core/TapTapBootstrap.cs` | **新建**。SDK 初始化 / 登录 / 登出 / 游客身份 / 主线程回调泵。`#if TAPSDK_ENABLED` 守卫 |
| `Assets/Scripts/Core/GameAnalytics.cs` | **新建**。埋点语义化封装 + 账号属性 + 铁律注释 |
| `Assets/Scripts/UI/LoginTapRowBuilder.cs` | **新建**。运行时构建 TapTap 登录按钮（官方素材 270×50）+ 游客继续按钮 |
| `Assets/Scripts/UI/LoginUI.cs` | `Awake`/`OnEnable` 增 `EnsureTapLoginRow()`；新增 `OnClickTapLogin` / `OnClickGuestContinue`；`OnClickStart` 幂等兜一次初始化。**未动预制体** |
| `Assets/Scripts/Managers/BattleManager.cs` | `FillSettlementSnapshot()` 末尾调`ReportRunEndAnalytics()`（`:3906-3933`），接`#run_end` + 累计局数/死亡数 |
| `Assets/Plugins/Android/PrivacySplashActivity.java` | `PRIVACY_TEXT`「三、第三方 SDK」补 TapSDK 条目 |
| `Docs/接入第三方SDK情况说明.md` | TapSDK 由「预留未接入」→「已接入」完整字段 |
| `Assets/Resources/UI/Icons/TapTapLogin/` | **新建目录**，4 张官方按钮 @2x PNG（**尚无 .meta**） |

### 8.2 🔴 家里那台必须做的三件事（本机做不了）

#### ① 填 Client ID / Token（步骤见第九节）

`Assets/Scripts/Core/TapTapBootstrap.cs:32-33` 目前是占位：
```csharp
const string ClientId = "REPLACE_WITH_TAPTAP_CLIENT_ID";
const string ClientToken = "REPLACE_WITH_TAPTAP_CLIENT_TOKEN";
```
**不填则 SDK 初始化直接报错并落到游客身份**（已写LogError，不会静默）。

#### ② 加编译宏

Player Settings → Scripting Define Symbols → 添加 `TAPSDK_ENABLED`。
不加则整块 SDK 代码不参与编译（游戏能跑，但数据不上报）。

#### ③ EDM4U 解析 + 出包

`Assets > External Dependency Manager > Android Resolver > Force Resolve`，
产物（`Assets/Plugins/ExternalDependencyManager/` 等）**必须入库**。

---

## 九、Client ID / Token 与签名怎么填（2026-10-13 补充）

### 9.1 完整后台路径

**TapTap 开发者中心** https://developer.taptap.cn
→ 你的游戏（像素冒险：裂隙之刃）→ **游戏服务 → 应用配置** → 点「立即开启」

开启后页面里同时给你三样东西：

| 项 | 填到哪 | 格式 |
|---|---|---|
| **Client ID** | `TapTapBootstrap.cs:32` `ClientId` | 字符串，照抄 |
| **Client Token** | `TapTapBootstrap.cs:33` `ClientToken` | 字符串，照抄 |
| **包名** | 后台「配置签名证书」处 | `com.PixelAdventure.RiftBlade` |
| **签名** | 后台「配置签名证书」处 | **32 位大写十六进制、不带冒号** |
| 适用地区 | 建应用时选「中国大陆」 | 对应代码 `TapTapRegionType.CN` |

### 9.2 签名 MD5 怎么取（三种方式）

**方式 A · 项目脚本（推荐，主人已有 keystore）**
```bash
# 项目根目录执行，交互式输入密码（密码不落盘、不进 shell 历史）
C:/Users/Administrator/.workbuddy/binaries/python/versions/3.13.12/python.exe Tools/get_signature_md5.py --keystore Keys/pixeladventure-release.jks
```
脚本会自动找 keytool（本机在 `2022.3.62t16/Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin/keytool.exe`）、
列出别名、导出证书、算出 MD5，并打印成后台要的格式。

**方式 B · 只有 APK 时（不用密码）**
```bash
C:/Users/Administrator/.workbuddy/binaries/python/versions/3.13.12/python.exe Tools/get_signature_md5.py --apk Builds/xxx.apk
```

**方式 C · Android Studio**
`./gradlew signingReport` → 输出 MD5 **带冒号**，填后台前要手动删掉冒号。

### 9.3 格式红线（填错直接签名不匹配）

官方原文举例：
- ✅ 正确：`6EB4347CF9C098BE1C8D965D539C42E2`
- ❌ 错误：`6E:B4:34:7C:F9:C0:98:BE:1C:8D:96:5D:53:9C:42:E2`（带冒号）

### 9.4 ⚠️ 三条容易踩的坑

1. **keystore 密码在 `ProjectSettings.asset` 里没有存**（只有 keystore 路径）。
   出包时可能手输；算MD5 也需要密码。**别让密码进 git**。
2. **测试服 ≠ 正式服**：Client ID 每应用唯一，分测试/正式要各建一个应用、各自开启应用配置。
3. **签名一旦绑定后台就改不了**：包名或签名换了，运行时直接 `signature not match`，
   而官方明确写了 `1001 ConfigError` **重试无法恢复、必须重新发布**。
   → 所以 `Keys/pixeladventure-release.jks`（md5 `ba3c23bc5f14f6ac0035a7c1797a91c9`，4425 B，
   alias `pixeladventure`，2026-09-27 建）**务必留好备份，且两台机器必须是同一个文件**。

### 9.5 凭据安全边界

- `Client ID` / `Client Token` 是**客户端凭据**，本就随包可见（官方全平台 SDK 都这么发），写代码可接受，已集中在一处便于轮换。
- 后台若另有「**服务端密钥 / serverSecret**」之类，**绝不能写进客户端**。本项目 v4 登录不需要自建后端，不涉及服务端密钥。
- 仓库是 `git@github.com:fbi02042026/-game.git`（私有仓库）。但仍建议：**若主人将来要把仓库转公开或加外部协作者，先把 Token 抽到本地配置文件或构建期注入。**

### 8.3 🟡 .meta 待生成（4 张按钮 PNG + 3 个新脚本）

本机无编辑器，以下文件**没有 .meta**：
- `Assets/Resources/UI/Icons/TapTapLogin/` 下 4 张 PNG
- `Assets/Scripts/Core/TapTapBootstrap.cs`
- `Assets/Scripts/Core/GameAnalytics.cs`
- `Assets/Scripts/UI/LoginTapRowBuilder.cs`
- `Assets/Resources/UI/Icons/`（新目录，若引擎未自动补）

→ **在**家里那台**开引擎统一生成后提交**。别两台都生成（会签出不同 guid）。
→ 入库前跑 `Tools/ScanGuid56.py`：脚本 meta 不拖 prefab，风险低；**PNG 会被拖进 UI**，若被写成 56 字符必须换回 hex32。

### 8.4 ❓ 待验证（本机无法验，家里必须看）

1. **按钮是否正常显示**：素材被团结导入成 `Texture2D` 而非 `Sprite` 时，`LoginTapRowBuilder`已用 `Sprite.Create`（PPU=2）兜住；若图仍不显示，看Console 有无「官方 TapTap 登录按钮素材加载失败」。
2. **按钮位置**：ActionPanel 高 360，StartButton 占顶部 102~225 → Tap 按钮放y=78、游客按钮 y=24。若实际视觉重叠需调 `LoginTapRowBuilder.cs` 的 `TapButtonY` / `GuestButtonY`。
3. **`TapLogin.LoginWithScopes` 返回值类型**：官方示例用 `await`，本代码用 `.ContinueWith` + 主线程泵。若编译报签名不符，改为 `async/await`（需在 `MainThread` 上await）。
4. **`GetCurrentTapAccount()`** 同上，用了 `.GetAwaiter().GetResult()`（**有主线程死锁风险**）。若编译/运行异常，改成异步分支。
5. **`TapTapEvent.AddCommonProperty` / `UserAdd` / `UserUpdate`** 的签名以实际装的 4.10.9 为准，官方文档写 UserAdd 收 JSON 字符串，本代码按此写。