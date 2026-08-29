# UI 布局命名约定（全项目 · 现有 + 今后新建预制体均适用）

> **适用范围**：`Assets/Resources/Prefabs/` 下**所有** UI 预制体（Login、Town、Battle、Popup、Loading…），以及今后新增的任意 UI prefab。  
> 720×1280 + `UICanvasSetup` / `CanvasScaler` 只做**整页等比缩放**，不等于把美术底图或控件零件强行拉大。

---

## 一、节点命名与代码策略

| 节点名 | 用途 | 代码可否改 rect |
|--------|------|------------------|
| **`BgStretch`** | 全屏铺满底图（登录等） | ✅ `UiLayoutStretch.ApplyBgStretch`（等比 EnvelopeParent） |
| **`Bg`** | Loading 等同上 | ✅ 同上 |
| **`Dim` / `ModalDim`** | 弹窗半透明遮罩 | ✅ `ApplyFillScreen`（纯色，无需等比） |
| **`Background` / `BgArt`** | 手调尺寸场景美术（GuildHall、Battle） | ❌ **Fixed** + `UiPrefabRectGuard` |
| **`map` / `MapRoot`** | 战斗条带 | ✅ 仅横向 `ApplyStretchHorizontal` |
| **Toggle/Slider 内 `Background`、`Checkmark`、`Handle`** | Unity 标准控件零件 | ❌ **永远 Fixed**，禁止 Stretch |
| **按钮 Icon、Logo、立绘、装备格、波次图** | 手调美术 | ❌ 只换 Sprite/显隐，不改 rect |
| **Resources UI 切图（`wave_next_incoming` 等）** | 原图比例 | ✅ `SetNativeSize` 终态 100%；入场 **`UiBannerPopAnim`**：300%→100% / 0.3s / 停1s / 淡出 |
| **`TopBar` / `BottomNav`** | 顶栏/底栏 | 以预制体锚点为准，代码不批量 Stretch |

### 关键歧义

同名 `Background` 有三种含义，**禁止**根级 `Find("Background")` 后统一 Stretch：

| 位置 | 策略 |
|------|------|
| Login 根 `BgStretch`（原 Background） | 等比全屏 |
| GuildHall / Battle `Background` | Fixed 799×1420 / 720×1280 |
| `LegalBar/AgreeToggle/Background` | Toggle 框图，**36×36 内拉伸父级**，代码不改尺寸 |

---

## 二、各界面（现有预制体）

| 预制体 | 底图/条带节点 | 策略 |
|--------|---------------|------|
| LoginUI | `BgStretch` | 等比 FillScreen |
| LoginUI | AgreeToggle/Background | Fixed，Guard |
| LoadingUI | `Bg` | 等比 FillScreen |
| GuildHallUI | `Background` | Fixed + Guard |
| BattleUI | `Background` | Fixed + Guard |
| BattleUI | `map` | 仅横向拉满 |
| 各 Popup | `Dim` | FillScreen；面板本体不动 |
| DialogueUI | `SceneBackground` | 单独 AspectRatioFitter，不并入 Background 规则 |
| AdventureUI | `MapRoot` / `MapBg` | 区域自适应，非整页 Background |

---

## 三、今后新建 UI 预制体 Checklist

1. **全屏底图** → 命名 `BgStretch` 或 `Bg`，不要用 `Background`
2. **场景美术底图** → `Background`，在编辑器定好 sizeDelta，代码只换 Sprite
3. **弹窗** → 遮罩命名 `Dim`；内容面板尺寸手调，运行时不动
4. **Toggle/Slider** → 子节点保持 Unity 默认名（Background、Checkmark…），**代码不得改 rect、不得复制 Sprite 到父节点放大热区**
5. **禁止** `Find("Background")` + 全屏 Stretch
6. **禁止**未经用户同意对任意预制体节点 `SetNativeSize`、改 anchor/sizeDelta 做「看得更大」

---

## 四、工具类

- [UiLayoutStretch.cs](UiLayoutStretch.cs) — 唯一入口：`ApplyBgStretch` / `ApplyFillScreen` / `ApplyStretchHorizontal`；`IsWidgetPart` 跳过 Toggle 等控件
- [UiPrefabRectGuard.cs](UiPrefabRectGuard.cs) — 仅挂 Fixed 节点（GuildHall/Battle Background、Toggle 框图等）
- [UiBannerPopAnim.cs](UiBannerPopAnim.cs) — Resources Banner 标准动效：300%→100%（0.3s）→ 停 1s → 淡出；今后同类切图复用

Cursor 规则：`.cursor/rules/prefab-scale-no-touch.mdc`
