using UnityEngine;

/// <summary>
/// AI 行为：索敌、交战、离队收绳（UpdateHeroLeash / 回召 / 目标刷新 / 跟随待机）。
/// 【2026-10-23 主人拍板】原 Mercenary.cs 已 1025 行（项目约定 >600 行必须拆），
/// 按职责切成 4 个 partial，本文件是其中之一。代码为原样搬运，逻辑未改。
/// </summary>
public partial class Mercenary : UnitBase
{
    /// <summary>【2026-11-15 主人拍板「就算怪物不贴近 佣兵也要去寻敌 / 上前战斗」】
    /// 我方佣兵交战前压停步线：距目标 ≤ 有效射程 × 此比例才站桩（0.55 ≈ 走进射程一半）。
    /// 只影响「已进攻击射程后是否继续往前走」，不改射程、攻击 CD、伤害与索敌。
    /// 【2026-11-22】改 protected virtual：佣兵默认仍 0.55（行为零变化），宠物 override 成进射程即站桩。
    /// 【2026-11-23 主人拍板「牧师攻击距离太近」】牧师佣兵不参与前压：进射程（1.0 × 射程）即站桩 ——
    /// 否则射程调到 2.30 也会被 0.55 拉到 1.27 贴脸站。判据走花名册职业名，其它职业仍 0.55。</summary>
    protected virtual float EngageDistRatio
    {
        get
        {
            if (MercRosterDefs.IsPriestJob(hireId, mercId)) return 1f;
            return 0.55f;
        }
    }

    // —— 屏外走入（2026-10-31 主人拍板「竞技场敌人从屏幕外走进来，不能突然出现」）——
    // 仅敌方佣兵（isAlly=false，竞技场机器人小队）使用；己方抽卡入场走 SpawnRunMerc 的拴绳回队，不经过这里。
    bool _mapEnterActive;
    float _mapEnterTargetX;
    /// <summary>是否仍在屏外走入段（供演出侧等"走到位再开口"，读的就是 _mapEnterActive 单一真源）。</summary>
    public bool IsMapEntering => _mapEnterActive;
    // 【2026-10-31 晚】开打前抽到己方佣兵：AI 闸（UnitsCanAct=false）不解冻，走入段会被定在屏外
    // （这正是早上「抽到佣兵没有立即上阵」的根因，当时误改成编队位瞬现）。
    // 抽卡入场这条线改传 walkWhileFrozen=true，让佣兵在冻场期间也由代码驱动走回编队位。
    // 默认 false：竞技场敌方小队 / 怪物入场等现有调用方「开打前定住、解冻才走」的口径不变。
    bool _mapEnterWalkWhileFrozen;

    // —— 战斗体验（2026-11-06 主人拍板）——
    // 新入队冲刺：战斗中抽到/获得佣兵时，以更高倍率冲到玩家身边（到 MERC_RUSH_STOP_DIST 结束）。
    // 掉队追赶：离玩家超过 MERC_LAG_ENTER_DIST 自动加速，回到 MERC_LAG_EXIT_DIST 内结束（滞回防抖）。
    // 两项都只是「临时移速倍率」，由 GetCombatMoveSpeed 覆写统一生效，
    // 阵型/站位/索敌规则一行不动；敌方佣兵（竞技场机器人）不参与。
    bool _rushToHero;
    bool _lagBoost;

    /// <summary>战斗中抽到/获得佣兵：开启入队冲刺（走到玩家附近自动结束）。</summary>
    public void BeginRushToHero()
    {
        _rushToHero = true;
    }

    /// <summary>每帧刷新冲刺/掉队加速的开关状态（AIUpdate 开头调用，只读距离，不改站位）。</summary>
    void UpdateSpeedBoostState()
    {
        if (!isAlly)
        {
            _rushToHero = false;
            _lagBoost = false;
            return;
        }

        var hero = Hero.Instance;
        if (hero == null || hero.isDead)
        {
            _rushToHero = false;
            _lagBoost = false;
            return;
        }

        float dist = Mathf.Abs(GetCombatX(this) - GetCombatX(hero));

        if (_rushToHero && dist <= GameConfig.MERC_RUSH_STOP_DIST)
            _rushToHero = false;

        if (dist > GameConfig.MERC_LAG_ENTER_DIST)
            _lagBoost = true;
        else if (dist <= GameConfig.MERC_LAG_EXIT_DIST)
            _lagBoost = false;
    }

