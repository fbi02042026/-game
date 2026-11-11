using UnityEngine;

/// <summary>
/// 《跑酷小游戏》V1 —— SPUM 单位渲染分部（EndlessRunGame partial，2026-11-11）。
///
/// 规格来源：`Docs/《跑酷小游戏V1复盘整改》_待办任务清单.md` T3（玩家 / 佣兵改 SPUM）+ T7（大小对齐战斗）。
///
/// 【职责唯一】把 `Resources/Units/{spumId}` 预制体实例化进跑酷世界层，并归一渲染参数
/// （层 / 排序 / 缩放 / RectTransform / 动画初始化）。逻辑坐标、判定、槽位一个都不碰。
/// 【不兜底】预制体 / SPUM 组件 / Animator 取不到一律 LogError 并返回 null，绝不造占位图。
/// 【对照模板】战斗侧同款初始化见 `AutoGameInitializer.EnsureHero` / `UnitAnimation.Awake`。
/// </summary>
public partial class EndlessRunGame
{
    const string SPUM_PLAYER_ID = "wanjia";      // 玩家本体形象 = 战斗同款 Units/wanjia

    /// <summary>
    /// SPUM 根缩放：同一 SPUM 单位在战斗 / 跑酷里屏幕像素高度一致。
    /// 战斗相机 orthographicSize=5.4 → 1 world unit ≈ 1280/10.8 ≈ 118.5 逻辑 px；
    /// 跑酷相机 6.4 → 1 world unit = 100 逻辑 px；118.5/100 ≈ 1.185。
    /// 只作用 SPUM 根 transform.localScale，判定 / 槽位 / 射程全在逻辑 px，不受影响。
    /// </summary>
    const float SPUM_UNIT_SCALE = 1.1852f;

    /// <summary>
    /// SPUM 部件自带 sortingOrder 分布在 -100~25（Shadow 最低 -100）；跑酷所有 sprite 同处默认
    /// sortingLayer，平台是 1、道具 2~4，不整体上抬就会被背景(-20)与平台盖住。
    /// </summary>
    const int SPUM_SORT_BASE_PLAYER = 200;
    const int SPUM_SORT_BASE_MERC = 190;

    /// <summary>
    /// 实例化一只 SPUM 单位到玩法世界层。取不到预制体 / 组件 → LogError + 返回 null（调用方自行判空）。
    /// </summary>
    /// <param name="spumId">Resources/Units 下的预制体名（wanjia / dunbing101 …）</param>
    /// <param name="sortBase">排序整体抬升量（SPUM_SORT_BASE_*）</param>
    /// <param name="goName">实例名，便于 Hierarchy 里辨认</param>
    GameObject CreateSpumUnit(string spumId, int sortBase, string goName)
    {
        if (string.IsNullOrEmpty(spumId))
        {
            Debug.LogError("[跑酷] SPUM 实例化收到空 id（goName=" + goName + "）");
            return null;
        }

        var prefab = Resources.Load<GameObject>("Units/" + spumId);
        if (prefab == null)
        {
            Debug.LogError("[跑酷] SPUM 预制体加载失败：Resources/Units/" + spumId);
            return null;
        }

        var go = Instantiate(prefab);
        go.name = goName;
        go.transform.SetParent(_worldRoot.transform, false);

        // SPUM 根是 RectTransform（在 UI 空间录制，sizeDelta 100）；世界空间下把锚点 / pivot 归中心，
        // 否则初始位置会被 UI 布局拉到别处（照 AutoGameInitializer.EnsureHero 的归一处理）。
        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(100f, 100f);
            rt.anchoredPosition3D = Vector3.zero;
        }
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one * SPUM_UNIT_SCALE;

        // 子部件全进玩法层（相机 cullingMask = 1<<_worldLayer，不进层＝看不见）。
        var trs = go.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < trs.Length; i++) trs[i].gameObject.layer = _worldLayer;

        // 排序整体上抬，保证在平台 / 道具之上；不改部件之间的相对顺序（那是角色内部遮挡关系）。
        var srs = go.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
        {
            srs[i].sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            srs[i].sortingOrder += sortBase;
        }

        EnsureSpumReady(go);
        return go;
    }

    /// <summary>SPUM 运行时初始化：补 _anim 引用 → 生成 OverrideController（PlayAnimation 前置）。</summary>
    void EnsureSpumReady(GameObject go)
    {
        var spum = go.GetComponent<SPUM_Prefabs>();
        if (spum == null) spum = go.GetComponentInChildren<SPUM_Prefabs>(true);
        if (spum == null)
        {
            Debug.LogError("[跑酷] SPUM 预制体上没有 SPUM_Prefabs 组件：" + go.name);
            return;
        }

        if (spum._anim == null) spum._anim = go.GetComponent<Animator>();
        if (spum._anim == null) spum._anim = go.GetComponentInChildren<Animator>(true);
        if (spum._anim == null)
        {
            Debug.LogError("[跑酷] SPUM 预制体上没有 Animator：" + go.name);
            return;
        }
        if (spum._anim.runtimeAnimatorController == null)
        {
            Debug.LogError("[跑酷] SPUM Animator 缺 runtimeAnimatorController，动画无法初始化：" + go.name);
            return;
        }

        // 与 UnitAnimation.Awake 同口径：OverrideController 未建则建（PlayAnimation 内部会写它）。
        if (spum.OverrideController == null) spum.OverrideControllerInit();

        // 跑酷是世界向前滚、角色一直在跑 → 默认 MOVE；该预制体没配 MOVE 才退回 IDLE。
        if (!PlaySpumState(go, PlayerState.MOVE)) PlaySpumState(go, PlayerState.IDLE);
    }

    /// <summary>
    /// 切 SPUM 动画状态。返回 false = 该状态没有配动画（PlayAnimation 会对空列表直接越界，
    /// 这里先挡一次，不刷屏报错）。调用方可据此回退到 IDLE。
    /// </summary>
    bool PlaySpumState(GameObject go, PlayerState state)
    {
        if (go == null) return false;
        var spum = go.GetComponent<SPUM_Prefabs>();
        if (spum == null) spum = go.GetComponentInChildren<SPUM_Prefabs>(true);
        if (spum == null || spum.StateAnimationPairs == null) return false;

        if (!spum.StateAnimationPairs.TryGetValue(state.ToString(), out var list)) return false;
        if (list == null || list.Count == 0) return false;

        spum.PlayAnimation(state, 0);
        return true;
    }

    /// <summary>跑酷攻击动作：播一次 ATTACK（到点由 tick 显式切回 MOVE，不停末帧）。</summary>
    public void PlayRunAttack(GameObject go)
    {
        PlaySpumState(go, PlayerState.ATTACK);
    }

    /// <summary>跑酷移动动作：优先 MOVE，该预制体没配 MOVE 才退回 IDLE。</summary>
    public void PlayRunMove(GameObject go)
    {
        if (!PlaySpumState(go, PlayerState.MOVE)) PlaySpumState(go, PlayerState.IDLE);
    }
}
