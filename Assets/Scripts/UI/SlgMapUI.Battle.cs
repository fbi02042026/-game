using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 城镇入口 · 大地图「战斗演出」部分（单一职责：据点建筑上方的双方头像对冲可视化 + 占领倒计时 + +X）。
/// <para>胜负恒按出发时战力比算好的快照（SlgMarchEntry.myHpFinal/enemyHpFinal），本文件只把血条画出来，
/// 不改任何数值结果（主人拍板：演出 + 战力比定胜负）。演出时长按大陆 10~20 秒。</para>
/// <para>演出根节点 BattleClash 一次性创建后隐藏/复用（不每次创建销毁），挂在 MapContent 下的 Battle 层，
/// 随地图缩放平移；协程宿主用主文件的 _host；血条走 Image.fillAmount（项目通用做法）。</para>
/// </summary>
public static partial class SlgMapUI
{
    // 一次性复用的对冲根节点（战斗开始移到 def.Pos 并显示，结束隐藏）
    static GameObject _battleClash;
    static readonly RectTransform[] _myCells = new RectTransform[SlgDefs.SquadSize];
    static readonly RectTransform[] _enemyCells = new RectTransform[SlgDefs.SquadSize];
    static readonly Image[] _myFrames = new Image[SlgDefs.SquadSize];     // 底层：稀有度头像框（每次换图）
    static readonly Image[] _enemyFrames = new Image[SlgDefs.SquadSize];
    static readonly Image[] _myHeads = new Image[SlgDefs.SquadSize];      // 上层：头像图
    static readonly Image[] _enemyHeads = new Image[SlgDefs.SquadSize];
    static readonly Image[] _myHp = new Image[SlgDefs.SquadSize];
    static readonly Image[] _enemyHp = new Image[SlgDefs.SquadSize];
    static Text _battleStatus;       // 交战中 / 通商进行中 / 占领中 N / +X
    static Image _slashA, _slashB;   // 中间交错刀光
    static readonly HashSet<string> _performing = new HashSet<string>();

    /// <summary>据点上方头像格坐标（root 锚在 def.Pos；PointSize=150，建筑中心即 def.Pos）。</summary>
    static Vector2 HeadPos(bool mine, int i)
        => new Vector2(mine ? -78f : 78f, 150f + (2 - i) * 58f);

    /// <summary>在 Battle 层下建一次 BattleClash：我方 3 头像在左、守军 3 头像在右、中间刀光、头顶状态文字。</summary>
    static void BuildBattleOverlay()
    {
        if (_battleLayer == null) return;
        var root = NewRect("BattleClash", _battleLayer);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(4f, 4f);
        root.anchoredPosition = Vector2.zero;
        _battleClash = root.gameObject;

        _battleStatus = NewText("Status", root, "", 24, TextAnchor.MiddleCenter);
        var srt = _battleStatus.rectTransform;
        srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
        srt.pivot = new Vector2(0.5f, 0.5f);
        srt.sizeDelta = new Vector2(280f, 50f);
        srt.anchoredPosition = new Vector2(0f, 330f);

        for (int i = 0; i < SlgDefs.SquadSize; i++)
        {
            _myCells[i] = BuildHead(root, HeadPos(true, i), true, i);
            _enemyCells[i] = BuildHead(root, HeadPos(false, i), false, i);
        }

        _slashA = BuildSlash(root, "SlashA", new Color(1f, 1f, 1f, 0.9f), 38f);
        _slashB = BuildSlash(root, "SlashB", new Color(1f, 0.78f, 0.82f, 0.9f), -38f);

        _battleClash.SetActive(false);
    }

    /// <summary>无 sprite 细长刀光：130×10，斜置 ±38°，循环里做缩放/透明度脉冲。</summary>
    static Image BuildSlash(Transform parent, string name, Color color, float angle)
    {
        var img = NewImage(name, parent, color);
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(130f, 10f);
        rt.anchoredPosition = new Vector2(0f, 208f);
        rt.localEulerAngles = new Vector3(0f, 0f, angle);
        img.raycastTarget = false;
        return img;
    }

