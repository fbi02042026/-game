# 项目长期记忆（本地 · 跨会话）

## UI 适配总规矩（2026-10-31 主人拍板「以后全部是摄像机模式 自动做适配」）
- 所有界面/弹窗 Canvas 一律 Screen Space - Camera 模式，适配自动做：
  新界面实例化后必调 `UICanvasSetup.ApplyPopup(canvas, sortOrder)`（全屏页走 `Apply`）；
  DDOL 弹窗每次 Show/Open 先 `RefreshPopup`（重绑相机+重算适配，幂等）。
- 禁止 Overlay / 预制体里写死 Match 参数当适配；刘海避让走 SafeAreaFitter（禁止裸 AddComponent）；
  瘦屏重锚定/垫黑底由 UiLayoutStretch 管。完整铁律见 AGENTS.md §4.6（跨机权威）。
- 教训：广告送佣兵面板（AdMercEventUI）当初 Overlay+Match Height 没接统一出口，瘦屏被左右裁。

## 仓库与远程（2026-11-02 主人拍板：以后都按微信端来）
- **主仓库＝微信小游戏端** `git@github.com:fbi02042026/xiangsumaoxianweixin.git`（远程名 `weixin`），
  `main` 已 `--set-upstream-to=weixin/main`，日常 pull/push 默认走它。
- `gitee`（原主）、`origin`（GitHub 安卓版）**保留作历史备份**，不再作为日常跟踪。
- 项目已转向微信小游戏：无内购、无后端、TapTap 端放弃（见 `Docs/微信小游戏移植方案_2026-11-01.md`）。

## 会话工作习惯
- 主人短指令常指「继续/重试」＝上次会话中断的活接着收尾：先查代码现状，改完的要核对落盘，文档/记忆补齐。
- 每日工作日志追加到 `.workbuddy-ai/memory/YYYY-MM-DD.md`（append-only）。

## 🔴 家里机「编译/shader 类灵异问题」第一反应查火绒（2026-11-02 血的教训）
- 家里机装火绒（`C:\Program Files (x86)\Huorong\Sysdiag`，`HipsDaemon`），HIPS 会掐断
  `TuanjieShaderCompiler.exe` → 编译器 `Not connected` 秒退 → 所有 shader 从未真正编译
  → 表现为 `Kernel 'GenerateLuminanceSRI_8' not found`、导入只要 0.0038s、编辑器卡「Hold on」。
- **标准处置顺序（别再绕）**：① 火绒关防护/加信任区 `E:\Program Files\Tuanjie\Hub\Editor`
  → ② 删整个 `Library` → ③ 重开编辑器全量重导。删 ShaderCache/artifacts 都没用，因为编译器根本没跑。
- **判断实锤**：看 `Logs/shadercompiler-TuanjieShaderCompiler.exe0.log` 有没有
  `Protocol error - failed to read magic number (Not connected)`；有就是被杀软拦。
- 公司机无火绒所以一直正常；删项目重下也救不了（不是项目文件问题）。

## 动效归属（2026-11-12 主人拍板）
- 主人自己做的 Animator 动画优先：集结落点「旗子落下 + 圈缩放」一律播 `jijie` 状态，
  `BattleRally.cs` 只做定位/激活/播/隐藏，**禁止再写逐帧改位置、缩放、ease 曲线的代码动画**。
- 找不到 Animator / controller / 状态只报错，不补组件、不按路径加载 controller 兜底（不造资源）。
- 关卡图 `BattleStageMapUI.CoFlagSlamAnim` 那处代码写的「旗子砸地」不用管：
  【2026-10-21 主人拍板】石墩关卡图 + 老虎机 + 恢复关三件套已**停用不删除**，旧流程 dormant
  （口径写在 `BattleManager.Settlement.cs:2851-2853`，关间现走 `GoChapterGate` 直接 `SelectStage` 进下一关）。
  代码原样保留，别拿「老虎机」当现行流程来问要不要改。

## 🔴 战斗开局口径（2026-11-12 主人拍板，覆盖 10-22 / 11-11 全部续局设计）
- **不要「继续上一局」这个功能；中途退出一律按撤离失败静默结算；每次进战斗一律全新开局。**
  10-22 的中断局弹窗（RunResumeChoiceUI / AdventureUI.TryPromptInterruptedRun）与 11-11
  「切后台不算放弃、让位续局弹窗」整段作废；类/方法保留不删便于回退，但**不得再启用**。
- 唯一出口：`BattleManager.StartNewRun()` 开战前统一 SettleInterruptedRun → EndMemoryMode →
  RunLoadout.Clear → ClearInterruptedSave → ClearAllMercs；`RunDraftDirector.OnRunStart()`
  非裂隙一律 `RunLoadout.BeginNew(job)`（resumed=false 才会重置启动金/免费抽/抽数/保底）。
  `BattleStateSaver.SettleInterruptedRun()` 不再让位弹窗，入账前先清 KEY 故幂等。
- **无限裂隙「每 10 层记录点」是独立玩法，不是续局**：EndlessRiftCheckpoint.RestoreToPrefs
  在 StartNewRunInternal 里晚于清档重新写盘，仍靠 ResumeOrBegin 读回，别误删这个例外。
- 装备不用额外清：`StartNewRunInternal` → `hero.InitNewRun()` → `BattleBagSystem.InitNewRun()`
  已清穿戴/背包；**别在 StartNewRun 调 `ClearRunEquipment()`**（会 MoveItemsToTownBag，
  把局内道具搬进城镇包，与「中途退出按失败」冲突）。
- 「按失败」沿用原撤离经济（本局净赚 delta 仍入账、体力不退、不弹面板）。
  若主人要更狠「一分钱不给」需另改 SettleInterruptedRun 的 delta 段。

## 战前选择 UI 口径（2026-11-13 主人拍板）
- Boss 关战前剧情之后的三选一：**全屏背景底统一 50% 半透黑**，唯一真源
  `BattleAmbienceBanner.PersistentDimAlpha`；对话窗自带的 `DialogueUI` prefab `Dim`(0.7) 在这个界面
  **必须关掉**（`StoryDirector.Play(hideFullDim: true)` → `ui.HideFullDim()`）。
  原因：DialogueUI 是 DDOL 单例，Dim 初始开着、Hide 后没人再打开 → 首次叠两层、第二回只剩一层，
  表现就是主人报的「第二回进剧情底又变透明了一些」。别改成"每次重新打开 Dim"，会更乱。
- **三个选项背后不要黑色半透底**：`DialogueUI.Portraits.cs` 的 ChoiceDim 不再新建
  （`EnsureChoiceDim` 已改 `HideChoiceDim`，只关不建）。
- 【11-13 上午补充·所有剧情背景底兜底】凡走 `StoryDirector.PlayRoutine` 的剧情：开播先
  `ui.ShowFullDim()` 拉起自带 Dim（0.7）兜底；有背景图时 `SetSceneBackground(非null)` 会自动关掉它，
  没背景就留黑底，**不许剧情窗裸浮在战斗画面上**。战前选择仍传 hideFullDim=true 只用 50% 常驻底。
  显隐唯一出口 = `DialogueUI.ShowFullDim()/HideFullDim()`（Hide() 关、下一场 Play 开头开）。
- 【11-13 上午补充·toast 铺垫】Loading 收掉 ≠ 立刻弹 toast：`GlobalToastUI.FlushHeldToast`
  改为延迟冲刷——1.2s 基础缓冲 → 等主线章节卡（ChapterSplashOverlay）演完 → 再缓 0.8s。
  层级上章节卡(12000)本来就盖住 Toast(11000)，别把 Toast order 调高去「看得见」。
