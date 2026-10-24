using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 台词气泡：闲聊调度（Update 只做这一件事）、候选登场台词、气泡定位与收起。
/// 【2026-10-23 主人拍板】原 MercenaryRecruitPopupUI.cs 已 1461 行（项目约定 >600 行必须拆），
/// 按职责切成 5 个 partial，本文件是其中之一。代码为原样搬运，逻辑未改。
/// </summary>
public partial class MercenaryRecruitPopupUI : MonoBehaviour
{

    const float AppearBanterChance = 0.6f;
    const float IdleBanterMinSec = 8f;
    const float IdleBanterMaxSec = 14f;

    void Update()
    {
        if (root == null || !root.activeInHierarchy) return;
        if (_infoRoot != null && _infoRoot.activeSelf) return;
        if (Time.unscaledTime < _nextIdleBanterUnscaled) return;
        MaybeToastAppearBanter(force: true, avoidLast: true);
        ScheduleNextIdleBanter();
    }

    void ScheduleNextIdleBanter()
    {
        _nextIdleBanterUnscaled = Time.unscaledTime + UnityEngine.Random.Range(IdleBanterMinSec, IdleBanterMaxSec);
    }

    bool MaybeToastAppearBanter(bool force, bool avoidLast)
    {
        if (_offers == null || _offers.Count == 0) return false;
        if (!force && UnityEngine.Random.value >= AppearBanterChance) return false;

        var pool = new List<int>();
        for (int i = 0; i < _offers.Count; i++)
        {
            if (_offers[i] == null) continue;
            if (MercHireSession.IsAlreadyHired(_offers[i])) continue;
            pool.Add(i);
        }
        if (pool.Count == 0) return false;

        if (avoidLast && !string.IsNullOrEmpty(_lastBanterKey) && pool.Count > 1)
        {
            var others = new List<int>();
            for (int i = 0; i < pool.Count; i++)
            {
                string k = OfferBanterKey(_offers[pool[i]]);
                if (k != _lastBanterKey) others.Add(pool[i]);
            }
            if (others.Count > 0) pool = others;
        }

        int pick = pool[UnityEngine.Random.Range(0, pool.Count)];
        var offer = _offers[pick];
        string key = OfferBanterKey(offer);
        string line = MercLineTable.Pick(key, MercLineTable.Scene.TavernRefresh);
        if (string.IsNullOrEmpty(line)) return false;

        // 【2026-10-17 主人拍板】佣兵说话改走该佣兵卡上方的气泡框，不再占顶部 Toast。
        ShowMercBubble(pick, line);
        _lastBanterKey = key;
        return true;
    }

    static string OfferBanterKey(MercenaryData offer)
    {
        if (offer == null) return "";
        return !string.IsNullOrEmpty(offer.hireId) ? offer.hireId : (offer.mercId ?? "");
    }

    // ==========================================================================
    // 佣兵台词气泡：【2026-10-18 主人拍板「对话框按我预制体的位置走」】台词填进主人在
    // 招募预制体里摆好的「对话框」节点（Root/Frame 下），位置 / 底图 / 字号全以预制体为准，
    // 代码只填字 + 打字机；超长句仍走 SpeechBubbleFit 等比放大（每次先复位基准，不累积）。
    // 旧的「运行时在佣兵卡内自建 MercBubble、写死卡内坐标」整段废弃删除。
    // ==========================================================================

    /// <summary>在弹窗的「对话框」里让佣兵说 line；上一条气泡（若有）立即打断。
    /// cardIndex 仅为兼容旧调用保留，位置不再跟说话佣兵的卡走。</summary>
    void ShowMercBubble(int cardIndex, string line)
    {
        if (string.IsNullOrEmpty(line)) return;
        EnsureMercBubble();
        if (_mercBubbleRoot == null || _mercBubbleText == null) return;
        if (_mercBubbleCo != null) StopCoroutine(_mercBubbleCo);
        _mercBubbleCo = StartCoroutine(CoMercBubble(cardIndex, line));
    }

