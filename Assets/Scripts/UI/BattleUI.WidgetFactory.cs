using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 槽位工厂与查找工具：底部技能槽 / 装备槽的运行时构建，以及按名字找节点的通用方法。
/// BattleUI 的 partial 分部，与 BattleUI.cs 同属一个类，成员签名保持原名。
/// </summary>
public partial class BattleUI : MonoBehaviour
{
    // ============================================================
    // 新底部布局：skill（4 主动技槽） / zhuangbei（5 装备快捷槽）
    // 临时 UI 上没挂任何项目脚本，这里纯按节点顺序建视图对象并缓存。
    // ============================================================

    void BindBottomQuickSlots()
    {
        Transform backpack = FindDeepChildIgnoreCase(transform, "BackpackPanel");
        if (skillSlotRoot == null)
        {
            // 新预制体：4 个主动技槽挂在 BackpackPanel/SkillBar（旧预制体叫 skill）。
            // 坑：MercSlot1 / MercSlot2 下还残留两个同名的空 "skill" 节点，
            // 深度优先查找会先命中它们（子节点 0 个）导致技能槽全绑不上。
            // 所以先找 SkillBar，并且只认「真的有子节点」的容器。
            skillSlotRoot = PickSlotContainer(
                (backpack != null ? FindDeepChildIgnoreCase(backpack, "SkillBar") : null)
                    ?? FindDeepChildIgnoreCase(transform, "SkillBar"),
                (backpack != null ? FindDeepChildIgnoreCase(backpack, "skill") : null)
                    ?? FindDeepChildIgnoreCase(transform, "skill"));
        }
        if (equipSlotRoot == null)
        {
            Transform raw = (backpack != null ? FindDeepChildIgnoreCase(backpack, "zhuangbei") : null)
                            ?? FindDeepChildIgnoreCase(transform, "zhuangbei");
            // 新预制体把装备槽又包了一层 bg，真正槽位在下一级，这里下探到槽位层
            equipSlotRoot = ResolveEquipSlotContainer(raw);
        }

        // SkillBar 链路（BackpackPanel -> SkillBar）里任何一级被关掉，
        // 表现就是「战斗中技能不见了」，连带挂在链路上的说明文字（如「战斗中无法调整」）也不显示。
        // 这里在绑定前统一拉活，不动 prefab。
        EnsureVisibleUpwards(backpack);
        EnsureVisibleUpwards(skillSlotRoot);

        BindRunSkillSlots();
        BindEquipQuickSlots();
        // 若此刻已经在战斗中，遮罩状态不会发生变化（TickBattleMask 会提前 return），
        // 这里补一次，保证技槽在绑定后立刻被顶到遮罩之上。
        EnsureBattleMask();
        RefreshSkillBarMaskState();
        Debug.Log($"[BattleUI] 底部快捷槽绑定 skill={runSkillSlots?.Count ?? 0} equip={equipQuickSlots?.Count ?? 0}");
    }

    /// <summary>
    /// 从候选容器里挑真正装了槽位的那一个：优先「有子节点」的，全空则返回第一个非空。
    /// 预制体里常有同名的空壳节点（历史残留），只按名字找会绑到空容器上。
    /// </summary>
    static Transform PickSlotContainer(params Transform[] candidates)
    {
        Transform first = null;
        for (int i = 0; i < candidates.Length; i++)
        {
            var c = candidates[i];
            if (c == null) continue;
            if (first == null) first = c;
            if (c.childCount > 0) return c;
        }
        return first;
    }

    /// <summary>
    /// 向上逐级拉活：prefab 合并或美术误操作可能把关掉某一级整条链路。
    /// 只拉到 BattleUI 自身节点为止（不含），避免把整个 HUD 强行点亮。
    /// </summary>
    static void EnsureVisibleUpwards(Transform t)
    {
        var stop = Instance != null ? Instance.transform : null;
        Transform cur = t;
        int guard = 0;
        while (cur != null && cur != stop && guard++ < 32)
        {
            if (!cur.gameObject.activeSelf)
                cur.gameObject.SetActive(true);
            cur = cur.parent;
        }
    }

