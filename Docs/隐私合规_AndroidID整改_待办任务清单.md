# 隐私合规 · AndroidID 违规整改（条目 164014）待办任务清单

> 文档性质：施工记录 + 待办。**现状：已落地 T1~T4 / T6 / T7，T5 跳过，全部未提交。**
> 跨机一致：本文件随仓库走；本机无法编译，出包与编译验证需在主人的高配机上做。

---

## 一、背景与根因（已定位，未变）

- 渠道隐私检测命中 **序号 164014**：`违规收集个人信息 / Unity3d SDK 未经用户同意收集(读取AndroidID)`，结果=存在风险。
- **根因不是项目代码**：读取方是团结/Unity 引擎本体，在 `UnityPlayer` 创建后的 native 渲染循环里读 `Settings.Secure.ANDROID_ID`（用于 `SystemInfo.deviceUniqueIdentifier`），**关掉所有 Unity 服务也照读，无官方开关**。
- 既有证据（排查已于 2026-10-13 完成）：
  - 全 `Assets/Scripts` 对 `deviceUniqueIdentifier / android_id / Settings.Secure / getContentResolver / AdvertisingId / oaid` **0 命中**；
  - `Assets/` 无 `Plugins/`、无 `.aar/.jar/.androidlib`，`Packages/manifest.json` 仅 Unity 官方包；
  - `ProjectSettings.asset`：`cloudEnabled: 0`、`enableCrashReportAPI: 0`、`cloudServicesEnabled: {}`；`CliAndroidBuild.cs` `forceInternetPermission=false`；
  - 引擎模板 `TuanjieManifest.xml` 仅一个 `UnityPlayerActivity`，零权限。
- **致命现状**：隐私政策勾选放在 **Unity 内登录页**（`LoginUI.cs:11/37/45-46/350/494`），引擎早已启动 → 机器 hook 必抓。

## 二、目标

在「用户同意」之前，任何读取设备标识的动作（引擎 / 第三方 SDK）都不发生。彻底消除 164014，并通过渠道复测。

## 三、已落地改造点（文件 + 行号 + 验收）

### T1 · 开启自定义主 Manifest ✅
- 文件：`ProjectSettings/ProjectSettings.asset`
- 改动：`:311 useCustomMainManifest: 0 → 1`
- 验收：Unity 打开工程后，Player Settings → Publishing Settings 应显示「Custom Main Manifest」已勾选；构建时使用 `Assets/Plugins/Android/AndroidManifest.xml`。

### T2 · 原生隐私启动 Activity ✅
- 新建：`Assets/Plugins/Android/PrivacySplashActivity.java`
- 要点：
  - **继承 `android.app.Activity`（非 `UnityPlayerActivity`）**，确保同意前不发生 `new UnityPlayer()`；
  - `SharedPreferences` 标记 `privacy_accepted`；未同意 → 原生滚动政策全文 + 两按钮；同意 → 写标记 + 显式 `startActivity(UnityPlayerActivity)` + `finish()`；不同意 → `finishAffinity()` + 杀进程退出；
  - 「同意」按钮滚动到底前禁用（确保已读）；
  - 用 `Intent.setClassName("com.unity3d.player", ...)` 启动，避免编译期依赖引擎类。
- 验收：首启看到原生隐私弹窗；点同意后才进 Unity；点不同意直接退出、无 Unity 画面。

### T3 · 主 Manifest 节点改写 ✅
- 新建：`Assets/Plugins/Android/AndroidManifest.xml`
- 要点：
  - `PrivacySplashActivity` 承载 `MAIN + LAUNCHER` 且 `android:exported="true"`（targetSdk=34 强制要求）；
  - `UnityPlayerActivity` 移除 `MAIN/LAUNCHER`，保留 `meta-data unityplayer.UnityActivity=true`，`exported=false`；
  - `package="com.PixelAdventure.RiftBlade"`，与 `applicationIdentifier` 一致；
  - 主题用 `@android:style/Theme.Material.Light.NoActionBar`（不依赖引擎主题，规避合并风险）。
