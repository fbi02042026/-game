using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 挂机向雷击奥义：击杀充能，满条自动释放。
/// 总开关 GameConfig.THUNDER_ULT_ENABLED（当前关，等后期装备技能再开）。
/// </summary>
public class HeroThunderUltimate : Singleton<HeroThunderUltimate>
{
    int _charge;
    int _need = GameConfig.THUNDER_ULT_NEED_MIN;
    bool _casting;
    Coroutine _castCo;
    /// <summary>教程内仅第一次雷击走压暗电影感。</summary>
    bool _tutorialCinematicDone;

    Button _btn;

    GameObject _sceneDimGo;
    SpriteRenderer _sceneDimSr;
    static Sprite _whiteSprite;

    public float ChargeRatio => _need <= 0 ? 0f : Mathf.Clamp01(_charge / (float)_need);
    public bool IsCasting => _casting;
    public bool IsReady => !_casting && _charge >= _need;

    protected override void Awake()
    {
        base.Awake();
    }

    void Update()
    {
        if (!GameConfig.THUNDER_ULT_ENABLED) return;
        // 对话冻结时攒满：恢复行动后自动放
        if (!IsReady) return;
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
        _charge = 0;
        if (GameConfig.THUNDER_ULT_ENABLED)
            RecalcNeed();
        else
            _need = GameConfig.THUNDER_ULT_NEED_MIN;
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

    public void RecalcNeed()
    {
        int ch = ChapterManager.Instance != null ? ChapterManager.Instance.currentChapter : 1;
        int st = 0;
        if (BattleManager.Instance != null && BattleManager.Instance.currentStage != null)
            st = BattleManager.Instance.currentStage.stageIndex;
        _need = GameConfig.GetThunderUltNeedPoints(ch, st);
        if (_charge > _need) _charge = _need;
        RefreshUi();
    }

    /// <summary>击杀记账：小怪 1 / 精英 3 / Boss 6。</summary>
    public void OnMonsterKilled(Monster m)
    {
        if (!GameConfig.THUNDER_ULT_ENABLED) return;
        if (m == null || _casting) return;
        if (BattleManager.Instance == null || !BattleManager.Instance.isInBattle) return;

        int pts = 1;
        if (m.IsBossUnit) pts = 6;
        else if (m.IsEliteWave) pts = 3;

        _charge = Mathf.Min(_need, _charge + pts);
        RefreshUi();

        if (_charge >= _need)
            TryAutoCast();
    }

    public bool TryManualCast()
    {
        if (!GameConfig.THUNDER_ULT_ENABLED) return false;
        if (!IsReady) return false;
        return BeginCast();
    }

    void TryAutoCast()
    {
        if (!GameConfig.THUNDER_ULT_ENABLED) return;
        if (!IsReady) return;
        BeginCast();
    }

    bool BeginCast()
    {
        if (!GameConfig.THUNDER_ULT_ENABLED) return false;
        if (_casting) return false;
        var bm = BattleManager.Instance;
        var hero = bm != null ? bm.hero : Hero.Instance;
        if (hero == null || hero.isDead) return false;
        if (bm != null && !bm.UnitsCanAct)
            return false;

        _casting = true;
        _charge = 0;
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
        float atk = hero.attr != null ? hero.attr.GetAttr(AttrType.Attack) : 10f;
        float raw = Mathf.Max(1f, atk * dmgMul);
        float strikeGap = tutorialCinematic ? 0.28f : 0.18f;

        for (int i = 0; i < strikes; i++)
        {
            UnitBase target = PickRandomEnemy();
            if (target == null) break;

            Vector3 hitPos = target.GetHitPosition();
            BattleVFXSystem.Instance?.PlayLeiji(hitPos);
            float dmg = DamageFormula.FinalHit(raw, target.attr, false);
            target.TakeDamage(dmg, false, false, true, hero.GetVfxFacingDir(), hero);

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
        // 无 UI：不创建按钮
        HideUltButton();
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
