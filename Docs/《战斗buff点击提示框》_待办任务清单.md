# 《战斗 buff 点击提示框》待办任务清单

> 2026-11-18 主人指令：点击战斗中 buff 图标，弹出像装备属性那种小框。
> 状态：**待施工**（只出清单，未落地代码）。未起团结编辑器、未提交，等「提交」指令。
>
> **主人已拍板三项**（不再问，照此施工）：
> 1. 框内**只显示名字和属性**——标题=属性名（如「攻击」），正文=属性值（如「+50%」）；**不显示剩余秒数、不显示来源**。
> 2. 弹出方向**向上**（沿用 `EquipStatTipUI.PlaceAbove` 的锚点上方定位，不改成向下）。
> 3. 自动收起**沿用 3 秒**（接受装备框的 `AutoHideSeconds = 3f` 口径）。

---

## 一、现状与调用链（已逐行核实）

### 现状：buff 图标没有点击入口
- 战斗 buff 条是 `Assets/Scripts/UI/BuffHudUI.cs` **运行时 `new GameObject` 画出来的**（整条 + 每张牌都不是预制体），挂在玩家头像「战斗力」右侧（`BuffHudUI.cs:129-153`）。
- 每张牌的三个可视组件**全部 `raycastTarget = false`**：底图 `:249`、汉字 `:257`、倒计时 `:272`。
- 全类**无任何** `Button` / `OnClick` / `IPointerClickHandler` / `EventTrigger` → 现在点 buff = 射线穿透，**点了没反应**。根因是「没有点击入口」，不是「框没做出来」。

```
BattleUI.Update（0.1s 节流，BattleUI.cs:281-286）
  → BuffHudUI.Tick（0.25s 节流，BuffHudUI.cs:159-181）
      → Refresh（:186-233）
          ① 计时 buff：Hero.attr.CollectActiveTimedBuffs(_timed)（AttrSystem.cs:379-391）
          ② 常驻 buff：BattleManager.tempBuffs（BattleManager.State.cs:61）
          → GetIcon 池化画牌（:235-291）→ 到此为止，无点击
```

### 参照：装备属性小框（同款模板）
`Assets/Scripts/UI/EquipStatTipUI.cs`（运行时建树，不落预制体、不 Resources.Load 图）：

```
EquipQuickSlotUI.WireStatTipClick（:89-106，挂全透明 Image+Button 接射线）
  → OnStatTipClick（:110-121）→ EquipStatTipUI.Toggle(eq, anchor, key)（:183-200）
      → IsOpenFor 判同 key → Show（:112-142）
          → 拼标题/属性行 → PlaceAbove 锚到图标上方 + Clamp 进屏（:268-292）
          → SetAsLastSibling 抬层级 → 3 秒 CoAutoHide（:144-158）
```

- 骨架方法：`Ensure(53-63)` / `Build(65-109)` / `Show(112-142)` / `CoAutoHide(144-158)` / `Hide(160-169)` / `IsOpenFor(172-177)` / `Toggle(183-200)` / `PlaceAbove(268-292)` / `MakeText+Stretch(296-322)`。
- 嵌套 Canvas + `overrideSorting` + `sortingOrder=1000`（`EquipStatTipUI.cs:70-77`），**不加 GraphicRaycaster**，panel/文字全 `raycastTarget=false`，不抢下层点击。
- `SkillInfoTipUI` 是照它复制的同款（类注释明写「沿用 EquipStatTipUI，别另起一套」），但已整体下线（唯一调用点移除），新框不接它，只保留防御性互斥引用。
- 项目里**没有**统一 tip 抽象基类/接口，是「复制同款写法」。本次照抄 `EquipStatTipUI` 骨架即可，不重构装备框。

### 数据口径（决定框里显示什么）
| 类别 | 数据源 | 名字取法 | 属性值取法 |
|---|---|---|---|
| 计时 buff | `AttrSystem.ActiveTimedBuff`（`AttrSystem.cs:363-372`，`CollectActiveTimedBuffs:379-391` 确认 `mul` 有赋值） | `EquipUiText.Attr(b.type)`（如「攻击」） | `mul`：1.5 = +50% → `(mul-1f)*100f` 取整拼「+N%」 |
| 常驻 buff | `BattleManager.tempBuffs` → `AttrBonusData`（`SaveData.cs:2615-2621`：`attrType/value/isPercent`） | `EquipUiText.Attr(b.attrType)` | `EquipUiText.Value(t, value, isPercent)`（`EquipUiText.cs:157-170`） |

