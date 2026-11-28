using System.Collections;
using UnityEngine;

/// <summary>
/// 【2026-11-26 主人拍板】强盗三人组的专属战斗行为（老雷眩晕 / 阿刃脆皮换目标 / 夜薇星落+闪现）。
/// <para>设计红线：只作用于 GenerateRing 里由 <see cref="WavePlanner"/> 打标的敌方佣兵，其余佣兵/竞技场/我方
/// 一律 <see cref="BanditRole.None"/>、行为零开销。逐个能力都是「复用现成机制 + 强盗独占开关」，不造新资源：</para>
/// <list type="bullet">
/// <item>老雷命中玩家 → <see cref="UnitBase.ApplyStun"/>：眩晕期间 UnitBase.Update 停 AI + HealCastBar 读条 Cancel，一步实现「僵直 + 断治疗」。带内置 CD 防连晕锁死。</item>
/// <item>阿刃索敌 → <see cref="FindBanditTarget"/> 按「脆皮分」选目标，命中一次后清锁换下一个，实现「随机挑脆皮、打一下换目标」。</item>
/// <item>夜薇 → 主动技 VFX 换 starfall（复用玩家星落演出）；被近身时 <see cref="TryBanditBlink"/> 复用怪物的压扁瞬移三段演出。</item>
/// </list>
/// 数值全部集中在 <see cref="GameConfig"/> 的 BANDIT_* 出口，表无专属列时不造配置，用与同类机制对齐的默认值。
/// </summary>
public partial class Mercenary
{
    /// <summary>强盗角色。None = 普通单位（绝大多数），三档 = 强盗专属行为。</summary>
    public enum BanditRole
    {
        None = 0,
        OldLei = 1,   // 老雷 模型 bandit_boss（数据 id zhongzhan201）：命中玩家眩晕（僵直 + 断治疗）
        Arens = 2,    // 阿刃 模型 bandit_blade（数据 id kuangzhan201）：随机挑脆皮，打一下换目标
        NightWei = 3  // 夜薇 模型 bandit_witch（数据 id fashi202）：星落演出 + 近身闪现
    }

    public BanditRole banditRole { get; private set; } = BanditRole.None;
    public bool IsBandit => banditRole != BanditRole.None;

    /// <summary>
    /// 【2026-11-28 主人「头像跟其他单位一样，哪里需要就用到哪里」】强盗专属头像 id
    /// （MercHead/bandit_*）。按 mercId=assetId 走 <see cref="IntruderPicker.BanditHeadOfAssetId"/>
    /// 唯一映射，不另写 role→id 表；非强盗恒 null，调用方照旧走常规头像链，零影响。
    /// </summary>
    public string BanditHeadId => IsBandit ? IntruderPicker.BanditHeadOfAssetId(mercId) : null;

    /// <summary>强盗头像 Sprite（按路径从 Resources 取）；取不到返回 null，回退或露白由调用方定，不兜底造图。</summary>
    public Sprite BanditHeadSprite
    {
        get
        {
            string id = BanditHeadId;
            return string.IsNullOrEmpty(id) ? null : MercPortraitSprites.GetHead(id);
        }
    }

    // 老雷：眩晕 CD（妝 Time.time，不随切关持久化，重新生成即归零）
    float _banditStunCdUntil;

    // 阿刃：本次交战已打过几次（每命中一次换目标），目标亲和表用最近一次目标排除防反复锁同一脆皮
    int _banditHitsOnTarget;
    UnitBase _banditLastVictim;

    // 夜薇：闪现 CD / 演出互斥
    float _banditBlinkCdUntil;
    bool _banditBlinking;