    /// <summary>一个头像格：底层=稀有度头像框 104×104（保存引用供换图），上层=头像图 80，底边=血条。返回格子根节点供冲刺位移。</summary>
    static RectTransform BuildHead(Transform parent, Vector2 pos, bool mine, int idx)
    {
        var go = new GameObject((mine ? "My" : "Enemy") + idx, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        PlaceCentered(rt, pos, 104f);
        var frame = go.GetComponent<Image>();       // 先建 → 在下层
        frame.preserveAspect = true;
        frame.raycastTarget = false;
        (mine ? _myFrames : _enemyFrames)[idx] = frame;

        var head = NewImage("Head", rt, Color.white); // 后建 → 在上层
        var hrt = head.rectTransform;
        hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 0.5f);
        hrt.pivot = new Vector2(0.5f, 0.5f);
        hrt.sizeDelta = new Vector2(80f, 80f);
        hrt.anchoredPosition = Vector2.zero;
        head.preserveAspect = true;
        head.raycastTarget = false;
        (mine ? _myHeads : _enemyHeads)[idx] = head;

        var bg = NewImage("HpBg", rt, new Color(0.10f, 0.10f, 0.10f, 0.92f));
        var bgrt = bg.rectTransform;
        bgrt.anchorMin = new Vector2(0f, 0f); bgrt.anchorMax = new Vector2(1f, 0f);
        bgrt.pivot = new Vector2(0.5f, 0f);
        bgrt.sizeDelta = new Vector2(4f, 18f);
        bgrt.anchoredPosition = new Vector2(0f, 4f);
        var bgSprite = LoadSprite(SlgDefs.HpBgPath);
        if (bgSprite != null) bg.sprite = bgSprite;
        bg.raycastTarget = false;

        var fill = NewImage("HpFill", bgrt, mine ? new Color(0.31f, 0.92f, 0.22f, 1f) : new Color(0.92f, 0.22f, 0.31f, 1f));
        var frt = fill.rectTransform;
        Stretch(frt);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        fill.fillAmount = 1f;
        var fillSprite = LoadSprite(mine ? SlgDefs.HpFillPath : SlgDefs.HpEnemyFillPath);
        if (fillSprite != null) fill.sprite = fillSprite;
        fill.raycastTarget = false;
        (mine ? _myHp : _enemyHp)[idx] = fill;
        return rt;
    }

    /// <summary>Show 后检查到达未结算的行军单，逐条排队播战斗演出（同一条不重复播）。</summary>
    static void BattleOnShow()
    {
        if (_host == null || _battleClash == null) return;
        var marches = SlgSystem.Marches();
        for (int i = 0; i < marches.Count; i++)
        {
            var m = marches[i];
            if (SlgSystem.IsArrived(m) && !_performing.Contains(m.pointId))
            {
                _performing.Add(m.pointId);
                _host.StartCoroutine(BattleRoutine(m));
                break;   // 一次只播一场，结算后再检查下一条
            }
        }
    }

