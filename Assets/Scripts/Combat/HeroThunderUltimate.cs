using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 挂机向雷击奥义：击杀充能，满条自动释放（也可点按钮）。
/// 与头像治疗/配置技能（playerSkillEnergy）分离。
/// </summary>
public class HeroThunderUltimate : Singleton<HeroThunderUltimate>
{
    int _charge;
    int _need = GameConfig.THUNDER_ULT_NEED_MIN;
    bool _casting;
    Coroutine _castCo;
    /// <summary>教程内仅第一次雷击走压暗+拉镜电影感。</summary>
    bool _tutorialCinematicDone;

    Button _btn;
    Image _fill;
    Text _label;
    CanvasGroup _btnCg;

    readonly List<SpriteRenderer> _dimmed = new List<SpriteRenderer>(128);
    readonly List<Color> _dimmedColors = new List<Color>(128);

    public float ChargeRatio => _need <= 0 ? 0f : Mathf.Clamp01(_charge / (float)_need);
    public bool IsCasting => _casting;
    public bool IsReady => !_casting && _charge >= _need;

    protected override void Awake()
    {
        base.Awake();
    }

    void Update()
    {
        // 对话冻结时攒满：恢复行动后自动放
        if (!IsReady) return;
        var bm = BattleManager.Instance;
        if (bm == null || !bm.isInBattle || !bm.UnitsCanAct) return;
        TryAutoCast();
    }

    public void EnsureBattleUi()
    {
        EnsureUi();
        RefreshUi();
    }

    public void ResetForBattle()
    {
        if (_castCo != null)
        {
            StopCoroutine(_castCo);
            _castCo = null;
        }
        RestoreDim();
        _casting = false;
        _tutorialCinematicDone = false;
        _charge = 0;
        RecalcNeed();
        EnsureUi();
        RefreshUi();
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
        if (!IsReady) return false;
        return BeginCast();
    }

    void TryAutoCast()
    {
        if (!IsReady) return;
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

            ApplyDimKeepHero(hero);
            var cam = Object.FindObjectOfType<CameraFollow>();
            cam?.BeginKillCamZoom(GameConfig.THUNDER_ULT_ZOOM_MUL, GameConfig.THUNDER_ULT_ZOOM_IN);
            Object.FindObjectOfType<ParallaxBackground>()?.ApplyKillCamZoomMul(GameConfig.THUNDER_ULT_ZOOM_MUL);
            BattleUI.ApplyKillCamHudCompensation(GameConfig.THUNDER_ULT_ZOOM_MUL);
            MonsterHealthBar.SetKillCamHidden(true);
            BattleBossHpBar.SetKillCamHidden(true);

            AttackVfxKit kit = hero.GetWeaponVfxKit();
            hero.PlayAttackAnimOnly(kit, true);
            CombatJuice.Instance?.PlaySwingSfx();

            yield return new WaitForSecondsRealtime(GameConfig.THUNDER_ULT_ZOOM_IN + 0.15f);

            cam?.ForceResetKillCamZoom();
            Object.FindObjectOfType<ParallaxBackground>()?.ResetKillCamZoom();
            BattleUI.ResetKillCamHudCompensation();
            MonsterHealthBar.SetKillCamHidden(false);
            BattleBossHpBar.SetKillCamHidden(false);

            yield return new WaitForSecondsRealtime(0.08f);
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
            RestoreDim();

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

    void ApplyDimKeepHero(Hero hero)
    {
        RestoreDim();
        var srs = Object.FindObjectsOfType<SpriteRenderer>();
        Transform heroRoot = hero != null ? hero.transform : null;
        for (int i = 0; i < srs.Length; i++)
        {
            var sr = srs[i];
            if (sr == null) continue;
            if (heroRoot != null && sr.transform.IsChildOf(heroRoot))
                continue;
            _dimmed.Add(sr);
            _dimmedColors.Add(sr.color);
            Color c = sr.color;
            c.r *= GameConfig.THUNDER_ULT_DIM;
            c.g *= GameConfig.THUNDER_ULT_DIM;
            c.b *= GameConfig.THUNDER_ULT_DIM;
            sr.color = c;
        }
    }

    void RestoreDim()
    {
        for (int i = 0; i < _dimmed.Count; i++)
        {
            if (_dimmed[i] != null)
                _dimmed[i].color = _dimmedColors[i];
        }
        _dimmed.Clear();
        _dimmedColors.Clear();
    }

    void EnsureUi()
    {
        if (_btn != null) return;
        var battleUi = BattleUI.Instance;
        if (battleUi == null) return;

        Transform parent = battleUi.transform;
        var go = new GameObject("ThunderUltButton", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.sizeDelta = new Vector2(110f, 110f);
        rt.anchoredPosition = new Vector2(-24f, 180f);

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.12f, 0.14f, 0.22f, 0.92f);
        bg.raycastTarget = true;

        var fillGo = new GameObject("Fill", typeof(RectTransform));
        fillGo.transform.SetParent(go.transform, false);
        var fillRt = fillGo.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = new Vector2(8f, 8f);
        fillRt.offsetMax = new Vector2(-8f, -8f);
        _fill = fillGo.AddComponent<Image>();
        _fill.color = new Color(0.45f, 0.75f, 1f, 0.85f);
        _fill.type = Image.Type.Filled;
        _fill.fillMethod = Image.FillMethod.Radial360;
        _fill.fillOrigin = (int)Image.Origin360.Top;
        _fill.fillClockwise = true;
        _fill.raycastTarget = false;

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        var lrt = labelGo.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;
        _label = labelGo.AddComponent<Text>();
        _label.alignment = TextAnchor.MiddleCenter;
        _label.fontSize = 22;
        _label.color = Color.white;
        _label.raycastTarget = false;
        _label.text = "雷击";
        if (GameFonts.GetChinese() != null)
            _label.font = GameFonts.GetChinese();

        _btn = go.AddComponent<Button>();
        _btn.targetGraphic = bg;
        _btn.onClick.AddListener(() => TryManualCast());

        _btnCg = go.AddComponent<CanvasGroup>();
        GameFonts.ApplyToHierarchy(go.transform);
    }

    void RefreshUi()
    {
        EnsureUi();
        if (_fill != null)
            _fill.fillAmount = ChargeRatio;
        if (_label != null)
        {
            if (_casting)
                _label.text = "雷击";
            else if (IsReady)
                _label.text = "就绪";
            else
                _label.text = $"雷击\n{_charge}/{_need}";
        }
        if (_btn != null)
            _btn.interactable = IsReady;
        if (_btnCg != null)
            _btnCg.alpha = _casting ? 0.55f : 1f;
    }

    void OnDestroy()
    {
        RestoreDim();
    }
}