    /// <summary>
    /// 【2026-11-06】移速统一出口的覆写：只叠「入队冲刺 / 掉队追赶」两个临时倍率。
    /// - 开打前剧本走入（_mapEnterActive）不吃加速，保持原有入场节奏；
    /// - 敌方佣兵不吃（竞技场敌方小队口径不变）；
    /// - 冲刺优先于追赶，两者不叠乘；玩家与怪物仍走基类实现。
    /// </summary>
    protected override float GetCombatMoveSpeed()
    {
        float spd = base.GetCombatMoveSpeed();
        if (!isAlly || _mapEnterActive) return spd;
        if (_rushToHero) return spd * GameConfig.MERC_RUSH_SPEED_MUL;
        if (_lagBoost) return spd * GameConfig.MERC_LAG_SPEED_MUL;
        return spd;
    }

    /// <summary>
    /// 从屏外缓步走到 targetX（到达后交还普通 AI）。速度 = 自身移速，朝向 faceDir。
    /// walkWhileFrozen=true = UnitsCanAct=false（开打前）也照走（抽卡入场专用）。
    /// </summary>
    public void BeginMapEnter(float targetX, int faceDir, bool walkWhileFrozen = false)
    {
        _mapEnterTargetX = targetX;
        _mapEnterActive = true;
        _mapEnterWalkWhileFrozen = walkWhileFrozen;
        facingDir = faceDir > 0 ? 1 : -1;
        ApplyFacing(facingDir);
        if (unitAnim != null) unitAnim.SetMove(true, facingDir);
    }

    /// <summary>走入段：UnitsCanAct=false（开打前）与怪物同口径定住不走进，解冻才走；到点收队交还普通 AI。</summary>
    bool StepMapEnter()
    {
        float dx = _mapEnterTargetX - GetCombatX(this);
        // 【2026-11-14 主人报「佣兵还是一会就跑后面消失」】走入速度提到 3.5 倍后，
        // 低帧率下每帧位移可能超过旧的 0.08 到点阈值 → 越过目标后 dir 反号、左右震荡，
        // _mapEnterActive 永不结束，AIUpdate 每帧在走入段 return（拴绳 / 回队整段不执行）。
        // 到点容差按本帧位移放大：走过目标即算到点，交还普通 AI。
        float step = GetCombatMoveSpeed() * GameConfig.MERC_ENTER_SPEED_MUL * Time.deltaTime;
        if (Mathf.Abs(dx) <= Mathf.Max(0.08f, step))
        {
            _mapEnterActive = false;
            if (rb != null) rb.velocity = Vector2.zero;
            if (unitAnim != null) unitAnim.SetMove(false, facingDir);
            return false;
        }
        int dir = dx > 0f ? 1 : -1;
        facingDir = dir;
        ApplyFacing(facingDir);
        // 【2026-11-14 主人报「佣兵出来得太慢」】走入段原本吃 1.0 倍移速（屏外到编队位约 9 个单位 → 9 秒多），
        // 单独乘 MERC_ENTER_SPEED_MUL 加速；GetCombatMoveSpeed 在 _mapEnterActive 时故意不吃冲刺/追赶倍率，这里显式乘。
        if (rb != null)
            rb.velocity = new Vector2(dir * GetCombatMoveSpeed() * GameConfig.MERC_ENTER_SPEED_MUL, rb.velocity.y);
        if (unitAnim != null) unitAnim.SetMove(true, facingDir);
        return true;
    }

    /// <summary>【临时诊断，定位后删除】敌方单位 AI 状态，每秒一条。</summary>
    float _aiDbgTimer;
    void LogEnemyAiStateOncePerSec()
    {
        if (isAlly) return;
        _aiDbgTimer -= Time.deltaTime;
        if (_aiDbgTimer > 0f) return;
        _aiDbgTimer = 1f;
        bool hasTarget = target != null && !target.isDead;
        float dist = hasTarget ? Mathf.Abs(GetCombatX(this) - GetCombatX(target)) : -1f;
        Debug.Log($"[ArenaAI] {mercId} x={GetCombatX(this):F2} mapEnter={_mapEnterActive} " +
                  $"canAct={(BattleManager.Instance != null ? BattleManager.Instance.UnitsCanAct : false)} " +
                  $"target={(hasTarget ? target.name : "null")} dist={dist:F2} " +
                  $"range={(attr != null ? attr.GetAttr(AttrType.AttackRange) : -1f):F2} eff={GetEffectiveAttackRange():F2} " +
                  $"atkLock={(unitAnim != null && unitAnim.InAttackLock)} dmgRec={(unitAnim != null && unitAnim.InDamagedRecovery())} " +
                  $"spd={GetCombatMoveSpeed():F2} hp={currentHp:F0} stun={IsStunned}");
    }