- 验收：APK 反编译确认两个 Activity 节点正确，Launcher 指向 PrivacySplashActivity。

### T4 · Unity 侧衔接 —— 按建议不改动 ✅（即「不动」本身就是落地项）
- 保留 `LoginUI.cs` 内协议勾选（双重确认不违规）。
- 红线：不要为了「原生已同意」就去改 `LoginUI`，会伤软著截图链路（`SoftCopyrightAutoCaptureRunner.cs:96` 依赖 Toast「请先阅读并同意」）。

### T6 · 出包与验收（步骤，本机无法执行）
- 本机 CLI 构建会挂（`build_android.bat` 头注释已证），**请用编辑器 GUI 出包**：
  Tuanjie → File → Build Settings → Build，输出 `Builds/Android`。
- keystore：`Keys/pixeladventure-release.jks`（alias `pixeladventure`）；`ProjectSettings.asset:323/324/332` 已配，但**密码可能要手输**（asset 里无密码字段）。
- 验收标准：
  1. 首启先见原生隐私弹窗，同意后才进 Unity（黑幕→Logo）；
  2. 不同意＝直接退出，无 Unity 画面；
  3. 二次启动不再弹（或按渠道要求改每次弹）；
  4. 渠道复测 164014 消失；
  5. 本地可选自测：frida hook `Settings$Secure.getString` 打印堆栈，同意前应**零输出**。
- 平台影响：微信小游戏 / Standalone **不受影响**（`Plugins/Android` 仅 Android 构建生效）。

### T7 · 隐私政策与 SDK 情况说明 ✅
- 新建：`Docs/隐私政策_草稿.md`（正式文本占位，待主人填公司名/联系方式/链接）
- 新建：`Docs/接入第三方SDK情况说明.md`（含 Unity3d/团结引擎 条目模板）
- 要点：报告整改建议要求「单独配备《隐私政策》+ 说明接入第三方 SDK 情况」，二者缺一不可；当前项目要披露的就 Unity3d 引擎一条。

## 四、已跳过项（含理由）

### T5 · 删 `Packages/manifest.json:26 com.unity.modules.unityanalytics` —— 跳过
- 理由：① 对消除 164014 **零作用**（引擎 native 层读 android_id 与该模块无关，已 grep 确认项目零引用 `UnityEngine.Analytics`）；② 删除包依赖后需 Unity 重新 resolve，本机无法做，有破坏包一致性风险；③ 属「顺手优化」范畴，违反最小增量纪律。
- 若主人坚持，可单独评估，不在本次范围。

## 五、待确认（主人拍板 / 填内容）

1. **隐私政策正式文本**：`PrivacySplashActivity.java` 内 `PRIVACY_TEXT` 与 `Docs/隐私政策_草稿.md` 目前是占位，需主人替换为法务/渠道认可版本（或改为 WebView 加载远程 H5）。
2. **`onOpenPrivacyPolicy` / `onOpenUserAgreement`**：`LoginUI.cs:45-46` 是 UnityEvent，C# 内无订阅者 → 预制体上是否真有政策内容页？未查 prefab，待确认（不影响 164014 整改）。
3. **keystore 密码**：编辑器重新构建时可能要求手输。
4. **二次启动是否每次弹**：✅ 已确认「仅首次」（主人 2026-10-13 拍板）。当前 `PrivacySplashActivity` 用 SharedPreferences 持久化 `privacy_accepted`，即仅首次弹，无需改代码。

## 六、施工红线（沿用 AGENTS.md）

- 不改无关代码；不删掉重建；新资源/代码/.meta 入库；入库后跑 `Tools/ScanGuid56.py`（新文件无引用，应安全）。
- 本机无法编译 → 改动只做静态核对，编译/出包验证留待主人编辑器侧。
- 未提交：按纪律，改完停在未提交状态，等主人验证后说「提交」再 commit + push。