    /// <summary>生成链在 SetupBattleSkills 后调用；只有强盗打标才会进来。</summary>
    public void ConfigureBandit(BanditRole role)
    {
        banditRole = role;
        _banditStunCdUntil = 0f;
        _banditHitsOnTarget = 0;
        _banditLastVictim = null;
        _banditBlinkCdUntil = 0f;
        _banditBlinking = false;
        // 老雷的眩晕挂普攻命中：与被动 OnPassiveBasicAttackHit 同一 OnAttack 出口，不另造事件。
        if (role == BanditRole.OldLei)
        {
            OnAttack -= OnBanditBasicAttackHit;
            OnAttack += OnBanditBasicAttackHit;
        }
        // 【2026-11-26 主人拍板】按 Boss 出场：补真名供屏幕血条名牌 / 头顶名字条用
        //（强盗生成链原本不调 SetDisplayName，名字条是空的）。
        switch (role)
        {
            case BanditRole.OldLei: SetDisplayName("独眼老雷"); break;
            case BanditRole.Arens: SetDisplayName("阿刃"); break;
            case BanditRole.NightWei: SetDisplayName("夜薇"); break;
        }
        Debug.Log($"[Bandit] 强盗配置 mercId={mercId} role={role}");
    }

    void OnDisable()
    {
        if (banditRole == BanditRole.OldLei)
            OnAttack -= OnBanditBasicAttackHit;
    }

    /// <summary>
    /// 【2026-11-26】对象池复用 / 切关重置：打标与所有计时/计数归零，并摘掉老雷的命中订阅。
    /// 由 <see cref="ResetForReuse"/> 调用 —— 不重置会让复用的普通佣兵残留强盗行为、或闪现协程被
    /// StopAllCoroutines 打断后 _banditBlinking 永久卡 true 再也闪不了。
    /// </summary>
    void ResetBanditState()
    {
        if (banditRole == BanditRole.OldLei)
            OnAttack -= OnBanditBasicAttackHit;
        banditRole = BanditRole.None;
        _banditStunCdUntil = 0f;
        _banditHitsOnTarget = 0;
        _banditLastVictim = null;
        _banditBlinkCdUntil = 0f;
        _banditBlinking = false;
    }

    // =====================================================================
    // 老雷：命中玩家 → 眩晕（僵直 + 断治疗读条）
    // =====================================================================
    void OnBanditBasicAttackHit(UnitBase target, float damage, bool isCrit)
    {
        if (banditRole != BanditRole.OldLei) return;
        if (isDead || target == null || target.isDead || target.isAlly == isAlly) return;
        // 闪避那一下 damage=0 不算命中，不触发眩晕（与 ResolveBasicAttackHit 的 OnAttack 口径一致）
        if (damage <= 0f) return;
        if (Time.time < _banditStunCdUntil) return;
        if (BattleManager.Instance != null && !BattleManager.Instance.UnitsCanAct) return;

        _banditStunCdUntil = Time.time + GameConfig.BANDIT_OLDLEI_STUN_CD;
        // ApplyStun 一处同时做三件事：停 AI（僵直）、清速度、PlayDebuff；
        // HealCastBar.Update 每帧查 IsStunned 会 CancelInternal 打断治疗读条。不重复造打断逻辑。
        target.ApplyStun(GameConfig.BANDIT_OLDLEI_STUN_SEC);
        Debug.Log($"[Bandit] 老雷命中眩晕 target={target.name} " +
                  $"{GameConfig.BANDIT_OLDLEI_STUN_SEC:0.00}s CD={GameConfig.BANDIT_OLDLEI_STUN_CD:0.0}s");
    }

    // =====================================================================
    // 阿刃：随机挑脆皮 + 每命中一次换目标
    // =====================================================================
    /// <summary>脆皮分：血上限低 / 防御低 → 分高。玩家 Hero 优先（主人「随机挑一个脆皮」的核心观感）。</summary>
    float BanditSquishScore(UnitBase u)
    {
        if (u == null || u.isDead) return float.MinValue;
        float maxHp = u.attr != null ? Mathf.Max(1f, u.attr.GetAttr(AttrType.MaxHp)) : 1f;
        float def = u.attr != null ? u.attr.GetAttr(AttrType.Defense) : 0f;
        // 血量倒数为主、防御为辅；不读当前血（否则残血坦克也会变「脆皮」目标而抢戏）
        float score = 10000f / maxHp - def * 0.5f;
        if (u is Hero) score += GameConfig.BANDIT_ARENS_HERO_PRIORITY;
        // 加一点随机扰动 → 同为脆皮时也会「随机挑」，不是永远锁血最少的那个
        score += Random.Range(0f, GameConfig.BANDIT_ARENS_RANDOM_JITTER);
        return score;
    }