- `EquipUiText.Attr` 签名 `:75-111`（`public static string Attr(AttrType t)`），`Value` 签名 `:157`（`public static string Value(AttrType t, float value, bool isPercent)`）——两个都是 public static，可直接调。
- 计时 buff 只有 Attack/Defense/CritRate/MagicAttack 四类有 `mul`（`AttrSystem.cs:383-390`），MaxHp/AttackSpeed/MoveSpeed 虽在 `StyleOf` 里有牌，但不在计时出口里——**不要为此改 AttrSystem**，框只显示当前牌实际绑定的数据即可。

---

## 二、任务清单

### T1 新建 `BuffStatTipUI.cs`（照抄装备框骨架，只显示名字+属性）

**文件**：`Assets/Scripts/UI/BuffStatTipUI.cs`（新建）

**做法**：
- 照抄 `EquipStatTipUI.cs` 的 `Ensure/Build/CoAutoHide/Hide/IsOpenFor/Toggle/PlaceAbove/MakeText/Stretch` 骨架与尺寸/字号常量（`PanelWidth=220 / HeadHeight=56 / LineHeight=36 / Body=26 / Title=24 / EdgePad=6`），保持同款观感。
- `Build` 里建**两行 Text**（标题 + 正文）即可，不需要装备框的多行属性列表。
- `Show` 入参改为：`Show(string title, string attrValue, RectTransform anchor, object key)`——`title`=属性名、`attrValue`=属性值，**不传剩余时间、不显示时长/来源**。
- 沿用嵌套 Canvas + `sortingOrder=1000` + 全 `raycastTarget=false` + 不加 GraphicRaycaster（`EquipStatTipUI.cs:70-83` 同款）。
- 沿用 3 秒 `CoAutoHide`（`AutoHideSeconds=3f`，unscaled）。
- **补 `OnDestroy` 重置 `Instance`**（装备框缺这个，重开战斗静态 Instance 可能指向已销毁对象；`Hide()` 开头 `if (this == null) return;` 也照抄）。
- `Toggle` 里 `parent` 为空时从 `anchor.GetComponentInParent<Canvas>()?.transform` 找，找不到 `Debug.LogError`（同款口径，不静默 return）。

**验收**：调 `BuffStatTipUI.Toggle("攻击", "+50%", 图标Rt, key)` → 图标上方弹出深灰小框，标题「攻击」、正文「+50%」，3 秒后自动消失；再点同一个 key 关闭；框不抢下层点击。

**依赖/风险**：纯新建，不动现有文件；`sortingOrder=1000` 卡在结算框(990)之上、Toast(11000)之下，与装备框同层。

---

### T2 `BuffHudUI.BuffIcon` 加数据快照字段

**文件**：`Assets/Scripts/UI/BuffHudUI.cs:27-34`（`BuffIcon` 类）

**做法**：
- 加字段存「本牌当前绑定的数据」，供点击瞬间读取：`public AttrType Attr; public bool Timed; public float Mul; public AttrBonusData Bonus;`（或等价的 `string Title/AttrValue` 现算）。
- 因图标池化复用、每 0.25s 刷新会换 buff，**点击时绝不能现算数据**，必须读快照。

**验收**：图标池里每张牌在任意刷新周期点开，显示的内容与当帧该牌图/色一致（不会张冠李戴）。

**依赖/风险**：`AttrBonusData` 是 class（`SaveData.cs:2616`），存引用即可；计时/常驻两类要分别写快照，别混。

---

### T3 `Refresh` 写快照

**文件**：`Assets/Scripts/UI/BuffHudUI.cs:186-233`（`Refresh`）

**做法**：
- 计时牌循环（`:195-209`）：拿到 `icon` 后写 `icon.Attr = b.type; icon.Timed = true; icon.Mul = b.mul;`。
- 常驻牌循环（`:217-227`）：写 `icon.Attr = b.attrType; icon.Timed = false; icon.Bonus = b;`。
- 只加赋值，**不改**现有图标显隐/倒计时/闪烁/去重逻辑。

**验收**：`CollectActiveTimedBuffs` 出的每条、`tempBuffs` 每种属性，点开框读到的数据与牌一一对应。

**依赖/风险**：常驻牌有同类去重（`_seen.Add(b.attrType)`，`:220`），快照按亮出来的那张牌写即可。

---

### T4 `GetIcon` 挂透明点击层 + Button

**文件**：`Assets/Scripts/UI/BuffHudUI.cs:235-291`（`GetIcon`）

