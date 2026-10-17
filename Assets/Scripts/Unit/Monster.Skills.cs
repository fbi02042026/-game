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
    private int _bossSwingIndex;
    bool _skillTelegraphing;
    /// <summary>【2026-10-17 主人拍板】精英蓄力跳滞空中：AI 提前返回时跳过归道（ApplyLaneY），
    /// 否则身体 Y 每帧被拉回 FootY、跳跃演出起不来。置位/复位只在 CoSkillTelegraphThenCast 内。</summary>
    bool _leaping;
    static Sprite _circleSprite;
    static Sprite _fanSprite;
    static Sprite _ringSprite;
    const float SkillProjectileScale = 1.6f;
    // 2026-09-21：MONSTER_SKILL_PROJECTILE_SPEED_MUL 已迁 combat_tuning 表（运行时读），
    // 右值不再是编译期常量 → const 改 static readonly（仅此一处引用，行为不变）。
    static readonly float SkillProjectileSpeedMul = GameConfig.MONSTER_SKILL_PROJECTILE_SPEED_MUL;

    MonsterAttackStyle ResolveSwingStyle(UnitBase target)
    {
        if (!_isBossUnit)
            return _attackStyle;

        // Boss??????????????????????
        float dist = target != null
            ? Mathf.Abs(transform.position.x - target.transform.position.x)
            : 99f;
        const float meleeReach = 1.8f;
        MonsterAttackStyle byDist = dist <= meleeReach ? MonsterAttackStyle.Melee : MonsterAttackStyle.Ranged;
        _bossSwingIndex++;
        if (_bossSwingIndex % 3 == 0)
            return byDist == MonsterAttackStyle.Melee ? MonsterAttackStyle.Ranged : MonsterAttackStyle.Melee;
        return byDist;
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
        // Attack() 已 ResolveSwingStyle；此处复用，避免 _bossSwingIndex 双跳

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
            // 阶段 2：更偏向近身砸击节奏
            if (!MonsterAttackStyleTable.IsRanged(_attackStyle))
                _swingStyle = MonsterAttackStyle.Melee;
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
        }

        // 精英 / Boss：地面红色警示后再结算
        if (_isBossUnit || _eliteWave)
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
        bool eliteLeap = _eliteWave && !_isBossUnit;
        Vector3 center = primaryTarget != null
            ? new Vector3(primaryTarget.transform.position.x, GROUND_Y + 0.02f, primaryTarget.transform.position.z)
            : new Vector3(transform.position.x, GROUND_Y + 0.02f, transform.position.z);

        // 精英：要跳到目标点砸 → 预警红圈统一画在目标点（不再用脚下扇形，主人原话「目标的地点有个红圈」）；
        // Boss 仍按攻击风格走原有 圆盘/扇形。
        GameObject disc = (!eliteLeap && !MonsterAttackStyleTable.IsRanged(_swingStyle))
            ? CreateTelegraphFan(new Vector3(transform.position.x, GROUND_Y + 0.02f, transform.position.z), radius, GetVfxFacingDir())
            : CreateTelegraphDisc(center, radius);
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
                        // （半透走 CreateTelegraphDisc 自带的 alpha 0.42，不另改颜色）
                        float grow = Mathf.Lerp(GameConfig.ELITE_TELEGRAPH_GROW_FROM, 1f, Mathf.Clamp01(t / warn));
                        disc.transform.localScale = new Vector3(baseScale.x * grow, baseScale.y * grow, 1f);
                    }
                    else
                    {
                        // 呼吸脉冲：基准 scale 上叠 ±5% 正弦脉动（不受冻场影响，fan 的 x 带符号乘法安全）
                        float pulse = 1f + 0.05f * Mathf.Sin(pulseT * 7f);
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
                ExecuteActiveSkillNow(primaryTarget, damage, radius, kit);
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
                        ExecuteActiveSkillNow(primaryTarget, damage * 0.75f, radius * 0.85f, kit);
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

    static GameObject CreateTelegraphDisc(Vector3 center, float radius)
    {
        var go = new GameObject("SkillTelegraph");
        go.transform.position = center;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = MakeCircleSprite();
        // 【2026-10-18 主人反馈红圈太浅看不清】alpha 0.42→0.68，颜色再饱和一档
        sr.color = new Color(0.9f, 0.07f, 0.06f, 0.68f);
        sr.sortingOrder = GameConfig.SORT_UNIT - 2;
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

    static Sprite MakeFanSprite()
    {
        if (_fanSprite != null) return _fanSprite;
        // 贴图宽 w、高 h=2w：锚点(左中)即扇形顶点，贴图右半为一张 90° 扇形（朝 +X）。
        const int w = 128;
        const int h = 256;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float c = (h - 1) * 0.5f;   // 顶点(左中)对应像素 y
        float maxR = w;             // 扇形最大半径(像素，沿 +X)
        float half = Mathf.PI * 0.25f; // 半角 45°（总 90°，-45°~+45°）
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float dx = x;
            float dy = y - c;
            float radial = Mathf.Sqrt(dx * dx + dy * dy);
            if (radial > maxR || dx < 0f)
            {
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, 0f));
                continue;
            }
            float ang = Mathf.Atan2(dy, dx); // 以 +X 为 0
            if (Mathf.Abs(ang) > half)
            {
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, 0f));
                continue;
            }
            // 半径方向软边衰减
            float aR = 1f - Mathf.SmoothStep(0.82f, 1f, radial / maxR);
            // 角度两侧软边
            float aA = 1f - Mathf.SmoothStep(half * 0.9f, half, Mathf.Abs(ang));
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, aR * aA));
        }
        tex.Apply();
        _fanSprite = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0f, 0.5f), w);
        return _fanSprite;
    }

    static GameObject CreateTelegraphFan(Vector3 origin, float radius, int facingDir)
    {
        var go = new GameObject("SkillTelegraphFan");
        go.transform.position = origin;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = MakeFanSprite();
        // 【2026-10-18 主人反馈红圈太浅看不清】alpha 0.42→0.68，颜色再饱和一档
        sr.color = new Color(0.9f, 0.07f, 0.06f, 0.68f);
        sr.sortingOrder = GameConfig.SORT_UNIT - 2;
        // 锚点(左中)：扇形从施法者脚下朝外展开；朝左时 localScale.x 取负实现 X 翻转。
        // 贴图 128x256 / ppu=128 → 世界尺寸 (1,2)，故 radius 即 scale.x；Y 压缩比与红圈一致 0.35。
        int sign = facingDir >= 0 ? 1 : -1;
        go.transform.localScale = new Vector3(radius * sign, radius * 0.35f, 1f);
        return go;
    }

    /// <summary>预警砸下瞬间的一圈波纹：从中心快速外扩、渐隐（2026-10-16 主人要求「扩散效果」）。</summary>
    IEnumerator CoTelegraphBurst(Vector3 center, float dia)
    {
        var go = new GameObject("SkillTelegraphBurst");
        go.transform.position = center;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = MakeRingSprite();
        sr.color = new Color(0.95f, 0.12f, 0.1f, 0.85f);
        sr.sortingOrder = GameConfig.SORT_UNIT - 1;
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

    void ExecuteActiveSkillNow(UnitBase primaryTarget, float damage, float radius, AttackVfxKit kit)
    {
        Vector3 hitPos = primaryTarget != null ? primaryTarget.GetHitPosition() : GetFirePosition();

        if (unitAnim != null)
            unitAnim.PlaySkillCast(kit, (_isBossUnit || _eliteWave) ? 1.15f : 1f);

        if (kit == AttackVfxKit.Bow || kit == AttackVfxKit.Orb)
        {
            StartCoroutine(CoRangedSkillProjectile(primaryTarget, damage, radius, kit));
            return;
        }

        SkillRegistry.Instance?.PlaySkillVfx(_skillId, hitPos, false, GetVfxFacingDir(), transform);
        // 右侧镜像技能释放通知：与玩家侧同一模板，x 取反（不翻 scale.x，文字不镜像）
        SkillCastNotifyController.PushEnemySkill(_skillId, SkillNotifyName());
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
        UnitBase primaryTarget, float damage, float radius, AttackVfxKit kit)
    {
        float delay = GameConfig.RANGED_FIRE_RELEASE_DELAY;
        if (delay > 0.001f)
            yield return new WaitForSeconds(delay);
        if (this == null || isDead)
            yield break;

        Vector3 firePos = GetFirePosition();
        Vector3 hitPos = primaryTarget != null && !primaryTarget.isDead
            ? primaryTarget.GetHitPosition() : firePos;
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
    /// 技能结算：AoE 圆心用开火/落点固定 X，不跟目标当前 X（可躲开）。
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
