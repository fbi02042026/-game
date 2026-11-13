using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 挂机向雷击奥义：独立 CD 计时，冷却归零自动释放（2026-11-13 主人拍板）。
/// THUNDER_ULT_ENABLED 只作 HUD 充能条隐藏判据（恒 false），不再闸功能。
/// </summary>
public class HeroThunderUltimate : Singleton<HeroThunderUltimate>, ICombatBoundSingleton
{
    float _cdRemaining;   // 独立 CD 剩余秒数（Time.deltaTime 轴）
    bool _casting;
    Coroutine _castCo;
    /// <summary>教程内仅第一次雷击走压暗电影感。</summary>
    bool _tutorialCinematicDone;

    Button _btn;

    GameObject _sceneDimGo;
    SpriteRenderer _sceneDimSr;
    static Sprite _whiteSprite;

    // CD 制下充能条恒隐藏（THUNDER_ULT_ENABLED=false）。以下 4 个属性只保留给
    // BattleUI.CharacterBar.RefreshPlayerUltBar 编译用，不参与任何战斗判定。
    public float ChargeRatio => 0f;
    public bool IsCasting => _casting;
    public bool IsReady => !_casting && _cdRemaining <= 0f;
    public int Charge => 0;
    public int Need => GameConfig.THUNDER_ULT_NEED_MIN;

    protected override void Awake()
    {
        base.Awake();
    }

    void Update()
    {
        // 2026-11-13：改独立 CD。对话/结算冻结期间 Time.deltaTime 同步停走，恢复行动后再放。
        if (_cdRemaining > 0f) _cdRemaining -= Time.deltaTime;
        if (_casting || _cdRemaining > 0f) return;
        var bm = BattleManager.Instance;
        if (bm == null || !bm.isInBattle || !bm.UnitsCanAct) return;
        TryAutoCast();
    }

    public void EnsureBattleUi()
    {
        // 雷击按钮隐藏：只保留充能 + 自动释放
        HideUltButton();
    }

    public void ResetForBattle()
    {
        if (_castCo != null)
        {
            StopCoroutine(_castCo);
            _castCo = null;
        }
        RestoreSceneDim();
        _casting = false;
        _tutorialCinematicDone = false;
        _cdRemaining = GameConfig.THUNDER_ULT_CD;
        HideUltButton();
    }

    void HideUltButton()
    {
        if (_btn != null)
        {
            _btn.gameObject.SetActive(false);
            return;
        }
        // 兼容：上一局已创建过的节点
        var battleUi = BattleUI.Instance;
        if (battleUi == null) return;
        var existing = battleUi.transform.Find("ThunderUltButton");
        if (existing != null)
            existing.gameObject.SetActive(false);
    }

    public bool TryManualCast()
    {
        return BeginCast();
    }

    void TryAutoCast()
    {
        if (_casting || _cdRemaining > 0f) return;
        BeginCast();
    }

    bool BeginCast()
    {
        if (_casting) return false;
        var bm = BattleManager.Instance;
        var hero = bm != null ? bm.hero : Hero.Instance;
        if (hero == null || hero.isDead) return false;
        if (bm != null && !bm.UnitsCanAct)
            return false;

        _casting = true;
        _cdRemaining = GameConfig.THUNDER_ULT_CD;
        RefreshUi();
        _castCo = StartCoroutine(CoCast(hero));
        return true;
    }

