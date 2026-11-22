using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 城镇入口 · 大地图「战斗演出」部分（单一职责：只做到达后的头像对战可视化 + 占领倒计时 + +X）。
/// <para>胜负恒按出发时战力比算好的快照（SlgMarchEntry.myHpFinal/enemyHpFinal），本文件只把血条画出来，
/// 不改任何数值结果（主人拍板：演出 + 战力比定胜负）。演出时长按大陆 10~20 秒。</para>
/// <para>协程宿主用主文件的 _host（MonoBehaviour）；血条走 Image.fillAmount（项目通用做法）。</para>
/// </summary>
public static partial class SlgMapUI
{
    static GameObject _battleOverlay;
    static readonly Image[] _myHeads = new Image[SlgDefs.SquadSize];
    static readonly Image[] _enemyHeads = new Image[SlgDefs.SquadSize];
    static readonly Image[] _myHp = new Image[SlgDefs.SquadSize];
    static readonly Image[] _enemyHp = new Image[SlgDefs.SquadSize];
    static Text _battleStatus;       // "交战中 / 占领中 N / +X"
    static readonly HashSet<string> _performing = new HashSet<string>();

    static void BuildBattleOverlay(Transform canvas)
    {
        _battleOverlay = new GameObject("BattleOverlay", typeof(RectTransform));
        _battleOverlay.transform.SetParent(canvas, false);
        Stretch((RectTransform)_battleOverlay.transform);
        var veil = NewImage("Veil", _battleOverlay.transform, new Color(0f, 0f, 0f, 0.78f));
        Stretch(veil.rectTransform);
        veil.raycastTarget = true;

        var panel = NewImage("Panel", _battleOverlay.transform, new Color(0.10f, 0.09f, 0.12f, 1f));
        var prt = panel.rectTransform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(680f, 380f);
        prt.anchoredPosition = Vector2.zero;

        _battleStatus = NewText("Status", prt, "", 30, TextAnchor.MiddleCenter);
        var srt = _battleStatus.rectTransform;
        srt.anchorMin = new Vector2(0f, 1f); srt.anchorMax = new Vector2(1f, 1f);
        srt.pivot = new Vector2(0.5f, 1f);
        srt.sizeDelta = new Vector2(-40f, 70f);
        srt.anchoredPosition = new Vector2(0f, -14f);

        var myLabel = NewText("MyLabel", prt, "我方", 26, TextAnchor.MiddleCenter);
        PlaceLabel(myLabel.rectTransform, new Vector2(-190f, -46f));
        var enLabel = NewText("EnemyLabel", prt, "守军", 26, TextAnchor.MiddleCenter);
        PlaceLabel(enLabel.rectTransform, new Vector2(190f, -46f));

        for (int i = 0; i < SlgDefs.SquadSize; i++)
        {
            _myHeads[i] = BuildHead(prt, new Vector2(-190f + (i - 1) * 128f, -172f), true, i);
            _enemyHeads[i] = BuildHead(prt, new Vector2(190f + (1 - i) * 128f, -172f), false, i);
        }
        _battleOverlay.SetActive(false);
    }

    static void PlaceLabel(RectTransform rt, Vector2 pos)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(140f, 44f);
        rt.anchoredPosition = pos;
    }

    /// <summary>一个头像 + 头像上的血条（血条底 + 填充 fillAmount）。返回头像 Image 供填 sprite。</summary>
    static Image BuildHead(Transform parent, Vector2 pos, bool mine, int idx)
    {
        var go = new GameObject((mine ? "My" : "Enemy") + idx, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        PlaceCentered(rt, pos, 108f);
        var head = go.GetComponent<Image>();
        head.preserveAspect = true;
        head.raycastTarget = false;

        var bg = NewImage("HpBg", rt, new Color(0.10f, 0.10f, 0.10f, 0.92f));
        var bgrt = bg.rectTransform;
        bgrt.anchorMin = new Vector2(0f, 0f); bgrt.anchorMax = new Vector2(1f, 0f);
        bgrt.pivot = new Vector2(0.5f, 0f);
        bgrt.sizeDelta = new Vector2(4f, 18f);
        bgrt.anchoredPosition = new Vector2(0f, 4f);
        var bgSprite = LoadSprite(SlgDefs.HpBgPath);
        if (bgSprite != null) bg.sprite = bgSprite;

        var fill = NewImage("HpFill", bgrt, mine ? new Color(0.31f, 0.92f, 0.22f, 1f) : new Color(0.92f, 0.22f, 0.31f, 1f));
        var frt = fill.rectTransform;
        Stretch(frt);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        fill.fillAmount = 1f;
        var fillSprite = LoadSprite(mine ? SlgDefs.HpFillPath : SlgDefs.HpEnemyFillPath);
        if (fillSprite != null) fill.sprite = fillSprite;
        // 用 tint 保留阵营色（绿/红），fill 图本身是纯色/白底
        (mine ? _myHp : _enemyHp)[idx] = fill;
        return head;
    }

    /// <summary>Show 后检查到达未结算的行军单，逐条排队播战斗演出（同一条不重复播）。</summary>
    static void BattleOnShow()
    {
        if (_host == null || _battleOverlay == null) return;
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

        _battleOverlay.SetActive(true);
        _battleOverlay.transform.SetAsLastSibling();

        // 填头像：我方=派遣佣兵，敌方=守军派生头像
        for (int i = 0; i < SlgDefs.SquadSize; i++)
        {
            _myHeads[i].sprite = i < m.squad.Count ? MercPortraitSprites.GetHead(m.squad[i]) : null;
            _myHeads[i].color = _myHeads[i].sprite != null ? Color.white : new Color(0.6f, 0.6f, 0.6f, 0.5f);
            _myHp[i].fillAmount = 1f;

            int av = SlgDefs.DefenderAvatars(m.pointId)[i];
            _enemyHeads[i].sprite = AvatarDefs.LoadSprite(av);
            _enemyHeads[i].color = _enemyHeads[i].sprite != null ? Color.white : new Color(0.6f, 0.6f, 0.6f, 0.5f);
            _enemyHp[i].fillAmount = 1f;
        }

        // 战斗演出：血条按快照系数线性下降（前期 10 秒 / 后期最多 20 秒）
        float dur = SlgDefs.BattleShowSecFor(def.Continent);
        bool peaceful = m.peaceful && def.Type == SlgDefs.PointType.City;
        _battleStatus.text = peaceful ? "通商进行中…" : "交战中…";
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
            yield return null;
        }
        for (int i = 0; i < SlgDefs.SquadSize; i++)
        {
            _myHp[i].fillAmount = m.myHpFinal;
            _enemyHp[i].fillAmount = m.enemyHpFinal;
        }

        // 占领倒计时（秒级，unscaled，暂停也走）
        for (int s = SlgDefs.OccupyCountdownSec; s > 0; s--)
        {
            _battleStatus.text = "占领中 " + s;
            yield return new WaitForSecondsRealtime(1f);
        }

        // +X 跳字（结算快照，入账前展示；数值与 SettleMarch 用同一份快照）
        string gain = BuildGainText(m);
        _battleStatus.text = gain;
        yield return new WaitForSecondsRealtime(0.9f);

        SlgSystem.SettleMarch(m.pointId);   // 幂等入账 + 点位冷却
        _battleOverlay.SetActive(false);
        _performing.Remove(m.pointId);
        Refresh();
        BattleOnShow();                     // 继续下一条到达单
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
        _performing.Clear();
        _battleOverlay = null;
    }
}