    // 【2026-11-19 临时探针 · 佣兵打不到屏外怪 / 边缘乱转归因，定位后连同调用点删除】
    // 我方锁到屏外目标时每秒一条：记录各层回队/攻击闸的实时距离 + 朝向与速度方向是否一致，
    // 一次定位「打不到」的主瓶颈和「乱转」是朝向与位移相反还是回队/交战拉锯。
    float _offscreenProbeTimer;
    void LogAllyOffscreenChaseProbe()
    {
        if (!isAlly || target == null || target.isDead) return;
        if (GameConfig.IsInCombatViewport(target)) return;   // 只查屏外追击场景
        _offscreenProbeTimer -= Time.deltaTime;
        if (_offscreenProbeTimer > 0f) return;
        _offscreenProbeTimer = 1f;
        var hero = Hero.Instance;
        var cam = Camera.main;
        float distHero = hero != null && !hero.isDead
            ? Mathf.Abs(GetCombatX(this) - GetCombatX(hero)) : -1f;
        float distTgt = GetCombatDist(this, target);
        float myX = GetCombatX(this);
        bool atLeftEdge = cam != null && cam.orthographic
            && myX < cam.transform.position.x - cam.orthographicSize * cam.aspect + 0.6f;
        float vx = rb != null ? rb.velocity.x : 0f;
        int velDir = Mathf.Abs(vx) < 0.05f ? 0 : (vx > 0f ? 1 : -1);
        Debug.Log($"[ProbeMercOffscreen] {mercId} myX={myX:F2} " +
                  $"distHero={distHero:F2} distTgt={distTgt:F2} " +
                  $"attackGate={GameConfig.GetCombatScreenWidth():F2} " +
                  $"leash={GameConfig.MERC_LEASH_RADIUS:F2} stick={GameConfig.MERC_STICK_RADIUS:F2} " +
                  $"leftEdge={atLeftEdge} recalling={_recallingToHero} " +
                  $"face={facingDir} vel={velDir} " +
                  $"inRange={IsInBasicAttackRange(target)} cd={attackCd:F2}");
    }

    /// <summary>
    /// 【2026-11-14 主人报「佣兵一会就跑后面消失」】相机只跟玩家（CameraFollow 仅 X 跟随），
    /// 竖屏半宽约 3 个世界单位；MERC_LEASH_RADIUS=4.2 比屏幕还宽 —— 按旧口径佣兵必须先跑出
    /// 左边屏幕才会被拴绳拉回。而拴绳（UpdateHeroLeash）排在走入段 / 攻击锁 / 受击硬直 / 集结 /
    /// 牧师定身之后，任一分支 return 就整帧不判回队，玩家继续右推 → 越落越远、出屏消失。
    /// 这里只在「快到相机左缘」或「已在回队途中」时抢回行动权，沿玩家方向追回编队距离；
    /// 屏内正常交战 / 入场 / 集结 / 传送门 / 竞技场敌方完全不介入。
    /// </summary>
    bool TryForceRejoinWhenOffscreen()
    {
        if (!isAlly || _mapEnterActive || TutorialStunned || BattleRally.Active) return false;
        var bm = BattleManager.Instance;
        if (bm == null || !bm.UnitsCanAct || bm.PortalWalkMode) return false;
        var hero = Hero.Instance;
        if (hero == null || hero.isDead) return false;
        var cam = Camera.main;
        if (cam == null || !cam.orthographic) return false;

        float myX = GetCombatX(this);
        float heroX = GetCombatX(hero);
        float distHero = Mathf.Abs(heroX - myX);

        if (_recallingToHero)
        {
            // 已在回队途中：一路追回编队距离再交还普通 AI，中途不再被攻击锁 / 受击硬直打断
            if (distHero <= GameConfig.MERC_LAG_EXIT_DIST)
            {
                _recallingToHero = false;
                _awayFightTimer = 0f;
                return false;
            }
        }
        else
        {
            // 相机中心≈玩家；出屏前 0.6 个单位就开追（半宽按当前屏幕比例算，横竖屏都成立）
            float halfW = cam.orthographicSize * cam.aspect;
            float leftEdge = cam.transform.position.x - halfW + 0.6f;
            if (myX >= leftEdge) return false;   // 还在屏内：照旧走下面的拴绳 / 交战
        }

        BeginRecallToHero();
        int dir = heroX > myX ? 1 : -1;
        // 【2026-11-19 主人「佣兵到屏幕边上会左右乱转」】原实现把 facingDir 写死为 1（朝右），
        // 但速度方向 dir 由玩家位置决定（玩家在左时 dir=-1）→ 面朝右却往左走，边缘反复触发
        // 「回队/交战」切换时朝向与位移对不上，看起来就是左右乱转。改为朝向跟移动同向。
        facingDir = dir;
        ApplyFacing(facingDir);
        // 不走 GetCombatMoveSpeed 覆写，避免与 lagBoost / rush 叠乘；紧急回队固定吃入队冲刺倍率
        float speed = base.GetCombatMoveSpeed() * GameConfig.MERC_RUSH_SPEED_MUL;
        if (rb != null) rb.velocity = new Vector2(dir * speed, rb.velocity.y);
        if (unitAnim != null) unitAnim.SetMove(true, facingDir);
        ApplyLaneY(Time.deltaTime);
        return true;
    }

