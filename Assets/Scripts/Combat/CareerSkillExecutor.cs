using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 职业自带技能的演出与伤害（2026-11-13 主人拍板）。
/// 数值全部读 player_skills.csv 合成的 ActiveSkill（damageMultiplier / aoeRadius /
/// aoeMaxTargets / projectileCount / cooldown），本类不写第二份数值。
/// 伤害统一走 TakeDamage(source=Hero)，末尾 ApplyHitKnockback 复用已有击退协程与 AI 避让；
/// 借新增的 knockbackDistanceOverride 把普通怪推得更远（精英/Boss 仍按 11-05 口径免退）。
/// 特效全部复用项目现成共用套（刀光 / 弓 / 法球飞行体），不新增任何资源与预制体。
/// </summary>
public static class CareerSkillExecutor
{
    /// <summary>破阵冲锋的半盾特效 id（Resources/VFX/Skills/Ally/{id}.prefab，与 player_skills.allyConfigId 同名）。</summary>
    const string ChargeShieldVfxId = "ally_career_charge";
    /// <summary>【2026-11-15】冲锋脚后尘土：项目现成 Resources 资源，本次不新增任何资源/预制体。</summary>
    const string ChargeDustResourcePath = "Effect/texiao/尘土";

    public static bool HasVisibleEnemy()
    {
        var bm = BattleManager.Instance;
        if (bm == null || bm.monsters == null) return false;
        for (int i = 0; i < bm.monsters.Count; i++)
        {
            var m = bm.monsters[i];
            if (m == null || m.isDead) continue;
            if (!GameConfig.IsInCombatViewport(m)) continue;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 【2026-11-20 三职业天赋分表】自带技「精通」形态判定 —— 全技能唯一入口。
    /// 真源 = 右列「技能精通」节点 R_DUAL 的等级 &gt; 0；换职业清右列（复用洗点退 80%）后自动归零，
    /// 新职业需重新点精通才拿到完整形态，与「退 80% 重新解锁」口径一致。
    /// 后续若把精通挪到职业专属第一槽节点，只改这里一处常量即可，三个演出协程感知不到。
    /// 无存档（存档未初始化）时回退 false = 未精通形态。
    /// </summary>
    const string CareerMasteryNodeId = "R_DUAL";
    static bool IsCareerMastered()
    {
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        return data != null && TalentDefs.GetRightNodeLevel(data.talents, CareerMasteryNodeId) > 0;
    }

    public static bool Cast(MonoBehaviour runner, PlayerJobId job, string skillId)
    {
        var hero = Hero.Instance;
        if (runner == null || hero == null || hero.isDead) return false;
        var reg = SkillRegistry.Instance;
        var skill = reg != null ? reg.GetActiveSkill(skillId) : null;
        if (skill == null)
        {
            Debug.LogError($"[CareerSkill] 取不到职业技能配置，拒绝释放: {skillId}");
            return false;
        }
        if (BattleVFXSystem.Instance == null)
        {
            Debug.LogError("[CareerSkill] BattleVFXSystem 为空，拒绝释放职业技能");
            return false;
        }

        UnitBase target = PickTarget(hero);
        if (target == null) return false;

        SkillCastNotifyController.PushPlayerSkill(skillId, skill.skillName);

        // 【2026-11-20 精通形态】施放瞬间算一次精通状态（整段演出内不变），传给三个协程。
        bool mastered = IsCareerMastered();
        // 演出类型读职业真源，不按职业枚举硬编码分叉。
        switch (PlayerJobDefs.Get(job).CareerSkillKind)
        {
            case CareerSkillKind.Charge:
                runner.StartCoroutine(CoCharge(hero, skill, target, mastered));
                break;
            case CareerSkillKind.Scatter:
                runner.StartCoroutine(CoScatter(hero, skill, target, mastered));
                break;
            case CareerSkillKind.Orb:
                runner.StartCoroutine(CoOrb(hero, skill, target, mastered));
                break;
            default:
                Debug.LogError($"[CareerSkill] 职业 {job} 未登记自带技能演出，拒绝释放: {skillId}");
                return false;
        }
        return true;
    }

    static UnitBase PickTarget(Hero hero)
    {
        var bm = BattleManager.Instance;
        if (bm == null || bm.monsters == null) return null;
        UnitBase best = null;
        float bestD = float.MaxValue;
        Vector2 self = hero.transform.position;
        for (int i = 0; i < bm.monsters.Count; i++)
        {
            var m = bm.monsters[i];
            if (m == null || m.isDead) continue;
            if (!GameConfig.IsInCombatViewport(m)) continue;
            float d = Vector2.Distance(self, m.transform.position);
            if (d < bestD) { bestD = d; best = m; }
        }
        return best;
    }

    // —— 剑盾：分段破阵冲锋 —— 撞到一簇怪就顿一下、撞开，再继续往前 ——
    static IEnumerator CoCharge(Hero hero, SkillSystem.ActiveSkill skill, UnitBase target, bool mastered)
    {
        Vector3 start = hero.transform.position;
        // 【2026-11-14 曾拍板「只往一个方向冲、恒 +X」；2026-11-15 主人推翻：要往玩家冲的方向】
        // 方向改读玩家朝向真源 GetVfxFacingDir()（FaceToward/ApplyFacing 动态维护，±1），不再写死 +X。
        // 下游 end / seg / contact / rest / faceDir / dustXOffset 全按 dir 符号推导，天然支持朝左。
        float dir = hero.GetVfxFacingDir();
        // 【2026-11-14 主人拍板「冲目标往前迅速冲一段距离，不是冲到怪物跟前就不动了」】
        // 终点 = 目标位置 + OVERSHOOT：越过目标继续冲，冲过它再停（旧算法是停在目标身前 0.8）。
        // 目标贴身时 toTarget≈0，仍由 CAREER_CHARGE_MIN_DIST 保证冲得出一段。
        float toTarget = target != null
            ? Mathf.Abs(target.transform.position.x - start.x)
            : GameConfig.CAREER_CHARGE_DIST * 0.5f;
        // 【2026-11-16 主人报「只剩 Boss 一个时冲过去血没减」】根因是上限把终点截在目标之前：
        // toTarget 一旦超过 (CAREER_CHARGE_DIST - OVERSHOOT)，终点就停在目标前面，目标既不在走廊里
        // （CollectChargeVictims 用 end 截走廊）、也不在落点判定框里 —— 全程一次都碰不到。
        // 口径改为「上限只限制越过目标后的额外量」：cap 取 max(原上限, toTarget + CONTACT_GAP)，
        // 目标再远也保证冲到它身前 CONTACT_GAP；目标近时行为与旧版一字不差。
        float reachCap = Mathf.Max(GameConfig.CAREER_CHARGE_DIST,
            toTarget + GameConfig.CAREER_CHARGE_CONTACT_GAP);
        float dist = Mathf.Clamp(toTarget + GameConfig.CAREER_CHARGE_OVERSHOOT,
            GameConfig.CAREER_CHARGE_MIN_DIST, reachCap);
        // 只横移、不碰 Y：竖屏 lane 由 BattleLaneBounds 管，改 Y 会让单位脱离所在行。
        Vector3 end = start + new Vector3(dir * dist, 0f, 0f);

        // 【2026-11-14 主人报「冲撞速度太快了，比走快 40%」】按「当前移速 × CAREER_CHARGE_SPEED_MUL」定速度，
        // 每段位移都按它算时长，不写死秒数。
        float walkSpeed = hero.attr != null ? hero.attr.GetAttr(AttrType.MoveSpeed) : GameConfig.BASE_MOVE_SPEED;
        float chargeSpeed = Mathf.Max(0.1f, walkSpeed * GameConfig.CAREER_CHARGE_SPEED_MUL);
        int faceDir = dir >= 0f ? 1 : -1;

        // 【2026-11-14 主人「不要冲锋的时候还在攻击」】整段冲锋独占英雄的行动权：
        // 位移与朝向归本协程，AI 不再索敌/出手/写 rb.velocity（Hero.AIUpdate 读这个标记）。
        MarkCharging();
        // 【2026-11-24 主人报「冲锋反着身子」】不主动翻朝向，只钉住冲锋开始那一刻的 scale.x 符号——
        // 原因：Hero 的 ApplyFacing 要配合 spriteDefaultFacesRight 做映射，手动翻 scale.x 会漏这层；
        // 而 Hero 在冲锋触发瞬间已经朝 target 方向了（AI 追敌先翻朝向），只需把这个朝向冻结全程。
        var flipRoot = hero.GetFlipRoot();
        int chargeScaleSign = flipRoot != null && flipRoot.localScale.x >= 0f ? 1 : -1;
        if (hero.unitAnim != null) hero.unitAnim.ForceWalkAnim();

        // 【2026-11-14 主人拍板「用这半个盾跟着玩家前面一起冲锋，不要比玩家大太多，冲锋完就消失」】
        // 半盾（pixelfx impacts/shield-block）= 独立世界对象，坐标每帧同步英雄受击点（CoChargeSlide 的 follow 参数）；
        // 基础尺寸写在预制体上（localScale 与角色关系见该预制体），本类只叠 2026-11-15 的调整倍率，不写第二份尺寸数值。
        // 收尾在 CoCharge 末尾统一销毁，绝不留到冲锋之后。
        // 【2026-11-20 精通形态】精通=现尺寸；未精通=现尺寸×0.5（盾小一半）。撞击判定框在下方按盾的实例世界尺寸推导，自动跟随缩小。
        GameObject chargeShield = SpawnChargeShield(hero, faceDir, mastered);
        // 【2026-11-14 主人「底部要跟玩家脚底持平」+「护盾只是跟着玩家往前走不要上下移动」】
        // spawn 那一刻 go 正摆在受击点上，量一次「锚点 → sprite 最低点」的世界距离（已含预制体缩放），
        // 再按「脚底 + 这段距离 − 帧底透明边」定一个固定 Y，整个冲锋过程只跟 X、Y 一律不重算
        //（不走 lane，玩家上下并道时盾不跟着上下飘）。【2026-11-15 主人「最下面都要和脚的位置对齐」】
        // 整帧矩形底边不是玩家看到的盾底，扣掉透明边才真正贴脚；与重斩共用 sheet 同一出口，不写死数值。
        // 【2026-11-15 主人「盾再往下 20」】贴脚公式不变，只在其上叠 20 设计像素下移（换算真源在 GameConfig）。
        float shieldY = hero.FootY + MeasureShieldFootOffset(chargeShield) - ShieldFootTrimWorld(chargeShield)
            - GameConfig.CAREER_CHARGE_SHIELD_DROP_WORLD;
        // 【2026-11-16 主人「伤害范围再小一半，应该是盾的一半大小的范围」】撞击判定框尺寸从盾的实例量出来
        // （含预制体缩放 3.6 与 SpawnChargeShield 叠的 0.9 倍率），不再用写死的半径。
        // 范围 = 盾世界尺寸 × CAREER_CHARGE_HIT_RANGE_MUL，再横纵各取一半当半径 → 撞一下只打盾前后那一小块。
        MeasureShieldWorldSize(chargeShield, out float shieldWorldW, out float shieldWorldH);
        // 盾没加载出来（缺 prefab，SpawnChargeShield 已 LogError）时退回旧的固定半径，不让技能变成零伤害；
        // 正常路径一律按盾的实例尺寸走，不写第二份数值。
        bool shieldMeasured = shieldWorldW > 0.01f && shieldWorldH > 0.01f;
        float hitHalfW = shieldMeasured
            ? shieldWorldW * GameConfig.CAREER_CHARGE_HIT_RANGE_MUL * 0.5f
            : GameConfig.CAREER_CHARGE_BURST_RADIUS;
        float hitHalfH = shieldMeasured
            ? shieldWorldH * GameConfig.CAREER_CHARGE_HIT_RANGE_MUL * 0.5f
            : GameConfig.CAREER_CHARGE_BURST_RADIUS;
        // 【2026-11-15 主人「尘土这个特效在玩家冲锋的时候加到脚后面」】与半盾同口径：独立世界对象、
        // 每帧同步到脚后、冲锋结束立刻销毁；不往撞击点补任何刀光。
        GameObject chargeDust = SpawnChargeDust(hero, faceDir);
        float dustXOffset = -faceDir * GameConfig.CAREER_CHARGE_DUST_BACK;

        var victims = CollectChargeVictims(hero, start, end, hitHalfH);
        float baseDamage = BuildDamage(hero, skill) * GameConfig.CAREER_CHARGE_DAMAGE_MUL;
        var alreadyHit = new HashSet<UnitBase>();

        Debug.Log($"[CareerSkill] 破阵冲锋 演出开始 dist={dist:0.00} 路线怪={victims.Count} 顿帧上限={GameConfig.CAREER_CHARGE_HITSTOP_MAX}");

        // 【2026-11-16 主人「玩家能推着精英往后退一段距离，但退 Boss 推不动，玩家也只是在 Boss 跟前，保持推的状态一段时间」】
        // 逐簇推进分三种收场：
        //  · 小怪      → 老口径：顿一下 + 范围内弹开（击飞，距离读 CAREER_CHARGE_KNOCKBACK）；
        //  · 精英      → 不弹飞：先占住顶推锁再结算伤害，随后一路被英雄顶着后退（pushed 名单）；
        //  · 真 Boss   → 推不动：推到它身前 CONTACT_GAP 就停住，保持顶住 CAREER_CHARGE_BOSS_PIN 秒后收招，绝不穿过它。
        Vector3 cursor = start;
        int stops = 0;
        bool pinnedByBoss = false;
        var pushed = new List<UnitBase>();   // 正被顶着的精英：后续每一段滑行都带着它们同量后退
        for (int i = 0; i < victims.Count; i++)
        {
            var m = victims[i];
            if (m == null || m.isDead || alreadyHit.Contains(m)) continue;
            if (stops >= GameConfig.CAREER_CHARGE_HITSTOP_MAX) break;   // 定额用完：剩下的一路冲过去
            float seg = (m.transform.position.x - cursor.x) * dir;
            if (seg <= 0.05f) continue;                                  // 已在身后，不回头
            Vector3 contact = cursor + Vector3.right * dir
                * Mathf.Max(0f, seg - GameConfig.CAREER_CHARGE_CONTACT_GAP);
            yield return CoChargeSlide(hero, cursor, contact, chargeSpeed, chargeShield, shieldY, chargeDust, dustXOffset, pushed, chargeScaleSign);
            cursor = contact;

            // 顿帧 / 顶住期间也保持「冲锋中」标记（防 0.6s 心跳过期）
            MarkCharging();
            // 顿帧期间也钉朝向：WaitForSeconds 这一小段里可能触发别的回调翻朝向
            if (flipRoot != null)
            {
                Vector3 sc2 = flipRoot.localScale;
                flipRoot.localScale = new Vector3(Mathf.Abs(sc2.x) * chargeScaleSign, sc2.y, sc2.z);
            }

            // —— 真 Boss：推不动，停在跟前「原地冲锋」直到这段冲程本应走完，盾/尘土全程不消失 ——
            if (IsTrueBoss(m))
            {
                BurstChargeHit(hero, skill, cursor, baseDamage, alreadyHit, hitHalfW, hitHalfH);   // 照常掉血
                // 【2026-11-16 主人「冲锋那个盾不要一碰到 boss 就消失了，应该是原地冲锋直到时间到了」】
                // 剩余冲程按当前冲锋速度折算成时间：人顶着 Boss 不再前进，但冲锋状态 / 半盾 / 脚后尘土
                // 全部保持，等于把这段路"原地跑完"再收招；不足 BOSS_PIN 时按下限兜住，不会一碰就散。
                float restX = Mathf.Max(0f, (end.x - cursor.x) * dir);
                float hold = Mathf.Max(GameConfig.CAREER_CHARGE_BOSS_PIN,
                    restX / Mathf.Max(0.1f, chargeSpeed));
                float held = 0f;
                while (held < hold)
                {
                    held += Time.deltaTime;
                    MarkCharging();
                    SyncFollow(chargeShield, hero, shieldY);
                    SyncFollow(chargeDust, hero, hero.FootY + GameConfig.CAREER_CHARGE_DUST_UP_WORLD, dustXOffset);
                    // Boss 原地顶住期间也钉朝向：AI 恢复前不让它翻回朝 Boss
                    if (flipRoot != null)
                    {
                        Vector3 sc3 = flipRoot.localScale;
                        flipRoot.localScale = new Vector3(Mathf.Abs(sc3.x) * chargeScaleSign, sc3.y, sc3.z);
                    }
                    yield return null;
                }
                pinnedByBoss = true;
                break;                                                                              // 不冲过去、不补落点爆
            }

            // 精英：先占住顶推锁（deltaX=0 只锁不推）→ 下面这次伤害结算里的击退会被挡掉，
            // 表现是「被推着后退」而不是「被弹飞」；伤害本身照常结算、照常掉血。
            if (m is Monster em && em.IsEliteWave)
            {
                m.PushAlongX(0f, 0.2f);
                pushed.Add(m);
            }

            // 顿一下：只暂停自己的位移，全场时间照走（10-20 砍掉的全场慢放不再复活）
            yield return new WaitForSeconds(GameConfig.CAREER_CHARGE_HITSTOP);

            // 撞开这一簇：范围内每只怪各自朝背离撞击点的方向弹开 = 「往四周撞」
            // 【2026-11-14 主人拍板「冲撞的时候不要出现刀光的特效」】这里原本补一发 PlaySlash，已删；
            // 冲锋的观感只由半盾承担，不要再往撞击点补任何刀光。
            BurstChargeHit(hero, skill, cursor, baseDamage, alreadyHit, hitHalfW, hitHalfH);
            stops++;
        }

        if (!pinnedByBoss)
        {
            // 冲完剩余距离（顿帧用完 / 后面没怪的部分一路冲到位），顶着的精英一路跟着后退
            float rest = (end.x - cursor.x) * dir;
            if (rest > 0.02f)
                yield return CoChargeSlide(hero, cursor, end, chargeSpeed, chargeShield, shieldY, chargeDust, dustXOffset, pushed, chargeScaleSign);
            else
            {
                GameConfig.SetWorldPosition(hero.transform, end);
                SyncFollow(chargeShield, hero, shieldY);
                SyncFollow(chargeDust, hero, hero.FootY + GameConfig.CAREER_CHARGE_DUST_UP_WORLD, dustXOffset);
                // 收尾直接摆到位时也钉朝向，防止摆完被下一帧翻
                if (flipRoot != null)
                {
                    Vector3 esc = flipRoot.localScale;
                    flipRoot.localScale = new Vector3(Mathf.Abs(esc.x) * chargeScaleSign, esc.y, esc.z);
                }
            }

            // 落点收尾：只结算伤害，不补刀光（主人 2026-11-14「冲撞的时候不要出现刀光的特效」）
            BurstChargeHit(hero, skill, end, baseDamage, alreadyHit, hitHalfW, hitHalfH);
        }

        // 冲锋结束：半盾立刻消失（主人口径「冲锋完就消失」）；脚后尘土同批销毁；行动权交还 AI
        ReleaseChargeShield(chargeShield);
        if (chargeDust != null) Object.Destroy(chargeDust);
        EndCharging();
    }

    /// <summary>
    /// 破阵冲锋的半盾特效：生成后挂到英雄身上，随冲锋位移一起走。
    /// 资源走既定技能特效路径 Resources/VFX/Skills/{Ally}/ally_career_charge（SkillRegistry 同一条解析链），
    /// 缺文件按项目口径 LogError + 本次无盾，不做任何兜底特效。
    /// 不走池：本特效的存活期等于一次冲锋协程（0.5~3s 不定），提前回收会和池的定时释放互抢，
    /// 因此与 SkillRegistry.PlaySkillVfx 无 BattleVFXSystem 分支同口径，直接 Instantiate + 结束时 Destroy。
    /// </summary>
    static GameObject SpawnChargeShield(Hero hero, int faceDir, bool mastered)
    {
        if (hero == null || hero.isDead) return null;
        var prefab = SkillRegistry.Instance != null
            ? SkillRegistry.Instance.GetSkillVfxPrefab(ChargeShieldVfxId)
            : null;
        if (prefab == null)
        {
            Debug.LogError($"[CareerSkill] 破阵冲锋缺半盾特效：请放 Resources/VFX/Skills/Ally/{ChargeShieldVfxId}.prefab");
            return null;
        }

        GameObject go = Object.Instantiate(prefab, hero.GetHitPosition(), prefab.transform.rotation);
        // 朝向用世界 scale.x 正负定（Sprite 镜像），不依赖任何父级
        Vector3 baseScale = prefab.transform.localScale;
        // 【2026-11-15 主人「盾再小 10%」】只叠倍率，不碰 prefab；贴脚的两处测量读实例缩放，自动跟随。
        // 【2026-11-20 精通形态】精通=现倍率(0.9)；未精通再×0.5 = 盾小一半（主人「没精通前盾小，精通后就是现在的大小」）。
        float s = GameConfig.CAREER_CHARGE_SHIELD_SCALE_MUL
            * (mastered ? 1f : GameConfig.CAREER_CHARGE_UNMASTERED_SHIELD_MUL);
        go.transform.localScale = new Vector3(
            Mathf.Abs(baseScale.x) * s * (faceDir < 0 ? -1f : 1f), baseScale.y * s, baseScale.z * s);
        // 【2026-11-14 主人报「冲撞的特效没出来」】不挂到英雄节点下：SPUM 玩家预制体的 UnitRoot 上带
        // UnityEngine.Rendering.SortingGroup（GameConfig.ApplyUnitSorting 维护，order 随世界 Y 变化，
        // y=0.63 时只有 15-25=-10）。SortingGroup 会接管组内所有 Renderer 的排序，
        // 一旦盾落进那一组，写在 SpriteRenderer 上的 sortingOrder=50 就失效、被排到背景后面。
        // 现在盾放世界根 + 每帧同步坐标（CoChargeSlide 的 follow），排序只由它自己的 SortingGroup 决定。

        var sr = go.GetComponentInChildren<SpriteRenderer>(true);
        if (sr != null)
        {
            sr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            sr.sortingOrder = GameConfig.SORT_VFX;
        }
        // 再给盾自己挂一层 SortingGroup 兜住排序（口径仍读 GameConfig 单一出口）：
        // 只有它是组的根，才不会继承任何上级组的 order。
        var sg = go.GetComponent<UnityEngine.Rendering.SortingGroup>();
        if (sg == null) sg = go.AddComponent<UnityEngine.Rendering.SortingGroup>();
        sg.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
        sg.sortingOrder = GameConfig.SORT_VFX;
        // 冲锋可能长于一轮 1.1s：循环播放，存活完全交给 ReleaseChargeShield
        var sheet = go.GetComponentInChildren<PixelFxSheetPlayer>(true);
        if (sheet != null) sheet.SetLooping();

        // 诊断（2026-11-14 主人报「冲撞的特效没出来」）：一次跑完就能判定是 prefab 没取到 / 组件没挂上 /
        // sheet 没加载 / 位置跑偏 中的哪一种，不再靠猜。
        if (sr == null || sr.sprite == null || sheet == null)
            Debug.LogError($"[CareerSkill] 半盾异常 | prefab={prefab.name} sr={sr != null}"
                + $" sprite={(sr != null && sr.sprite != null)} sheet={sheet != null}"
                + "（sr/sprite 为空 = 组件没挂上或 sheet 没加载；sheet 为空 = 预制体丢了 PixelFxSheetPlayer）");
        else
            Debug.Log($"[CareerSkill] 半盾 spawn | sr={sr.name} sprite={sr.sprite.name} order={sr.sortingOrder}"
                + $" layer={sr.sortingLayerName} enabled={sr.enabled} lossy={go.transform.lossyScale}"
                + $" pos={go.transform.position} parent={(go.transform.parent != null ? go.transform.parent.name : "null")}");

        // 保险：协程被 StopAllCoroutines 打断（战斗结束/切关）时不留孤儿
        Object.Destroy(go, 8f);
        return go;
    }

    /// <summary>
    /// 【2026-11-15 主人「尘土这个特效在玩家冲锋的时候加到脚后面」】冲锋脚后尘土：
    /// 与半盾同口径放世界根 + 每帧同步，不挂英雄节点（SPUM UnitRoot 的 SortingGroup 会接管组内排序，
    /// 挂上去会被背景盖住）。【2026-11-15】小一半（倍率读 GameConfig）；prefab 原始 x=-1 是朝右冲时
    /// 往后扬的方向，朝左冲按 faceDir 镜像，尘土才跟着冲锋方向扬。
    /// 缺资源 LogError + 本次无尘土，不复制资源、不运行时补图。
    /// </summary>
    static GameObject SpawnChargeDust(Hero hero, int faceDir)
    {
        if (hero == null || hero.isDead) return null;
        GameObject prefab = Resources.Load<GameObject>(ChargeDustResourcePath);
        if (prefab == null)
        {
            Debug.LogError($"[CareerSkill] 冲锋尘土特效缺失：Resources/{ChargeDustResourcePath}.prefab");
            return null;
        }
        Vector3 pos = new Vector3(
            hero.GetHitPosition().x - faceDir * GameConfig.CAREER_CHARGE_DUST_BACK,
            hero.FootY + GameConfig.CAREER_CHARGE_DUST_UP_WORLD,   // 【2026-11-15】往上 10 设计像素
            hero.transform.position.z);
        GameObject go = Object.Instantiate(prefab, pos, prefab.transform.rotation);
        Vector3 baseScale = prefab.transform.localScale;
        float s = GameConfig.CAREER_CHARGE_DUST_SCALE_MUL;
        go.transform.localScale = new Vector3(
            baseScale.x * s * (faceDir < 0 ? -1f : 1f), baseScale.y * s, baseScale.z * s);
        var psr = go.GetComponent<ParticleSystemRenderer>();
        if (psr != null)
        {
            psr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            psr.sortingOrder = GameConfig.SORT_VFX;
        }
        var sg = go.GetComponent<UnityEngine.Rendering.SortingGroup>();
        if (sg == null) sg = go.AddComponent<UnityEngine.Rendering.SortingGroup>();
        sg.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
        sg.sortingOrder = GameConfig.SORT_VFX;
        Object.Destroy(go, 8f);   // 协程被强停时不留孤儿（与半盾同口径）
        return go;
    }

    static void ReleaseChargeShield(GameObject go)
    {
        if (go != null) Object.Destroy(go);
    }

    /// <summary>
    /// 匀速横移一段（只动 X，Y 由 lane 系统管）。
    /// follow = 要跟着走的特效（半盾），每帧只同步 X —— Y 用 <paramref name="fixedY"/> 锁死
    ///（2026-11-14 主人「护盾只是跟着玩家往前走不要上下移动」），玩家并道时盾不跟着上下飘。
    /// dustFollow = 脚后尘土（2026-11-15），Y 恒用 FootY、X 再叠 dustXOffset；两者共用同一同步出口。
    /// pushUnits = 【2026-11-16 主人「玩家能推着精英往后退一段距离」】被英雄顶着的精英：
    /// 英雄这一帧前进多少，它们就同量后退多少，间距恒为 CONTACT_GAP —— 表现是「推着走」而不是「弹飞」。
    /// </summary>
    static IEnumerator CoChargeSlide(Hero hero, Vector3 from, Vector3 to, float speed,
                                     GameObject follow = null, float fixedY = 0f,
                                     GameObject dustFollow = null, float dustXOffset = 0f,
                                     List<UnitBase> pushUnits = null,
                                     int scaleSign = 0)
    {
        float dur = Mathf.Max(0.02f, Mathf.Abs(to.x - from.x) / Mathf.Max(0.1f, speed));
        float t = 0f;
        var root = scaleSign != 0 ? hero.GetFlipRoot() : null;
        while (t < dur)
        {
            t += Time.deltaTime;
            float prevX = hero.transform.position.x;
            Vector3 next = Vector3.Lerp(from, to, Mathf.Clamp01(t / dur));
            GameConfig.SetWorldPosition(hero.transform, next);
            PushChargeUnits(pushUnits, next.x - prevX);
            MarkCharging();   // 心跳：即使协程被强停，标记最多残留一小段
            SyncFollow(follow, hero, fixedY);
            SyncFollow(dustFollow, hero, hero.FootY + GameConfig.CAREER_CHARGE_DUST_UP_WORLD, dustXOffset);
            if (root != null)
            {
                Vector3 sc = root.localScale;
                root.localScale = new Vector3(Mathf.Abs(sc.x) * scaleSign, sc.y, sc.z);
            }
            yield return null;
        }
        float tailX = hero.transform.position.x;
        GameConfig.SetWorldPosition(hero.transform, to);
        PushChargeUnits(pushUnits, to.x - tailX);   // 收尾那一帧的位移也补推，精英不会落后一抖
        SyncFollow(follow, hero, fixedY);
        SyncFollow(dustFollow, hero, hero.FootY + GameConfig.CAREER_CHARGE_DUST_UP_WORLD, dustXOffset);
    }

    /// <summary>
    /// 【2026-11-16】把被顶着的精英按英雄本帧位移同量后移（每帧续 0.15s 顶推锁，AI 不抢位置）。
    /// 死亡/回收的自动跳过；空名单直接返回，普通冲锋（没顶到精英）行为一字不变。
    /// </summary>
    static void PushChargeUnits(List<UnitBase> units, float deltaX)
    {
        if (units == null || units.Count == 0 || Mathf.Abs(deltaX) < 0.0001f) return;
        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            if (u == null || u.isDead) continue;
            u.PushAlongX(deltaX, 0.15f);
        }
    }

    /// <summary>
    /// 「真 Boss」口径，与 Monster.Skills.cs 的长 CD 判定同一份：
    /// 贴图编号 ≥ BOSS_SPRITE_START 被误判成 _isBossUnit 的精英（IsEliteWave=true）不算，仍按精英处理。
    /// </summary>
    static bool IsTrueBoss(UnitBase u)
        => u is Monster mon && mon.IsBossUnit && !mon.IsEliteWave;

    /// <summary>
    /// 【2026-11-14 主人「护盾只是跟着玩家往前走不要上下移动」】半盾只跟 X；
    /// Y 在 spawn 时按「脚底 + 锚点到盾底的距离 − 下移量」定死（见 CoCharge），此后不再变。
    /// </summary>
    static void SyncFollow(GameObject follow, Hero hero, float fixedY, float xOffset = 0f)
    {
        if (follow == null || hero == null) return;
        follow.transform.position = new Vector3(hero.GetHitPosition().x + xOffset, fixedY, hero.transform.position.z);
    }

    // ── 冲锋占用英雄行动权的标记（2026-11-14 主人「不要冲锋的时候还在攻击」）──
    // 用「过期时刻」而不是 bool：协程被 StopAllCoroutines 强停时也能在心跳窗口后自动解除，不靠 finally。
    static float _chargeBusyUntil;

    /// <summary>冲锋进行中：英雄位移/朝向归冲锋协程，AI 不索敌、不出手、不写速度（Hero.AIUpdate 读它）。</summary>
    public static bool IsCharging => Time.time < _chargeBusyUntil;

    /// <summary>续期「冲锋中」标记（心跳窗口，防止任何路径漏清导致永久锁死）。</summary>
    static void MarkCharging() => _chargeBusyUntil = Time.time + 0.6f;

    static void EndCharging() => _chargeBusyUntil = 0f;

    /// <summary>
    /// 【2026-11-15 主人「最下面都要和脚的位置对齐」】半盾「可见最下沿」贴 <c>FootY</c>：
    /// <see cref="MeasureShieldFootOffset"/> 量的是整帧矩形底边，还差帧底那截透明边，
    /// 扣掉 sheet 真源的 footTrimPx 才是玩家看到的盾底（取代 11-14/11-15 手工下移 30→50 的补偿）。
    /// 与重斩共用 <see cref="PixelFxSheetPlayer.VisibleFootTrimWorld"/> 同一出口，不写第二份数值。
    /// </summary>
    static float ShieldFootTrimWorld(GameObject go)
    {
        if (go == null) return 0f;   // spawn 已 LogError，不重复报
        var sheet = go.GetComponent<PixelFxSheetPlayer>();
        if (sheet == null)
        {
            Debug.LogError("[CareerSkill] 半盾缺 PixelFxSheetPlayer，无法按可见最下沿对齐脚底");
            return 0f;
        }
        return sheet.VisibleFootTrimWorld;
    }

    /// <summary>
    /// 量「半盾锚点 → sprite 最低点」的世界距离（已含预制体 localScale）。
    /// 调用时机必须是 <b>刚 Instantiate、go 正摆在英雄受击点上</b> 的那一刻：此时
    /// <c>bounds.min.y</c> 就是盾摆在该位置时底边的世界 Y，差值即所需偏移。
    /// 不在本类写死像素/世界数值 —— 改预制体缩放，这里自动跟随。
    /// </summary>
    static float MeasureShieldFootOffset(GameObject go)
    {
        if (go == null) return 0f;
        var sr = go.GetComponentInChildren<SpriteRenderer>(true);
        if (sr == null || sr.sprite == null) return 0f;
        return go.transform.position.y - sr.bounds.min.y;
    }

    /// <summary>
    /// 量半盾特效的世界尺寸（含预制体缩放与 SpawnChargeShield 叠的倍率），供撞击判定框使用。
    /// 取不到（prefab 缺 SpriteRenderer / 帧未加载）时返回 0，由调用方下限兜住；
    /// 缺资源本身已在 SpawnChargeShield 里 LogError，这里不再重复报。
    /// </summary>
    static void MeasureShieldWorldSize(GameObject go, out float width, out float height)
    {
        width = 0f; height = 0f;
        if (go == null) return;
        var sr = go.GetComponentInChildren<SpriteRenderer>(true);
        if (sr == null || sr.sprite == null) return;
        width = sr.bounds.size.x;
        height = sr.bounds.size.y;
    }

    /// <summary>
    /// 收集冲锋走廊里的怪（按 X 由近到远）。
    /// 纵向（lane）容差 = 撞击判定框的半高（与 BurstChargeHit 同一份口径），不写第二份数值。
    /// 【2026-11-16 主人报「只剩 Boss 一个时冲过去血没减」】删掉旧版按配表 aoeMaxTargets 的截断：
    /// 那会把走廊里第 4 只起的怪（Boss 固定占本波最后名额、永远站最远）整体丢掉，
    /// 导致 Boss 既不被当撞击点、落点也补不到。分段次数本身已由 HITSTOP_MAX 限制，不会拖沓。
    /// </summary>
    static List<UnitBase> CollectChargeVictims(Hero hero, Vector3 from, Vector3 to, float laneTol)
    {
        var list = new List<UnitBase>();
        var bm = BattleManager.Instance;
        if (bm == null || bm.monsters == null || hero == null) return list;
        float lane = Mathf.Max(0.1f, laneTol);
        float xMin = Mathf.Min(from.x, to.x);
        float xMax = Mathf.Max(from.x, to.x);
        for (int i = 0; i < bm.monsters.Count; i++)
        {
            var m = bm.monsters[i];
            if (m == null || m.isDead) continue;
            if (!GameConfig.IsInCombatViewport(m)) continue;
            Vector3 p = m.transform.position;
            if (p.x < xMin || p.x > xMax) continue;          // 只认路线本身
            if (Mathf.Abs(p.y - from.y) > lane) continue;    // 只撞判定框盖得住的那几行
            list.Add(m);
        }
        list.Sort((a, b) => Mathf.Abs(a.transform.position.x - from.x)
            .CompareTo(Mathf.Abs(b.transform.position.x - from.x)));
        return list;
    }

    /// <summary>
    /// 单次撞击爆开：以 center 为中心的矩形框（半宽 halfW / 半高 halfH）内的怪全部结算，
    /// 尺寸来自半盾的世界大小（2026-11-16 主人「应该是盾的一半大小的范围」），不再用写死半径 + 整列容差。
    /// 击退方向交给 <see cref="HitOne"/> 的老口径「从 center 往外推」—— 左的半边往左、右的半边往右，
    /// 就是主人要的「往四周撞」。alreadyHit 防同一只怪被相邻两次撞击重复打。
    /// skill 参数保留以维持调用形状，判定不再读它的 aoeRadius / aoeMaxTargets。
    /// </summary>
    static void BurstChargeHit(Hero hero, SkillSystem.ActiveSkill skill, Vector3 center,
        float baseDamage, HashSet<UnitBase> alreadyHit, float halfW, float halfH)
    {
        var bm = BattleManager.Instance;
        if (bm == null || bm.monsters == null || hero == null) return;
        for (int i = 0; i < bm.monsters.Count; i++)
        {
            var m = bm.monsters[i];
            if (m == null || m.isDead) continue;
            if (alreadyHit.Contains(m)) continue;
            if (!GameConfig.IsInCombatViewport(m)) continue;
            Vector3 p = m.transform.position;
            if (Mathf.Abs(p.x - center.x) > halfW) continue;
            if (Mathf.Abs(p.y - center.y) > halfH) continue;
            alreadyHit.Add(m);   // 先标记：TakeDamage 可能触发连锁，避免本次内重复
            HitOne(hero, m, baseDamage, center, GameConfig.CAREER_CHARGE_KNOCKBACK);
        }
    }

    // —— 游侠：一次齐射多条独立箭道（结构照 Monster.CoFanVolley），每条飞到自己的终点再结算 ——
    static IEnumerator CoScatter(Hero hero, SkillSystem.ActiveSkill skill, UnitBase target, bool mastered)
    {
        float delay = GameConfig.RANGED_FIRE_RELEASE_DELAY_RANGER;
        if (delay > 0.001f) yield return new WaitForSeconds(delay);
        if (hero == null || hero.isDead || BattleVFXSystem.Instance == null) yield break;

        Vector3 fire = hero.GetFirePosition();
        int facing = hero.GetVfxFacingDir();
        Vector3 aim = target != null && !target.isDead
            ? target.GetHitPosition()
            : fire + Vector3.right * facing * GameConfig.CAREER_SCATTER_RANGE;
        Vector2 baseDir = new Vector2(aim.x - fire.x, aim.y - fire.y);
        if (baseDir.sqrMagnitude < 1e-6f) baseDir = new Vector2(facing, 0f);
        float baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;
        float flyDist = Mathf.Min(baseDir.magnitude, GameConfig.CAREER_SCATTER_RANGE);

        // 【2026-11-20 精通形态】精通=配表箭数(5)；未精通=3 条（主人「一开始射3个箭，精通后6个」——以现配表为精通基准，未精通弱化）。
        // 角度步长 GameConfig.CAREER_SCATTER_ANGLE_STEP 不变，箭少则扇面窄、箭多则扇面宽，天然区分。
        int count = mastered
            ? Mathf.Max(1, skill.projectileCount)
            : Mathf.Max(1, GameConfig.CAREER_SCATTER_UNMASTERED_ARROWS);
        Debug.Log($"[CareerSkill] 散射箭 演出开始 {count} 条箭道 mastered={mastered}");
        var hitCount = new Dictionary<UnitBase, int>();
        float baseDamage = BuildDamage(hero, skill);

        for (int i = 0; i < count; i++)
        {
            float ang = (baseAngle + (i - (count - 1) * 0.5f) * GameConfig.CAREER_SCATTER_ANGLE_STEP) * Mathf.Deg2Rad;
            Vector2 d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            Vector3 end = fire + (Vector3)(d * flyDist);
            Vector3 lockedEnd = end;
            BattleVFXSystem.Instance.PlaySkillProjectile(
                VfxFaction.Ally, fire, end, facing, null, AttackVfxKit.Bow, null,
                1f, 1f,
                () => DealArrowHit(hero, lockedEnd, baseDamage, hitCount),
                caster: hero);
        }
    }

    static void DealArrowHit(Hero hero, Vector3 end, float baseDamage, Dictionary<UnitBase, int> hitCount)
    {
        var bm = BattleManager.Instance;
        if (bm == null || bm.monsters == null || hero == null) return;
        UnitBase best = null;
        float bestD = float.MaxValue;
        for (int i = 0; i < bm.monsters.Count; i++)
        {
            var m = bm.monsters[i];
            if (m == null || m.isDead) continue;
            if (!GameConfig.IsInCombatViewport(m)) continue;
            float d = Vector2.Distance(end, m.transform.position);
            if (d > GameConfig.CAREER_SCATTER_HIT_RADIUS || d >= bestD) continue;
            if (hitCount.TryGetValue(m, out int n) && n >= GameConfig.CAREER_SCATTER_MAX_HITS_PER_ENEMY) continue;
            bestD = d;
            best = m;
        }
        if (best == null) return;
        hitCount.TryGetValue(best, out int prev);
        hitCount[best] = prev + 1;
        HitOne(hero, best, baseDamage, end, GameConfig.CAREER_SKILL_KNOCKBACK);
    }

    // —— 法师：巨型法球沿朝向飞一段距离后自爆，范围伤害 + 强击退 ——
    static IEnumerator CoOrb(Hero hero, SkillSystem.ActiveSkill skill, UnitBase target, bool mastered)
    {
        Vector3 fire = hero.GetFirePosition();
        int facing = hero.GetVfxFacingDir();
        // 【2026-11-14 主人「要往前飞一段距离后爆炸」】落点不再锁在最近目标身上（贴身目标会让法球几乎原地炸），
        // 改为沿朝向固定前飞 CAREER_ORB_FLIGHT_DIST 后自爆；lockedEnd 即爆点＝范围伤害中心。
        // 【2026-11-15】固定 8f 大于竖屏全宽（orthoSize×aspect×2≈6.08）会飞出屏外，且旧版
        // 「到怪群就炸」的落点上限在 11-14 被一并丢掉；11-15 又用「最远可见怪」封顶，
        // 11-23 起改为怪群重心＋前行提前量（见下）。屏内剩余仍由 edgeLimit 封顶，绝不出屏。
        float orbDist = GameConfig.CAREER_ORB_FLIGHT_DIST;
        var cam = Camera.main;
        bool hasEdge = cam != null && cam.orthographic;
        float edgeLimit = 0f;   // 沿朝向还剩多少屏内空间（防飞出屏）
        if (hasEdge)
        {
            float halfW = cam.orthographicSize * cam.aspect;
            float edgeX = cam.transform.position.x + facing * (halfW - 0.3f);
            edgeLimit = Mathf.Max(0f, (edgeX - fire.x) * facing);
            orbDist = Mathf.Min(orbDist, edgeLimit);
        }
        // 【2026-11-23 主人「法师发射大火球的时候发射到敌人后面，而敌人都在往前走导致打中的敌人少了」】
        // 旧版把前飞距离压到「朝向最远那只可见怪的当前位置」（farthest）＝爆点钉在怪群最靠外的一端，
        // 而怪群整体朝英雄走、法球又慢（0.55×0.4 ≈ 5.3 单位/秒），落地时怪已移开半个身位，
        // 爆点始终留在怪群身后；DealArea 再按「离爆点最近」取前 N 个，命中名单就永远是靠外那几只。
        // 现改为：① 落点取屏内怪群 x 的中位数（重心），且剔除刚刷在屏外 margin 0.35 的怪，
        // 避免新刷怪把落点拽到屏幕边缘；② 按飞行时间 t = 距离 / 实际弹速补偿怪群平均横向速度，
        // 落点加 vx*t 提前量，让爆炸盖在「法球到达那一刻」的怪群中心；
        // ③ 提前量不越过最靠英雄的那只怪（否则炸到已经走过去的空位），也不越过最外那只，
        // 并仍受屏内边缘 / CAREER_ORB_FLIGHT_DIST 约束，绝不出屏。
        var bm = BattleManager.Instance;
        if (bm != null && bm.monsters != null)
        {
            var xs = new List<float>(8);
            float vxSum = 0f;
            for (int i = 0; i < bm.monsters.Count; i++)
            {
                var m = bm.monsters[i];
                if (m == null || m.isDead) continue;
                // margin 传 0：只用真正在屏内的怪；屏外刚刷出来的怪不参与落点
                if (!GameConfig.IsInCombatViewport(m, 0f)) continue;
                var mp = m.transform.position;
                xs.Add(mp.x);
                vxSum += m.rb != null ? m.rb.velocity.x : -facing * m.attr.GetAttr(AttrType.MoveSpeed);
            }
            if (xs.Count > 0)
            {
                xs.Sort();
                float medianX = xs[xs.Count / 2];
                float aheadMedian = (medianX - fire.x) * facing;
                // 怪群整体在英雄背后（已贴身越过）时不用重心，沿用固定前飞，避免原地炸
                if (aheadMedian > 0.05f)
                {
                    float vfxSpeed = BattleVFXSystem.Instance != null
                        ? BattleVFXSystem.Instance.projectileSpeed : 24f;
                    float speed = Mathf.Max(0.1f, vfxSpeed * Mathf.Max(0.05f, GameConfig.CAREER_ORB_SPEED_MUL)
                                                         * GameConfig.PROJECTILE_SPEED_GLOBAL_MUL);
                    float travelT = aheadMedian / speed;
                    float predX = medianX + (vxSum / xs.Count) * travelT;
                    float closestX = facing >= 0 ? xs[0] : xs[xs.Count - 1];
                    float farthestX = facing >= 0 ? xs[xs.Count - 1] : xs[0];
                    if ((predX - closestX) * facing < 0f) predX = closestX + facing * 0.1f;
                    if ((predX - farthestX) * facing > 0f) predX = farthestX;
                    orbDist = Mathf.Max(0f, (predX - fire.x) * facing);
                }
            }
        }
        if (hasEdge) orbDist = Mathf.Min(orbDist, edgeLimit);
        Vector3 end = fire + Vector3.right * facing * orbDist;
        Vector3 lockedEnd = end;
        // 【2026-11-14 主人「法师的大法球技能发射点再往前点」】只给本发大法球的起点沿朝向额外前移，
        // 不改全局 firePointOffset，爆点 lockedEnd 与伤害中心不动。
        // 前移量不超过净飞行距离，避免起点被推过爆点让法球往回飞。
        float roomAhead = Mathf.Max(0f, (end.x - fire.x) * facing);
        fire += Vector3.right * facing * Mathf.Min(GameConfig.CAREER_ORB_FIRE_FORWARD, roomAhead);
        // 【2026-11-20 精通形态】精通=现大小(倍率2)、命中配表数(4)；未精通=小法球(倍率1)、命中 3
        //（主人「一开始小法球打3个，精通后大的打5个」——以现配表为精通基准，未精通弱化；命中数随 scale 一起降）。
        float scale = mastered
            ? GameConfig.CAREER_ORB_SCALE_MUL
            : GameConfig.CAREER_ORB_UNMASTERED_SCALE_MUL;
        int maxTargets = mastered
            ? skill.aoeMaxTargets
            : GameConfig.CAREER_ORB_UNMASTERED_MAX_TARGETS;
        Debug.Log($"[CareerSkill] 大法球 演出开始 scale={scale:0.0} maxTargets={maxTargets} mastered={mastered} fireX={fire.x:0.00}");
        BattleVFXSystem.Instance.PlaySkillProjectile(
            VfxFaction.Ally, fire, end, facing, target != null ? target.transform : null,
            AttackVfxKit.Orb, null, scale, GameConfig.CAREER_ORB_SPEED_MUL,
            () => DealArea(hero, skill, lockedEnd, GameConfig.CAREER_SKILL_KNOCKBACK, maxTargets),
            // 只给本发职业技能传技能 id（player_skills.csv 的 career_orb）选 3 号专属套；
            // 普攻法球不传，仍走旧名 Base。
            caster: hero, impactScaleMul: scale * GameConfig.CAREER_ORB_IMPACT_SCALE_MUL, skillId: "career_orb");
        yield break;
    }

    static float BuildDamage(Hero hero, SkillSystem.ActiveSkill skill)
    {
        return DamageFormula.BuildSkillBase(skill.baseDamage, skill.damageMultiplier,
            hero.attr, hero.IsMagicDamageDealer());
    }

    /// <summary>maxTargetsOverride：精通形态传进来的命中数（未精通 3 / 精通读配表）；&lt;=0 时回退配表值。</summary>
    static void DealArea(Hero hero, SkillSystem.ActiveSkill skill, Vector3 center, float knockDist, int maxTargetsOverride)
    {
        var bm = BattleManager.Instance;
        if (bm == null || bm.monsters == null || hero == null) return;
        // 【2026-11-20 法师「溅射」天赋】英雄的 AoeRadius 加成（右列天赋写入，0.1 = +10%）放大法球爆炸半径。
        // 只作用于本方法（法师大法球专属）；其它 AOE 技能（星落/天雷/重斩）走各自演出，不吃这个加成。
        float radius = skill.aoeRadius;
        if (hero.attr != null)
        {
            float aoeBonus = hero.attr.GetAttr(AttrType.AoeRadius);
            if (aoeBonus > 0f) radius *= (1f + aoeBonus);
        }
        var list = new List<UnitBase>();
        for (int i = 0; i < bm.monsters.Count; i++)
        {
            var m = bm.monsters[i];
            if (m == null || m.isDead) continue;
            if (!GameConfig.IsInCombatViewport(m)) continue;
            if (Vector2.Distance(center, m.transform.position) > radius) continue;
            list.Add(m);
        }
        int maxTargets = maxTargetsOverride > 0 ? maxTargetsOverride : skill.aoeMaxTargets;
        if (maxTargets > 0 && list.Count > maxTargets)
        {
            list.Sort((a, b) => Vector2.Distance(center, a.transform.position)
                .CompareTo(Vector2.Distance(center, b.transform.position)));
            list.RemoveRange(maxTargets, list.Count - maxTargets);
        }
        float baseDamage = BuildDamage(hero, skill);
        for (int i = 0; i < list.Count; i++)
            HitOne(hero, list[i], baseDamage, center, knockDist);
    }

    static void HitOne(Hero hero, UnitBase victim, float baseDamage, Vector3 center, float knockDist)
    {
        if (victim == null || victim.isDead) return;
        float dmg = DamageFormula.ApplyCrit(baseDamage, hero.attr, out bool isCrit);
        dmg = DamageFormula.ApplyAttackerSpecials(dmg, hero, victim);
        // 击退统一「从 center 往外推」：撞击点的左半边往左、右半边往右 = 往四周撞散。
        int dir = victim.transform.position.x >= center.x ? 1 : -1;
        victim.TakeDamage(dmg, isCrit, false, true, dir, hero, false, knockDist);
    }
}
