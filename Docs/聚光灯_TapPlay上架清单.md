# 聚光灯 × TapPlay 上架清单

面向 **2026 聚光灯 21 天游戏创作挑战**。工程侧开关与构建见文末；报名 / DC / 投稿需在 TapTap 后台人工完成。

官方：[聚光灯活动页](https://www.taptap.cn/poster/NIKiDS1YShdA) · [TapPlay 上架](https://developer.taptap.cn/docs/sdk/tap-play/input/) · [开发者中心](https://developer.taptap.cn/)

包名（勿改）：`com.PixelAdventure.RiftBlade`  
产品名：像素冒险：裂隙之刃  
参赛包：`Builds/Android/PixelAdventure-Spotlight-android.apk`（菜单打）

---

## A. 报名与后台（至 9/30）

- [ ] 队长打开活动页完成 **报名 / 组队**（可单人）
- [ ] 登录 [开发者中心](https://developer.taptap.cn/) 创建游戏页（名称可暂定，开题后再改介绍）
- [ ] 确认厂商主体；Jam 试玩期按 DC 当前聚光灯指引选上架形态（须能过审、可试玩）
- [ ] 开发者日志节奏：开发期累计 **≥5 篇**真实日志（过程 / 设计 / 实现）
  - 可先发聚光灯论坛，标签 `#聚光灯gamejam开发者日志`，有游戏页后再迁回
  - 建议：报名后 #1；开题后 #2；中期 #3–4；投稿前 #5

---

## B. 工程合规（仓库已落地）

编译宏：`SPOTLIGHT_BUILD`（Spotlight 参赛包自动带上）

生效行为：

| 项 | 行为 |
|---|---|
| 云存档 | 禁止微信云；`UploadToCloud` / `DownloadFromCloud` 直接失败并提示 |
| 激励广告 | 不播广告；顶栏金币+/体力+ 隐藏；战前刷新改为 **免费刷新一次** |
| 登录 | 保持仅「开始游戏」；强制隐藏微信/QQ/游客/用户中心 |
| 联网 | `ForceInternetPermission=0`；不接 TDS / 微信 SDK |
| Android | IL2CPP + **仅 ARM64**；Target SDK **34** |

菜单：

- `Tools/Build/Spotlight Android APK` → 打参赛 APK
- `Tools/Build/Spotlight Windows64` → 可选双端包
- `Tools/Build/聚光灯 TapPlay 清单` → 弹窗摘要

运行时类：`SpotlightBuild`（`Assets/Scripts/Platform/SpotlightBuild.cs`）

---

## C. TapPlay 上架（建议 ≤10/18）

- [ ] DC → 本游戏 → 上传 **Spotlight Android APK**（包名不变）
- [ ] 商店资料：图标、截图、简介、年龄分级、隐私政策（写明：**无联网 / 本地存档**）
- [ ] 开启 **TapPlay**（DC「商店 - TapPlay」）
- [ ] 创建 **内部测试计划**，按[自测用例](https://developer.taptap.cn/docs/sdk/tap-play/input/)跑主流程
- [ ] 清掉 P1：闪退 / 黑屏 / 主流程卡死 / ANR
- [ ] 提交并等待 **TapPlay 稳定性检测**通过；失败按报告改包重传
- [ ] （可选）开启 TapPlay 存档备份；仍以本地存档可玩为验收底线

禁止：游戏内 WebView 热更、微信/QQ SDK、真广告/真云、未报备的加固方案。

### 技术验收

- TapTap 启动 → Boot Logo → 健康忠告 → 登录 → 城镇 → 至少一局可完赛/撤离
- 断网可玩；无微信/QQ 登录入口
- APK 为 ARM64；TapPlay 检测通过

---

## D. 平台活动投稿与双端（≤10/21 12:00）

- [ ] **开发者中心 → 平台活动** 绑定本作品并 **完成投稿**
- [ ] 日志 ≥5；试玩期冲有效试玩 ≥50（全程参与奖）
- [ ] （建议）同页再传 Windows64 包，勾选 Android + PC 冲「最佳双端奖」
- [ ] 第三方/AI 素材按规则附说明文档；AI 美术占比 >50% 放弃「最佳美术」

建议 **10/18 前**上传，留审核与改包时间。硬截止：**10/21 12:00**。

---

## 时间线速查

| 窗口 | 动作 |
|---|---|
| 即刻–9/30 | 报名；DC 建页；打 Spotlight APK 冒烟；日志 #1–2 |
| 10/1 开题后 48h | 定主题向改动范围 |
| 10/1–10/15 | 内容 + 真机；正式包；上传 + TapPlay 检测 |
| ≤10/18 | 过审；平台活动投稿；日志凑满 5 |
| 10/21 12:00 | 投稿硬截止 |
| 10/25–12/3 | 试玩运营 |

---

## 明确不做

- 不接 TapTap 登录 SDK / TDS 云（活动禁联网）
- 不接支付、防沉迷 SDK（TapPlay 已带）
- 不用微信小游戏包参赛
- 不把未贴题的商业全量包原样当 Jam 终包（开题后需可见主题向改动）