    /// <summary>
    /// 装备快捷槽容器解析：新预制体是 zhuangbei/bg/[头|胸|脚|主手|副手]，
    /// 旧预制体是 zhuangbei/[槽位]。这里返回真正含槽位的那一层。
    /// </summary>
    static Transform ResolveEquipSlotContainer(Transform root)
    {
        if (root == null) return null;
        if (CountSlotLikeChildren(root) >= 2) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var r = ResolveEquipSlotContainer(root.GetChild(i));
            if (r != null) return r;
        }
        return null;
    }

    static int CountSlotLikeChildren(Transform parent)
    {
        int n = 0;
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (c == null) continue;
            if (IsEquipSlotNode(c)) n++;
        }
        return n;
    }

    /// <summary>装备槽判定：按预制体实际命名（头/胸/手/脚/主手/副手），或带 icon底 子节点。</summary>
    static bool IsEquipSlotNode(Transform t)
    {
        if (t == null) return false;
        switch (t.name)
        {
            case "头": case "胸": case "手": case "脚":
            case "主手": case "副手": case "披风":
                return true;
        }
        return FindDeepChildIgnoreCase(t, "icon底") != null
               || FindDeepChildIgnoreCase(t, "ItemIcon") != null;
    }

    /// <summary>槽位 → 装备部位。按名字认，不再按下标（新预制体少了「手」且顺序不同）。</summary>
    static EquipSlotType EquipSlotTypeOf(string nodeName, int index)
    {
        if (!string.IsNullOrEmpty(nodeName))
        {
            // 先判主手/副手，避免被「手」抢先命中
            if (nodeName.IndexOf("主手", System.StringComparison.Ordinal) >= 0) return EquipSlotType.MainHand;
            if (nodeName.IndexOf("副手", System.StringComparison.Ordinal) >= 0) return EquipSlotType.OffHand;
            if (nodeName.IndexOf("头", System.StringComparison.Ordinal) >= 0) return EquipSlotType.Head;
            if (nodeName.IndexOf("胸", System.StringComparison.Ordinal) >= 0) return EquipSlotType.Chest;
            if (nodeName.IndexOf("手", System.StringComparison.Ordinal) >= 0) return EquipSlotType.Hands;
            if (nodeName.IndexOf("脚", System.StringComparison.Ordinal) >= 0) return EquipSlotType.Feet;
            if (nodeName.IndexOf("披风", System.StringComparison.Ordinal) >= 0) return EquipSlotType.Cape;
        }
        return index < QuickSlotOrder.Length ? QuickSlotOrder[index] : EquipSlotType.Head;
    }

    void BindRunSkillSlots()
    {
        if (runSkillSlots == null) runSkillSlots = new List<SkillAvatarUI>();
        runSkillSlots.Clear();
        if (skillSlotRoot == null) return;

        // 槽位是美术手摆的 anchoredPosition，没有 LayoutGroup（见 SkillOrderChip.UseLayoutGroup）
        float step = 0f;
        if (skillSlotRoot.childCount >= 2)
            step = Mathf.Abs(skillSlotRoot.GetChild(1).position.x - skillSlotRoot.GetChild(0).position.x);
        if (step <= 0f) step = 100f;

        for (int i = 0; i < skillSlotRoot.childCount; i++)
        {
            Transform t = skillSlotRoot.GetChild(i);
            if (t == null) continue;
            EnsureVisibleUpwards(t);
            var av = new SkillAvatarUI { root = t.gameObject };
            // 图标层：不能用 icon底 自身背景（会盖掉美术底图），统一补一个子层
            av.avatarImage = ResolveSlotIcon(t);
            // 底框就是槽根自己那层 Image；空槽压暗只动它（见 SkillAvatarUI.SetEmptyDim）
            av.frameImage = t.GetComponent<Image>();
            av.labelText = t.GetComponentInChildren<Text>(true);   // 原「被动」底字，改成显示技能名
            // 右下角等级：美术在每个技能槽下放了 level 节点，优先用它；没有再运行时补
            av.levelText = FindTextNamed(t, "level", "Level", "SkillLevel")
                ?? EnsureCornerText(t, "SkillLevel", 12);
            av.cooldownText = EnsureChildText(t, "SkillCd", 16);
            av.cooldownMask = EnsureChildMask(t, "SkillCdMask");
            av.energyFill = EnsureChildBar(t, "SkillEnergy", new Color(0.98f, 0.78f, 0.28f, 1f));
            // 纯冷却制：节点照样建（置 true 就能回来），默认隐藏
            av.SetEnergyFillVisible(GameConfig.PLAYER_SKILL_USE_ENERGY);
            runSkillSlots.Add(av);

            // 整理阶段可拖拽调序（空槽与「只有 1 个技能」时禁用，见 OnSkillSlotReordered）
            var chip = t.GetComponent<SkillOrderChip>();
            if (chip == null) chip = t.gameObject.AddComponent<SkillOrderChip>();
            chip.UseLayoutGroup = false;
            chip.SlotStep = step;
            chip.DragEnabled = false;          // 由 RefreshSkillSlotDragState 按阶段打开
            chip.OnOrderChanged = OnSkillSlotReordered;
        }
    }

    /// <summary>
    /// 保险丝：战斗 UI 因预制体重导入 / 父链变化 / 别处代码改动导致底部 4 个被动技能槽消失时，
    /// 兜底修复。能不修就不修；只有真正发生了修复动作才打 [BattleUI-保险丝] 警告，正常时不刷日志。
    /// 调用点：AutoGameInitializer.FixBattleUICanvas、BattleManager 两处 EnsureBattleControls 之后。
    /// </summary>
    public void EnsureRunSkillSlotsRepaired()
    {
        // 1) 容器根丢失 → 重新解析容器（BindBottomQuickSlots 内部会顺带 BindRunSkillSlots）
        if (skillSlotRoot == null)
        {
            BindBottomQuickSlots();
            Debug.LogWarning("[BattleUI-保险丝] skillSlotRoot 为空，已重新执行 BindBottomQuickSlots 重建容器");
        }

        // 2) 槽列表为空或含空引用 → 重新实例化
        if (runSkillSlots == null || runSkillSlots.Count == 0 || runSkillSlots.Exists(s => s == null))
        {
            BindRunSkillSlots();
            Debug.LogWarning("[BattleUI-保险丝] runSkillSlots 为空或含空引用，已重新执行 BindRunSkillSlots 重建");
        }

        if (skillSlotRoot != null)
        {
            // 3) 强制把 SkillBar 自身嵌套 Canvas 抬到 BackpackPanel 的 Canvas order + 1
            //    （直接以 BackpackPanel 为准，不依赖 SkillBar 父链查找）；没有 Canvas 就补一个 overrideSorting。
            Canvas skillCanvas = skillSlotRoot.GetComponent<Canvas>();
            bool addedCanvas = false;
            if (skillCanvas == null)
            {
                skillCanvas = skillSlotRoot.gameObject.AddComponent<Canvas>();
                skillCanvas.overrideSorting = true;
                addedCanvas = true;
            }
            int orderBefore = skillCanvas.sortingOrder;
            RaiseSlotCanvasAboveParent(skillSlotRoot);
            if (addedCanvas || skillCanvas.sortingOrder != orderBefore)
                Debug.LogWarning($"[BattleUI-保险丝] SkillBar 嵌套 Canvas 已抬到 {skillCanvas.sortingOrder}（基准 BackpackPanel +1）");

            // 4) 任一槽未激活 → 置 active；并确保 SkillBar 的 sibling 顺序在 zhezhao 遮罩之后
            bool needActive = false;
            for (int i = 0; i < runSkillSlots.Count; i++)
            {
                var av = runSkillSlots[i];
                if (av != null && av.root != null && !av.root.activeInHierarchy)
                {
                    av.root.SetActive(true);
                    needActive = true;
                }
            }
            if (needActive)
                Debug.LogWarning("[BattleUI-保险丝] 存在未激活的技能槽，已置 active");

            var maskNode = FindDeepChildIgnoreCase(transform, "zhezhao");
            if (maskNode != null)
            {
                var maskTr = maskNode.transform;
                if (skillSlotRoot.parent == maskTr.parent
                    && skillSlotRoot.GetSiblingIndex() < maskTr.GetSiblingIndex())
                {
                    skillSlotRoot.SetSiblingIndex(maskTr.GetSiblingIndex() + 1);
                    Debug.LogWarning("[BattleUI-保险丝] SkillBar sibling 顺序已抬到 zhezhao 遮罩之后");
                }
            }
        }
    }

    void BindEquipQuickSlots()
    {
        if (equipQuickSlots == null) equipQuickSlots = new List<EquipQuickSlotUI>();
        equipQuickSlots.Clear();
        if (equipSlotRoot == null) return;

        for (int i = 0; i < equipSlotRoot.childCount; i++)
        {
            Transform t = equipSlotRoot.GetChild(i);
            if (t == null) continue;
            // 槽位层里混着 BackpackTitle 之类装饰节点，只认真正的装备槽
            if (!IsEquipSlotNode(t)) continue;
            var slot = new EquipQuickSlotUI
            {
                root = t.gameObject,
                slotType = EquipSlotTypeOf(t.name, i)
            };
            slot.iconImage = ResolveSlotIcon(t);
            // 先记下美术在这层放的占位图（临时图），空槽时 Bind 会把它还原回来
            slot.placeholderSprite = slot.iconImage != null ? slot.iconImage.sprite : null;
            slot.slotLabel = t.GetComponentInChildren<Text>(true);  // 头 / 胸甲 / 手 / 脚 / 左手 / 右手
            equipQuickSlots.Add(slot);
        }
    }

    /// <summary>
    /// 取槽位图标层：**优先用美术自己摆的节点**（icon / ItemIcon / Icon，不区分大小写）。
    /// 美术调好位置的节点必须优先，否则代码另建一层就会和他摆的图叠在一起。
    /// 若同时存在美术节点与代码以前补建的 ItemIcon，把后者关掉。
    /// </summary>
    static Image ResolveSlotIcon(Transform slotRoot)
    {
        if (slotRoot == null) return null;
        var art = FindImageNamedNoFallback(slotRoot, "icon", "ItemIcon", "Icon");
        if (art != null)
        {
            KillRedundantGeneratedIcon(slotRoot, art);
            art.preserveAspect = true;
            art.raycastTarget = false;
            return art;
        }
        return EnsureChildIcon(slotRoot);
    }

    /// <summary>代码补建的图标层固定叫 ItemIcon；若美术另有自己的节点，把代码那份关掉避免重叠。</summary>
    static void KillRedundantGeneratedIcon(Transform slotRoot, Image keep)
    {
        if (slotRoot == null || keep == null) return;
        var gen = FindDeepChildIgnoreCase(slotRoot, "ItemIcon");
        if (gen != null && gen != keep.transform && gen.name == "ItemIcon")
            gen.gameObject.SetActive(false);
    }

    /// <summary>在槽位下补一个居中图标层，避免覆盖 icon底 的背景图。</summary>
    static Image EnsureChildIcon(Transform parent)
    {
        var exist = FindDeepChildIgnoreCase(parent, "ItemIcon");
        if (exist != null)
        {
            var e = exist.GetComponent<Image>();
            if (e != null) { e.preserveAspect = true; e.raycastTarget = false; return e; }
        }
        var go = new GameObject("ItemIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.16f, 0.16f);
        rt.anchorMax = new Vector2(0.84f, 0.84f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.preserveAspect = true;
        img.sprite = null;   // 无 sprite 时不绘制，靠 SetAvatar 显隐控制
        return img;
    }

    /// <summary>在槽位下补一行文字（星级 / 冷却）。</summary>
    static Text EnsureChildText(Transform parent, string name, int fontSize)
    {
        var exist = FindDeepChildIgnoreCase(parent, name);
        if (exist != null)
        {
            var e = exist.GetComponent<Text>();
            if (e != null) return e;
        }
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.14f);
        rt.anchorMax = new Vector2(1f, 0.36f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var txt = go.GetComponent<Text>();
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontSize = fontSize;
        txt.color = new Color(1f, 0.92f, 0.72f);
        txt.raycastTarget = false;
        var f = GameFonts.GetChinese();
        if (f != null) txt.font = f;
        return txt;
    }

    /// <summary>
    /// 在槽位右下角补一行小字（等级/星级）。锚在角上，只占一小块，不碰任何图片节点的尺寸。
    /// </summary>
    static Text EnsureCornerText(Transform parent, string name, int fontSize)
    {
        var exist = FindDeepChildIgnoreCase(parent, name);
        if (exist != null)
        {
            var e = exist.GetComponent<Text>();
            if (e != null) return e;
        }
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        go.transform.SetAsLastSibling();
        var rt = go.GetComponent<RectTransform>();
        // 右下角固定一个小盒子：宽 = 父的 45%，高 = 父的 22%
        rt.anchorMin = new Vector2(0.55f, 0.02f);
        rt.anchorMax = new Vector2(1f, 0.24f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var txt = go.GetComponent<Text>();
        txt.alignment = TextAnchor.LowerRight;
        txt.fontSize = fontSize;
        txt.color = new Color(1f, 0.96f, 0.78f);
        txt.raycastTarget = false;
        var f = GameFonts.GetChinese();
        if (f != null) txt.font = f;
        return txt;
    }

    /// <summary>冷却遮罩复用的纯白 sprite（只创建一次）。</summary>
    static Sprite _maskWhiteSprite;
    static Sprite GetMaskWhiteSprite()
    {
        if (_maskWhiteSprite != null) return _maskWhiteSprite;
        var tex = Texture2D.whiteTexture;
        _maskWhiteSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 1f);
        _maskWhiteSprite.name = "SkillCooldownMaskWhite";
        return _maskWhiteSprite;
    }

    /// <summary>在槽位上补一层黑色半透遮罩（Radial360 填充），用于技能冷却的钟表式收缩。</summary>
    static Image EnsureChildMask(Transform parent, string name)
    {
        var exist = FindDeepChildIgnoreCase(parent, name);
        if (exist != null)
        {
            var e = exist.GetComponent<Image>();
            if (e != null)
            {
                // 缺陷1：已存在节点也要确保有 sprite，否则 fillAmount 不生效
                if (e.sprite == null) e.sprite = GetMaskWhiteSprite();
                return e;
            }
        }
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        // 铺满整个技能槽图标区域（stretch 锚点），不改槽位本身的锚点/尺寸/位置。
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.color = new Color(0f, 0f, 0f, 0.6f);   // 黑色半透，不遮死图标
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Radial360;
        img.fillOrigin = (int)Image.Origin360.Top;
        img.fillClockwise = true;
        img.fillAmount = 0f;
        img.sprite = GetMaskWhiteSprite();   // 缺陷1：必须有 sprite，Radial360 才会按 fillAmount 收缩
        // 缺陷2：把遮罩插到第一个带 Text 子节点之前（图标之后、文字之下），避免盖住技能名/等级文字
        Transform textTr = null;
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (c != null && c.GetComponent<Text>() != null) { textTr = c; break; }
        }
        if (textTr != null) go.transform.SetSiblingIndex(textTr.GetSiblingIndex());
        go.SetActive(false);   // 默认隐藏，冷却时再点亮
        return img;
    }

    /// <summary>在槽位底部补一条细进度条，返回填充 Image。</summary>
    static Image EnsureChildBar(Transform parent, string name, Color color)
    {
        var exist = FindDeepChildIgnoreCase(parent, name);
        if (exist != null)
        {
            var existFill = FindImageNamed(exist, "Fill");
            if (existFill != null) return existFill;
        }
        var bgGo = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bgGo.transform.SetParent(parent, false);
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0.10f, 0.03f);
        bgRt.anchorMax = new Vector2(0.90f, 0.11f);
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        var bg = bgGo.GetComponent<Image>();
        bg.color = new Color(0.06f, 0.06f, 0.09f, 0.85f);
        bg.raycastTarget = false;

        var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillGo.transform.SetParent(bgGo.transform, false);
        var fRt = fillGo.GetComponent<RectTransform>();
        fRt.anchorMin = Vector2.zero;
        fRt.anchorMax = Vector2.one;
        fRt.offsetMin = Vector2.zero;
        fRt.offsetMax = Vector2.zero;
        var fill = fillGo.GetComponent<Image>();
        fill.color = color;
        fill.raycastTarget = false;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 0f;
        return fill;
    }

    Text FindUIText(string name)
    {
        Transform t = FindDeepChildIgnoreCase(transform, name);
        return t != null ? t.GetComponent<Text>() : null;
    }

    static Image FindImageNamed(Transform root, params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            Transform t = FindDeepChildIgnoreCase(root, names[i]);
            if (t != null)
            {
                var img = t.GetComponent<Image>();
                if (img != null) return img;
            }
        }
        // 退而求其次：根上的 Image
        return root.GetComponent<Image>();
    }

    /// <summary>
    /// 严格查找：只按名字找，不回退 root Image（避免把 PlayerSlot 背景误绑成头像/血条）
    /// </summary>
    static Image FindImageNamedNoFallback(Transform root, params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            Transform t = FindDeepChildIgnoreCase(root, names[i]);
            if (t != null)
            {
                var img = t.GetComponent<Image>();
                if (img != null) return img;
            }
        }
        return null;
    }

    static Text FindTextNamed(Transform root, params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            Transform t = FindDeepChildIgnoreCase(root, names[i]);
            if (t != null)
            {
                var tx = t.GetComponent<Text>();
                if (tx != null) return tx;
            }
        }
        return null;
    }

    /// <summary>
    /// 职业图标位：美术在 xuetiaodi 下放了「职业icon」节点，但可能没挂 Image 组件，缺了就运行时补一个。
    /// 节点不存在时返回 null（老预制体没有这一层，不强行新建，避免挡住血条）。
    /// </summary>
    static Image EnsureJobIcon(Transform root)
    {
        Transform t = FindDeepChildIgnoreCase(root, "职业icon")
                      ?? FindDeepChildIgnoreCase(root, "JobIcon");
        if (t == null) return null;
        var img = t.GetComponent<Image>();
        if (img == null) img = t.gameObject.AddComponent<Image>();
        img.raycastTarget = false;
        img.preserveAspect = true;
        img.sprite = null;
        img.color = new Color(1f, 1f, 1f, 0f);
        return img;
    }

    /// <summary>只在直接子节点里按名找（不递归），避免被更深层的同名节点抢走。</summary>
    static Transform FindDirectChildIgnoreCase(Transform parent, string name)
    {
        if (parent == null || string.IsNullOrEmpty(name)) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (c != null && string.Equals(c.name, name, System.StringComparison.OrdinalIgnoreCase))
                return c;
        }
        return null;
    }

    /// <summary>
    /// 佣兵自带技能槽：挂在各自角色卡下（MercSlot1/skill、MercSlot2/skill）。
    /// 与底部 4 个「玩家被动技能」槽是**两套不同的东西**，不要合并、也不要互相顶替：
    /// 玩家 4 槽走 BackpackPanel/SkillBar，佣兵技能走角色卡里的 skill 节点。
    /// </summary>
    void BindMercSkillSlots()
    {
        if (mercSkillSlots == null) mercSkillSlots = new List<SkillAvatarUI>();
        mercSkillSlots.Clear();
        mercSkillSlots.Add(BuildMercSkillSlot(mercSlot1));
        mercSkillSlots.Add(BuildMercSkillSlot(mercSlot2));
        Debug.Log($"[BattleUI] 佣兵技能槽绑定 " +
                  $"1={(mercSkillSlots[0].root != null)} 2={(mercSkillSlots[1].root != null)}");
    }

    static SkillAvatarUI BuildMercSkillSlot(CharacterSlotUI slot)
    {
        var av = new SkillAvatarUI();
        Transform t = slot?.root != null ? FindDirectChildIgnoreCase(slot.root.transform, "skill") : null;
        if (t == null) return av;
        av.root = t.gameObject;
        // 容器在预制体里是空的（美术待填），图标/能量条/冷却字都运行时补
        av.avatarImage = FindImageNamedNoFallback(t, "ItemIcon", "Icon") ?? EnsureChildIcon(t);
        av.energyFill = EnsureChildBar(t, "MercSkillEnergy", new Color(0.55f, 0.85f, 1f, 1f));
        av.cooldownText = EnsureChildText(t, "MercSkillCd", 14);
        av.cooldownMask = EnsureChildMask(t, "MercSkillCdMask");
        return av;
    }

    static Transform FindDeepChildIgnoreCase(Transform parent, string name)
    {
        if (parent == null || string.IsNullOrEmpty(name)) return null;
        if (string.Equals(parent.name, name, System.StringComparison.OrdinalIgnoreCase))
            return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var r = FindDeepChildIgnoreCase(parent.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }

    static Transform FindChildIgnoreCase(Transform root, string name)
    {
        if (root == null) return null;
        if (string.Equals(root.name, name, System.StringComparison.OrdinalIgnoreCase))
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindChildIgnoreCase(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }
}