    IEnumerator CoCast(Hero hero)
    {
        var bm = BattleManager.Instance;
        bool tutorialCinematic = bm != null && bm.IsTutorialRun && !_tutorialCinematicDone;
        bool restoreAct = false;

        if (tutorialCinematic)
        {
            if (bm != null && bm.UnitsCanAct)
            {
                bm.UnitsCanAct = false;
                restoreAct = true;
            }

            // 场景压暗（背景层），单位/特效保持原亮度
            ApplySceneDim();
            AttackVfxKit kit = hero.GetWeaponVfxKit();
            hero.PlayAttackAnimOnly(kit, true);
            CombatJuice.Instance?.PlaySwingSfx();
            // 短停顿即落雷，避免「全场黑一秒才出特效」
            yield return new WaitForSecondsRealtime(0.12f);
            _tutorialCinematicDone = true;
        }

        int ch = ChapterManager.Instance != null ? ChapterManager.Instance.currentChapter : 1;
        int st = bm != null && bm.currentStage != null ? bm.currentStage.stageIndex : 0;
        float dmgMul = GameConfig.GetThunderUltDamageMul(ch, st, tutorialCinematic);

        int strikes = Random.Range(3, 6); // 3~5
        // 2026-09-29：按职业取攻击力 —— 法师/牧师的攻击力在 MagicAttack 上，
        // 读 Attack 会拿到一个自己根本不用的物攻值。
        AttrType atkKey = PlayerJobBaseStats.CurrentAttackAttr();
        float atk = hero.attr != null ? hero.attr.GetAttr(atkKey) : 10f;
        float raw = Mathf.Max(1f, atk * dmgMul);
        float strikeGap = tutorialCinematic ? 0.28f : 0.18f;

        for (int i = 0; i < strikes; i++)
        {
            UnitBase target = PickRandomEnemy();
            if (target == null) break;

            Vector3 hitPos = target.GetHitPosition();
            BattleVFXSystem.Instance?.PlayLeiji(hitPos);
            // 【2026-10-24 修双重扣防】原码先 FinalHit 一次再 TakeDamage，而 TakeDamage 内部
            // （UnitBase.Damage.cs:39）会再 FinalHit 一次 —— 防御被扣两遍（法师的雷霆更是
            // 物防/魔防各扣一遍），2026-10-24 新加的类型克制加成也会算两次。属本次核查新发现，
            // 已顺手修：直接传 raw，由 TakeDamage 统一结算（扣防/类型克制只走一处）。
            // ⚠ 行为变化：雷霆单发伤害会明显变高（原被多扣一遍防），过强再调 GetThunderUltDamageMul。
            target.TakeDamage(raw, false, false, true, hero.GetVfxFacingDir(), hero);

            yield return new WaitForSecondsRealtime(strikeGap);
        }

        if (tutorialCinematic)
            RestoreSceneDim();

        if (restoreAct && bm != null && bm.isInBattle)
            bm.UnitsCanAct = true;

        _casting = false;
        _castCo = null;
        RefreshUi();
    }

    UnitBase PickRandomEnemy()
    {
        var bm = BattleManager.Instance;
        if (bm == null || bm.monsters == null) return null;
        UnitBase pick = null;
        int n = 0;
        for (int i = 0; i < bm.monsters.Count; i++)
        {
            var u = bm.monsters[i];
            if (u == null || u.isDead) continue;
            n++;
            if (Random.Range(0, n) == 0)
                pick = u;
        }
        return pick;
    }

    /// <summary>
    /// 在 map(10) 与单位(15) 之间铺半透明黑幕，不改任何单位 Sprite 颜色。
    /// </summary>
    void ApplySceneDim()
    {
        RestoreSceneDim();
        EnsureSceneDim();
        if (_sceneDimGo == null || _sceneDimSr == null) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        float halfH = cam.orthographicSize;
        float halfW = halfH * Mathf.Max(0.2f, cam.aspect);
        Vector3 p = cam.transform.position;
        p.z = 0f;
        _sceneDimGo.transform.position = p;
        // 1x1 sprite → 铺满镜头
        _sceneDimGo.transform.localScale = new Vector3(halfW * 2.2f, halfH * 2.2f, 1f);
        _sceneDimGo.SetActive(true);

        Color c = _sceneDimSr.color;
        c.a = Mathf.Clamp01(1f - GameConfig.THUNDER_ULT_DIM); // DIM=0.35 → alpha≈0.65
        _sceneDimSr.color = c;
    }

    void EnsureSceneDim()
    {
        if (_sceneDimGo != null && _sceneDimSr != null) return;

        _sceneDimGo = new GameObject("ThunderSceneDim");
        Object.DontDestroyOnLoad(_sceneDimGo);
        _sceneDimSr = _sceneDimGo.AddComponent<SpriteRenderer>();
        _sceneDimSr.sprite = GetWhiteSprite();
        _sceneDimSr.color = new Color(0f, 0f, 0f, 0f);
        _sceneDimSr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
        // 压在地图之上、单位之下，人物/怪不吃暗
        _sceneDimSr.sortingOrder = GameConfig.SORT_MAPROOT + 2;
        _sceneDimGo.SetActive(false);
    }

    void RestoreSceneDim()
    {
        if (_sceneDimGo != null)
            _sceneDimGo.SetActive(false);
    }

    static Sprite GetWhiteSprite()
    {
        if (_whiteSprite != null) return _whiteSprite;
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        tex.hideFlags = HideFlags.HideAndDontSave;
        _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        _whiteSprite.hideFlags = HideFlags.HideAndDontSave;
        return _whiteSprite;
    }

    void EnsureUi()
    {
        // 产品要求：战斗不显示雷击按钮（充能与自动释放仍走逻辑）
        HideUltButton();
    }

    void RefreshUi()
    {
        // 无按钮 UI：奥义自动释放，不提供手动按钮
        HideUltButton();
        // 玩家头像下第二条 = 奥义充能；只刷这一条，不牵动整个角色栏
        BattleUI.Instance?.RefreshPlayerUltBar();
    }

    protected override void OnDestroy()
    {
        RestoreSceneDim();
        if (_sceneDimGo != null)
        {
            Object.Destroy(_sceneDimGo);
            _sceneDimGo = null;
            _sceneDimSr = null;
        }
        base.OnDestroy();
    }
}
