using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 恭喜升级弹窗（2026-10-25 主人拍板「接玩家升级 · 玩家升级按完成的一次关卡给一次经验」）。
///
/// 预制体 = Resources/Prefabs/Town/恭喜升级（主人摆好：根自带 Canvas，Dim 黑幕带 Button，
/// 升级特效粒子 quan/MagicDust 挂在 effect 节点下、playOnAwake）。代码只写文本
/// （第一行 等级前后；第二行 轮换职业 + 其倾向属性 + 点数，2026-10-25 主人拍板「轮流来」）
/// + 播弹出动效，其余全用预制体本身——不改预制体、不造节点、不动布局尺寸（铁律 20）。
///
/// 弹出动效（2026-10-25 主人口述时序，各段时长为动效常量可调）：
///   ① 「升级」标题从 pos.y=0 猛弹出（scale 0→预制体基准，OutBack 回弹）；
///   ② 紧接着 effect 节点出现（粒子 playOnAwake，靠激活播放）；
///   ③ 「升级」标题 pos.y 从 0 上移到预制体基准位（主人定的 ≈300，真值取预制体）；
///   ④ 「底」节点 CanvasGroup alpha 0→1；
///   ⑤ 「第一行」下子节点按 x 从左到右（等级提升→升级前→箭头→升级后）依次弹出。
/// 几何真值全部取自预制体节点本身（捕获基准→播完精确写回，零布局残留，先例 TownEntranceMotion）。
///
/// 触发 = LevelSystem 通关结算升级（经验唯一入口 = 完成关卡，主线/支线才给），见 LevelSystem.AddStageClearExp。
/// 【2026-10-25 主人拍板「战斗结束回到冒险界面才触发」】结算时刻只挂起（QueueForTown），
/// Town 场景就绪后由 TownSceneBootstrap 调 FlushPending 弹出；中途退游戏挂起即丢（等级已落盘，只丢演出）。
/// 点 Dim（黑幕）随时关闭；一次性演出用完即毁（先例 BattleResultScreen）。
/// 预制体 / 关键节点缺失 LogError 不兜底（AGENTS.md §4）。
/// </summary>
public static class LevelUpPopup
{
    const string PrefabPath = "Prefabs/Town/恭喜升级";

    static GameObject _host;

    // —— 挂起账（2026-10-25 主人拍板「战斗结束回到冒险界面才触发」）——
    // 结算时刻战斗里不弹，只记账；Town 就绪（TownSceneBootstrap 收尾）统一吐出。
    static bool _hasPending;
    static int _pendingBefore;
    static int _pendingAfter;

    // —— 第二行职业轮换（2026-10-25 主人拍板「不要按所选 轮流来 每个职业都要有」）——
    // 不绑 GetSelected：每弹一次按开放职业顺序（PlayerJobDefs.Playable，当前 剑盾卫士→法师→游侠）
    // 轮换一个，轮一圈每个职业都会被展示到。游标只在内存里转，退游戏归零无影响（纯演出）。
    static int _jobCursor;

    /// <summary>
    /// 结算时刻挂起（不弹）。同一局连着多关都升级时合并成一段：before 取最早、after 取最新，只弹一次。
    /// 中途退游戏挂起即丢（升级已立刻落盘，丢的只是演出）。
    /// </summary>
    public static void QueueForTown(int beforeLevel, int afterLevel)
    {
        if (_hasPending)
        {
            _pendingAfter = afterLevel;   // 连升合并：区间一头一尾
            return;
        }
        _hasPending = true;
        _pendingBefore = beforeLevel;
        _pendingAfter = afterLevel;
    }

    /// <summary>
    /// Town 场景就绪时调用（TownSceneBootstrap.BootstrapRoutine 收尾，每次回主界面必经）。
    /// 有挂起才弹，弹完清账；无挂起（登录直接进镇 / 本局没升级 / 失败局）为 no-op。
    /// </summary>
    public static void FlushPending()
    {
        if (!_hasPending) return;
        _hasPending = false;
        Show(_pendingBefore, _pendingAfter);
    }

    /// <summary>弹恭喜升级（升级前 beforeLevel → 升级后 afterLevel）。一关连升多级也只弹一次、区间一步到位。</summary>
    public static void Show(int beforeLevel, int afterLevel)
    {
        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError("[LevelUpPopup] 找不到预制体 Resources/" + PrefabPath + "，升级弹窗不弹（不兜底）");
            return;
        }