    static IEnumerator BattleRoutine(SlgMarchEntry m)
    {
        var def = SlgSystem.FindPoint(m.pointId);
        if (string.IsNullOrEmpty(def.Id)) { _performing.Remove(m.pointId); yield break; }
        if (_battleLayer == null || _battleClash == null) { _performing.Remove(m.pointId); yield break; }

        // 根节点移到据点建筑上方并置顶（一次性复用，不重建）
        var root = (RectTransform)_battleClash.transform;
        root.anchoredPosition = def.Pos;
        _battleClash.SetActive(true);
        _battleClash.transform.SetAsLastSibling();

        bool peaceful = m.peaceful && def.Type == SlgDefs.PointType.City;
        int[] guardAvatars = SlgDefs.DefenderAvatars(m.pointId);
        var guardRarity = RarityForGuard(def.GuardPower);

        // 填头像框（三档稀有度）/ 头像图 / 血条；格子回位
        for (int i = 0; i < SlgDefs.SquadSize; i++)
        {
            string hireId = i < m.squad.Count ? m.squad[i] : null;
            _myFrames[i].sprite = MercHireSession.LoadPortraitFrame(string.IsNullOrEmpty(hireId)
                ? MercRosterDefs.MercRarity.Common : RarityPalette.ResolveMercRarity(hireId));
            _myHeads[i].sprite = string.IsNullOrEmpty(hireId) ? null : MercPortraitSprites.GetHead(hireId);
            _myHeads[i].color = _myHeads[i].sprite != null ? Color.white : new Color(0.6f, 0.6f, 0.6f, 0.5f);
            _myHp[i].fillAmount = 1f;
            _myCells[i].anchoredPosition = HeadPos(true, i);

            _enemyFrames[i].sprite = MercHireSession.LoadPortraitFrame(guardRarity);
            _enemyHeads[i].sprite = AvatarDefs.LoadSprite(guardAvatars[i]);
            _enemyHeads[i].color = _enemyHeads[i].sprite != null ? Color.white : new Color(0.6f, 0.6f, 0.6f, 0.5f);
            _enemyHp[i].fillAmount = 1f;
            _enemyCells[i].anchoredPosition = HeadPos(false, i);
        }

        _battleStatus.text = peaceful ? "通商进行中…" : "交战中…";
        _slashA.gameObject.SetActive(!peaceful);
        _slashB.gameObject.SetActive(!peaceful);

        // 战斗演出：血条按快照系数线性下降（前期 10 秒 / 后期最多 20 秒）；通商不播刀光/冲刺
        float dur = SlgDefs.BattleShowSecFor(def.Continent);
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            for (int i = 0; i < SlgDefs.SquadSize; i++)
            {
                _myHp[i].fillAmount = Mathf.Lerp(1f, m.myHpFinal, k);
                _enemyHp[i].fillAmount = Mathf.Lerp(1f, m.enemyHpFinal, k);
            }
            if (!peaceful)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 9f);
                var sa = _slashA.rectTransform;
                var sb = _slashB.rectTransform;
                sa.localScale = sb.localScale = new Vector3(Mathf.Lerp(0.72f, 1.16f, pulse), 1f, 1f);
                var ca = _slashA.color; ca.a = Mathf.Lerp(0.35f, 0.95f, pulse); _slashA.color = ca;
                var cb = _slashB.color; cb.a = Mathf.Lerp(0.35f, 0.95f, 1f - pulse); _slashB.color = cb;
                float phase = Mathf.Repeat(t * 1.5f, 1f);
                float push = 8f * (phase < 0.5f ? phase * 2f : (1f - phase) * 2f);
                for (int i = 0; i < SlgDefs.SquadSize; i++)
                {
                    _myCells[i].anchoredPosition = HeadPos(true, i) + new Vector2(push, 0f);
                    _enemyCells[i].anchoredPosition = HeadPos(false, i) + new Vector2(-push, 0f);
                }
            }
            if (_battleLayer == null || _battleClash == null) { _performing.Remove(m.pointId); yield break; }
            yield return null;
        }
        for (int i = 0; i < SlgDefs.SquadSize; i++)
        {
            _myHp[i].fillAmount = m.myHpFinal;
            _enemyHp[i].fillAmount = m.enemyHpFinal;
            _myCells[i].anchoredPosition = HeadPos(true, i);
            _enemyCells[i].anchoredPosition = HeadPos(false, i);
        }

        // 占领倒计时（秒级，unscaled，暂停也走）
        for (int s = SlgDefs.OccupyCountdownSec; s > 0; s--)
        {
            if (_battleLayer == null || _battleClash == null) { _performing.Remove(m.pointId); yield break; }
            _battleStatus.text = "占领中 " + s;
            yield return new WaitForSecondsRealtime(1f);
        }

        // +X 跳字（结算快照，入账前展示；数值与 SettleMarch 用同一份快照）
        string gain = BuildGainText(m);
        _battleStatus.text = gain;
        yield return new WaitForSecondsRealtime(0.9f);
        if (_battleLayer == null || _battleClash == null) { _performing.Remove(m.pointId); yield break; }

        SlgSystem.SettleMarch(m.pointId);   // 幂等入账 + 点位冷却
        _battleClash.SetActive(false);
        _performing.Remove(m.pointId);
        Refresh();
        BattleOnShow();                     // 继续下一条到达单
    }

    /// <summary>守军头像框稀有度：按守军战力分档（守军不是佣兵，不查佣兵表，也不碰装备 Rarity）。</summary>
    static MercRosterDefs.MercRarity RarityForGuard(int guardPower)
    {
        if (guardPower < 120) return MercRosterDefs.MercRarity.Common;
        if (guardPower < 200) return MercRosterDefs.MercRarity.Rare;
        return MercRosterDefs.MercRarity.Legendary;
    }

    static string BuildGainText(SlgMarchEntry m)
    {
        var sb = new System.Text.StringBuilder("+  ");
        bool any = false;
        if (m.merit > 0) { sb.Append("战功 ").Append(m.merit).Append("  "); any = true; }
        if (m.ore > 0) { sb.Append("矿 ").Append(m.ore).Append("  "); any = true; }
        if (m.stones > 0) { sb.Append("石材 ").Append(m.stones).Append("  "); any = true; }
        if (m.food > 0) { sb.Append("粮草 ").Append(m.food).Append("  "); any = true; }
        if (m.dropItems != null)
            for (int i = 0; i < m.dropItems.Count; i++)
            {
                var e = m.dropItems[i];
                if (e == null || string.IsNullOrEmpty(e.id) || e.value <= 0) continue;
                var idef = ItemDefs.Get(e.id);
                sb.Append(idef != null ? idef.name : e.id).Append(" ").Append(e.value).Append("  ");
                any = true;
            }
        return any ? sb.ToString() : "+  无功而返";
    }

    static void BattleReset()
    {
        if (_battleClash != null) _battleClash.SetActive(false);
        _performing.Clear();
        _battleLayer = null;
    }
}
