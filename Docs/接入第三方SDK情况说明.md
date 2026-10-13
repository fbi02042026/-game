# 接入第三方 SDK 情况说明

> 用途：随《隐私政策》一并提交渠道审核 / 上架备案。本游戏当前仅接入下方 1 个 SDK（引擎内置）。
> 凡后续新增 SDK（如 TapSDK、微信、友盟、广告 SDK 等），**必须**：① 在「用户同意隐私政策之后」才初始化；② 在本表追加对应条目。

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
| 隐私政策链接（含用户协议） | https://docs.qq.com/aio/DQWxJRWRoYkJ5SWZN |

### 合规处置
- 已采用「原生隐私弹窗前置」方案：用户同意前不创建 UnityPlayer，从根本上消除「未经同意读取 AndroidID」的违规（对应隐私检测条目 164014）。
- 相关代码：`Assets/Plugins/Android/PrivacySplashActivity.java`、`Assets/Plugins/Android/AndroidManifest.xml`。

---

## 二、（预留）TapSDK / TapTap —— 当前未接入

> 若后续接入，按以下模板补充，并**务必在隐私同意之后初始化**：

| 项目 | 说明 |
|---|---|
| SDK 名称 | TapSDK（TapTap 登录 / 社区 / 支付 / 防沉迷） |
| 收集个人信息类型 | Android ID、设备型号、应用列表、网络信息、（可能）定位等 |
| 使用目的 | 渠道登录、社区、支付、合规防沉迷 |
| 读取时机 | **必须在用户同意隐私政策之后**初始化（在 `PrivacySplashActivity` 同意回调之后，进入 Unity 再初始化） |
| 备注 | 接入后本表与《隐私政策》均需同步更新 |

---

## 三、说明
- 除上述条目外，本项目 Android 包当前**未接入**任何第三方分析、广告、崩溃、支付 SDK。
- 任何新增 SDK 的自启动 / 自读取行为，都必须在「用户同意」这道闸门之后，否则将新增同类违规条目。
