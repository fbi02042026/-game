using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 底部 4 个主动技槽与 5 个装备快捷槽的刷新，以及技能/自动战斗相关的点击回调。
/// BattleUI 的 partial 分部，与 BattleUI.cs 同属一个类，成员签名保持原名。
/// </summary>
public partial class BattleUI : MonoBehaviour
{
    /// <summary>
    /// 刷新底部 4 个技能槽：全部为被动技能，充能满后由 PlayerSkillPassive 自动释放，
    /// 头像/槽位不再接手动释放。
    /// </summary>
    /// <summary>技能槽没绑上时只报一次，别每帧刷屏。</summary>
    static bool _runSkillSlotsWarned;

    public void UpdateRunSkillSlots()
    {
        if (runSkillSlots == null || runSkillSlots.Count == 0)
        {
            // 2026-10-11：主人反馈「抽中了技能还是没有立即显示图标」。刷新链路
            // （CoInstantPick → UpdateRunSkillSlots）本来就是通的，若槽位列表为空则**整条静默失效** ——
            // 这里报一次，主人跑一把就能从日志判断是「没绑定」还是「别处没刷」。
            if (!_runSkillSlotsWarned)
            {
                _runSkillSlotsWarned = true;
                Debug.LogError("[BattleUI] runSkillSlots 未绑定或为空 → 底部技能槽不会显示任何图标，" +
                               "抽到技能也看不见（查 BattleUI 预制体上的槽位绑定 / EnsureRunSkillSlotsRepaired）");
            }
            return;
        }

        var job = PlayerJobDefs.GetSelected();
        var ids = RunLoadout.IsActive ? RunLoadout.SkillIds() : null;
        for (int i = 0; i < runSkillSlots.Count; i++)
        {
            var slot = runSkillSlots[i];
            if (slot == null) continue;

            string id = ids != null && i < ids.Count ? ids[i] : null;
            if (string.IsNullOrEmpty(id))
            {
                slot.SetAvatar(null);
                // 没装备技能：底框压暗（base × 0.45），空槽仍保留美术的框体
                slot.SetEmptyDim(true);
                // 空槽保留美术的框体，但不写「无」字：
                // 与佣兵技能槽同一口径（没有就不显示，别留空字占位）。
                slot.SetLabelVisible(false);
                slot.SetLevelText("");
                slot.SetOrderText("");   // 空槽不标序号，避免「这格也算一发」的误读
                slot.SetNewBadge(false); // 空槽不可能「新」
                slot.SetEnergyFillVisible(GameConfig.PLAYER_SKILL_USE_ENERGY);
                slot.SetEnergyFill(0f);
                if (slot.cooldownText != null) slot.cooldownText.gameObject.SetActive(false);
                slot.SetCooldownRatio(0f);
                continue;
            }

            var active = RunDraftDirector.BuildRunSkill(id, job);
            int star = RunLoadout.StarOf(id);
            // 2026-09-22：ActiveSkill.icon 从来没有赋值链路（SkillRegistry 不填），技槽因此一直空白。
            // 这里按 id 兜底加载（图在 Resources/Icons/SkillIcon/{id}.png，6 张玩家技能全齐）。
            var icon = active != null ? active.icon : null;
            if (icon == null) icon = LoadRunSkillIcon(id);
            slot.SetAvatar(icon);
            // 已装备技能：底框还原原始色，不压暗
            slot.SetEmptyDim(false);
            // 底字不再写死「被动」：有技能就显示技能名。
            // 【2026-10-19 主人拍板】名字颜色 = 角色界面技能弹窗（SkillSelectUI）的手涂色：
            // 技槽 id 是运行时 id（可能带后缀），先 ResolveRealSkillId 归一，再 IndexOf 定位；
            // 查不到（-1）时 NameColorOf 回退白色。
            slot.SetSkillName(active != null ? active.skillName : id,
                nameColor: SkillSelectUI.NameColorOf(PlayerSkillDefs.IndexOf(PlayerSkillDefs.ResolveRealSkillId(id))));
            // 右下角等级：2026-09-22 主人要求——战斗内技能只升级，右下角只显示**数字**，
            // 不要再拼「★」前缀（数字节点用预制体里美术摆的 level，见 BindRunSkillSlots）
            slot.SetLevelText(star > 0 ? star.ToString() : "");
            // 左上角序号 = 释放优先级（① 最先放）。不拖拽时 = 获得技能的先后顺序，
            // 玩家在整理阶段拖动槽位改顺序后，这里跟着 RunLoadout.SkillIds() 一起变。
            slot.SetOrderText(SkillOrderLabel(i));
            // 【2026-10-06 主人拍板】本拍抽奖刚拿到的技能，右上角亮「新」，点「继续」后清。
            slot.SetNewBadge(NewLootMarks.Has(NewLootMarks.KindSkill, id));
        }

        // 【2026-10-11 主人报「刚抽出来就不显示，抽奖瞬间闪一下又没了」】
        // 拖拽开关跟着技能数走：技能列表一变就重评，
        // 别再等背包开合（旧唯一调用点）才刷新 —— 那正是「拖拽也不管用」的帮凶之一。
        RefreshSkillSlotDragState();
        LogSkillBarOcclusion();
        LogRunSkillDiagnostics(ids);
    }