        Close();   // 重入保护：旧窗先收（连过关/连升级重弹时保证只有一个）
        _host = Object.Instantiate(prefab);
        _host.name = "LevelUpPopup";
        Object.DontDestroyOnLoad(_host);   // 弹窗开着时结算链可能切场景，防误杀（先例 StaminaConfirmPopup）

        var canvas = _host.GetComponent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[LevelUpPopup] 恭喜升级预制体根上没有 Canvas，弹窗不弹（不兜底）");
            Close();
            return;
        }
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.LevelUpPopup, UICanvasSetup.ResolveUiCamera());

        // 文本绑定（按行作用域取，两行各有一个「升级后」，不能全局按名找）：
        // 第一行 = 等级 1 → 2；第二行 = 轮换职业名 + 其倾向属性名 + 本次升级点数
        var row1 = FindDeep(_host.transform, "第一行");
        if (row1 == null)
            Debug.LogError("[LevelUpPopup] 恭喜升级预制体缺「第一行」节点，等级数字不显示（不兜底）");
        else
        {
            SetText(row1, "升级前", beforeLevel.ToString());
            SetText(row1, "升级后", afterLevel.ToString());
        }
        var row2 = FindDeep(_host.transform, "第二行");
        if (row2 == null)
            Debug.LogError("[LevelUpPopup] 恭喜升级预制体缺「第二行」节点，属性成长不显示（不兜底）");
        else
        {
            // 【2026-10-25 主人拍板「不要按所选 轮流来 每个职业都要有」】
            // 每弹一次按开放职业顺序轮换一个（剑盾卫士→法师→游侠→循环），不按当前所选职业。
            var playable = PlayerJobDefs.Playable;
            var job = playable[_jobCursor % playable.Length];
            _jobCursor = (_jobCursor + 1) % playable.Length;
            SetText(row2, "职业", PlayerJobDefs.Get(job).DisplayName);
            SetText(row2, "属性", LevelSystem.JobTendencyLabel(job));   // 玩家口径：生命/攻速/魔攻（10-25 拍板）
            SetText(row2, "升级后", "+" + Mathf.Max(1, afterLevel - beforeLevel));
        }

        var dim = FindDeep(_host.transform, "Dim");
        var dimBtn = dim != null ? dim.GetComponent<Button>() : null;
        if (dimBtn == null)
        {
            Debug.LogError("[LevelUpPopup] 恭喜升级预制体缺 Dim 节点或 Dim 上没有 Button，弹窗关不掉（不兜底）");
            return;
        }
        dimBtn.onClick.RemoveAllListeners();
        dimBtn.onClick.AddListener(Close);

        // 弹出动效（2026-10-25 主人口述时序，见类注释）
        var motion = _host.GetComponent<LevelUpPopupMotion>();
        if (motion == null) motion = _host.AddComponent<LevelUpPopupMotion>();
        motion.Play();
    }

    /// <summary>关闭并销毁弹窗（Dim 点击 / 重弹前置收窗共用）。</summary>
    public static void Close()
    {
        if (_host == null) return;
        Object.Destroy(_host);
        _host = null;
    }

    static void SetText(Transform scope, string nodeName, string value)
    {
        var node = scope != null ? FindDeep(scope, nodeName) : null;
        var text = node != null ? node.GetComponent<Text>() : null;
        if (text == null)
        {
            Debug.LogError("[LevelUpPopup] 恭喜升级预制体 " + scope.name + " 下缺 " + nodeName + " 文本节点，文案不显示（不兜底）");
            return;
        }
        text.text = value;
    }

    internal static Transform FindDeep(Transform root, string name)
    {
        // 2026-10-28 收敛：实现已统一到 UiFind/UiFactory 唯一真源，此处保留私有签名以免动调用点
        return UiFind.Deep(root, name);
    }
}

