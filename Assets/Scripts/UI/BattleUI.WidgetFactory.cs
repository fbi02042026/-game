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

        BindRunSkillSlots();
        BindEquipQuickSlots();
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
            var av = new SkillAvatarUI { root = t.gameObject };
            // 图标层：不能用 icon底 自身背景（会盖掉美术底图），统一补一个子层
            av.avatarImage = FindImageNamedNoFallback(t, "ItemIcon", "Icon") ?? EnsureChildIcon(t);
            av.labelText = t.GetComponentInChildren<Text>(true);   // 原「被动」底字，改成显示技能名
            // 右下角等级：美术在每个技能槽下放了 level 节点，优先用它；没有再运行时补
            av.levelText = FindTextNamed(t, "level", "Level", "SkillLevel")
                ?? EnsureCornerText(t, "SkillLevel", 12);
            av.cooldownText = EnsureChildText(t, "SkillCd", 16);
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
            slot.iconImage = FindImageNamedNoFallback(t, "ItemIcon", "Icon") ?? EnsureChildIcon(t);
            slot.slotLabel = t.GetComponentInChildren<Text>(true);  // 头 / 胸甲 / 手 / 脚 / 左手 / 右手
            equipQuickSlots.Add(slot);
        }
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
