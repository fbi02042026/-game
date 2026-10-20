using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 「获得佣兵」全屏演出（2026-10-20 主人拍板）：
/// 背景淡入 → 立绘冲击登场（大→过冲→落定）→ 立绘呼吸浮动 → 脚下名字淡入上移 → 点击任意处关闭。
///
/// 预制体：Assets/Resources/Prefabs/Town/MercGainCinematic（Tools → 生成获得佣兵演出预制体 生成/重建）。
/// 节点：BG（全屏底）/ Stand（立绘，pivot 底部中心）/ NamePlate（脚下名字）。
/// 【位置以预制体为准】动画只改 alpha / scale / 相对偏移，落定值 = 预制体里摆的值，
/// 主人挪完节点代码不会掰回去；名字颜色由代码按稀有度刷（RarityPalette 真源），预制体里改色无效。
/// </summary>
public class MercGainCinematicUI : MonoBehaviour
{
    const string PrefabPath = "Prefabs/Town/MercGainCinematic";

    const float BgFadeIn = 0.25f;      // 背景淡入
    const float NameDelay = 0.15f;     // 立绘落定后名字延迟
    const float NameRise = 0.3f;       // 名字上移淡入时长
    const float AutoCloseAfter = 8f;   // 无点击兜底自动关（防卡流程）

    Transform _stand;
    Text _name;
    CanvasGroup _bgCg, _standCg, _nameCg;
    ParticleSystem[] _bgFx;   // 主人在 BG 节点下摆的特效，界面出现时一起播放
    Vector2 _nameBasePos;
    float _bgTargetAlpha;
    Coroutine _co;
    System.Action _onClosed;
    bool _closing;