    // 2026-10-11 主人报「技能还是看不见」→ 加屏幕级遮挡诊断。
    // 原来只打一次（首次出现带技能的槽），正好错过「抽中瞬间闪一下又没了」那一刻，
    // 这里放宽到 8 次：技能列表每次变化打一条，够抓现行又不刷屏。
    /// <summary>技槽体检最多打 8 次（技能列表每次变化打一次），别刷屏；原来只打一次抓不到「闪一下又没了」。</summary>
    const int MaxOcclusionLogs = 8;
    static int _occlusionLogCount;

    /// <summary>
    /// 技槽为什么看不见 —— 一次把量打全：自带 Canvas order / 父链最高 Canvas / 有没有同父兄弟盖在上面。
    /// 主症状「闪一下又没了」= 画上去了但被别的东西压住，只有这条日志能一次说清是谁压的。
    /// </summary>
    void LogSkillBarOcclusion()
    {
        if (_occlusionLogCount >= MaxOcclusionLogs || skillSlotRoot == null) return;
        var rt = skillSlotRoot as RectTransform;
        if (rt == null) return;
        _occlusionLogCount++;

        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        Vector2 center = (c[0] + c[2]) * 0.5f;

        var sb = new System.Text.StringBuilder();
        sb.Append("[BattleUI-技槽体检] 世界中心=").Append(center.x.ToString("F0")).Append(",").Append(center.y.ToString("F0"));

        var self = skillSlotRoot.GetComponent<Canvas>();
        sb.Append(" 自带Canvas=").Append(self != null
            ? self.sortingOrder + (self.overrideSorting ? "(override)" : "")
            : "无");

        int maxOrder = -1;
        string maxName = "-";
        for (var p = skillSlotRoot.parent; p != null; p = p.parent)
        {
            var pc = p.GetComponent<Canvas>();
            if (pc != null && pc.sortingOrder > maxOrder) { maxOrder = pc.sortingOrder; maxName = p.name; }
        }
        sb.Append(" 父链最高画布=").Append(maxName).Append("(").Append(maxOrder).Append(")");

        var parent = skillSlotRoot.parent;
        var c2 = new Vector3[4];
        for (int i = skillSlotRoot.GetSiblingIndex() + 1; i < parent.childCount; i++)
        {
            var sib = parent.GetChild(i);
            if (sib == null || !sib.gameObject.activeInHierarchy) continue;
            var srt = sib as RectTransform;
            if (srt == null) continue;
            srt.GetWorldCorners(c2);
            bool covers = center.x >= Mathf.Min(c2[0].x, c2[2].x) && center.x <= Mathf.Max(c2[0].x, c2[2].x)
                       && center.y >= Mathf.Min(c2[0].y, c2[2].y) && center.y <= Mathf.Max(c2[0].y, c2[2].y);
            if (covers)
                sb.Append(" ⚠被同父兄弟[").Append(sib.name).Append("]覆盖");
        }
        Debug.Log(sb.ToString());
        // 2026-10-11 主人报「技能还是看不见」→ 加屏幕级遮挡诊断：上面那条只比 order 和同父兄弟，
        // 看不出到底哪个画布压在技槽上面；这条按屏幕矩形求交，直接点名。
        DumpScreenRectAndOccluders(rt);
    }

