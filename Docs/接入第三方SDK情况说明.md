# 接入第三方 SDK 情况说明

> 用途：随《隐私政策》一并提交渠道审核 / 上架备案。
> **唯一权威政策全文**：TapTap 隐私政策页 https://www.taptap.cn/app/959893/common-privacy
> （运营者：谷成亮／苹果核工坊，联系邮箱 635016409@qq.com）。政策正文只在该页维护一份。
> 凡后续新增 SDK（如微信、友盟、广告 SDK 等），**必须**：① 在「用户同意隐私政策之后」才初始化；② 在本表追加对应条目。

---

## 一、Unity3d（团结）引擎 SDK

| 项目 | 说明 |
|---|---|
| SDK 名称 | Unity3d（团结引擎 / Tuanjie Engine）运行时 |
| 开发者 | Unity Technologies / 团结引擎 |
| 收集个人信息类型 | 设备标识（**Android ID**）、设备型号、操作系统版本、屏幕分辨率、CPU/GPU 信息、语言、运行区域（由 IP 推导）等 |
| 使用目的 / 场景 | 游戏渲染与运行、设备兼容性适配、统计与反作弊、设备唯一识别（`SystemInfo.deviceUniqueIdentifier`） |
| 涉及的权限 | 无额外敏感权限（网络权限由游戏功能按需申请） |
| 收集方式 | **引擎在启动（UnityPlayer 初始化 / 渲染循环）时自动读取**，非业务代码主动调用 |
| 信息处理方式 | 本地使用 / 依引擎服务配置上报（本项目已关闭 Unity Cloud Services：`cloudEnabled=0`、`enableCrashReportAPI=0`） |
| 读取时机 | 引擎启动阶段（已通过「两段式启动」将用户同意前置，确保**同意前不启动引擎**） |
| 隐私政策链接（含用户协议） | https://www.taptap.cn/app/959893/common-privacy （TapTap 页= 权威全文；游戏内登录页按钮亦跳此址，见 `Assets/Scripts/UI/LoginUI.cs:13-18`） |

### 合规处置
- 已采用「原生隐私弹窗前置」方案：用户同意前不创建 UnityPlayer，从根本上消除「未经同意读取 AndroidID」的违规（对应隐私检测条目 164014）。
- 原生首启弹窗展示的是**同文摘要**（渠道机器检测只认弹窗内文字，不认链接），完整正文以 TapTap 页为准。
- 相关代码：`Assets/Plugins/Android/PrivacySplashActivity.java`（弹窗文本见 `PRIVACY_TEXT`）、`Assets/Plugins/Android/AndroidManifest.xml`。

---

## 二、TapSDK（TapTap 登录 + 数据分析）—— 已接入（2026-10-13）

| 项目 | 说明 |
|---|---|
| SDK 名称 | TapSDK（TapTap 登录 / 数据分析） |
| 提供方 | 易玩（上海）网络科技有限公司 |
| 依赖版本 | `com.taptap.sdk.core` + `com.taptap.sdk.login` 4.10.9（UPM） |
| 收集个人信息类型 | 系统版本、设备型号、设备 CPU 信息、网络类型、Android ID、设备内存信息 |
| 使用目的 | TapTap 账号登录、游戏运行数据统计（关卡漏斗 / 留存 / 时长），用于玩法调优 |
| 涉及的权限 | `INTERNET`、`ACCESS_NETWORK_STATE`（SDK 自动加入） |
| **读取时机** | **必须在用户同意隐私政策之后**。已通过「两段式启动 + Unity 侧初始化」双重保证：`PrivacySplashActivity` 同意后才创建 UnityPlayer，`TapTapBootstrap.Init()` 挂在 Unity 侧 |
| 关闭项 | 不采集 IDFA（`enableAdvertiserIDCollection=false`）、不反射 OAID（`disableReflectionOAID=true`）、不自动上报 IAP（`enableAutoIAPEvent=false`） |
| 上报内容 | 仅玩法数值：章节 / 关卡 ID、胜负、金币、天赋石、装备数、材料、抽奖、引导步数。**不含**密码、支付明细、精确位置、相册、通讯录 |
| 隐私政策链接 | https://developer.taptap.cn/docs/sdk/start/agreement/ |
| 相关代码 | `Assets/Scripts/Core/TapTapBootstrap.cs`、`Assets/Scripts/Core/GameAnalytics.cs`、`Assets/Plugins/Android/PrivacySplashActivity.java`（PRIVACY_TEXT 摘要已同步） |

### 说明
- TapTap 数据分析能力由 `TapSDK.Core` 内的 `TapTapEvent` 提供，**无需独立的 analytics 模块**。
- 游客模式不调用 TapSDK 登录，仅以本地 UUID 作为上报用户标识（`TapTapEvent.SetUserID`），保证不授权也可产出调优数据。
- 事件属性必须先在 TapTap 后台「配置 → 事件属性管理」预登记，否则属性可能被丢弃。

---

## 三、说明
- 除上述条目外，本项目Android 包当前**未接入**任何第三方分析、广告、崩溃、支付 SDK。
- 任何新增 SDK 的自启动 / 自读取行为，都必须在「用户同意」这道闸门之后，否则将新增同类违规条目。
