using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

// 怪物单位 partial —— 2026-10-17 由 Monster.cs 纯位移拆出（无逻辑改动）
// 职责：主动技能（充能/预警/精英跳跃/结算/弹道）、攻击风格（SwingStyle）、预警图元生成
public partial class Monster
{
    private string _skillId;
    private bool _canUseActiveSkill;
    private float _skillEnergy = 0f;
    private float _skillCooldown = 0f;
    private MonsterAttackStyle _swingStyle = MonsterAttackStyle.Melee;
    bool _skillTelegraphing;
    /// <summary>【2026-10-17 主人拍板】精英蓄力跳滞空中：AI 提前返回时跳过归道（ApplyLaneY），
    /// 否则身体 Y 每帧被拉回 FootY、跳跃演出起不来。置位/复位只在 CoSkillTelegraphThenCast 内。</summary>
    bool _leaping;
    /// <summary>【2026-10-20 主人拍板】远程专属反应技「瞬移遁走」演出中：AI 提前返回（不追人、
    /// 不归道、不普攻），置位在触发判定、复位只在 CoMonsterBlinkAway 的 finally。</summary>
    bool _teleporting;
    /// <summary>瞬移冷却倒计时（秒），AIUpdate 每帧递减；触发时置满，放弃落点时清零允许下帧重试。</summary>
    float _blinkCd;
    // 【2026-10-20 主人拍板】远程精英/Boss 专属双弹幕技：扇形弹幕 / 追踪弹幕（各 20s 冷却）
    float _fanVolleyCd;
    float _homingBarrageCd;
    /// <summary>是否携带扇形弹幕：远程 Boss 恒带；远程精英随机二选一（2026-10-20 主人拍板，Init 里定）。</summary>
    bool _fanVolleyOwned;
    /// <summary>是否携带追踪弹幕：远程 Boss 恒带；远程精英只在与扇形二选一时才带。</summary>
    bool _homingBarrageOwned;
    /// <summary>扇形弹幕单发弹道缩放（比单体技能弹略小，5 发同屏不糊）。</summary>
    const float FanProjectileScale = 1.3f;
    static Sprite _circleSprite;
    static Sprite _ringSprite;
    const float SkillProjectileScale = 1.6f;
    // 2026-09-21：MONSTER_SKILL_PROJECTILE_SPEED_MUL 已迁 combat_tuning 表（运行时读），
    // 右值不再是编译期常量 → const 改 static readonly（仅此一处引用，行为不变）。
    static readonly float SkillProjectileSpeedMul = GameConfig.MONSTER_SKILL_PROJECTILE_SPEED_MUL;

    MonsterAttackStyle ResolveSwingStyle(UnitBase target)
    {
        // 【2026-10-19 主人拍板】Boss 只允许一种攻击方式：表上是远程就全程远程（贴脸也射），
        // 近战就全程近战 —— 不做「贴脸临时改挥砍」的兜底，杜绝又远程又近战的混用。
        return _attackStyle;
    }

    /// <summary>?????Boss ??????????</summary>
    /// <summary>?????????????Bow=???Ranged=????/summary>
    protected override AttackVfxKit GetAttackVfxKit()
    {
        if (!_isBossUnit)
            return MonsterAttackStyleTable.GetVfxKit(_attackStyle);
        return MonsterAttackStyleTable.GetVfxKit(_swingStyle);
    }

    void UseActiveSkill(UnitBase primaryTarget)
    {
        _skillEnergy = 0f;
        _skillCooldown = (_isBossUnit || _eliteWave) ? 2.5f + 3f : 5f;
        // Attack() 已 ResolveSwingStyle；此处复用（Boss _swingStyle = 表 style，全程一种攻击方式）

        var skill = SkillRegistry.Instance?.GetActiveSkill(_skillId);
        float mult = skill != null ? skill.damageMultiplier : 2.2f;
        float extra = skill != null ? skill.baseDamage : 0f;
        float tier = (_isBossUnit || _eliteWave) ? 1f : GameConfig.MONSTER_NORMAL_SKILL_DAMAGE_MUL;
        float damage = (GetMainAttackStat() * mult + extra) * tier;
        float radius = skill != null && skill.aoeRadius > 0 ? skill.aoeRadius : 5f;
        float telegraph = GameConfig.BOSS_PHASE1_TELEGRAPH;

        if (_isBossUnit && GetBossPhase() >= 2)
        {
            damage *= GameConfig.BOSS_PHASE2_DAMAGE_MUL;
            radius *= GameConfig.BOSS_PHASE2_RADIUS_MUL;
            telegraph = GameConfig.BOSS_PHASE2_TELEGRAPH;
            // 【2026-10-19 修B】原「阶段 2 强制近战」是无效代码：这里改的 _swingStyle
            // 会被下次 Attack() 开头重新 ResolveSwingStyle 覆盖，等于没写，已删。
            // 阶段 2 的近远节奏由表 style 统一决定（一种攻击方式），不再单独干预。
        }
        else if (_eliteWave)
        {
            telegraph = _eliteGlass ? GameConfig.ELITE_GLASS_TELEGRAPH : GameConfig.ELITE_TANK_TELEGRAPH;
        }

        AttackVfxKit kit = MonsterAttackStyleTable.GetVfxKit(_isBossUnit ? _swingStyle : _attackStyle);

        if (_isBossUnit)
        {
            var skillCfg = SkillRegistry.Instance?.Get(_skillId);
            if (skillCfg != null && skillCfg.attackKit != AttackVfxKit.None)
                kit = skillCfg.attackKit;
            // 【2026-10-20 主人拍板「砸地与远程互斥」】Boss 技能形态必须与攻击风格同边：
            // 近战 Boss 的砸地绝不放远程弹道 —— 表配 Bow/Orb 也不采用，回退近战刀光；
            // 远程 Boss 全程弹道（本就不跳不砸地）。治疗套与互斥无关，不纠偏。
            bool styleRanged = MonsterAttackStyleTable.IsRanged(_swingStyle);
            if (kit != AttackVfxKit.Heal && SkillNaming.IsRangedKit(kit) != styleRanged)
                kit = MonsterAttackStyleTable.GetVfxKit(_swingStyle);
        }

        // 精英 / Boss 分流【2026-10-20 主人拍板「砸地与远程互斥」补全】：红圈砸地预警是近战专属演出
        // —— 远程风格不走红圈蓄力（主人报：远程怪也出红圈、原地站桩 2~3 秒像在攒槽，观感混乱），
        //     直接瞬发弹道（与普通远程怪同一条瞬发链路，弹道飞行本身就是反应窗口）；
        // 近战保持「红圈预警 → 蓄力压扁/跳砸 → 砸地结算」不变。
        if ((_isBossUnit || _eliteWave) && !MonsterAttackStyleTable.IsRanged(_swingStyle))
        {
            StartCoroutine(CoSkillTelegraphThenCast(primaryTarget, damage, radius, kit, telegraph));
            return;
        }

        ExecuteActiveSkillNow(primaryTarget, damage, radius, kit);
    }