    /// <summary>
    /// 2026-10-11 主人报「技能还是看不见」→ 加屏幕级遮挡诊断。
    /// 把技槽换算成屏幕矩形，再把场景里所有 sortingOrder 更高（或 layer 更高）的画布也算成屏幕矩形，
    /// 两者相交的就是「可能盖住技槽」的嫌疑对象 —— 一条日志点名，纯只读，不碰任何节点。
    /// </summary>
    void DumpScreenRectAndOccluders(RectTransform rt)
    {
        var rootCanvas = rt.GetComponentInParent<Canvas>();
        Camera cam = null;
        if (rootCanvas != null) cam = rootCanvas.worldCamera;
        if (cam == null) cam = Camera.main;

        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        float sx0 = float.MaxValue, sy0 = float.MaxValue, sx1 = float.MinValue, sy1 = float.MinValue;
        for (int i = 0; i < 4; i++)
        {
            // 相机拿不到时直接用世界坐标当屏幕坐标（正交 + 原点对齐的兜底），不抛异常。
            Vector2 p = cam != null ? RectTransformUtility.WorldToScreenPoint(cam, c[i])
                                    : new Vector2(c[i].x, c[i].y);
            if (p.x < sx0) sx0 = p.x;
            if (p.x > sx1) sx1 = p.x;
            if (p.y < sy0) sy0 = p.y;
            if (p.y > sy1) sy1 = p.y;
        }

        var sb2 = new System.Text.StringBuilder();
        sb2.Append("[BattleUI-技槽屏幕] 屏幕=").Append(Screen.width).Append("x").Append(Screen.height)
           .Append(" 技槽屏幕矩形=(").Append(sx0.ToString("F0")).Append(",").Append(sy0.ToString("F0"))
           .Append(")~(").Append(sx1.ToString("F0")).Append(",").Append(sy1.ToString("F0")).Append(")");

        var selfC = skillSlotRoot != null ? skillSlotRoot.GetComponent<Canvas>() : null;
        int myOrder = selfC != null ? selfC.sortingOrder : 0;
        int myLayer = selfC != null ? selfC.sortingLayerID : 0;
        sb2.Append(" 自身Canvas=").Append(selfC != null ? myOrder.ToString() : "无")
           .Append(" layer=").Append(myLayer);

        var all = FindObjectsOfType<Canvas>();
        var list = new List<Canvas>();
        if (all != null)
            for (int i = 0; i < all.Length; i++) list.Add(all[i]);
        list.Sort((a, b) => (a != null ? a.sortingOrder : 0).CompareTo(b != null ? b.sortingOrder : 0));
        list.Reverse();   // 从大到小

        int found = 0;
        for (int i = 0; i < list.Count && found < 5; i++)
        {
            var cv = list[i];
            if (cv == null || cv == selfC) continue;
            if (!cv.enabled) continue;
            if (!(cv.transform is RectTransform)) continue;
            // 只留「画在技槽之上」的：layer 更高，或同 layer 下 order 更高。
            if (!(cv.sortingLayerID > myLayer
                  || (cv.sortingLayerID == myLayer && cv.sortingOrder > myOrder))) continue;

            var crt = cv.transform as RectTransform;
            if (crt == null) continue;
            var cc = new Vector3[4];
            crt.GetWorldCorners(cc);
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            for (int k = 0; k < 4; k++)
            {
                Vector2 p = cam != null ? RectTransformUtility.WorldToScreenPoint(cam, cc[k])
                                        : new Vector2(cc[k].x, cc[k].y);
                if (p.x < x0) x0 = p.x;
                if (p.x > x1) x1 = p.x;
                if (p.y < y0) y0 = p.y;
                if (p.y > y1) y1 = p.y;
            }
            // 相交：两个轴都要有重叠
            bool hitX = x0 < sx1 && x1 > sx0;
            bool hitY = y0 < sy1 && y1 > sy0;
            if (!hitX || !hitY) continue;

            found++;
            // 节点路径：从该节点往上拼 3 层名字
            string path = cv.name;
            if (cv.transform.parent != null)
            {
                path = cv.transform.parent.name + "/" + path;
                if (cv.transform.parent.parent != null)
                    path = cv.transform.parent.parent.name + "/" + path;
            }
            var img = cv.GetComponent<Image>();
            sb2.Append(" ⚠上层[").Append(path).Append("] order=").Append(cv.sortingOrder)
               .Append(" layer=").Append(cv.sortingLayerID)
               .Append(" 矩形=(").Append(x0.ToString("F0")).Append(",").Append(y0.ToString("F0"))
               .Append(")~(").Append(x1.ToString("F0")).Append(",").Append(y1.ToString("F0"))
               .Append(") 自身Image颜色=").Append(img != null ? img.color.ToString() : "无Image");
        }
        Debug.Log(sb2.ToString());
    }

    /// <summary>技能槽序号文案：0→① … 9→⑩；再往后没有圈字符号，返回空串（底部只有 4 槽，走不到）。</summary>
    static string SkillOrderLabel(int index)
    {
        if (index < 0 || index > 9) return "";
        return ((char)(0x2460 + index)).ToString();
    }

    /// <summary>玩家技能图标：Resources 优先（打包可用），编辑器再兜 Art 源目录；与 SkillSelectUI 同路径口径。
    /// <para>2026-10-09：技能卡（LevelUpDraftUI）也走这里取图 —— 同一语义只留这一个出口，别再抄一份路径。</para></summary>
    public static Sprite LoadRunSkillIcon(string skillId)
    {
        if (string.IsNullOrEmpty(skillId)) return null;
        var sp = Resources.Load<Sprite>("Icons/SkillIcon/" + skillId);
        if (sp != null) return sp;
        var all = Resources.LoadAll<Sprite>("Icons/SkillIcon/" + skillId);
        if (all != null && all.Length > 0) return all[0];
#if UNITY_EDITOR
        // 2026-09-28 主人拍板：编辑器不再回退美术源目录/AssetDatabase，缺图直接露白框。
        if (DeviceParity.EditorFallbackEnabled)
        {
            sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Art/UI/Icons/玩家SkillIcon/" + skillId + ".png");
        }
#endif
        return sp;
    }

