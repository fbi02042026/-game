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
        RestoreDim();
        _casting = false;
        _tutorialCinematicDone = false;
        _charge = 0;
        RecalcNeed();
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
            // 击杀/雷击拉镜已关闭：教程雷击也不再 BeginKillCamZoom
            AttackVfxKit kit = hero.GetWeaponVfxKit();
            hero.PlayAttackAnimOnly(kit, true);
            CombatJuice.Instance?.PlaySwingSfx();

            yield return new WaitForSecondsRealtime(GameConfig.THUNDER_ULT_ZOOM_IN + 0.15f);

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
        RestoreDim();
        base.OnDestroy();
    }
}