/// <summary>
/// 恭喜升级弹出动效驱动（挂在弹窗实例上，随弹窗销毁）。
/// 先例 = TownEntranceMotion：手写协程、unscaledDeltaTime + 钳帧、捕获预制体基准、播完精确写回。
/// </summary>
class LevelUpPopupMotion : MonoBehaviour
{
    // ===== 时序常量（秒，动效参数非布局尺寸；主人想调节奏只动这里）=====
    const float TitlePopDur = 0.22f;     // ① 升级标题猛弹出
    const float EffectAt = 0.20f;        // ② effect 出现（紧跟弹出）
    const float MoveDelay = 0.45f;       // ③ 升级标题上移开始
    const float MoveDur = 0.30f;         //    上移时长（OutBack 收尾轻弹）
    const float BottomFadeDelay = 0.75f; // ④ 底 CanvasGroup 0→1（上移落位后）
    const float BottomFadeDur = 0.25f;
    const float RowStartDelay = 1.00f;   // ⑤ 第一行子节点依次弹出
    const float RowPopDur = 0.18f;
    const float RowStagger = 0.12f;
    /// <summary>单帧时长上限：结算帧可能卡顿，钳住防整段动画一帧跳完（先例 TownEntranceMotion）。</summary>
    const float MaxFrameDt = 0.05f;
    /// <summary>OutBack 回弹强度（轻量档，与 TownEntranceMotion 同值）。</summary>
    const float BackOvershoot = 1.0f;

    RectTransform _title;          // 「升级」标题
    GameObject _effect;            // effect（粒子容器，激活即播）
    CanvasGroup _bottomGroup;      // 「底」
    readonly List<RectTransform> _row1 = new List<RectTransform>();  // 「第一行」子节点，x 从左到右
    readonly List<RectTransform> _row2 = new List<RectTransform>();  // 「第二行」子节点，x 从左到右

    Vector2 _titleBasePos;
    Vector3 _titleBaseScale;
    readonly List<Vector3> _row1BaseScale = new List<Vector3>();
    readonly List<Vector3> _row2BaseScale = new List<Vector3>();

    public void Play()
    {
        CaptureNodes();
        SetStartState();
        StartCoroutine(CoPlay());
    }

    /// <summary>捕获预制体基准（真源），缺关键节点 LogError 并跳过对应段落（fail closed，不造替身）。</summary>
    void CaptureNodes()
    {
        var root = transform;
        var titleT = LevelUpPopup.FindDeep(root, "升级");
        if (titleT == null)
            Debug.LogError("[LevelUpPopup] 恭喜升级预制体缺「升级」标题节点，弹出/上移段跳过（不兜底）");
        else
        {
            _title = titleT as RectTransform;
            _titleBasePos = _title.anchoredPosition;
            _titleBaseScale = _title.localScale;
        }

        var effectT = LevelUpPopup.FindDeep(root, "effect");
        if (effectT == null)
            Debug.LogError("[LevelUpPopup] 恭喜升级预制体缺 effect 节点，特效段跳过（不兜底）");
        else
            _effect = effectT.gameObject;

        var bottomT = LevelUpPopup.FindDeep(root, "底");
        if (bottomT == null)
            Debug.LogError("[LevelUpPopup] 恭喜升级预制体缺「底」节点，底淡入段跳过（不兜底）");
        else
        {
            _bottomGroup = bottomT.GetComponent<CanvasGroup>();
            if (_bottomGroup == null)
                Debug.LogError("[LevelUpPopup] 「底」节点上没有 CanvasGroup，底淡入段跳过（不兜底）");
        }

        var row1T = LevelUpPopup.FindDeep(root, "第一行");
        if (row1T == null)
        {
            Debug.LogError("[LevelUpPopup] 恭喜升级预制体缺「第一行」节点，依次弹出段跳过（不兜底）");
            return;
        }
        CaptureRow(row1T, _row1, _row1BaseScale);

        // 第二行（2026-10-25 主人加）：在第一行播完后接着依次弹出；缺失只跳过本段（老预制体兼容）
        var row2T = LevelUpPopup.FindDeep(root, "第二行");
        if (row2T == null)
            Debug.LogError("[LevelUpPopup] 恭喜升级预制体缺「第二行」节点，第二行弹出段跳过（不兜底）");
        else
            CaptureRow(row2T, _row2, _row2BaseScale);
    }

    /// <summary>收一行子节点：按 x 从左到右（视觉顺序），捕获各自基准 scale（箭头 0.4 这类非 1 基准也正确）。</summary>
    void CaptureRow(Transform rowT, List<RectTransform> list, List<Vector3> scales)
    {
        var kids = new List<RectTransform>();
        for (int i = 0; i < rowT.childCount; i++)
            if (rowT.GetChild(i) is RectTransform rt) kids.Add(rt);
        kids.Sort((a, b) => a.anchoredPosition.x.CompareTo(b.anchoredPosition.x));
        list.AddRange(kids);
        scales.Clear();
        for (int i = 0; i < list.Count; i++) scales.Add(list[i].localScale);
    }