    /// <summary>播一场获得佣兵演出；关闭（点击 / 超时）后回调 onClosed。预制体缺失时直接回调不阻塞流程。
    /// subtitle 非空时在名字下方显示副标题（引导入队用「已加入小队」；领取场景不传）。
    /// 【2026-10-20 主人拍板】默认发放的 3 白（路加/马库斯/米娅）**永远不弹演出**——他们开局就在队，
    /// 没有"获得"仪式；此闸加在入口，任何接线点都不会误弹。</summary>
    public static void Show(string hireId, System.Action onClosed, string subtitle = null)
    {
        string resolved = MercPortraitSprites.ResolveHireId(hireId) ?? hireId;
        if (System.Array.IndexOf(SaveData.DefaultUnlockedMercIds, resolved) >= 0)
        {
            onClosed?.Invoke();
            return;
        }
        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[MercGainCinematic] 找不到预制体 {PrefabPath}——先跑 Tools → 生成获得佣兵演出预制体");
            onClosed?.Invoke();
            return;
        }
        var go = Object.Instantiate(prefab);
        var ui = go.GetComponent<MercGainCinematicUI>() ?? go.AddComponent<MercGainCinematicUI>();
        ui.Play(hireId, onClosed, subtitle);
    }

    void Play(string hireId, System.Action onClosed, string subtitle = null)
    {
        _onClosed = onClosed;
        var canvas = GetComponent<Canvas>();
        if (canvas != null)
            UICanvasSetup.RefreshPopup(canvas, GameConfig.UiSort.MercGainCinematic);

        var bg = transform.Find("BG");
        _stand = transform.Find("Stand");
        var nameTr = transform.Find("NamePlate");
        if (bg == null || _stand == null || nameTr == null)
        {
            Debug.LogError("[MercGainCinematic] 预制体缺 BG/Stand/NamePlate 节点，演出跳过");
            Done();
            return;
        }

        // 副标题（可选）：挂在 NamePlate 子节点，随名字一起淡入上移；null / 空 → 隐藏
        var subTr = nameTr.Find("SubTitle");
        if (subTr != null)
        {
            var subTxt = subTr.GetComponent<Text>();
            bool hasSub = !string.IsNullOrEmpty(subtitle);
            if (subTxt != null) subTxt.text = subtitle;
            subTr.gameObject.SetActive(hasSub);
        }

        _bgCg = EnsureGroup(bg.gameObject, out _bgTargetAlpha);
        _standCg = EnsureGroup(_stand.gameObject, out _);
        _nameCg = EnsureGroup(nameTr.gameObject, out _);

        // BG 下特效：关掉 Play On Awake、清空残留，等 CoPlay 开场统一播（2026-10-20 主人拍板）
        _bgFx = bg.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in _bgFx)
        {
            var m = ps.main;
            m.playOnAwake = false;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        _name = nameTr.GetComponent<Text>();
        _nameBasePos = nameTr.GetComponent<RectTransform>().anchoredPosition;

        ApplyPortrait(hireId);
        ApplyName(hireId);

        var btn = bg.GetComponent<Button>() ?? bg.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(Close);

        _co = StartCoroutine(CoPlay());
    }

    void ApplyName(string hireId)
    {
        if (_name == null) return;
        bool hasRoster = MercRosterDefs.TryGetByHireId(hireId, out var rd);
        string fullName = hasRoster && !string.IsNullOrEmpty(rd.Name) ? rd.Name : hireId;
        _name.text = fullName;
        var rarity = hasRoster ? rd.Rarity : MercRosterDefs.MercRarity.Common;
        var c = RarityPalette.Get(rarity);
        c.a = 1f;
        _name.color = c;
    }

    /// <summary>
    /// 立绘按 hireId 刷（2026-10-20 修·主人报「人物立绘不对」）：预制体 Stand 摆的是 H011
    /// 路加的静态图，此前代码只刷名字从不换图 → 获得谁都显示路加。统一入口
    /// MercPortraitSprites.GetStand（与酒馆招募卡/图鉴同源，预制体摆的 H011 也是这套）；
    /// 只换 sprite 不动排版。取不到 LogError 并清空，不留上一次的图（同 MercObtainPopupUI 口径）。
    /// </summary>
    void ApplyPortrait(string hireId)
    {
        if (_stand == null) return;
        var img = _stand.GetComponent<Image>();
        if (img == null)
        {
            Debug.LogError("[MercGainCinematic] Stand 节点上没有 Image，立绘不显示");
            return;
        }
        var sp = MercPortraitSprites.GetStand(hireId);
        if (sp == null)
            Debug.LogError("[MercGainCinematic] 取不到佣兵立绘 " + hireId +
                           "（Resources/Icons/MercStand/ 下缺图，需主人补图）");
        img.sprite = sp;
        img.enabled = sp != null;
    }

    static CanvasGroup EnsureGroup(GameObject go, out float targetAlpha)
    {
        var img = go.GetComponent<Image>();
        targetAlpha = img != null ? img.color.a : 1f;
        var cg = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        return cg;
    }

    IEnumerator CoPlay()
    {
        float t = 0f;
        // ⓪ BG 特效随界面出现一起播放
        foreach (var ps in _bgFx)
            if (ps != null && ps.gameObject.activeInHierarchy)
                ps.Play(true);
        // ① 背景先出现
        while (t < BgFadeIn) { t += Time.unscaledDeltaTime; _bgCg.alpha = _bgTargetAlpha * Mathf.Clamp01(t / BgFadeIn); yield return null; }
        _bgCg.alpha = _bgTargetAlpha;

        // ② 立绘直接静止显示（2026-10-20 主人拍板：立绘不要动态，不再播冲击登场/呼吸浮动）
        _standCg.alpha = 1f;

        // ③ 名字从脚下淡入上移
        yield return new WaitForSecondsRealtime(NameDelay);
        yield return StartCoroutine(CoNameIn());

        // ④ 等点击 / 超时
        float wait = 0f;
        while (!_closing && wait < AutoCloseAfter)
        {
            wait += Time.unscaledDeltaTime;
            yield return null;
        }
        if (!_closing) Close();
    }

    IEnumerator CoNameIn()
    {
        float t = 0f;
        while (t < NameRise)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / NameRise);
            float ease = 1f - (1f - u) * (1f - u);
            _nameCg.alpha = ease;
            var rt = _name.transform.GetComponent<RectTransform>();
            rt.anchoredPosition = _nameBasePos + new Vector2(0f, (1f - ease) * 26f);
            yield return null;
        }
        _nameCg.alpha = 1f;
        _name.transform.GetComponent<RectTransform>().anchoredPosition = _nameBasePos;
    }

    /// <summary>点击任意处 / 超时 → 全体快速淡出 → 回调。</summary>
    public void Close()
    {
        if (_closing) return;
        _closing = true;
        if (_co != null) { StopCoroutine(_co); _co = null; }
        StartCoroutine(CoClose());
    }

    IEnumerator CoClose()
    {
        float t = 0f;
        const float dur = 0.2f;
        float bg0 = _bgCg != null ? _bgCg.alpha : 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float u = 1f - Mathf.Clamp01(t / dur);
            if (_bgCg != null) _bgCg.alpha = bg0 * u;
            if (_standCg != null) _standCg.alpha = u;
            if (_nameCg != null) _nameCg.alpha = u;
            yield return null;
        }
        Done();
    }

    void Done()
    {
        var cb = _onClosed;
        _onClosed = null;
        Destroy(gameObject);
        cb?.Invoke();
    }
}