**做法**：
- 每张牌仿 `EquipQuickSlotUI.cs:89-106` 加一层全透明 `Image`（`raycastTarget=true`，`color=new Color(0,0,0,0)`）作点击层，`transform.SetAsLastSibling()` 盖在牌上；原底图 `bg.raycastTarget` 保持 false（别和点击层抢）。
- 挂 `Button`（`transition=None`、`targetGraphic=透明图`），`onClick` 回调读本牌快照：
  - 计时 → `title=EquipUiText.Attr(icon.Attr)`、`value=((int)Mathf.Round((icon.Mul-1f)*100f))` 拼「+N%」；
  - 常驻 → `title=EquipUiText.Attr(icon.Attr)`、`value=EquipUiText.Value(icon.Attr, bonus.value, bonus.isPercent)` 前拼「+」（负值是减益，直接带负号，不硬加「+」——施工时按 `value>=0` 决定前缀）。
  - 调 `BuffStatTipUI.Toggle(title, value, icon.Root.GetComponent<RectTransform>(), icon.Root)`（key 传牌 Root，稳定）。
- 点击层随牌显隐（是 `go` 子节点，牌 `SetActive(false)` 时自然隐藏）。

**验收**：点任意 buff 牌弹出对应属性框；点同牌关闭；战前整条隐藏时点击层一起不可点。

**依赖/风险**：⚠ `BuffHudUI` 牌是运行时建的、无预制体，点击层也是运行时补——**不动 BattleUI.prefab**。原 `Mark`/`Cd` 的 `raycastTarget=false` 保持不动。

---

### T5 互斥（弹新框时收掉其他浮框，反向同理）

**文件**：新类 `BuffStatTipUI.cs` 的 `Show`；`EquipStatTipUI.cs:117-121` 的 `Show`

**做法**：
- 新框 `Show` 里 Hide 掉 `EquipStatTipUI.Instance` / `SkillInfoTipUI.Instance` / `BackpackItemActionUI.Instance`（照 `EquipStatTipUI.cs:117-121` 同款互斥三行）。
- 反向：`EquipStatTipUI.Show`（`:117-121`）补一行 `BuffStatTipUI.Instance?.Hide()`，避免两个框叠出。

**验收**：先开装备框再点 buff → 只剩 buff 框；反之只剩装备框；不与道具操作浮层同屏。

**依赖/风险**：`SkillInfoTipUI` 已下线，其 `.Instance` 判空即可，不接新调用点。

---

### T6 清理残留（开局 / 战前隐藏）

**文件**：`Assets/Scripts/Managers/BattleManager.Waves.cs:155-160`；`Assets/Scripts/UI/BuffHudUI.cs:163-175`（`Tick`）

**做法**：
- `BattleManager.Waves.cs:160` 附近（现有 `BackpackItemActionUI.Instance?.Hide(); EquipStatTipUI.Instance?.Hide();` 之后）补一行 `BuffStatTipUI.Instance?.Hide();`。
- `BuffHudUI.Tick` 的两个「整条隐藏」分支（`:163-167` 上一局残留、`:171-175` 战前等待开战）里，各补 `BuffStatTipUI.Instance?.Hide();`，避免 buff 条藏了框还挂着。

**验收**：重开战斗 / 进关等「开始战斗」阶段 → 不出现孤立 buff 框；切场景无残留。

**依赖/风险**：`BuffStatTipUI.Hide()` 开头要有 `if (this == null) return;`（T1），这里用 `?.` 只判托管 null，靠 Hide 内挡销毁态。

---

## 三、待确认项

已全部拍板，无待确认。三项决策见文首。

*(施工期若发现新分歧，报位置 + 行号 + 原文 + 判断，不私自改。)*

---

## 四、施工红线

1. **只改上文点名的文件 + 行号，禁顺手重构**；不动 `EquipStatTipUI` 除 T5 那一行互斥外的任何逻辑。
2. **不动美术排版**：不改 buff 图标位置/尺寸/颜色/闪烁（`BuffHudUI.cs:129-233` 只加点击与快照赋值）；不改 `BattleUI.prefab`。
3. 框内**只显示名字 + 属性**，不要显示剩余秒数、来源、倍率说明等额外文字——主人明确拍板。
4. 弹出方向**向上**，沿用 `PlaceAbove`；若真机发现顶边裁切/压住图标，报实测截图等主人定夺，不擅自改向下。
5. 新框全 `raycastTarget=false`、不加 `GraphicRaycaster`，不挡下层点击；不写全屏遮罩、不做「点屏幕任意处关闭」。
6. **按约束：本清单只给改造点与验收标准，不写完整 C# 代码**，落地交子模型（C#/UI 类优先 GLM 5.3 Flash 或 Step-5-Preview）。
7. 改完静态核对 + 逐段回读，**不主动起编辑器编译**（改动小且集中），交主人进编辑器实测；提交前跑 `Tools/EnsureMeta.py`（虽无资源改动，走流程）。