    protected override void AIUpdate()
    {
        // 【2026-11-06】先刷新冲刺/掉队加速开关，本帧所有移动点统一走 GetCombatMoveSpeed。
        UpdateSpeedBoostState();

        // 【2026-11-19 临时探针 · 佣兵打不到屏外怪归因，定位后连同调用点删除】
        // 只在我方追「屏外」目标时每秒一条，一次区分是哪层在拉回：左缘强制回队 / 聚队 / 拴绳 /
        // 偏远交战 / 攻击闸。放在强制回队之前，才能记录到触发前的真实状态。
        LogAllyOffscreenChaseProbe();

        // 【2026-11-14】回队判定必须在所有早退分支之前（详见 TryForceRejoinWhenOffscreen 注释）。
        if (TryForceRejoinWhenOffscreen())
            return;

        // 【2026-11-01 临时诊断】主人报「竞技场玩家形象的机器人（弓）一直往左走 不攻击，佣兵攻击了」
        // —— 敌方单位每秒一条 AI 状态，定位卡在哪条分支；定位根因后整段删掉。
        LogEnemyAiStateOncePerSec();

        if (TutorialStunned)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            if (_stunAnimTimer < 9000f)
            {
                _stunAnimTimer -= Time.deltaTime;
                if (_stunAnimTimer <= 0f && unitAnim != null)
                {
                    unitAnim.PlayDebuff();
                    _stunAnimTimer = 1.6f;
                }
            }
            return;
        }

        // 屏外走入段（在冻场闸之前分流）：默认解冻才走（怪物/竞技场敌方同口径）；
        // _mapEnterWalkWhileFrozen=true（开打前抽到己方佣兵）冻场期间照走。
        if (_mapEnterActive)
        {
            if (BattleManager.Instance != null && !BattleManager.Instance.UnitsCanAct
                && !_mapEnterWalkWhileFrozen)
            {
                if (rb != null) rb.velocity = Vector2.zero;
                if (unitAnim != null) unitAnim.SetMove(false, facingDir);
                ApplyLaneY(Time.deltaTime);
                return;
            }
            bool entering = StepMapEnter();
            ApplyLaneY(Time.deltaTime);
            if (entering) return;   // 到点后落入下方正常 AI（索敌/交战）
        }