    IEnumerator CoSkillTelegraphThenCast(UnitBase primaryTarget, float damage, float radius, AttackVfxKit kit, float telegraphSec)
    {
        _skillTelegraphing = true;
        // 【2026-10-17 主人拍板】只有精英加「蓄力压扁 → 高跳快落」演出；Boss 节奏一律不动。
        // 【2026-10-20 主人拍板「砸地与远程互斥」】本协程只接待近战：远程精英/Boss 在 UseActiveSkill
        // 分流瞬发弹道、根本不进来。近战精英蓄力跳，落地直接砸地近战结算（kit 同为近战刀光，
        // 绝不出弹道）—— 修复引导精英「跳到最高下落还放远程、落点拖沓」。
        bool eliteLeap = _eliteWave && !_isBossUnit && !MonsterAttackStyleTable.IsRanged(_attackStyle);
        Vector3 center = primaryTarget != null
            ? new Vector3(primaryTarget.transform.position.x, GROUND_Y + 0.02f, primaryTarget.transform.position.z)
            : new Vector3(transform.position.x, GROUND_Y + 0.02f, transform.position.z);

        // 【2026-10-19 主人拍板「圆心锁定+统一红圈」】精英/Boss 预警一律画目标点红圈（取消脚下扇形）：
        // 伤害圆心 = 本红圈位置，预警期跑出圈即真躲开 —— 演出与判定同位（修复「躲了照样挨打」）。
        GameObject disc = CreateTelegraphDisc(center, radius);
        // 【2026-10-16 主人报「引导对白中精英还攻击了一次」】根因：预警协程一旦启动不感知冻场，
        // TalkBlock 冻场后照样倒数、到点直接结算。修法：冻场（UnitsCanAct=false）期间不计时，
        // warn 计满仍冻着就继续等，解冻瞬间才砸 —— 与普攻入口（Attack 的 UnitsCanAct 检查）口径一致。
        // 顺带（2026-10-16 主人要求「是个圈+扩散效果」）：预警期加呼吸脉冲，砸下瞬间加波纹扩散。
        Vector3 baseScale = disc != null ? disc.transform.localScale : Vector3.one;
        // 蓄力底账：身体缩放（精英 ×2.0 根缩放含在内）与玩家影子 —— 任何退出路径都在 finally 里还原
        Vector3 bodyScale0 = MoveRoot.localScale;
        Transform shadowRoot = Hero.Instance != null ? Hero.Instance.transform : null;
        if (eliteLeap)
        {
            // 蓄力开始：玩家影子 alpha ×0.8（主人原话「玩家的shadow透明降20%」，落地还原）
            UnitShadowAlpha.SetPlayerShadowDim(shadowRoot);
        }
        float t = 0f;
        float pulseT = 0f;
        float warn = Mathf.Max(0.5f, telegraphSec);
        try
        {
            while (t < warn || (BattleManager.Instance != null && !BattleManager.Instance.UnitsCanAct))
            {
                if (isDead || this == null) yield break;   // 收尾统一走 finally
                // 冻场不倒数（红圈原地等待），解冻才继续计时
                if (BattleManager.Instance == null || BattleManager.Instance.UnitsCanAct)
                    t += Time.deltaTime;
                pulseT += Time.deltaTime;
                if (disc != null)
                {
                    if (eliteLeap)
                    {
                        // 主人原话「红圈慢慢放大 有半透效果」：从 35% 线性放大到全尺寸
                        // （半透走 CreateTelegraphDisc 自带的 alpha 0.85，不另改颜色）
                        float grow = Mathf.Lerp(GameConfig.ELITE_TELEGRAPH_GROW_FROM, 1f, Mathf.Clamp01(t / warn));
                        disc.transform.localScale = new Vector3(baseScale.x * grow, baseScale.y * grow, 1f);
                    }
                    else
                    {
                        // 呼吸脉冲：基准 scale 上叠 ±10% 正弦脉动（不受冻场影响；disc 为正缩放，直接乘安全）
                        // 【2026-10-19 主人反馈红圈要更显眼】±5% → ±10%
                        float pulse = 1f + 0.10f * Mathf.Sin(pulseT * 7f);
                        disc.transform.localScale = new Vector3(baseScale.x * pulse, baseScale.y * pulse, 1f);
                    }
                }
                // 蓄力压扁：身体 Y 随预警进度线性压到 ELITE_LEAP_SQUASH（主人原话「慢慢缩小Y轴」；
                // X/Z 不动，精英 ×2.0 根缩放不破坏；JY 光环是根直子节点不受影响）
                if (eliteLeap && MoveRoot != null)
                {
                    float sq = Mathf.Lerp(1f, GameConfig.ELITE_LEAP_SQUASH, Mathf.Clamp01(t / warn));
                    MoveRoot.localScale = new Vector3(bodyScale0.x, bodyScale0.y * sq, bodyScale0.z);
                }
                yield return null;
            }
            if (disc != null) { Destroy(disc); disc = null; }

            if (!isDead && gameObject.activeInHierarchy)
            {
                if (eliteLeap)
                {
                    // 蓄力结束 → 起跳：身体弹回原比例，抛物线高跳快落到目标点（升慢降快）
                    _leaping = true;
                    if (rb != null) rb.velocity = Vector2.zero;
                    if (MoveRoot != null) MoveRoot.localScale = bodyScale0;
                    yield return CoEliteLeapArc(center);
                }
                // 砸下瞬间：红圈位置一圈波纹快速外扩、渐隐
                StartCoroutine(CoTelegraphBurst(center, radius * 2f));
                // 预警结束、技能真正释放瞬间触发一次震屏（仅首次释放；Boss 阶段2 连砸不再震）
                CombatJuice.Shake(
                    _isBossUnit ? GameConfig.BOSS_SKILL_RELEASE_SHAKE_AMP : GameConfig.ELITE_SKILL_RELEASE_SHAKE_AMP,
                    _isBossUnit ? GameConfig.BOSS_SKILL_RELEASE_SHAKE_DUR : GameConfig.ELITE_SKILL_RELEASE_SHAKE_DUR);
                // 落地还原玩家影子（主人拍板：蓄力期降 20%，落地恢复）
                UnitShadowAlpha.RestorePlayerShadowDim(shadowRoot);
                ExecuteActiveSkillNow(primaryTarget, damage, radius, kit, center);
                // Boss 阶段 2：连砸第二下（0.35s 间隙同样感知冻场，防演出中插刀）
                if (_isBossUnit && GetBossPhase() >= 2 && !MonsterAttackStyleTable.IsRanged(_swingStyle))
                {
                    float t2 = 0f;
                    while (t2 < 0.35f)
                    {
                        if (isDead || this == null) break;
                        if (BattleManager.Instance == null || BattleManager.Instance.UnitsCanAct)
                            t2 += Time.deltaTime;
                        yield return null;
                    }
                    if (!isDead)
                        ExecuteActiveSkillNow(primaryTarget, damage * 0.75f, radius * 0.85f, kit, center);
                }
            }
        }
        finally
        {
            // 协程任何路径退出（冻场中被杀 / 被停协程 / 正常收尾）都收尾干净：
            // 红圈物体销毁（否则死亡中断时预警盘泄漏常驻）、身体与影子还原
            // —— 参照 2026-10-17 波次闸门 try/finally 教训，标志位绝不许卡死
            if (disc != null) Destroy(disc);
            if (this != null && MoveRoot != null)
                MoveRoot.localScale = bodyScale0;
            UnitShadowAlpha.RestorePlayerShadowDim(shadowRoot);
            _leaping = false;
            _skillTelegraphing = false;
        }
    }