    bool _mercBubbleBindTried;
    Vector2 _mercBubbleBaseSize;
    // 【2026-10-18 主人口径】对话框 x 跟说话佣兵的卡走：卡0=-100 / 卡1=100 / 卡2=250（Frame 本地坐标）；
    // 纵向保持预制体的 y 不动。长句放大后按 Frame 边界钳制（ApplyBubbleX），绝不出屏外。
    static readonly float[] MercBubbleXByCard = { -100f, 100f, 250f };

    /// <summary>绑主人预制体里的「对话框」（框图 + 子 Text）；找不到报错，不运行时造节点。只绑一次。</summary>
    void EnsureMercBubble()
    {
        if (_mercBubbleRoot != null || _mercBubbleBindTried) return;
        _mercBubbleBindTried = true;
        var node = FindDeep(transform, "对话框");
        if (node == null)
        {
            Debug.LogError("[MercenaryRecruit] 预制体里找不到「对话框」节点，佣兵台词不显示");
            return;
        }
        var rt = node as RectTransform;
        if (rt == null || rt.sizeDelta.x < 8f || rt.sizeDelta.y < 8f)
        {
            Debug.LogError("[MercenaryRecruit] 「对话框」不是合法 RectTransform（或尺寸异常），佣兵台词不显示");
            return;
        }
        _mercBubbleRoot = node.gameObject;
        _mercBubbleBaseSize = rt.sizeDelta;   // SpeechBubbleFit 每次先复位到此基准，长句放大不累积
        _mercBubbleText = node.Find("Text")?.GetComponent<Text>();
        if (_mercBubbleText == null)
            Debug.LogError("[MercenaryRecruit] 「对话框」下没有 Text 子节点，台词没地方填");
        _mercBubbleRoot.SetActive(false);
    }

    IEnumerator CoMercBubble(int cardIndex, string line)
    {
        _mercBubbleRoot.SetActive(true);
        var rt = _mercBubbleRoot.transform as RectTransform;
        string display = SpeechBubbleFit.Apply(rt, _mercBubbleText, line, _mercBubbleBaseSize);
        // SpeechBubbleFit 结尾会把根图强制成 Simple；主人的对话框图是 Sliced，恢复回去防九宫格拉扁
        var rootImg = _mercBubbleRoot.GetComponent<Image>();
        if (rootImg != null) { rootImg.type = Image.Type.Sliced; rootImg.preserveAspect = false; }
        ApplyBubbleX(rt, cardIndex);
        _mercBubbleText.text = "";
        float delay = 1f / Mathf.Max(1f, 18f);
        for (int i = 1; i <= display.Length; i++)
        {
            _mercBubbleText.text = display.Substring(0, i);
            yield return new WaitForSeconds(delay);
        }
        _mercBubbleText.text = display;
        yield return new WaitForSeconds(2.8f);
        HideMercBubble();
        _mercBubbleCo = null;
    }

    /// <summary>
    /// 对话框横向定位：x 取 <see cref="MercBubbleXByCard"/>（说话佣兵所在卡），纵向保持预制体 y 不动。
    /// 长句被 SpeechBubbleFit 放大后，按父级 Frame 宽度左右钳制（各留 6px 余量），保证不出屏外。
    /// </summary>
    void ApplyBubbleX(RectTransform rt, int cardIndex)
    {
        if (rt == null) return;
        int i = Mathf.Clamp(cardIndex, 0, MercBubbleXByCard.Length - 1);
        float frameHalf = 350f;   // 主人 Frame 700 宽 → 本地坐标 ±350；读不到布局时兜底此值
        if (rt.parent is RectTransform p && p.rect.width > 100f)
            frameHalf = p.rect.width * 0.5f;
        float halfW = rt.sizeDelta.x * 0.5f;
        float minX = -frameHalf + halfW + 6f;
        float maxX = frameHalf - halfW - 6f;
        float x = Mathf.Clamp(MercBubbleXByCard[i], minX, maxX);
        rt.anchoredPosition = new Vector2(x, rt.anchoredPosition.y);
    }

    void HideMercBubble()
    {
        if (_mercBubbleCo != null) { StopCoroutine(_mercBubbleCo); _mercBubbleCo = null; }
        if (_mercBubbleRoot != null) _mercBubbleRoot.SetActive(false);
        if (_mercBubbleText != null) _mercBubbleText.text = "";
    }
}