    // ============================================================
    // 技槽点击 → 「获得技能」弹窗（2026-10-19 主人拍板「点击后弹出获得技能那个弹出 统一一下」）
    // ============================================================

    // 前身是 2026-10-12 的「技能简介小浮框」（WireSkillInfoClick），2026-10-19 按主人口径下线，
    // 统一改弹 <see cref="SkillObtainPopupUI"/>（与抽中技能、佣兵头像点弹详情同一套获得类弹窗）。

    /// <summary>
    /// 给技槽补一层透明点击层 + Button，点一下弹「获得技能」弹窗。
    /// 与佣兵头像点弹详情（WireMercAvatarClick）同一范式：不碰预制体、不加美术，
    /// 透明 Image 只负责接射线（槽位底图 raycastTarget 未必开着）；Button 挂在它上面，
    /// 与槽里原有的图标 / 顺序角标 / 「新」角标共存。幂等：已有就复用，只重绑 onClick。
    /// <para>⚠ 拖拽不受影响：拖拽事件会沿父链冒泡到槽根上的 <see cref="SkillOrderChip"/>，
    /// 而拖动超过阈值后 Unity 不再派发 click —— 再叠一层 <c>chip.IsDragging</c> 双保险。</para>
    /// </summary>
    void WireSkillSlotClick(SkillAvatarUI av, int index)
    {
        if (av == null || av.root == null) return;

        var node = FindDeepChildIgnoreCase(av.root.transform, "SkillSlotClick");
        if (node == null)
        {
            var hit = new GameObject("SkillSlotClick", typeof(RectTransform), typeof(Image));
            hit.transform.SetParent(av.root.transform, false);
            hit.transform.SetAsLastSibling();
            var rt = hit.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = hit.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);   // 全透明，纯接射线
            img.raycastTarget = true;
            node = hit.transform;
        }

