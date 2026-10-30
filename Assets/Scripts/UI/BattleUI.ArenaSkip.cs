using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 竞技场一键跳过（2026-10-30 主人拍板「是否可以加个一键跳过」）：
/// 仅 <see cref="BattleModeId.Arena"/> 的真战斗显示「跳过」键；BattleUI.prefab 无此节点（16:55 dump 确认）
/// → 运行时补节点（UI 运行时补节点先例）；主人日后在 prefab 摆了同名「跳过」节点则直接绑用
///（16:55 拍板「已经有的就按有的效果来」）。
/// 点击 → <see cref="Time.timeScale"/> 提到 <see cref="SkipTimeScale"/> 跑到结算（胜负仍由真实战斗产出，
/// 禁掷骰不扫荡）；恢复唯一出口 <see cref="Restore"/>（幂等）：
/// ① 胜利统一门（BattleManager.FinishStageAfterPortalReached 竞技场分支）
/// ② 死亡收尾（BattleManager.FinalizeHeroDeath 竞技场分支）
/// ③ 主动撤离（BattleManager.TriggerEvacuation）
/// ④ BattleUI.OnDestroy（切场景 / 异常兜底，必经）。
/// </summary>
public static class ArenaBattleSkip
{
    /// <summary>跳过加速倍率（具名常量可调，拍板口径 5~10×；跑到结算自动恢复）。</summary>
    public const float SkipTimeScale = 6f;

    /// <summary>跳过按钮节点名（prefab 同名节点即视为主人手摆，直接绑用）。</summary>
    const string NodeName = "跳过";

    static bool _accelerating;
    static GameObject _btnGo;

    /// <summary>确保按钮存在并按模式切显隐（幂等；BattleUI.EnsureBattleControls 每次进战斗调）。
    /// 非竞技场战斗：运行时建的按钮本不存在，prefab 同名节点若有则藏掉。</summary>
    public static void Ensure(Transform uiRoot)
    {
        if (uiRoot == null) return;
        var bm = BattleManager.Instance;
        bool arena = bm != null && bm.Mode != null && bm.Mode.Id == BattleModeId.Arena;
        if (!arena)
        {
            var any = uiRoot.Find(NodeName);
            if (any != null) any.gameObject.SetActive(false);
            return;
        }

        if (_btnGo == null)
        {
            var prefabNode = UiFind.Deep(uiRoot, NodeName);   // prefab 已有 → 用主人的（不重复建）
            if (prefabNode != null)
            {
                _btnGo = prefabNode.gameObject;
                var pb = _btnGo.GetComponent<Button>();
                if (pb == null) pb = _btnGo.AddComponent<Button>();
                pb.onClick.RemoveAllListeners();
                pb.onClick.AddListener(OnSkipClicked);
            }
            else
            {
                _btnGo = BuildRuntimeButton(uiRoot);
            }
        }
        if (_btnGo != null) _btnGo.SetActive(true);
    }

    /// <summary>运行时补节点：右上角 TopBar 之下的半透明黑底小钮（运行时建树，不动 prefab）。</summary>
    static GameObject BuildRuntimeButton(Transform uiRoot)
    {
        var img = UiFactory.NewImage(uiRoot, NodeName, new Color(0f, 0f, 0f, 0.55f));
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-24f, -200f);   // 右上角、TopBar（高 120）之下，与设置钮错开
        rt.sizeDelta = new Vector2(120f, 60f);

        var lbl = UiFactory.NewText(img.transform, "Label", "跳过", 24, TextAnchor.MiddleCenter);
        var lblRt = lbl.rectTransform;
        lblRt.anchorMin = Vector2.zero;
        lblRt.anchorMax = Vector2.one;
        lblRt.offsetMin = Vector2.zero;
        lblRt.offsetMax = Vector2.zero;
        GameFonts.ApplyToHierarchy(img.transform);

        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(OnSkipClicked);
        return img.gameObject;
    }

    /// <summary>点「跳过」：战斗加速跑到结算（不掷骰不扫荡，胜负仍由真实战斗产出）。</summary>
    static void OnSkipClicked()
    {
        if (_accelerating) return;
        _accelerating = true;
        Time.timeScale = SkipTimeScale;
        if (_btnGo != null) _btnGo.SetActive(false);
        Debug.Log($"[ArenaSkip] 跳过：timeScale → {SkipTimeScale}×（跑到结算自动恢复）");
    }

    /// <summary>恢复 timeScale=1（唯一出口，幂等）。所有退出路径（结算/死亡/撤离/OnDestroy）都会调。</summary>
    public static void Restore()
    {
        if (!_accelerating) return;
        _accelerating = false;
        Time.timeScale = 1f;
        Debug.Log("[ArenaSkip] 恢复 timeScale=1");
    }

    /// <summary>BattleUI.OnDestroy 调：恢复加速 + 清按钮引用（下一局重建；切场景/异常兜底必经）。</summary>
    public static void OnBattleUiDestroyed()
    {
        Restore();
        _btnGo = null;
    }
}