    /// <summary>
    /// 精英蓄力跳的滞空段（2026-10-17 主人拍板）：从当前位置抛物线跳到 center.x 落地。
    /// 升段 EaseOut（跳得高）、降段 EaseIn 平方（落得迅速）。只动 MoveRoot（与入场走位同一节点），
    /// 落点 Y = FootY 保持自身车道不串排；期间 AI 因 _leaping 提前返回，不与归道/追敌打架。
    /// </summary>
    IEnumerator CoEliteLeapArc(Vector3 center)
    {
        var body = MoveRoot;
        if (body == null) yield break;

        Vector3 from = body.position;
        float toX = center.x;
        float toY = FootY;
        float z = from.z;

        if (rb != null) rb.velocity = Vector2.zero;
        // 起跳朝向落点
        if (Mathf.Abs(toX - from.x) > 0.05f)
        {
            facingDir = toX > from.x ? 1 : -1;
            ApplyFacing(facingDir);
        }

        float rise = Mathf.Max(0.05f, GameConfig.ELITE_LEAP_RISE_SEC);
        float fall = Mathf.Max(0.05f, GameConfig.ELITE_LEAP_FALL_SEC);

        // ① 上升：EaseOut（起步猛、到顶收住），顶点高 = 落点上方 ELITE_LEAP_HEIGHT。
        // 冻场感知与预警同口径：UnitsCanAct=false 期间原地悬停不前移，解冻才继续跳。
        float t = 0f;
        while (t < rise)
        {
            if (isDead || this == null) yield break;
            if (BattleManager.Instance == null || BattleManager.Instance.UnitsCanAct)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / rise);
                float e = 1f - (1f - k) * (1f - k);
                float x = Mathf.Lerp(from.x, toX, k);
                float y = Mathf.Lerp(from.y, toY, k) + Mathf.Sin(e * Mathf.PI * 0.5f) * GameConfig.ELITE_LEAP_HEIGHT;
                GameConfig.SetWorldPosition(body, new Vector3(x, y, z));
            }
            yield return null;
        }

        // ② 下落：EaseIn（越落越快 —— 主人原话「落得迅速点」）；同样感知冻场
        float topY = toY + GameConfig.ELITE_LEAP_HEIGHT;
        t = 0f;
        while (t < fall)
        {
            if (isDead || this == null) yield break;
            if (BattleManager.Instance == null || BattleManager.Instance.UnitsCanAct)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / fall);
                float y = Mathf.Lerp(topY, toY, k * k);
                GameConfig.SetWorldPosition(body, new Vector3(toX, y, z));
            }
            yield return null;
        }
        GameConfig.SetWorldPosition(body, new Vector3(toX, toY, z));
    }

    // ==================== 远程专属反应技「瞬移遁走」（2026-10-20 主人拍板） ====================
    // 被玩家/佣兵贴脸 → 竖向压扁消失（scale.y→0）→ 远离威胁者的落点原地复原（scale.y→1）。
    // 纯位移演出，不出伤害不出弹道，与「砸地与远程互斥」不冲突（近战靠砸地，远程靠拉开距离）。
    // 【2026-10-20 主人拍板】仅远程精英/Boss 携带，靠冷却控频；近战怪与远程小怪均无此技。

    /// <summary>场上最近的威胁者（玩家或佣兵，按战斗距离）。无存活我方单位返回 null。</summary>
    UnitBase FindNearestThreat()
    {
        UnitBase nearest = null;
        float nd = float.MaxValue;
        var allies = BattleManager.Instance != null ? BattleManager.Instance.allyUnits : null;
        if (allies != null)
        {
            for (int i = 0; i < allies.Count; i++)
            {
                var u = allies[i];
                if (u == null || u.isDead) continue;
                float d = GetCombatDist(this, u);
                if (d < nd) { nd = d; nearest = u; }
            }
        }
        return nearest;
    }

    /// <summary>
    /// 远程被贴脸时尝试触发瞬移遁走。返回 true = 已启动协程，调用方本帧直接返回。
    /// 触发线/冷却/互斥（冻场、主动技能预警、跳跃、普攻出手锁）都在这里把关。
    /// </summary>
    bool TryBlinkAwayIfCrowded()
    {
        // 远程专属：近战怪靠贴脸砍，永不遁走（2026-10-20 主人拍板）
        if (!MonsterAttackStyleTable.IsRanged(_attackStyle)) return false;
        // 【2026-10-20 主人拍板】远程小怪不带瞬移：只有远程精英/Boss 保留遁走技
        if (!_isBossUnit && !_eliteWave) return false;
        if (_blinkCd > 0f) return false;
        if (_teleporting || _skillTelegraphing || _leaping || _bossPhaseShiftBusy) return false;
        if (unitAnim != null && unitAnim.InAttackLock) return false;
        if (BattleManager.Instance != null && !BattleManager.Instance.UnitsCanAct) return false;

        UnitBase threat = FindNearestThreat();
        if (threat == null) return false;
        if (GetCombatDist(this, threat) > GameConfig.MONSTER_BLINK_TRIGGER_DIST) return false;

        _blinkCd = GameConfig.MONSTER_BLINK_COOLDOWN;
        _teleporting = true;
        StartCoroutine(CoMonsterBlinkAway(threat));
        return true;
    }

    /// <summary>
    /// 瞬移遁走三段演出（节奏刻意放缓）：① 压扁遁地 0.30s（scale.y 1→0，EaseIn）
    /// → ② 隐形挪移 0.10s（完全消失，中点换位）→ ③ 原地复原 0.35s（scale.y 0→1，EaseOut）
    /// → 收势停顿 0.10s 才恢复行动。落点 = 远离威胁者随机位移，钳回战场可视范围，
    /// 一边贴墙换另一边，两边都挤不出最小位移则放弃（清冷却下帧重试）。
    /// 车道 Y 不变（= FootY，不串排）；精英 ×2.0 根缩放按比例压（记 bodyScale0 不写死）。
    /// 全程感知冻场：UnitsCanAct=false 时画面定住不推进。
    /// </summary>
    IEnumerator CoMonsterBlinkAway(UnitBase threat)
    {
        var body = MoveRoot;
        float toX;
        Vector3 bodyScale0 = body != null ? body.localScale : Vector3.one;
        try
        {
            if (body == null || threat == null) yield break;

            if (rb != null) rb.velocity = Vector2.zero;
            if (unitAnim != null) unitAnim.SetMove(false, facingDir);

            // 落点：沿远离威胁者的 X 方向随机位移；钳回可视范围，贴墙换向，双向都挤不出就放弃
            float fromX = body.position.x;
            float dir = GetCombatX(this) >= GetCombatX(threat) ? 1f : -1f;
            float hop = Random.Range(GameConfig.MONSTER_BLINK_MIN_DIST, GameConfig.MONSTER_BLINK_MAX_DIST);
            BattleManager.GetBattleVisibleX(out float vMin, out float vMax);
            toX = Mathf.Clamp(fromX + dir * hop, vMin, vMax);
            if (Mathf.Abs(toX - fromX) < GameConfig.MONSTER_BLINK_MIN_DIST)
            {
                toX = Mathf.Clamp(fromX - dir * hop, vMin, vMax);
                if (Mathf.Abs(toX - fromX) < GameConfig.MONSTER_BLINK_MIN_DIST)
                {
                    _blinkCd = 0f;   // 两边都出不去，放弃本次，下帧可重试
                    yield break;
                }
            }

            float toY = FootY;
            float z = body.position.z;
            // 遁走朝向：面向落点；复原后打谁的转身由 AI FaceToward 接管
            ApplyFacing(toX >= fromX ? 1 : -1);

            // ① 压扁遁地：scale.y → 0（EaseIn 越压越快），X/Z 不动
            float t = 0f;
            while (t < GameConfig.MONSTER_BLINK_SQUASH_SEC)
            {
                if (isDead || this == null) yield break;
                if (BattleManager.Instance == null || BattleManager.Instance.UnitsCanAct)
                {
                    t += Time.deltaTime;
                    float k = Mathf.Clamp01(t / GameConfig.MONSTER_BLINK_SQUASH_SEC);
                    float sy = Mathf.Lerp(1f, 0f, k * k);
                    body.localScale = new Vector3(bodyScale0.x, bodyScale0.y * sy, bodyScale0.z);
                }
                yield return null;
            }
            body.localScale = new Vector3(bodyScale0.x, 0f, bodyScale0.z);
            // 【2026-10-20 主人拍板】复用传送门的「传送」特效：原地消失时放一次
            PortalGate.PlayTeleportVfx(body.position);

            // ② 隐形挪移：完全消失的空窗，中点把 X 直接改到落点（瞬移本体）
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
                        // 复用传送特效：新位置出现时再放一次
                        PortalGate.PlayTeleportVfx(new Vector3(toX, toY, z));
                        // 【2026-10-20 主人拍板】传送后的方向始终对着敌人：换位瞬间即转身，
                        // 复原冒出来时已面向威胁者（压扁遁地阶段仍面向遁走方向）
                        ApplyFacing(GetCombatX(threat) >= toX ? 1 : -1);
                    }
                }
                yield return null;
            }

            // ③ 原地复原：scale.y 0 → 1（EaseOut 先快后慢弹出）
            t = 0f;
            while (t < GameConfig.MONSTER_BLINK_RESTORE_SEC)
            {
                if (isDead || this == null) yield break;
                if (BattleManager.Instance == null || BattleManager.Instance.UnitsCanAct)
                {
                    t += Time.deltaTime;
                    float k = Mathf.Clamp01(t / GameConfig.MONSTER_BLINK_RESTORE_SEC);
                    float e = 1f - (1f - k) * (1f - k);
                    float sy = Mathf.Lerp(0f, 1f, e);
                    body.localScale = new Vector3(bodyScale0.x, bodyScale0.y * sy, bodyScale0.z);
                }
                yield return null;
            }

            // 收势停顿：停稳才恢复行动
            t = 0f;
            while (t < GameConfig.MONSTER_BLINK_SETTLE_SEC)
            {
                if (isDead || this == null) yield break;
                if (BattleManager.Instance == null || BattleManager.Instance.UnitsCanAct)
                    t += Time.deltaTime;
                yield return null;
            }
        }
        finally
        {
            // 协程任何路径退出（冻场中被杀/被停协程/正常收尾）都还原缩放与标志位，
            // 标志位绝不许卡死（参照 2026-10-17 波次闸门 try/finally 教训）
            if (this != null && MoveRoot != null)
                MoveRoot.localScale = bodyScale0;
            _teleporting = false;
        }
    }

    // ==================== 远程精英/Boss 专属双弹幕技（2026-10-20 主人拍板） ====================
    // ① 扇形弹幕：一次朝目标方向扇形齐射多条弹道，单发伤害 = 普攻 × 80%，飞满距离在终点爆开，
    //    命中按爆点小圆判定（走位可躲）。
    // ② 追踪弹幕：连续发射 3~5 发追踪弹（随机），每发随机锁定一名存活玩家/佣兵，单发伤害 = 普攻 × 80%，
    //    弹体实时跟人、命中才结算。
    // 两技各 20s 冷却（GameConfig 可调）；仅远程风格精英/Boss 携带，与主动技能/瞬移/跳跃互斥。
    // 携带与解锁进程（2026-10-20 主人拍板）：远程精英随机二选一整场不变；远程 Boss 开局只有扇形，
    // 血量降到二阶段线（BOSS_PHASE2_HP_RATIO = 70%，触发时伴随 Boss 变身演出）后才开启追踪弹幕。

    /// <summary>弹幕单发伤害 = 普攻 raw × 80%（主人定）；暴击在命中时逐目标掷（与 ApplySkillDamage 同口径）。</summary>
    float VolleyBasicDamage()
    {
        float raw = DamageFormula.BuildAttackRaw(attr, out _, GetLowHpCritRateBonus(), IsMagicDamageDealer());
        return raw * GameConfig.MONSTER_VOLLEY_DAMAGE_MUL;
    }

    /// <summary>
    /// 双弹幕技触发把关：远程 + 精英/Boss、有活目标、已进射击站位（射程内）、能行动、
    /// 不在任何演出/出手锁中。两技同时转好先放扇形（Init 里首发已错开 2.5s，之后永远错峰）。
    /// 返回 true = 本帧已起手某技，调用方直接返回。
    /// </summary>
    bool TryCastRangedVolleySkill(UnitBase target)
    {
        if (!MonsterAttackStyleTable.IsRanged(_attackStyle)) return false;
        if (!_isBossUnit && !_eliteWave) return false;
        if (target == null || target.isDead) return false;
        if (!IsInBasicAttackRange(target)) return false;
        if (_teleporting || _skillTelegraphing || _leaping || _bossPhaseShiftBusy) return false;
        if (unitAnim != null && unitAnim.InAttackLock) return false;
        if (BattleManager.Instance != null
            && (!BattleManager.Instance.UnitsCanAct || BattleManager.Instance.AllowMonsterMapEnter)) return false;
        if (!GameConfig.IsInCombatViewport(target)) return false;

        // 携带判定：精英只随机带一种（Init 已定），Boss 两种全带但按进程解锁（2026-10-20 主人拍板）——
        // 开局只有扇形，血量降到二阶段线（≤ BOSS_PHASE2_HP_RATIO = 70%）才开启追踪弹幕；
        // 精英带追踪不受阶段限制（它本来就只有这一种）。没带/没转好的不出手。
        bool fanReady = _fanVolleyOwned && _fanVolleyCd <= 0f;
        bool homingReady = _homingBarrageOwned && _homingBarrageCd <= 0f
            && (!_isBossUnit || GetBossPhase() >= 2);
        if (!fanReady && !homingReady) return false;

        if (fanReady)
        {
            _fanVolleyCd = GameConfig.MONSTER_FAN_VOLLEY_COOLDOWN;
            StartCoroutine(CoFanVolley(target));
            return true;
        }
        _homingBarrageCd = GameConfig.MONSTER_HOMING_BARRAGE_COOLDOWN;
        StartCoroutine(CoHomingBarrage(target));
        return true;
    }

    /// <summary>① 扇形弹幕：以目标方向为基准，一次齐射 MONSTER_FAN_VOLLEY_COUNT 条弹道，等角扇形展开。</summary>
    IEnumerator CoFanVolley(UnitBase target)
    {
        FaceToward(target);
        AttackVfxKit kit = MonsterAttackStyleTable.GetVfxKit(_attackStyle);
        if (unitAnim != null) unitAnim.PlaySkillCast(kit, 1.15f);
        SkillCastNotifyController.PushEnemySkill("fan_volley", SkillNotifyName(), sr != null ? sr.sprite : null);

        float delay = GameConfig.RANGED_FIRE_RELEASE_DELAY;
        if (delay > 0.001f) yield return new WaitForSeconds(delay);
        if (this == null || isDead) yield break;

        Vector3 firePos = GetFirePosition();
        int facing = GetVfxFacingDir();
        Vector3 aim = target != null && !target.isDead
            ? target.GetHitPosition()
            : firePos + Vector3.right * facing * 8f;
        Vector2 baseDir = new Vector2(aim.x - firePos.x, aim.y - firePos.y);
        if (baseDir.sqrMagnitude < 1e-6f) baseDir = new Vector2(facing, 0f);
        float baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;

        int count = GameConfig.MONSTER_FAN_VOLLEY_COUNT;
        float step = GameConfig.MONSTER_FAN_VOLLEY_ANGLE_STEP;
        float damage = VolleyBasicDamage();
        float hitRadius = GameConfig.MONSTER_FAN_VOLLEY_HIT_RADIUS;
        for (int i = 0; i < count; i++)
        {
            float ang = (baseAngle + (i - (count - 1) * 0.5f) * step) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            // 弹道飞满 MONSTER_FAN_VOLLEY_RANGE 距离后原地爆；命中按爆点小圆判定（走位可躲）
            Vector3 end = firePos + (Vector3)(dir * GameConfig.MONSTER_FAN_VOLLEY_RANGE);
            float impactX = end.x;
            if (BattleVFXSystem.Instance != null)
            {
                BattleVFXSystem.Instance.PlaySkillProjectile(
                    VfxFaction.Enemy, firePos, end, facing, null, kit, null,
                    FanProjectileScale, SkillProjectileSpeedMul,
                    () => ApplySkillDamage(damage, hitRadius, null, impactX));
            }
            else
                ApplySkillDamage(damage, hitRadius, null, impactX);
        }
    }

    /// <summary>② 追踪弹幕：连续 3~5 发（随机），每发随机锁定一名存活玩家/佣兵，追踪弹飞行中实时跟人。</summary>
    IEnumerator CoHomingBarrage(UnitBase target)
    {
        FaceToward(target);
        AttackVfxKit kit = MonsterAttackStyleTable.GetVfxKit(_attackStyle);
        if (unitAnim != null) unitAnim.PlaySkillCast(kit, 1.15f);
        SkillCastNotifyController.PushEnemySkill("homing_barrage", SkillNotifyName(), sr != null ? sr.sprite : null);

        float delay = GameConfig.RANGED_FIRE_RELEASE_DELAY;
        if (delay > 0.001f) yield return new WaitForSeconds(delay);

        int count = Random.Range(GameConfig.MONSTER_HOMING_BARRAGE_MIN, GameConfig.MONSTER_HOMING_BARRAGE_MAX + 1);
        float damage = VolleyBasicDamage();
        for (int i = 0; i < count; i++)
        {
            // 连发间隔感知冻场：UnitsCanAct=false 时画面定住不推进（与瞬移/预警同口径）
            float t = 0f;
            while (t < GameConfig.MONSTER_HOMING_BARRAGE_GAP)
            {
                if (this == null || isDead) yield break;
                if (BattleManager.Instance == null || BattleManager.Instance.UnitsCanAct)
                    t += Time.deltaTime;
                yield return null;
            }
            if (this == null || isDead) yield break;

            UnitBase tgt = PickRandomAliveAlly();
            if (tgt == null) yield break;
            Vector3 firePos = GetFirePosition();
            int facing = GetVfxFacingDir();
            if (BattleVFXSystem.Instance != null)
            {
                BattleVFXSystem.Instance.PlayHomingProjectile(
                    VfxFaction.Enemy, firePos, tgt, kit,
                    SkillProjectileScale, GameConfig.MONSTER_HOMING_BARRAGE_SPEED_MUL,
                    () =>
                    {
                        if (this == null || isDead || tgt == null || tgt.isDead) return;
                        bool crit = Random.value < attr.GetAttr(AttrType.CritRate);
                        tgt.TakeDamage(crit ? damage * 1.5f : damage, crit, false, true, facing, this);
                    });
            }
            else if (tgt != null && !tgt.isDead)
                tgt.TakeDamage(damage, false, false, true, facing, this);
        }
    }

    /// <summary>随机挑一名存活我方单位（玩家或佣兵），轮转起点随机；无存活返回 null。</summary>
    UnitBase PickRandomAliveAlly()
    {
        var allies = BattleManager.Instance != null ? BattleManager.Instance.allyUnits : null;
        if (allies == null || allies.Count == 0) return null;
        int n = allies.Count;
        int start = Random.Range(0, n);
        for (int k = 0; k < n; k++)
        {
            var u = allies[(start + k) % n];
            if (u != null && !u.isDead) return u;
        }
        return null;
    }

    static GameObject CreateTelegraphDisc(Vector3 center, float radius)
    {
        var go = new GameObject("SkillTelegraph");
        go.transform.position = center;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = MakeCircleSprite();
        // 【2026-10-18 主人反馈红圈太浅看不清】alpha 0.42→0.68，颜色再饱和一档
        // 【2026-10-19 主人反馈还要再显眼】alpha 0.68→0.85，配 ±10% 呼吸脉动（见 CoSkillTelegraphThenCast）
        sr.color = new Color(0.9f, 0.07f, 0.06f, 0.85f);
        // 【2026-10-19 主人拍板】红圈不挡玩家：地图(10) 上面一级、所有单位(15+) 下面 → 固定 11
        sr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
        sr.sortingOrder = GameConfig.SORT_MAPROOT + 1;
        float dia = Mathf.Max(1.2f, radius * 2f);
        go.transform.localScale = new Vector3(dia, dia * 0.35f, 1f);
        return go;
    }

    static Sprite MakeCircleSprite()
    {
        if (_circleSprite != null) return _circleSprite;
        const int s = 64;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float c = (s - 1) * 0.5f;
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float dx = (x - c) / c;
            float dy = (y - c) / c;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float a = d <= 1f ? Mathf.Clamp01(1.15f - d) : 0f;
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        _circleSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        return _circleSprite;
    }

    /// <summary>预警砸下瞬间的一圈波纹：从中心快速外扩、渐隐（2026-10-16 主人要求「扩散效果」）。</summary>
    IEnumerator CoTelegraphBurst(Vector3 center, float dia)
    {
        var go = new GameObject("SkillTelegraphBurst");
        go.transform.position = center;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = MakeRingSprite();
        sr.color = new Color(0.95f, 0.12f, 0.1f, 0.85f);
        // 【2026-10-19 主人拍板】波纹与红圈同属预警演出，同样不挡单位：红圈(11) 之上、单位(15+) 之下 → 12
        sr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
        sr.sortingOrder = GameConfig.SORT_MAPROOT + 2;
        const float dur = 0.35f;
        float t = 0f;
        while (t < dur && sr != null)
        {
            float k = Mathf.Clamp01(t / dur);
            float s = dia * (0.35f + 0.85f * k);
            go.transform.localScale = new Vector3(s, s * 0.35f, 1f);
            var c = sr.color;
            c.a = 0.85f * (1f - k);
            sr.color = c;
            t += Time.deltaTime;
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    static Sprite MakeRingSprite()
    {
        if (_ringSprite != null) return _ringSprite;
        const int s = 96;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float c = (s - 1) * 0.5f;
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float dx = (x - c) / c;
            float dy = (y - c) / c;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            // 环带中心 0.82、半宽 0.16，两侧软边（ppu=s → 贴图世界尺寸 1×1，scale=dia 即世界直径）
            float a = d <= 1f ? Mathf.Clamp01(1f - Mathf.Abs(d - 0.82f) / 0.16f) : 0f;
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        _ringSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        return _ringSprite;
    }

    void ExecuteActiveSkillNow(UnitBase primaryTarget, float damage, float radius, AttackVfxKit kit, Vector3? lockedCenter = null)
    {
        // 【2026-10-19 圆心锁定】预警技能（Boss/精英）传入红圈锁定点：伤害/VFX/弹道全部落在红圈位置，
        // 不再取目标「砸下瞬间」的当前位置 —— 预警期跑出圈即真躲开。瞬发技能不传参，保持旧行为。
        Vector3 hitPos = lockedCenter
            ?? (primaryTarget != null ? primaryTarget.GetHitPosition() : GetFirePosition());

        if (unitAnim != null)
            unitAnim.PlaySkillCast(kit, (_isBossUnit || _eliteWave) ? 1.15f : 1f);

        if (kit == AttackVfxKit.Bow || kit == AttackVfxKit.Orb)
        {
            StartCoroutine(CoRangedSkillProjectile(primaryTarget, damage, radius, kit, lockedCenter));
            return;
        }

        SkillRegistry.Instance?.PlaySkillVfx(_skillId, hitPos, false, GetVfxFacingDir(), transform);
        // 右侧镜像技能释放通知：与玩家侧同一模板，x 取反（不翻 scale.x，文字不镜像）
        // 【2026-10-19 主人报白框】怪物技能 id 在玩家图标表里没图 → 白框；改传怪物自身 sprite 当图标
        SkillCastNotifyController.PushEnemySkill(_skillId, SkillNotifyName(), sr != null ? sr.sprite : null);
        ApplySkillDamage(damage, radius, primaryTarget, hitPos.x);
    }

    /// <summary>技能通知上的敌人名：模板怪物名 → 模板 id → 节点名。</summary>
    string SkillNotifyName()
    {
        if (config != null)
        {
            if (!string.IsNullOrEmpty(config.monsterName)) return config.monsterName;
            if (!string.IsNullOrEmpty(config.id)) return config.id;
        }
        return name;
    }

    IEnumerator CoRangedSkillProjectile(
        UnitBase primaryTarget, float damage, float radius, AttackVfxKit kit, Vector3? lockedCenter = null)
    {
        float delay = GameConfig.RANGED_FIRE_RELEASE_DELAY;
        if (delay > 0.001f)
            yield return new WaitForSeconds(delay);
        if (this == null || isDead)
            yield break;

        // 【2026-10-19 圆心锁定】弹道飞向红圈锁定点并原地爆炸；瞬发（无锁定）保持旧行为。
        Vector3 firePos = GetFirePosition();
        Vector3 hitPos = lockedCenter ?? (primaryTarget != null && !primaryTarget.isDead
            ? primaryTarget.GetHitPosition() : firePos);
        GameObject impact = SkillRegistry.Instance?.GetSkillVfxPrefab(_skillId);
        Transform targetTf = primaryTarget != null ? primaryTarget.transform : null;
        float impactX = hitPos.x;
        if (BattleVFXSystem.Instance != null)
        {
            BattleVFXSystem.Instance.PlaySkillProjectile(
                VfxFaction.Enemy, firePos, hitPos, GetVfxFacingDir(), targetTf, kit,
                impact, SkillProjectileScale, SkillProjectileSpeedMul,
                () => ApplySkillDamage(damage, radius, primaryTarget, impactX));
        }
        else
            ApplySkillDamage(damage, radius, primaryTarget, impactX);
    }

    /// <summary>
    /// 技能结算：AoE 圆心由调用方锁定 —— 预警技能（Boss/精英）= 红圈位置，跑出圈即躲开；
    /// 瞬发技能 = 出手瞬间目标点（2026-10-19 主人拍板「圆心锁定+统一红圈」）。
    /// </summary>
    void ApplySkillDamage(float damage, float radius, UnitBase primaryTarget, float centerX)
    {
        if (this == null || isDead || !gameObject.activeInHierarchy) return;

        int vfxDir = GetVfxFacingDir();
        var allies = BattleManager.Instance?.allyUnits;
        if (allies != null && allies.Count > 0)
        {
            for (int i = 0; i < allies.Count; i++)
            {
                var u = allies[i];
                if (u == null || u.isDead) continue;
                float dist = Mathf.Abs(u.transform.position.x - centerX);
                if (dist > radius) continue;
                bool crit = Random.value < attr.GetAttr(AttrType.CritRate);
                u.TakeDamage(crit ? damage * 1.5f : damage, crit, false, true, vfxDir);
            }
            return;
        }
        if (primaryTarget != null && !primaryTarget.isDead)
        {
            if (Mathf.Abs(primaryTarget.transform.position.x - centerX) > radius)
                return;
            primaryTarget.TakeDamage(damage, false, false, true, vfxDir);
        }
    }
}