    /// <summary>阿刃索敌覆写：在玩家方全体里挑脆皮分最高的，排除上一次目标（打一下就换）。</summary>
    public UnitBase FindBanditTarget(bool seekOnField)
    {
        if (isDead) return null;
        var bm = BattleManager.Instance;
        if (bm == null || bm.allyUnits == null) return null;

        UnitBase best = null;
        float bestScore = float.MinValue;
        var list = bm.allyUnits;
        float detect = GetDetectRange();   // 与 FindNearestEnemyInDetectRange 同一把尺
        for (int i = 0; i < list.Count; i++)
        {
            var u = list[i];
            if (u == null || u.isDead || !u.gameObject.activeInHierarchy) continue;
            if (u.isAlly == isAlly) continue;                       // 绝不打同阵营（与 FindNearestEnemy* 同口径）
            if (!GameConfig.CanAcquireTarget(this, u)) continue;
            if (!seekOnField && !GameConfig.IsInCombatViewport(u)) continue;
            // 索敌距外不锁：否则会挑到屏幕另一端的脆皮、跨屏追人（原最近索敌也有这道闸）
            if (UnitBase.GetCombatDist(this, u) > detect) continue;
            // 排除刚打过的目标 → 命中一次后换下一个脆皮（若场上只有一个目标则仍锁它，不空手）
            if (_banditLastVictim != null && u == _banditLastVictim && list.Count > 1) continue;
            float s = BanditSquishScore(u);
            if (s > bestScore) { bestScore = s; best = u; }
        }
        // 全被排除（只剩上次目标）就回退到它，不空手
        if (best == null && _banditLastVictim != null
            && !_banditLastVictim.isDead && GameConfig.CanAcquireTarget(this, _banditLastVictim))
            best = _banditLastVictim;
        return best;
    }

    /// <summary>
    /// 阿刃普攻命中后调：仅记 victim 为「上次目标」+命中数。真正换目标放在索敌刷新
    /// （<see cref="RefreshAcquireTarget"/> 里比对 _banditLastVictim 清锁），避开攻击动画中改 target 的风险。
    /// </summary>
    public void OnBanditTargetHit(UnitBase victim)
    {
        if (banditRole != BanditRole.Arens) return;
        if (victim == null || victim.isDead) return;
        _banditLastVictim = victim;
        _banditHitsOnTarget++;
    }

    // =====================================================================
    // 夜薇：近身闪现（复用 Monster 的三段遁走演出，不造新 VFX）
    // =====================================================================
    /// <summary>AIUpdate 开头调用。返回 true = 本帧已起闪现、调用方直接 return。</summary>
    public bool TryBanditBlink()
    {
        if (banditRole != BanditRole.NightWei) return false;
        if (isDead || _banditBlinking || Time.time < _banditBlinkCdUntil) return false;
        if (BattleManager.Instance == null || !BattleManager.Instance.UnitsCanAct) return false;
        if (unitAnim != null && unitAnim.InAttackLock) return false;
        // 治疗读条中不闪（与施法定身同口径）
        if (HealCastBar.IsCasting(this)) return false;

        UnitBase threat = FindNearestEnemy();
        if (threat == null || threat.isDead) return false;
        if (UnitBase.GetCombatDist(this, threat) > GameConfig.BANDIT_NIGHTWEI_BLINK_TRIGGER) return false;

        _banditBlinkCdUntil = Time.time + GameConfig.BANDIT_NIGHTWEI_BLINK_CD;
        StartCoroutine(CoBanditBlink(threat));
        return true;
    }

