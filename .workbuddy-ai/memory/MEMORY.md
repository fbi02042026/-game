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