        if (BattleManager.Instance != null && !BattleManager.Instance.UnitsCanAct)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            if (BattleManager.Instance.PartyIntroWalking)
            {
                facingDir = 1;
                if (unitAnim != null) unitAnim.SetMove(true, facingDir);
            }
            else if (unitAnim != null)
            {
                unitAnim.SetMove(false, facingDir);
            }
            return;
        }

        // 【2026-11-15 定稿·硬直期间也要前压】这两个硬直早退排在索敌/移动之前，每帧清零速度就 return。
        // 远程怪站在自己射程边缘放箭时我方佣兵持续受击 → InDamagedRecovery 长期为真 → 整段交战 AI
        // （含下方 pressForward 前压分支）永远执行不到，观感就是「佣兵不上前、也不往前走」。
        // 现只在「我方 + 场上有怪 + 距目标仍远于交战停步线」时补一次朝目标位移；
        // 硬直动画、出手判定、攻击锁、受击恢复一字不动，敌方竞技场机器人 / 无怪推图行为不变。
        if (TryHoldDuringAttack() || TryHoldDuringDamaged())
        {
            TryEngageMoveDuringHold();
            return;
        }

        EnsureAiPolicy();

        // 【2026-11-09】点击集结：我方全体优先走到点击点，期间不索敌 / 不出手。
        // 敌方佣兵（竞技场机器人）不参与，仍走原 AI。
        if (isAlly && BattleRally.Active && BattleRally.TryGetMoveDir(this, out Vector2 rallyDir))
        {
            if (rallyDir.sqrMagnitude > 0.0001f)
            {
                // 【2026-11-12 主人拍板「要」· 与玩家同口径修斜向滑步】TryGetMoveDir 给的是
                // sign(±1,±1) 的轴向分离值（未归一化）→ 斜向合速 √2 倍，而 MOVE 动画速率是固定常量
                // （UnitAnimation.MOVE_ANIM_SPEED_SCALE，按基础移速标定）→ 斜着走就滑步。
                // 归一化成单位向量后各方向速度一致。只改方向，速度倍率 / 动画一字未动。
                rallyDir = rallyDir.normalized;
                // 【2026-11-12 主人拍板「要」· 纯纵向倒退走】旧实现无条件 `rallyDir.x > 0f ? 1 : -1`，
                // 点正上方 / 正下方时 x=0 恒判 -1 → 全体佣兵朝左、侧着走/倒退着走。
                // 与玩家侧同口径（Hero.cs 的 Abs(_manualDir.x) > 0.05f）：横向分量不够就不翻朝向，保持原朝向走纵向。
                if (Mathf.Abs(rallyDir.x) > 0.05f)
                {
                    facingDir = rallyDir.x > 0f ? 1 : -1;
                    ApplyFacing(facingDir);
                }
                if (rb != null)
                    rb.velocity = new Vector2(rallyDir.x * GetCombatMoveSpeed(), rb.velocity.y);
                if (Mathf.Abs(rallyDir.y) > 0.01f)
                    SetLaneY(LaneY + rallyDir.y * GetCombatMoveSpeed() * Time.deltaTime);
                if (unitAnim != null) unitAnim.SetMove(true, facingDir);
            }
            else
            {
                if (rb != null) rb.velocity = Vector2.zero;
                if (unitAnim != null) unitAnim.SetMove(false, facingDir);
            }
            target = null;
            if (BattleManager.Instance == null || !BattleManager.Instance.PortalWalkMode)
                ClampToScreen();
            ApplyLaneY(Time.deltaTime);
            return;
        }

        // 牧师：有伤员且治疗技可放时，站住等自动奶。
        // 【2026-10-20 主人拍板】原实现无条件定身 return，两条路会脱队/死锁：
        // ① 定住时不过拴绳、不走 10-19 的「边打边跟」，玩家往前跑牧师钉在原地；
        // ② 施法失败（蓝不足回蓝慢 / merc_skills 表缺耗蓝 fail closed）时 CooldownRemain 一直 <= 0
        //    而 AllyNeedsHeal() 战斗中几乎恒真（任一队友含自己 < 92% 血）→ 无限站桩。
        // 现加两道保险：离玩家超过 MERC_STICK_RADIUS 不许定住；定住超过 MERC_HEAL_WAIT_MAX_SEC 强制跟队。
        // 治疗是 MercSkillCaster.Update 自动放的，不要求站着 —— 边走边奶成立，本分支只保留「奶人时站着」的表现。
        if (SkillCaster != null && SkillCaster.HasActiveSkill && SkillCaster.CooldownRemain <= 0f
            && AllyNeedsHeal())
        {
            // 【2026-11-18】技能真正放出（CD>0）→ 解除上轮的闭锁，恢复「奶人时站着」表现。
            // 读条期的真奶现在由 HealCastBar 在 UnitBase.Update 定身，到不了这里，不冲突。
            if (_healWaitAbandoned && SkillCaster.CooldownRemain > 0f)
                _healWaitAbandoned = false;
            // 【2026-11-01 主人报「竞技场敌方弓手总贴很近打」】锚玩家只属于我方佣兵（聚队概念）；
            // 敌方佣兵无玩家可跟，tooFarFromHero 恒 false，只吃下面的最大等待时长上限。
            var waitHero = isAlly ? Hero.Instance : null;
            bool tooFarFromHero = waitHero != null && !waitHero.isDead
                && Mathf.Abs(GetCombatX(this) - GetCombatX(waitHero)) > GameConfig.MERC_STICK_RADIUS;
            _healWaitTimer += Time.deltaTime;
            if (_healWaitAbandoned || tooFarFromHero || _healWaitTimer > GameConfig.MERC_HEAL_WAIT_MAX_SEC)
            {
                // 【2026-11-18】超时这一帧置闭锁：之前只是 timer 归零、下一帧条件又恒真 → 每 1.5s 喘一帧又站桩。
                // 现在超时即放弃本次交战的「站定等奶」，持续跟队；直到技能真放出（上面解锁）或切关（ResetForReuse）。
                if (_healWaitTimer > GameConfig.MERC_HEAL_WAIT_MAX_SEC)
                    _healWaitAbandoned = true;
                _healWaitTimer = 0f;
                // 不定住，落到底部正常 AI（跟队 / 粘跟随），治疗照常自动放
            }
            else
            {
                if (rb != null) rb.velocity = Vector2.zero;
                if (unitAnim != null) unitAnim.SetMove(false, facingDir);
                ApplyLaneY(Time.deltaTime);
                return;
            }
        }
        else
        {
            _healWaitTimer = 0f;
        }

        // 拴绳：离玩家太远 / 偏远交战过久 → 清目标回队
        if (UpdateHeroLeash(Time.deltaTime))
        {
            bool recallingMove = false;
            FollowHeroOrIdle(ref recallingMove);
            if (unitAnim != null)
                unitAnim.SetMove(recallingMove, facingDir);
            if (BattleManager.Instance == null || !BattleManager.Instance.PortalWalkMode)
                ClampToScreen();
            ApplyLaneY(Time.deltaTime);
            return;
        }

        RefreshAcquireTarget();

        bool isMoving = false;
        if (target != null)
        {
            float distance = Mathf.Abs(GetCombatX(this) - GetCombatX(target));
            float attackRange = GetEffectiveAttackRange();
            bool melee = UsesMeleeBasicAttack();
            FaceToward(target);
            // 2026-10-09 主人拍板：已进入攻击范围则站桩不并道，抗怪跑动抖动
            if (!ShouldHoldGround(target) && melee)
                AdjustLaneTowardTarget(target, Time.deltaTime);

            if (IsInBasicAttackRange(target))
            {
                // 【2026-10-19 主人拍板】交战跟随上限：离玩家超过 MERC_STICK_RADIUS 就「边打边跟」——
                // 朝玩家方向移动（攻距内照常按 CD 开火），保证佣兵始终聚在玩家身边；
                // 半径内仍维持站桩不并道（2026-10-09 拍板不变）。
                // 【2026-11-01 主人报「竞技场敌方弓手总贴很近打」】此逻辑只属我方佣兵（锚玩家聚队）；
                // 敌方佣兵走它会「一边开火一边朝玩家挪」，远程停不住 → 敌方恒站桩开火。
                var stickHero = isAlly ? Hero.Instance : null;
                bool mustStickBack = stickHero != null && !stickHero.isDead
                    && Mathf.Abs(GetCombatX(this) - GetCombatX(stickHero)) > GameConfig.MERC_STICK_RADIUS;
                if (mustStickBack)
                {
                    int backDir = GetCombatX(stickHero) > GetCombatX(this) ? 1 : -1;
                    if (rb != null)
                        rb.velocity = new Vector2(backDir * GetCombatMoveSpeed(), rb.velocity.y);
                    isMoving = true;
                    if (attackCd <= 0f
                        && (unitAnim == null || !unitAnim.InAttackLock))
                    {
                        Attack(target);
                        attackCd = GetAttackCooldown();
                    }
                }
                else
                {
                    // 【2026-11-15 主人拍板「就算怪物不贴近 佣兵也要去寻敌 / 上前战斗」】
                    // 旧实现一进攻击射程就把速度清零站桩：远程怪站在自己射程边缘不贴近时，
                    // 佣兵锁到目标后原地放箭、一步不往前（Editor.log 实证 AIUpdate 大量 Attack、
                    // 位移恒 0）。现改为：我方且场上有怪时，射程内继续朝目标前压到
                    // ENGAGE_DIST_RATIO × 射程 才站桩；前压期间下面这段出手逻辑一字不动，输出不中断。
                    // 敌方竞技场机器人 / 无怪 / mustStickBack 回队 / 牧师 / 集结一律走原路径。
                    bool pressForward = isAlly && HasAliveEnemyOnField()
                                        && distance > attackRange * EngageDistRatio;
                    if (pressForward)
                    {
                        if (rb != null)
                            rb.velocity = new Vector2(facingDir * GetCombatMoveSpeed(), rb.velocity.y);
                        isMoving = true;
                    }
                    else if (rb != null) rb.velocity = Vector2.zero;
                    // 受击动画不挡出手：轮到攻击时 PlayAttack 立即打断 DAMAGED
                    if (attackCd <= 0f
                        && (unitAnim == null || !unitAnim.InAttackLock))
                    {
                        Attack(target);
                        attackCd = GetAttackCooldown();
                    }
                }
            }
            else if (melee && distance <= attackRange)
            {
                if (rb != null) rb.velocity = Vector2.zero;
                isMoving = true;
            }
            else
            {
                // 已锁目标但未进攻击距：朝目标走，朝向只看怪（不用走位方向翻身）
                if (rb != null)
                    rb.velocity = new Vector2(facingDir * GetCombatMoveSpeed(), rb.velocity.y);
                isMoving = true;
            }
        }
        else
        {
            // 索敌距内无怪：跟玩家站位 / 推图，朝右，不左右找怪
            FollowHeroOrIdle(ref isMoving);
        }

        if (unitAnim != null)
            unitAnim.SetMove(isMoving, facingDir);
        if (BattleManager.Instance == null || !BattleManager.Instance.PortalWalkMode)
            ClampToScreen();
        ApplyLaneY(Time.deltaTime);
    }

    /// <summary>
    /// 维护与玩家的拴绳。返回 true 表示本帧应只回队、不交战。
    /// </summary>
    bool UpdateHeroLeash(float dt)
    {
        // 【2026-10-30 竞技场】敌方佣兵不拴玩家绳：无「回队/收召」概念，自由交战（己方路径一行不动）
        if (!isAlly) return false;

        var hero = Hero.Instance;
        if (hero == null || hero.isDead)
        {
            _recallingToHero = false;
            _awayFightTimer = 0f;
            return false;
        }

        float distHero = Mathf.Abs(GetCombatX(this) - GetCombatX(hero));

        if (_recallingToHero)
        {
            if (distHero <= GameConfig.MERC_REJOIN_RADIUS)
            {
                _recallingToHero = false;
                _awayFightTimer = 0f;
                return false;
            }
            ClearAcquireLock();
            return true;
        }

        if (distHero > GameConfig.MERC_LEASH_RADIUS)
        {
            BeginRecallToHero();
            return true;
        }

        // 偏远交战：打一会就回去找玩家
        bool fightingAway = target != null && !target.isDead && distHero > GameConfig.MERC_SOFT_AWAY_RADIUS;
        if (fightingAway)
        {
            _awayFightTimer += dt;
            if (_awayFightTimer >= GameConfig.MERC_AWAY_FIGHT_SEC)
            {
                BeginRecallToHero();
                return true;
            }
        }
        else
        {
            _awayFightTimer = 0f;
        }

        return false;
    }

    void BeginRecallToHero()
    {
        _recallingToHero = true;
        _awayFightTimer = 0f;
        ClearAcquireLock();
    }

    void ClearAcquireLock()
    {
        _acquireLock = null;
        target = null;
    }

    /// <summary>
    /// 【2026-11-15 主人口径「就算怪物不贴近 佣兵也要去寻敌」】场上有怪时与玩家同口径走全场寻敌
    /// （Hero.cs:567 → FindNearestEnemyOnField，不设屏宽距离闸，只挡屏外目标），锁到后由 AIUpdate 主动前压。
    /// 旧实现恒用 FindNearestEnemyInDetectRange + 出距即清锁：怪站远处时佣兵锁不到目标，
    /// 落进 FollowHeroOrIdle 贴玩家站桩，观感「不上前战斗」。
    /// 无怪时仍走索敌距（推图 / 集结口径不变）；敌方竞技场佣兵 isAlly=false 行为一字不变。
    /// 收队中不索敌。
    /// </summary>
    void RefreshAcquireTarget()
    {
        if (_recallingToHero)
        {
            ClearAcquireLock();
            return;
        }

        bool seekOnField = isAlly && HasAliveEnemyOnField();
        float detect = GetDetectRange();
        float myX = GetCombatX(this);
        const float leaveSlack = 0.75f;

        if (_acquireLock != null)
        {
            if (_acquireLock.isDead || _acquireLock.isAlly == isAlly)
                _acquireLock = null;
            else if (!seekOnField && Mathf.Abs(myX - GetCombatX(_acquireLock)) > detect + leaveSlack)
                _acquireLock = null;
            else if (!GameConfig.CanAcquireTarget(this, _acquireLock))
                _acquireLock = null;
        }

        if (_acquireLock == null)
            _acquireLock = seekOnField ? FindNearestEnemyOnField() : FindNearestEnemyInDetectRange();

        target = _acquireLock;
        if (target != null && target.isAlly == isAlly)
            ClearAcquireLock();
    }

    /// <summary>
    /// 攻击/受击硬直早退期间补一次交战前压位移。与 AIUpdate 里 pressForward 同一停步线
    /// （EngageDistRatio），同语义单一出口；不改硬直动画、不改出手、不改索敌口径。
    /// 保险：离玩家已超过拴绳半径时不再往外走，交给 UpdateHeroLeash 下一帧收队，避免脱队抖动。
    /// </summary>
    void TryEngageMoveDuringHold()
    {
        if (!isAlly || !HasAliveEnemyOnField()) return;

        var hero = Hero.Instance;
        if (hero != null && !hero.isDead
            && Mathf.Abs(GetCombatX(this) - GetCombatX(hero)) > GameConfig.MERC_LEASH_RADIUS)
            return;

        var enemy = target;
        if (enemy == null || enemy.isDead || enemy.isAlly == isAlly
            || !enemy.gameObject.activeInHierarchy || !GameConfig.CanAcquireTarget(this, enemy))
            enemy = FindNearestEnemyOnField();
        if (enemy == null) return;

        float dist = Mathf.Abs(GetCombatX(this) - GetCombatX(enemy));
        if (dist <= GetEffectiveAttackRange() * EngageDistRatio) return;

        int dir = GetCombatX(enemy) > GetCombatX(this) ? 1 : -1;
        if (dir != facingDir)
        {
            facingDir = dir;
            ApplyFacing(facingDir);
        }
        if (rb != null)
            rb.velocity = new Vector2(dir * GetCombatMoveSpeed(), rb.velocity.y);
        if (unitAnim != null)
            unitAnim.SetMove(true, facingDir);
    }

    /// <summary>无锁定目标：贴玩家编队站位；玩家在推图则跟着走。
    /// 【2026-10-30 竞技场】敌方佣兵不跟玩家推图：站住守半场、面向玩家（左），等敌人进索敌距再锁。</summary>
    void FollowHeroOrIdle(ref bool isMoving)
    {
        if (!isAlly)
        {
            facingDir = -1;
            ApplyFacing(facingDir);
            if (rb != null) rb.velocity = Vector2.zero;
            isMoving = false;
            return;
        }

        facingDir = 1;
        ApplyFacing(facingDir);
        // 2026-10-10 主人拍板：清场后不再换道归队（原 AdjustFormationLane 会把佣兵
        // 立刻搬回队形车道，看着像「打完敌人又往上走一步」）。场上还有怪时照常归队站位。
        if (HasAliveEnemyOnField())
            AdjustFormationLane(Time.deltaTime);

        var hero = Hero.Instance;
        if (hero == null || hero.isDead)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            isMoving = false;
            return;
        }

        float desiredX = UnitCrowd.GetMercDesiredCombatX(hero, this, ResolvePartyIndex());
        float dx = desiredX - GetCombatX(this);
        if (Mathf.Abs(dx) > 0.22f)
        {
            int moveDir = dx > 0f ? 1 : -1;
            // 【2026-10-19 主人拍板】追赶加速：落后编队位超过起步距离 → 比玩家快一档追回来。
            // 同速追赶永远追不上（见 GameConfig.MERC_CATCHUP_SPEED_MUL 注释），
            // 不加这条佣兵只会越掉越远。回队（拴绳触发）也走这里，一并享受加速。
            float speed = GetCombatMoveSpeed();
            // 【2026-11-06】全局加速（入队冲刺/掉队追赶）已在倍率里生效时不再叠编队位追赶，
            // 否则 1.35 × 1.5 会叠成两倍速；没触发全局加速时这条原有口径不变。
            if (!_rushToHero && !_lagBoost && Mathf.Abs(dx) > GameConfig.MERC_CATCHUP_ENTER_DIST)
                speed *= GameConfig.MERC_CATCHUP_SPEED_MUL;
            if (rb != null)
                rb.velocity = new Vector2(moveDir * speed, rb.velocity.y);
            isMoving = true;
        }
        else if (!HasAliveEnemyOnField())
        {
            // 场上无怪：与玩家一起右推
            if (rb != null)
                rb.velocity = new Vector2(GetCombatMoveSpeed(), rb.velocity.y);
            isMoving = true;
        }
        else
        {
            // 场上有怪但不在自己索敌距：站住跟队，等怪进距再锁
            if (rb != null) rb.velocity = Vector2.zero;
            isMoving = false;
        }
    }
}