    IEnumerator CoBanditBlink(UnitBase threat)
    {
        _banditBlinking = true;
        // 身体根节点：Mercenary 没有 Monster.MoveRoot，用自身 transform（SPUM 子物体随父缩放）
        Transform body = transform;
        Vector3 scale0 = body.localScale;
        try
        {
            if (rb != null) rb.velocity = Vector2.zero;
            if (unitAnim != null) unitAnim.SetMove(false, facingDir);

            // 落点：远离威胁者的 X 方向，钳回战场可视范围（与 Monster 同口径）
            float fromX = GetCombatX(this);
            float dir = fromX >= GetCombatX(threat) ? 1f : -1f;
            float hop = Random.Range(GameConfig.MONSTER_BLINK_MIN_DIST, GameConfig.MONSTER_BLINK_MAX_DIST);
            BattleManager.GetBattleVisibleX(out float vMin, out float vMax);
            float toX = Mathf.Clamp(fromX + dir * hop, vMin, vMax);
            if (Mathf.Abs(toX - fromX) < GameConfig.MONSTER_BLINK_MIN_DIST)
                toX = Mathf.Clamp(fromX - dir * hop, vMin, vMax);
            if (Mathf.Abs(toX - fromX) < GameConfig.MONSTER_BLINK_MIN_DIST)
                yield break;   // 贴墙挤不出，放弃（CD 已进，下轮再说）

            float toY = FootY;
            float z = body.position.z;
            ApplyFacing(toX >= fromX ? 1 : -1);

            // ① 压扁遁地
            float t = 0f;
            while (t < GameConfig.MONSTER_BLINK_SQUASH_SEC)
            {
                if (isDead || this == null) yield break;
                if (BattleManager.Instance == null || BattleManager.Instance.UnitsCanAct)
                {
                    t += Time.deltaTime;
                    float k = Mathf.Clamp01(t / GameConfig.MONSTER_BLINK_SQUASH_SEC);
                    float sy = Mathf.Lerp(1f, 0f, k * k);
                    body.localScale = new Vector3(scale0.x, scale0.y * sy, scale0.z);
                }
                yield return null;
            }
            body.localScale = new Vector3(scale0.x, 0f, scale0.z);
            PortalGate.PlayTeleportVfx(body.position);

            // ② 隐形挪移：中点换位 + 再放一次传送特效
            t = 0f;
            bool moved = false;
            while (t < GameConfig.MONSTER_BLINK_HIDE_SEC)
            {
                if (isDead || this == null) yield break;
                if (BattleManager.Instance == null || BattleManager.Instance.UnitsCanAct)
                {
                    t += Time.deltaTime;
                    if (!moved && t >= GameConfig.MONSTER_BLINK_HIDE_SEC * 0.5f)
                    {
                        moved = true;
                        GameConfig.SetWorldPosition(body, new Vector3(toX, toY, z));
                        PortalGate.PlayTeleportVfx(new Vector3(toX, toY, z));
                        // threat 可能在闪现演出途中死亡/被销毁：判空回退朝向落点方向，不空引用
                        if (threat != null && !threat.isDead)
                            ApplyFacing(GetCombatX(threat) >= toX ? 1 : -1);
                        else
                            ApplyFacing(toX >= fromX ? 1 : -1);
                    }
                }
                yield return null;
            }

            // ③ 原地复原
            t = 0f;
            while (t < GameConfig.MONSTER_BLINK_RESTORE_SEC)
            {
                if (isDead || this == null) yield break;
                if (BattleManager.Instance == null || BattleManager.Instance.UnitsCanAct)
                {
                    t += Time.deltaTime;
                    float k = Mathf.Clamp01(t / GameConfig.MONSTER_BLINK_RESTORE_SEC);
                    float sy = Mathf.Lerp(0f, 1f, Mathf.Sqrt(k));
                    body.localScale = new Vector3(scale0.x, scale0.y * sy, scale0.z);
                }
                yield return null;
            }
            body.localScale = scale0;

            t = 0f;
            while (t < GameConfig.MONSTER_BLINK_SETTLE_SEC)
            {
                if (isDead || this == null) yield break;
                if (BattleManager.Instance == null || BattleManager.Instance.UnitsCanAct) t += Time.deltaTime;
                yield return null;
            }
        }
        finally
        {
            if (this != null)
            {
                body.localScale = scale0;
                _banditBlinking = false;
            }
        }
    }

    /// <summary>夜薇专属：主动技的 VFX 别名（交给 SkillCastService 在播放时替换）；其它强盗返回 null 走原技能特效。</summary>
    public string BanditSkillVfxAlias(string skillId)
    {
        if (banditRole == BanditRole.NightWei) return GameConfig.BANDIT_NIGHTWEI_STARFALL_VFX_ALIAS;
        return null;
    }
}