        var btn = node.GetComponent<Button>();
        if (btn == null)
        {
            btn = node.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
        }
        btn.targetGraphic = node.GetComponent<Image>();
        btn.onClick.RemoveAllListeners();
        int capture = index;
        btn.onClick.AddListener(() => OnSkillSlotClick(capture, av.root));
    }

    /// <summary>
    /// 技槽点击：按下标取本局技能 id（与 <see cref="UpdateRunSkillSlots"/> 同一取法），
    /// 弹「获得技能」弹窗（<see cref="SkillObtainPopupUI.ShowFromId"/>）。空槽不弹。
    /// 点击<b>不会施放</b>技能：玩家技全由 PlayerSkillPassive 自动释放，这里纯查看。
    /// </summary>
    void OnSkillSlotClick(int index, GameObject slotRoot)
    {
        // 正在拖这个槽 = 调顺序，不是要看详情
        var chip = slotRoot != null ? slotRoot.GetComponent<SkillOrderChip>() : null;
        if (chip != null && chip.IsDragging) return;

        var ids = RunLoadout.IsActive ? RunLoadout.SkillIds() : null;
        string id = ids != null && index < ids.Count ? ids[index] : null;
        if (string.IsNullOrEmpty(id)) return;
        SkillObtainPopupUI.ShowFromId(id);
    }

    /// <summary>上次诊断输出的特征串，避免每次刷新都刷屏。</summary>
    string _lastSkillDiagKey = "\u0000";

    /// <summary>
    /// 4 个技槽不显示时的定位日志：一次就能分清是「节点没绑上 / 被关掉」还是「本局没有技能」。
    /// 只在结果特征变化时输出，不做任何显示上的改动。
    /// </summary>
    void LogRunSkillDiagnostics(System.Collections.Generic.List<string> ids)
    {
        if (runSkillSlots == null) return;
        var sb = new System.Text.StringBuilder();
        sb.Append("loadoutActive=").Append(RunLoadout.IsActive)
          .Append(" ids=").Append(ids == null ? "null" : string.Join(",", ids));
        if (SkillRegistry.Instance == null) sb.Append(" SkillRegistry=NULL");
        for (int i = 0; i < runSkillSlots.Count; i++)
        {
            var s = runSkillSlots[i];
            sb.Append(" | #").Append(i).Append(' ');
            if (s == null) { sb.Append("slot=null"); continue; }
            sb.Append("root=").Append(s.root == null ? "null"
                      : (s.root.activeInHierarchy ? "on" : "OFF"));
            sb.Append(" img=").Append(s.avatarImage == null ? "NO-IMG"
                      : (s.avatarImage.sprite != null ? s.avatarImage.sprite.name : "no-sprite"));
        }
        string key = sb.ToString();
        if (string.Equals(key, _lastSkillDiagKey)) return;
        _lastSkillDiagKey = key;
        Debug.Log("[BattleUI-技槽诊断] " + key);
    }

    /// <summary>
    /// 2026-10-09 主人拍板：佣兵技能槽 / 头像只服务「真正入队」的佣兵。
    /// 引导局「塔克天降」的演出替身会 SpawnMercenary 进 MercenaryManager._activeMercs、
    /// 并 SetupBattleSkills 挂上 MercSkillCaster，但此时玩家还没走第 2 抽把他招入队
    /// （hireId 已写入却不在 RunLoadout.mercs 里）。若不过滤，玩家会在塔克入队前就看到
    /// 他头像旁的技能槽并转 CD，与「没招募就不该出现」相悖。
    /// 同一语义只此一处：是否「在队」= 当前局 RunLoadout 里有没有这条 hireId。
    /// </summary>
    static bool MercIsInParty(Mercenary m)
    {
        if (m == null) return false;
        // 2026-10-09 复查修复：非局内（旧存档兜底路径，BattleManager 开局 SpawnMercenaries 分支）
        // RunLoadout 未激活，不能恒判 false——否则该路径下头像栏照常显示佣兵、技能槽却整槽隐藏且不转 CD。
        // 非局内回退旧判据：有 hireId 即视为在队（与 2026-09-17「没有佣兵不显示」口径一致）。
        if (!RunLoadout.IsActive) return !string.IsNullOrEmpty(m.hireId);
        if (string.IsNullOrEmpty(m.hireId)) return false;
        return RunLoadout.HasMerc(m.hireId);
    }

    /// <summary>刷新底部 5 个装备快捷槽：头 / 胸 / 脚 / 主手 / 副手（暂不做「手」和「披风」）。</summary>
    public void UpdateEquipQuickSlots()
    {
        if (equipQuickSlots == null || equipQuickSlots.Count == 0) return;
        var bag = GridBackpackSystem.Instance;
        for (int i = 0; i < equipQuickSlots.Count; i++)
        {
            var slot = equipQuickSlots[i];
            if (slot == null) continue;
            slot.Bind(bag != null ? bag.GetEquippedInSlot(slot.slotType) : null);
        }
    }

    /// <summary>
    /// 刷新佣兵自带技能槽（MercSlot1/skill、MercSlot2/skill）。
    /// 注意：这是**佣兵自己的技能**，和上面 4 个玩家被动技槽是两套，不要共用。
    /// 佣兵技能同样由 MercSkillCaster 自动释放，这里只表现 图标 / 充能 / 冷却。
    /// 没有对应佣兵或该佣兵没配技能时整槽隐藏，不露空框。
    /// </summary>
    public void UpdateMercSkillSlots()
    {
        if (mercSkillSlots == null || mercSkillSlots.Count == 0) return;
        var mm = MercenaryManager.Instance;
        var mercs = mm != null ? mm.GetActiveMercs() : null;

        for (int i = 0; i < mercSkillSlots.Count; i++)
        {
            var slot = mercSkillSlots[i];
            if (slot == null || slot.root == null) continue;

            var m = (mercs != null && i < mercs.Count) ? mercs[i] : null;
            var caster = m != null ? m.SkillCaster : null;
            // 2026-10-09 主人拍板：没入队的演出替身（如引导天降塔克）不显示技能槽
            bool has = MercIsInParty(m) && caster != null && caster.HasActiveSkill;
            // 没有佣兵 / 该佣兵没配主动技：整槽隐藏，既不留空框也不留「空」字。
            // （2026-09-17 用户口径：没有佣兵时不用显示佣兵技能图标。）
            slot.root.SetActive(has);
            if (!has)
            {
                slot.SetAvatar(null);
                if (slot.labelText != null) slot.labelText.gameObject.SetActive(false);
                slot.SetLevelText("");
                slot.SetEnergyFill(0f);
                if (slot.cooldownText != null) slot.cooldownText.gameObject.SetActive(false);
                slot.SetCooldownRatio(0f);
                slot.SetNewBadge(false);
                continue;
            }

            slot.SetAvatar(MercSkillTable.LoadIcon(caster.ActiveSkillId));
            // 【2026-10-06 主人拍板】佣兵技能刚抽到时右上角亮「新」，点「继续」后清。
            slot.SetNewBadge(NewLootMarks.Has(NewLootMarks.KindSkill, caster.ActiveSkillId));
            // 2026-10-06：底部细条语义由「充能」改为「剩余 MP 比例」（仍 0~1）
            slot.SetEnergyFill(BattleManager.Instance != null ? BattleManager.Instance.GetMercMp(i) : 0f);
        }
    }

    string _lastMercSkillKey = "\u0000";

    /// <summary>逐帧刷佣兵技能充能 / 冷却；佣兵进出队或换人时整槽重建。</summary>
    void TickMercSkillSlots()
    {
        if (mercSkillSlots == null || mercSkillSlots.Count == 0) return;
        var mm = MercenaryManager.Instance;
        var mercs = mm != null ? mm.GetActiveMercs() : null;

        string key = "";
        if (mercs != null)
        {
            for (int i = 0; i < mercs.Count && i < mercSkillSlots.Count; i++)
                key += (mercs[i] != null && mercs[i].SkillCaster != null
                           ? mercs[i].SkillCaster.ActiveSkillId : "-") + ",";
        }
        if (!string.Equals(key, _lastMercSkillKey))
        {
            _lastMercSkillKey = key;
            UpdateMercSkillSlots();
        }

        for (int i = 0; i < mercSkillSlots.Count; i++)
        {
            var slot = mercSkillSlots[i];
            if (slot == null || slot.root == null || !slot.root.activeSelf) continue;
            var m = (mercs != null && i < mercs.Count) ? mercs[i] : null;
            // 2026-10-09 主人拍板：没入队的演出替身不转 CD（与 UpdateMercSkillSlots 同一判据 MercIsInParty）
            if (!MercIsInParty(m)) continue;
            var caster = m != null ? m.SkillCaster : null;
            if (caster == null) continue;

            slot.SetEnergyFill(BattleManager.Instance != null ? BattleManager.Instance.GetMercMp(i) : 0f);
            if (slot.cooldownMask == null) continue;
            // 冷却改为黑色半透遮罩 + Radial360 收缩（钟表式）：剩余/总时长。
            float cd = caster.CooldownRemain;
            float total = caster.CooldownTotal;
            slot.SetCooldownRatio(total > 0f ? cd / total : 0f);
            // 【2026-10-17 主人拍板】数字倒计时去掉（原 2026-10-07「佣兵技能也给数字倒计时」口径作废）：
            // 手机端角色卡右上角只留技能图标 + 转圈冷却遮罩，不再显示数字。
        }
    }

    string _lastSkillKey = "\u0000";

    /// <summary>「槽上有技能但图标是空」只报一次，别每帧刷屏（2026-10-11 主人报「图标显示一下又不见了」）。</summary>
    static bool _iconLostWarned;

    /// <summary>逐帧（0.1s 节流）刷技能槽充能 / 冷却；技能列表变了就整槽重建。</summary>
    void TickRunSkillSlots()
    {
        if (runSkillSlots == null || runSkillSlots.Count == 0) return;

        var ids = RunLoadout.IsActive ? RunLoadout.SkillIds() : null;
        string key = ids == null ? "" : string.Join(",", ids);
        if (!string.Equals(key, _lastSkillKey))
        {
            _lastSkillKey = key;
            UpdateRunSkillSlots();
        }

        var bm = BattleManager.Instance;
        var sys = SkillSystem.Instance;
        for (int i = 0; i < runSkillSlots.Count; i++)
        {
            var slot = runSkillSlots[i];
            if (slot == null) continue;
            bool has = ids != null && i < ids.Count;

            // 【2026-10-11 主人报「抽中时图标能显示一下 然后又不见了」】
            // UpdateRunSkillSlots 明明设上了 sprite（日志实证 img=技能id），之后却看不到 ——
            // 若此处发现「有技能但图标为空」，说明中途被谁清掉/换掉了图标层。只报一次，不兜底，
            // 把节点信息打全（名字/父/激活/alpha）让主人下一把抓到现行。
            if (has && !_iconLostWarned
                && (slot.avatarImage == null || slot.avatarImage.sprite == null))
            {
                _iconLostWarned = true;
                var img = slot.avatarImage;
                Debug.LogError("[BattleUI] 技槽图标丢失现行：槽 #" + i + " 有技能 " +
                               (ids != null ? ids[i] : "?") + " 但 sprite 为空" +
                               " img=" + (img == null ? "NULL（图标层被删/换）" : img.name) +
                               " 父=" + (img != null && img.transform.parent != null ? img.transform.parent.name : "?") +
                               " 激活=" + (img != null && img.gameObject.activeInHierarchy) +
                               " alpha=" + (img != null ? img.color.a : -1f));
            }

            // 纯冷却制：能量条隐藏（PLAYER_SKILL_USE_ENERGY 置 true 可退回）
            slot.SetEnergyFillVisible(GameConfig.PLAYER_SKILL_USE_ENERGY);
            if (GameConfig.PLAYER_SKILL_USE_ENERGY)
                slot.SetEnergyFill(has && bm != null ? bm.GetPlayerSkillEnergy(i) : 0f);
            if (!has)
            {
                // 缺陷3：空槽（技能已卸下）清掉上一次残留的冷却遮罩
                slot.SetCooldownRatio(0f);
                slot.SetMpShortage(false);
                continue;
            }
            // 2026-10-06：蓝不够这一发 → 头像压暗，让玩家一眼看出「这个现在放不出来」。
            // 无蓝技能（耗蓝 0）永远不压暗 —— 它们是地板节奏，照常按 CD 放。
            float mpNeed = PlayerSkillDefs.MpCostOf(ids[i]);
            slot.SetMpShortage(bm != null && mpNeed > 0f && !bm.CanAffordPlayerMp(mpNeed));
            // 冷却改为黑色半透遮罩 + Radial360 收缩（钟表式）：剩余/总时长。
            float cd = sys != null ? sys.GetPlayerSkillCooldownRemaining(i) : 0f;
            float total = sys != null ? sys.GetPlayerSkillCooldownTotal(i) : 0f;
            slot.SetCooldownRatio(total > 0f ? cd / total : 0f);
            // 【2026-10-07 主人拍板】转圈之外再给一个**数字倒计时**（主人原话「技能的 CD 写个数字倒计时」）：
            // 只剩 0.05s 以上才显示，向上取整（剩 2.3s 显示 3），归零立刻清空，不留一个「0」闪一下。
            slot.SetCooldownText(cd > 0.05f ? Mathf.CeilToInt(cd).ToString() : "");
        }
    }

    /// <summary>自动战斗未接线：隐藏按钮；若仍被点到只 Toast，不改战斗 AI。</summary>
    void BindAutoBattleUnavailable()
    {
        if (autoButton == null) return;
        autoButton.onClick.RemoveListener(OnAutoBattleUnavailableClicked);
        autoButton.onClick.AddListener(OnAutoBattleUnavailableClicked);
        autoButton.gameObject.SetActive(false);
        if (BattleManager.Instance != null)
            BattleManager.Instance.isAutoBattle = false;
    }

    void OnAutoBattleUnavailableClicked()
    {
        if (BattleManager.Instance != null)
            BattleManager.Instance.isAutoBattle = false;
        UIManager.Instance?.ShowToast("未开放");
    }

    /// <summary>
    /// 暂停已收进设置弹窗（SettingsPopupUI：继续/撤离），这里不再维护独立的暂停面板。
    /// 头像/槽位也不再接手动释放：技能由 PlayerSkillPassive / MercSkillCaster 自动放。
    /// </summary>

    /// <summary>更新技能区圆形头像</summary>
    public void UpdateSkillAvatars()
    {
        var mm = MercenaryManager.Instance;
        if (playerSkillAvatar != null)
            playerSkillAvatar.SetAvatar(mm != null ? mm.GetPlayerIcon() : null);

        bool tutorialMerc = TutorialDirector.Instance != null && TutorialDirector.Instance.ShowMercHud;
        if (GameConfig.SOLO_PLAYER_BATTLE && !tutorialMerc) return;

        if (tutorialMerc)
        {
            var mercs = mm != null ? mm.GetActiveMercs() : null;
            Mercenary m0 = (mercs != null && mercs.Count > 0) ? mercs[0] : null;
            // 教程技能圆优先佣兵头像（H011），勿错绑技能图
            string hire = m0 != null && !string.IsNullOrEmpty(m0.hireId)
                ? m0.hireId
                : StoryProgress.TutorialMercHireId;
            Sprite icon = MercPortraitSprites.GetHead(hire)
                ?? (m0 != null && mm != null ? mm.GetIcon(m0.mercId) : null)
                ?? GetMercSkillIcon(m0);
            merc1SkillAvatar?.SetAvatar(icon);
            // 2026-10-06：蓝条只看「这个技能有没有蓝」—— 无蓝技能（物攻/防御/被动）纯 CD，不显示蓝条。
            if (mercSlot1 != null && m0 != null)
                mercSlot1.SetEnergyEnabled(m0.SkillCaster != null && m0.SkillCaster.HasActiveSkill
                                           && m0.SkillCaster.MpType != MpArchetype.None);
            return;
        }

        var mercIds = mm != null ? mm.GetActiveMercIds() : new List<string>();
        var mercsList = mm != null ? mm.GetActiveMercs() : null;
        if (merc1SkillAvatar != null)
            merc1SkillAvatar.SetAvatar(GetMercSkillIconAt(mercsList, 0) ?? GetMercPortraitAt(mm, mercIds, 0));
        if (merc2SkillAvatar != null)
            merc2SkillAvatar.SetAvatar(GetMercSkillIconAt(mercsList, 1) ?? GetMercPortraitAt(mm, mercIds, 1));
    }

    static Sprite GetMercSkillIcon(Mercenary m)
    {
        if (m?.SkillCaster == null || !m.SkillCaster.HasActiveSkill) return null;
        return MercSkillTable.LoadIcon(m.SkillCaster.ActiveSkillId);
    }

    static Sprite GetMercSkillIconAt(List<Mercenary> mercs, int index)
    {
        if (mercs == null || index < 0 || index >= mercs.Count) return null;
        return GetMercSkillIcon(mercs[index]);
    }

    static Sprite GetMercPortraitAt(MercenaryManager mm, List<string> mercIds, int index)
    {
        if (mm == null || mercIds == null || index < 0 || index >= mercIds.Count) return null;
        var mercs = mm.GetActiveMercs();
        if (mercs != null && index < mercs.Count && mercs[index] != null && !string.IsNullOrEmpty(mercs[index].hireId))
            return MercPortraitSprites.GetHead(mercs[index].hireId) ?? mm.GetIcon(mercs[index].mercId);
        var hireIds = mm.GetActiveMercHireIds();
        if (hireIds != null && index < hireIds.Count && !string.IsNullOrEmpty(hireIds[index]))
            return MercPortraitSprites.GetHead(hireIds[index]) ?? mm.GetIcon(mercIds[index]);
        return mm.GetIcon(mercIds[index]);
    }

    /// <summary>
    /// 更新技能头像能量（玩家=0, 佣兵1=1, 佣兵2=2）
    /// </summary>
    public void UpdateSkillEnergy(int skillIndex, float energyRatio)
    {
        // skillIndex 0 = 玩家。**玩家头像下第二条已改为「雷击奥义充能」**（见 SetUltCharge），
        // 这里不能再写 skill 能量，否则两边抢同一个 lanBarFill 会互相覆盖。
        // 玩家 4 个被动技各自有独立能量条（RunSkillBarUI），第二条不必再重复显示技能能量峰值。
        if (skillIndex == 0) return;

        if (skillIndex == 1 && mercSlot1 != null)
        {
            if (!mercSlot1.EnergyEnabled) energyRatio = 0f;
            mercSlot1.SetEnergy(energyRatio);
        }
        else if (skillIndex == 2 && mercSlot2 != null)
        {
            if (!mercSlot2.EnergyEnabled) energyRatio = 0f;
            mercSlot2.SetEnergy(energyRatio);
        }
    }

    /// <summary>
    /// 技槽拖拽结束 → 写回 RunLoadout 并重建技能。
    /// 规则：技能数 &lt; 2 不排（1 个技能默认就是最优先，没什么可排）；
    /// 空槽不参与——有几个技能就只有前几个槽能拖（2 个技能 = 只有前两个能调）。
    /// </summary>
    void OnSkillSlotReordered(int from, int to)
    {
        var ids = RunLoadout.SkillIds();
        int owned = ids != null ? ids.Count : 0;
        if (owned < 2) return;
        if (from < 0 || to < 0 || from >= owned || to >= owned) return;
        if (!RunLoadout.MoveSkill(from, to)) return;

        RunLoadout.Save();
        var dir = RunDraftDirector.Instance;
        if (dir == null && BattleManager.Instance != null)
            dir = RunDraftDirector.Ensure(BattleManager.Instance);
        if (dir != null) dir.RebuildPlayerSkills();
        RunSkillBarUI.Refresh();
        RefreshBattleHud();
    }

    /// <summary>
    /// 按「已拥有技能数」开/关技槽拖拽。
    /// 只有 1 个技能时不可拖；2 个技能时只有前 2 个槽可拖（第 3、4 是空槽）。
    /// 【2026-10-11 主人报「拖拽也不管用 调整不了顺序」→ 根因实锤】：
    /// 旧判据 <c>BattleLootMode.Active</c> 的唯一 Enter 调用点（整理阶段）已于 2026-10-11 删除 →
    /// Active 恒 false → 拖拽**永远禁用**。改为只要有 2 个以上技能就允许拖（战斗中也能调优先级）。
    /// </summary>
    public void RefreshSkillSlotDragState()
    {
        if (runSkillSlots == null) return;
        var ids = RunLoadout.SkillIds();
        int owned = ids != null ? ids.Count : 0;
        bool allow = owned >= 2;
        for (int i = 0; i < runSkillSlots.Count; i++)
        {
            var av = runSkillSlots[i];
            if (av == null || av.root == null) continue;
            var chip = av.root.GetComponent<SkillOrderChip>();
            if (chip == null) continue;
            chip.DragEnabled = allow && i < owned;
        }
    }
}