    /// <summary>摆动效起始位（同帧生效，首帧就是出场态，不会闪终态）。</summary>
    void SetStartState()
    {
        if (_title != null)
        {
            _title.anchoredPosition = new Vector2(_titleBasePos.x, 0f);   // 主人口径：pos.y=0 起跳
            _title.localScale = Vector3.zero;
        }
        if (_effect != null) _effect.SetActive(false);   // 粒子 playOnAwake：到拍再激活播放
        if (_bottomGroup != null) _bottomGroup.alpha = 0f;
        for (int i = 0; i < _row1.Count; i++) _row1[i].localScale = Vector3.zero;
        for (int i = 0; i < _row2.Count; i++) _row2[i].localScale = Vector3.zero;
    }

    /// <summary>第二行起始时刻 = 第一行最后一个弹完的瞬间（主人口径「第一行出现后接着出现」）。</summary>
    float Row2StartDelay
        => RowStartDelay + Mathf.Max(0, _row1.Count - 1) * RowStagger + RowPopDur;

    IEnumerator CoPlay()
    {
        bool effectShown = _effect == null;      // 缺节点视为该段已完成，不阻塞收尾
        bool bottomFaded = _bottomGroup == null;
        float endTime = Mathf.Max(
            MoveDelay + MoveDur,
            Mathf.Max(BottomFadeDelay + BottomFadeDur,
                      Row2StartDelay + Mathf.Max(0, _row2.Count - 1) * RowStagger + RowPopDur));

        float t = 0f;
        while (t < endTime)
        {
            t += Mathf.Min(Time.unscaledDeltaTime, MaxFrameDt);

            // ① 升级标题猛弹出（scale 0→基准，OutBack）
            if (_title != null)
            {
                float k = Mathf.Clamp01(t / TitlePopDur);
                _title.localScale = _titleBaseScale * EaseOutBack(k);
            }

            // ② effect 出现（激活即播粒子）
            if (!effectShown && t >= EffectAt)
            {
                _effect.SetActive(true);
                effectShown = true;
            }

            // ③ 升级标题上移（y 0→预制体基准位）
            if (_title != null)
            {
                if (t >= MoveDelay)
                {
                    float k = Mathf.Clamp01((t - MoveDelay) / MoveDur);
                    _title.anchoredPosition = new Vector2(_titleBasePos.x, _titleBasePos.y * EaseOutBack(k));
                }
                else
                {
                    _title.anchoredPosition = new Vector2(_titleBasePos.x, 0f);
                }
            }

            // ④ 底 CanvasGroup 0→1
            if (_bottomGroup != null)
            {
                _bottomGroup.alpha = t >= BottomFadeDelay
                    ? Mathf.Clamp01((t - BottomFadeDelay) / BottomFadeDur)
                    : 0f;
            }

            // ⑤ 第一行子节点依次弹出（x 从左到右，各自 OutBack）
            for (int i = 0; i < _row1.Count; i++)
            {
                float start = RowStartDelay + i * RowStagger;
                float k = Mathf.Clamp01((t - start) / RowPopDur);
                _row1[i].localScale = _row1BaseScale[i] * EaseOutBack(k);
            }

            // ⑥ 第二行子节点接着依次弹出（同款 OutBack，起始 = 第一行播完）
            for (int i = 0; i < _row2.Count; i++)
            {
                float start = Row2StartDelay + i * RowStagger;
                float k = Mathf.Clamp01((t - start) / RowPopDur);
                _row2[i].localScale = _row2BaseScale[i] * EaseOutBack(k);
            }

            yield return null;
        }

        // 终态兜底：精确写回捕获基准（与 TownEntranceMotion 同口径，零残留）
        Finish();
    }

    void Finish()
    {
        if (_title != null)
        {
            _title.anchoredPosition = _titleBasePos;
            _title.localScale = _titleBaseScale;
        }
        if (_bottomGroup != null) _bottomGroup.alpha = 1f;
        for (int i = 0; i < _row1.Count; i++)
            if (_row1[i] != null) _row1[i].localScale = _row1BaseScale[i];
        for (int i = 0; i < _row2.Count; i++)
            if (_row2[i] != null) _row2[i].localScale = _row2BaseScale[i];
    }

    /// <summary>OutBack：1 + (c1+1)·(k-1)^3 + c1·(k-1)^2，k=1 时精确回到 1（回弹不残留）。</summary>
    static float EaseOutBack(float k)
    {
        k = Mathf.Clamp01(k);
        float p = k - 1f;
        return 1f + (BackOvershoot + 1f) * p * p * p + BackOvershoot * p * p;
    }
}
