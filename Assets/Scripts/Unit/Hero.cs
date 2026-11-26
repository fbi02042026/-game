using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主英雄类
/// </summary>
public class Hero : UnitBase
{
    public static Hero Instance;
    public Transform endPoint;
    public int level = 1;
    public int currentExp = 0;
    public int expToNextLevel = 50;

    /// <summary>
    /// 待结算的升级次数（局内三选一抽卡队列）。
    /// 由 <see cref="AddExp"/> 累加，<c>RunDraftDirector.Tick</c> 在可行动时消费，UI 弹在战斗层。
    /// </summary>
    public int pendingLevelUps;

    // —— 闪避：独立按钮触发，无敌帧 + 飘字，不新增美术 ——
    internal float invincibleUntil;
    private float _dodgeCdUntil;
    private Button _dodgeBtn;
    private Image _dodgeBtnImg;

    [Header("SPUM换装")]
    public HeroCostumeManager costumeManager;

    protected override void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        // 【关键】必须在base.Awake()之前设置，因为base.Awake()中会调用EnsureHitPoint/EnsureFirePoint
        firePointOffset = new Vector3(0.3f, 0.32f, 0f);
        hitPointOffset = new Vector3(0f, 0.55f, 0f);

        base.Awake();
        Instance = this;
        isAlly = true;
        // SPUM 资源在 +scale.x 下实际朝左，故标记为 false，facingDir=1 时会镜像成朝右
        spriteDefaultFacesRight = false;
        // 不改 SPUM 人物 Sorting，保留预制体层级；换装走 SPUM 规范

        if (costumeManager == null)
            costumeManager = GetComponent<HeroCostumeManager>();
        KillComboAfterimage.Ensure(this);

        // 2026-09-26 主人拍板：挂载毒 DOT 运行器（游侠武器「中毒」词缀结算点），照抄 Mercenary 挂 PassiveRunner
        if (GetComponent<PoisonDotRunner>() == null)
            gameObject.AddComponent<PoisonDotRunner>();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// 玩家受击：播 DAMAGED 受击动画 + 白闪（含移动中；摇杆手动走位只不做脚步硬直）。
    /// 轮到攻击时攻击动画立即打断 DAMAGED（PlayAttack→InterruptDamaged）。
    /// </summary>
    protected override void PlayHitReaction()
    {
        if (unitAnim != null)
            unitAnim.PlayDamaged(playHitAnim: true);
    }

    public void InitNewRun()
    {
        if (BattleBagSystem.Instance != null)
            BattleBagSystem.Instance.InitNewRun();
        else
            Debug.LogWarning("[Hero] InitNewRun: BattleBagSystem 为空，跳过背包重置");

        attr.ResetToBase();
        attr.RecalcAllAttr(); // 现在可以安全调用（Awake已完成）
        level = 1;
        currentExp = 0;
        expToNextLevel = LevelSystem.GetExpForLevel(level);
        pendingLevelUps = 0;
        invincibleUntil = 0f;
        _dodgeCdUntil = 0f;
        ClearLaneAlignBias(); // 换局重新抽对齐偏移
        currentHp = attr.GetAttr(AttrType.MaxHp);
        // 优先用场景 SpawnPoint；无则回退硬编码 X + GROUND_Y
        Vector3 spawnPos = new Vector3(-7f, GROUND_Y, 0f);
        if (BattleManager.Instance != null)
        {
            float z = BattleManager.Instance.unitRoot != null
                ? BattleManager.Instance.unitRoot.position.z
                : 0f;
            if (BattleManager.Instance.spawnPoint != null)
            {
                var sp = BattleManager.Instance.spawnPoint.position;
                spawnPos = new Vector3(sp.x, GROUND_Y, z);
            }
            else
            {
                spawnPos = new Vector3(spawnPos.x, GROUND_Y, z);
            }
        }
        GameConfig.SetWorldPosition(gameObject, spawnPos);
        GameConfig.AttachToUnitRoot(transform);
        transform.localScale = Vector3.one * GameConfig.UNIT_SCALE;
        facingDir = 1;
        ApplyFacing(facingDir); // 默认资源朝左 → 朝右时 scale.x 为负
        gameObject.SetActive(true);

        // 重置死亡状态和动画
        ResetForReuse();

        EnsureDodgeButton();

        // 初始化换装后再按武器刷新攻击距离
        if (costumeManager != null)
            costumeManager.RefreshCostume();
        RecalcAttr();

        Debug.Log($"[Hero] InitNewRun完成 | pos={transform.position} | scale={transform.localScale} | facingDir={facingDir} | range={attr.GetAttr(AttrType.AttackRange)}");
    }

    /// <summary>
    /// 等级/经验已从战斗中移除：属性成长只走城镇天赋（天赋石），战斗内不再获得经验、不再升级。
    /// 保留方法让旧调用点（BattleManager 击杀经验等）仍能编译，调用即空转。
    /// </summary>
    public void AddExp(int exp)
    {
        // 有意空实现：等级系统停用时不做任何处理
    }

    // —— 闪避系统（D2）：独立按钮 + 无敌帧 + 飘字，不新增美术 ——
    // 与技能型无敌（基类 _skillInvulnUntil，SK010 神圣庇佑）叠加：任一生效即无敌
    protected override bool IsInvincibleNow() => Time.time < invincibleUntil || base.IsInvincibleNow();

    // —— 残血加成（R1）：HP 低于阈值时攻速/普攻暴击提升，制造残血反扑 ——
    private bool IsLowHp()
        => attr != null && attr.GetAttr(AttrType.MaxHp) > 0
        && currentHp > 0
        && (float)currentHp / attr.GetAttr(AttrType.MaxHp) < GameConfig.LOW_HP_THRESHOLD;

    protected override float GetLowHpAttackSpeedMul()
        => IsLowHp() ? GameConfig.LOW_HP_ATK_SPEED_MUL : 1f;

    protected override float GetLowHpCritRateBonus()
        => IsLowHp() ? GameConfig.LOW_HP_CRIT_BONUS : 0f;

    /// <summary>独立闪避按钮调用：冷却就绪、战斗中且可行动时触发，返回是否成功。</summary>
    public bool TryDodge()
    {
        if (isDead || !gameObject.activeInHierarchy) return false;
        var bm = BattleManager.Instance;
        if (bm == null || !bm.isInBattle || !bm.UnitsCanAct) return false;
        if (Time.time < _dodgeCdUntil) return false;

        _dodgeCdUntil = Time.time + GameConfig.DODGE_COOLDOWN;
        invincibleUntil = Time.time + GameConfig.DODGE_IFRAME;
        DamageTextSystem.Instance?.SpawnDodgeText(transform.position, true);
        return true;
    }

    private void EnsureDodgeButton()
    {
        if (_dodgeBtn != null) return;
        var ui = BattleUI.Instance;
        if (ui == null) return;

        var go = new GameObject("DodgeButton", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(ui.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-90f, 96f); // 右下角，避开摇杆与技能区
        rt.sizeDelta = new Vector2(96f, 96f);

        var img = go.GetComponent<Image>();
        img.color = new Color(0.18f, 0.7f, 1f, 0.55f);

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.ColorTint;
        btn.onClick.AddListener(() => { TryDodge(); });

        var txtGo = new GameObject("Label", typeof(Text));
        txtGo.transform.SetParent(go.transform, false);
        var txt = txtGo.GetComponent<Text>();
        txt.text = "闪避";
        txt.fontSize = 30;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        var trt = txtGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.sizeDelta = Vector2.zero;
        trt.anchoredPosition = Vector2.zero;

        _dodgeBtn = btn;
        _dodgeBtnImg = img;
    }

    private void TickDodgeButton()
    {
        if (_dodgeBtn == null)
        {
            // 战斗进行中但按钮尚未创建（如 BattleUI 晚于 InitNewRun 就绪）时自愈
            var bm = BattleManager.Instance;
            if (bm != null && bm.isInBattle && BattleUI.Instance != null)
                EnsureDodgeButton();
            return;
        }
        float remain = _dodgeCdUntil - Time.time;
        bool ready = remain <= 0f;
        _dodgeBtn.interactable = ready;
        if (_dodgeBtnImg != null)
        {
            Color c = _dodgeBtnImg.color;
            c.a = ready ? 0.55f : 0.2f;
            _dodgeBtnImg.color = c;
        }
    }

    public void RecalcAttr()
    {
        // 【2026-11-15 主人拍板】重算前留一份旧上限：换上加体力的装备后要把涨出来的血一起补上。
        float maxHpBefore = attr.GetAttr(AttrType.MaxHp);

        var bag = BattleBagSystem.Instance;
        if (bag == null)
        {
            Debug.LogWarning("[Hero] RecalcAttr: BattleBagSystem 为空，仅重算基础属性");
            attr.RecalcAllAttr(null);
            SyncCurrentHpOnMaxHpChange(maxHpBefore);
            return;
        }

        List<AttrBonusData> allBonus = EquipStatRollup.BuildBonusList(bag);
        if (BattleManager.Instance != null && BattleManager.Instance.tempBuffs != null)
            allBonus.AddRange(BattleManager.Instance.tempBuffs);
        attr.RecalcAllAttr(allBonus);

        // 主手优先，无主手则读副手（教程默认左手剑）
        float weaponRange = AttackRangeTable.GetJobWorld(PlayerJobDefs.GetSelected());
        EquipInstance weaponInst = TryGetEquippedWeaponInstance(bag);
        if (weaponInst?.template != null)
            weaponRange = WeaponCombatTable.GetAttackRangeWorld(WeaponCombatTable.ResolveKind(weaponInst));
        attr.SetAttr(AttrType.AttackRange, weaponRange);
        if (weaponInst?.template != null)
        {
            float swordSpd = WeaponCombatTable.GetBaseAttackSpeed(WeaponCombatTable.WeaponKind.Sword);
            float kindSpd = WeaponCombatTable.GetBaseAttackSpeed(WeaponCombatTable.ResolveKind(weaponInst));
            float mul = swordSpd > 0.01f ? kindSpd / swordSpd : 1f;
            attr.SetAttr(AttrType.AttackSpeed, Mathf.Max(0.2f, attr.GetAttr(AttrType.AttackSpeed) * mul));
        }
        SyncCurrentHpOnMaxHpChange(maxHpBefore);

        // 属性重算后同步外观（通关穿装 / 战前遗产等路径未必都走 EquipItem）
        if (costumeManager == null)
            costumeManager = GetComponent<HeroCostumeManager>();
        costumeManager?.RefreshCostume();
    }

    /// <summary>
    /// 【2026-11-15 主人拍板】属性重算后同步当前血：上限涨多少就把这部分血补上
    ///（获得加体力的装备 / 天赋 / 赐福 / 强化都走这一个出口）；上限下降仍只钳上限、不回血。
    /// 死亡单位不补，避免绕过复活流程；不飘字、不计入治疗统计。
    /// </summary>
    void SyncCurrentHpOnMaxHpChange(float maxHpBefore)
    {
        float maxHp = attr.GetAttr(AttrType.MaxHp);
        if (currentHp > 0f && maxHp > maxHpBefore)
            currentHp += maxHp - maxHpBefore;
        currentHp = Mathf.Min(currentHp, maxHp);
    }

    protected override void Update()
    {
        base.Update();
        TickLowHpWarn();
        // 通关只由 BattleManager 在「传送门已激活」后检测，避免未清怪就结算、每帧刷爆 OnStageClear
    }

    void TickLowHpWarn()
    {
        float maxHp = attr != null ? attr.GetAttr(AttrType.MaxHp) : 0f;
        float ratio = maxHp > 0.01f ? currentHp / maxHp : 1f;
        bool dead = isDead || currentHp <= 0f;
        // 2026-09-28 主人拍板：玩家形象不再身上闪红（残血改屏幕边缘闪红，见 LowHpScreenEdgeFlash）。
        // 这里只负责把历史残留的低血红清掉；佣兵仍保留身上闪红（Mercenary.TickLowHpWarn 未动）。
        if (unitAnim != null)
            unitAnim.CancelLowHpFlash();
        bool low = !dead && ratio <= GameConfig.LOW_HP_WARN_RATIO + 0.0001f;
        LowHpScreenEdgeFlash.Ensure().Tick(low);
    }

    /// <summary>
    /// 根据当前装备的武器返回攻击类型
    /// </summary>
    protected override WeaponAttackType GetAttackType()
    {
        if (BattleBagSystem.Instance == null) return WeaponAttackType.Physical;
        var tpl = TryGetEquippedWeaponTemplate(BattleBagSystem.Instance);
        if (tpl != null)
        {
            foreach (var item in BattleBagSystem.Instance.GetEquippedItems())
            {
                if (item.template == tpl) return item.weaponAttackType;
            }
        }
        return WeaponAttackType.Physical;
    }

    /// <summary>
    /// 是否魔法伤害单位（主人 2026-09-26 口径）：法师（P005）/ 牧师（P006）打出的伤害 = 魔法伤害，
    /// 走目标的魔法防御；其余职业走物理防御。
    /// 按职业判定而不是只看当前武器，避免法师捡了把剑就变物理。
    /// </summary>
    public override bool IsMagicDamageDealer()
    {
        // 2026-09-29：统一按职业判定，不再看当前武器。
        // 原实现法师/牧师按职业、其余职业按武器 —— 不对称，物理职业捡把法杖就会走进
        // magicAttack 分支、而自己的 MagicAttack 恒为 0 → 报 DF-004 并退回物攻结算。
        return PlayerJobBaseStats.IsMagicJob(PlayerJobDefs.GetSelected());
    }

    protected override AttackVfxKit GetAttackVfxKit()
    {
        AttackVfxKit jobKit = SkillNaming.KitFromWeaponKind(
            PlayerJobDefs.Get(PlayerJobDefs.GetSelected()).PrimaryWeapon);

        var bag = BattleBagSystem.Instance;
        EquipInstance inst = TryGetEquippedWeaponInstance(bag);
        if (inst == null)
            return jobKit;

        AttackVfxKit fromWeapon = SkillNaming.KitFromWeaponKind(WeaponCombatTable.ResolveKind(inst));
        // 法师/牧师职业或魔法武器：禁止回退近战刀光。
        // 注：暮火之杖的 spum 已于 2026-09-27 真源对齐为 New_Weapon_03（5_Wand 法杖图，不再需要 Ward_1 补丁），
        // 这里保留判断是因为**其它**魔法武器仍可能被 ResolveKind 判成 MeleeSlash。
        if (fromWeapon == AttackVfxKit.MeleeSlash
            && (jobKit == AttackVfxKit.Orb || inst.weaponAttackType == WeaponAttackType.Magic))
            return AttackVfxKit.Orb;
        return fromWeapon;
    }

    /// <summary>攻击特效以逻辑主手武器为准；无主手则不看副手剑。</summary>
    static EquipTemplate TryGetEquippedWeaponTemplate(BattleBagSystem bag)
        => TryGetEquippedWeaponInstance(bag)?.template;

    // 2026-10-24 主人拍板：供 WeaponVfxVariant 复用（只提可见性，语义不变；铁律 15 不多入口，不另写取主手武器的逻辑）
    internal static EquipInstance TryGetEquippedWeaponInstance(BattleBagSystem bag)
    {
        if (bag == null) return null;
        var main = bag.GetEquippedInLogicalSlot(EquipSlotType.MainHand);
        if (main?.template != null && (main.weaponType != WeaponType.None || main.slotType == EquipSlotType.MainHand))
            return main;
        var off = bag.GetEquippedInLogicalSlot(EquipSlotType.OffHand);
        if (off?.template != null && off.weaponType != WeaponType.None)
            return off;
        return null;
    }

    /// <summary>对外：当前主手武器对应的攻击特效套（技能回退也用）。</summary>
    public AttackVfxKit GetWeaponVfxKit() => GetAttackVfxKit();

    bool _manualMove;
    bool _manualHeld;
    Vector2 _manualDir;
    UnitBase _acquireLock;
    float _acquireUntil;
    float _manualReleaseUntil;

    // 2026-10-09 主人拍板：单帧最大步长钳制。抽奖励阶段 timeScale=0（战斗冻结），
    // 玩家等待「继续」期间若窗口失焦/编辑器暂停，恢复瞬间首帧 Time.deltaTime 会异常暴涨，
    // 直接乘进坐标 → 单帧位移过大即「瞬移很远」。钳到 0.1f（约 6 帧@60fps 上限）即可消除，
    // 正常帧 dt≈0.016 不受影响，手感无感。
    const float HERO_MAX_FRAME_DT = 0.1f;

    /// <summary>英雄追敌时启用车道对齐容错：不再严丝合缝站到敌人同一条水平线上（怪物/佣兵不受影响）。</summary>
    protected override bool UseLaneAlignTolerance => true;

    public bool IsManualMove => _manualMove;

    public void SetManualMove(Vector2 dir)
    {
        _manualHeld = true;
        // 按住期间即使死区也保持手动，避免 AI 抢方向
        _manualMove = true;
        _manualDir = dir;
        _acquireLock = null;
        _acquireUntil = 0f;
        _manualReleaseUntil = 0f;
    }

    public void ClearManualMove()
    {
        _manualMove = false;
        _manualHeld = false;
        _manualDir = Vector2.zero;
        _manualReleaseUntil = Time.time + GameConfig.HERO_MANUAL_RELEASE_HOLD;
    }

    /// <summary>松摇杆：短冷却后再索敌，一律锁最近怪（不按血量追远处）。</summary>
    public void BeginAutoAcquireOnRelease()
    {
        _manualMove = false;
        _manualHeld = false;
        _manualDir = Vector2.zero;
        _manualReleaseUntil = Time.time + GameConfig.HERO_MANUAL_RELEASE_HOLD;
        _acquireLock = FindNearestEnemyOnField();
        _acquireUntil = Time.time + GameConfig.COMBO_WINDOW;
    }

    protected override void AIUpdate()
    {
        // 【2026-11-26 主人报「剑盾冲锋的时候还是站立动画」】冲锋判定必须早于冻场判定：
        // 剧情 / 过场把 UnitsCanAct 置 false 时，基类 AIUpdate 会 SetMove(false) 把跑步盖回站立；
        // 冲锋协程本就独占行动权，这里提前返回，冻场中只要还在冲锋就保持跑步动画。
        // 【2026-11-14 主人「不要冲锋的时候还在攻击」】破阵冲锋期间行动权归冲锋协程：
        // AI 全停 —— 不索敌、不转身、不出手、不写 rb.velocity（否则协程按帧摆位置、
        // Rigidbody 又按速度推，两边抢同一个 transform，出现「边冲边砍还抖」）。
        // 保持跑步动画只停决策；标记由 CareerSkillExecutor.IsCharging 心跳续期，协程强停后自动解除。
        // ⚠ 放在摇杆判断之前：手动位移同样不能和冲锋抢位置。
        if (CareerSkillExecutor.IsCharging)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            if (unitAnim != null) unitAnim.SetMove(true, facingDir);
            return;
        }

        if (BattleManager.Instance != null && !BattleManager.Instance.UnitsCanAct)
        {
            base.AIUpdate();
            return;
        }

        // 【2026-11-19 临时探针 · 游侠滑步归因，确认后删除】必须在 TryHoldDuringAttack 之前，
        // 才能覆盖「被拦截停步」和「放行前压」两条分支。
        ProbeRangerSlide();

        if (!_manualMove && TryHoldDuringAttack())
            return;
        // 手动移动时不做受击脚步硬直（继续走位；DAMAGED 动画照播，UnitAnimation 内部保证播完/被攻击打断）
        if (!_manualMove && TryHoldDuringDamaged())
            return;

        // 2026-10-09 主人拍板：钳制单帧步长，防恢复瞬间 dt 暴涨导致瞬移（见 HERO_MAX_FRAME_DT）。
        float dt = Mathf.Min(Time.deltaTime, HERO_MAX_FRAME_DT);

        // 摇杆优先：手动位移，期间不跑自动追敌（直接改坐标，避免 velocity 被 ApplyLaneY/SetWorldPosition 清掉）
        if (_manualMove)
        {
            // 【2026-11-09】点击集结：每帧把方向喂给手动位移（不追敌、不推图）；
            // 到点后保持手动态但不播走路动画，等 BattleRally 的 2 秒驻留结束才恢复自动。
            if (BattleRally.Active && BattleRally.TryGetMoveDir(this, out Vector2 rallyDir))
            {
                if (rallyDir.sqrMagnitude > 0.0001f)
                {
                    // 【2026-11-12 修滑步】TryGetMoveDir 给的是 sign(±1,±1) 的轴向分离值，
                    // 斜向合速 √2 倍 → 统一归一化成单位向量，各方向速度一致。
                    _manualDir = rallyDir.normalized;
                    _manualHeld = true;
                }
                else
                {
                    _manualHeld = false;
                    _manualDir = Vector2.zero;
                    _acquireLock = null;
                    _acquireUntil = 0f;
                    _manualReleaseUntil = 0f;
                }
            }
            float spd = GetCombatMoveSpeed();
            // 【2026-11-12 修滑步 · 根因】HERO_MANUAL_MOVE_X_MUL(1.8) 是「摇杆左右手感」专用倍率，
            // 集结借道手动通道时误吃了这个倍率：位移 1.8 倍，而 MOVE 动画播放速率是固定常量
            // （UnitAnimation.MOVE_ANIM_SPEED_SCALE，按基础移速 0.96 标定，全工程无第二处按速度联动）
            // → 脚频只有位移的 1/1.8，看起来就是滑步。
            // 【2026-11-12 主人拍板「摇杆先注释，不要删」】摇杆自 2026-10-05 起整条链路停用
            // （BattleUI.Backpack.EnsureBattleControls 里 BattleJoystick.EnsureOn 已注释，Instance 恒 null），
            // 这条 manual 通道现在只剩「集结」一个使用者，1.8 倍已无任何合法入口 → 原式注释留档、不删：
            // float vx = _manualDir.x * spd * GameConfig.HERO_MANUAL_MOVE_X_MUL;
            // 恢复摇杆那天：放开上面那行、删掉下面这行，本文件其它不用动。
            float vx = _manualDir.x * spd;

            if (Mathf.Abs(_manualDir.x) > 0.05f)
            {
                facingDir = _manualDir.x > 0f ? 1 : -1;
                ApplyFacing(facingDir);
            }

            // 车道：上下界由 map 下的 walk 框投影决定（BattleLaneBounds，不再是常量），纵向与横向同基础移速
            if (Mathf.Abs(_manualDir.y) > 0.08f)
            {
                float lane = LaneY + _manualDir.y * spd * dt;
                lane = BattleLaneBounds.ClampLaneOffset(lane);
                SetLaneY(lane);
            }

            Vector3 p = transform.position;
            p.x += vx * dt;
            p.y = Mathf.MoveTowards(p.y, FootY, GameConfig.BATTLE_LANE_MOVE_SPEED * dt);
            if (BattleManager.Instance == null || !BattleManager.Instance.PortalWalkMode)
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    float halfH = cam.orthographicSize;
                    float halfW = halfH * cam.aspect;
                    float camX = cam.transform.position.x;
                    float margin = 0.3f;
                    p.x = Mathf.Clamp(p.x, camX - halfW + margin, camX + halfW - margin);
                }
            }
            GameConfig.SetWorldPosition(transform, p);
            RefreshDepthSort();

            // 手动移动也每帧刷新当前目标；没有合法候选时立刻清空，不能等到可攻击才换目标
            // 【2026-11-09】集结期间不索敌、不出手（首人到达 2 秒后才恢复自动）
            if (BattleRally.Active)
            {
                target = null;
            }
            else
            {
                var near = FindNearestEnemyInDetectRange();
                target = near;
                if (near != null && IsInBasicAttackRange(near)
                    && attackCd <= 0f
                    && (unitAnim == null || !unitAnim.InAttackLock))
                {
                    if (Mathf.Abs(_manualDir.x) <= 0.05f)
                        FaceToward(near);
                    Attack(near);
                    attackCd = GetAttackCooldown();
                }
            }

            // 按住摇杆时即使死区也保持走步，避免过中心闪站立
            bool stickWalk = _manualHeld
                || Mathf.Abs(vx) > 0.05f
                || Mathf.Abs(_manualDir.y) > 0.08f;
            if (unitAnim != null)
                unitAnim.SetMove(stickWalk, facingDir);
            return;
        }

        // 松摇杆短冷却：不追怪，避免与手动抢方向；进距仍可普攻
        if (Time.time < _manualReleaseUntil)
        {
            if (rb != null) rb.velocity = new Vector2(0f, rb.velocity.y);
            var nearHold = FindNearestEnemyInDetectRange();
            // 松手冷却只禁止追怪，不冻结索敌；无合法候选时也要清掉旧目标
            target = nearHold;
            if (nearHold != null && IsInBasicAttackRange(nearHold)
                && attackCd <= 0f
                && (unitAnim == null || !unitAnim.InAttackLock))
            {
                FaceToward(nearHold);
                Attack(nearHold);
                attackCd = GetAttackCooldown();
            }
            if (unitAnim != null) unitAnim.SetMove(false, facingDir);
            if (BattleManager.Instance == null || !BattleManager.Instance.PortalWalkMode)
                ClampToScreen();
            ApplyLaneY(dt);
            return;
        }

        // 松手索敌窗口：每帧重新比较最近目标，窗口只保留连击语义，不能把首次目标锁死
        if (_acquireLock != null)
        {
            if (Time.time > _acquireUntil)
                _acquireLock = null;
            else
            {
                // 场上索敌统一执行 0.45 粘滞：明显更近立即换，差距小时才保留当前目标
                _acquireLock = FindNearestEnemyOnField();
                target = _acquireLock;
            }
        }

        bool alliesEngaged = HasAliveEnemyOnField();
        if (_acquireLock == null)
        {
            target = alliesEngaged
                ? FindNearestEnemyOnField()
                : FindNearestEnemyInDetectRange();
        }
        if (target != null && target.isAlly == isAlly)
            target = null;

        // 复用基类其余逻辑：临时改写后调用会重新选目标，故内联走基类流程太重。
        // 把锁定目标塞进一次「伪 base」：直接复制移动/攻击分支。
        bool isMoving = false;
        if (target != null)
        {
            float distance = Mathf.Abs(GetCombatX(this) - GetCombatX(target));
            float attackRange = GetEffectiveAttackRange();
            bool melee = UsesMeleeBasicAttack();
            // 左右容错：比射程再近一点点才停，不精确卡在射程边缘（恒更靠内，进距普攻判定不受影响）
            float stopDist = attackRange - LaneAlignBiasX;
            if (stopDist < 0f) stopDist = 0f;
            FaceToward(target);
            // 2026-10-09 主人拍板：已进入攻击范围则站桩不并道，抗怪跑动抖动
            if (!ShouldHoldGround(target) && (melee || !alliesEngaged))
                AdjustLaneTowardTarget(target, dt);

            if (IsInBasicAttackRange(target))
            {
                if (rb != null) rb.velocity = Vector2.zero;
                // 受击动画不挡出手：轮到攻击时 PlayAttack 立即打断 DAMAGED
                if (attackCd <= 0 && (unitAnim == null || !unitAnim.InAttackLock))
                {
                    Attack(target);
                    attackCd = GetAttackCooldown();
                }
            }
            else if (melee && distance <= stopDist)
            {
                if (rb != null) rb.velocity = Vector2.zero;
                isMoving = true;
            }
            else
            {
                if (rb != null)
                    rb.velocity = new Vector2(facingDir * GetCombatMoveSpeed(), rb.velocity.y);
                isMoving = true;
            }
        }
        else
        {
            if (alliesEngaged)
            {
                // 【2026-11-23 主人「玩家出屏要能回来找怪继续打、不能怪在屏外看不到」】
                // 旧实现场上有怪却锁不到目标（怪被甩到 >1 屏宽外，被 FindNearestEnemyOnField
                // 的距离闸过滤）时写死朝右站桩 → 相机跟着静止的玩家，怪永远留在屏外。改为朝
                // 全场最近活怪回头走；进到一屏宽内后由正常索敌接管继续打。无怪分支一字不动。
                UnitBase back = null;
                float bestD = float.MaxValue;
                var bmBack = BattleManager.Instance;
                if (bmBack != null && bmBack.monsters != null)
                {
                    for (int i = 0; i < bmBack.monsters.Count; i++)
                    {
                        var m = bmBack.monsters[i];
                        if (m == null || m.isDead || !m.gameObject.activeInHierarchy) continue;
                        float d = Mathf.Abs(GetCombatX(this) - GetCombatX(m));
                        if (d < bestD) { bestD = d; back = m; }
                    }
                }
                if (back != null)
                {
                    target = back; // 让 ClampToScreen 走「离目标一屏宽」分支，允许出屏追回
                    facingDir = GetCombatX(back) >= GetCombatX(this) ? 1 : -1;
                    ApplyFacing(facingDir);
                    if (rb != null)
                        rb.velocity = new Vector2(facingDir * GetCombatMoveSpeed(), rb.velocity.y);
                    isMoving = true;
                }
                else
                {
                    facingDir = 1;
                    ApplyFacing(facingDir);
                    if (rb != null) rb.velocity = Vector2.zero;
                    isMoving = false;
                }
            }
            else
            {
                facingDir = 1;
                ApplyFacing(facingDir);
                // 2026-10-10 主人拍板：清场后不再换道归队（原 AdjustFormationLane 会把玩家
                // 立刻搬回队形车道，看着像「打完敌人又往上走一步」）。保持击杀时的车道继续推图，
                // 下一波索敌时由 AdjustLaneTowardTarget 自然并回。
                if (rb != null)
                    rb.velocity = new Vector2(GetCombatMoveSpeed(), rb.velocity.y);
                isMoving = true;
            }
        }

        if (unitAnim != null)
            unitAnim.SetMove(isMoving, facingDir);
        if (BattleManager.Instance == null || !BattleManager.Instance.PortalWalkMode)
            ClampToScreen();
        ApplyLaneY(dt);
    }

    // 【2026-11-19 临时探针 · 游侠滑步归因，定位后连同调用点一起删除】
    // 只在我方弓手普攻出手窗口采样：记录前摇/攻击锁、移动速度、目标是否可交战、是否在并道。
    // 判别：① 攻击锁内仍在移动 → 前压没被拦；② 攻击锁已过但箭未射出 → 前摇超出锁；
    //       ③ 目标被判不可交战 → TryHoldDuringAttack 放行导致前压。节流 0.15s 避免刷屏。
    float _slideProbeNext;
    void ProbeRangerSlide()
    {
        if (Time.time < _slideProbeNext) return;
        if (!(this is Hero) || PlayerJobDefs.GetSelected() != PlayerJobId.Ranger) return;
        if (GetAttackVfxKit() != AttackVfxKit.Bow) return;   // 只看弓普攻窗口
        _slideProbeNext = Time.time + 0.15f;
        float vx = rb != null ? rb.velocity.x : 0f;
        bool lockOn = unitAnim != null && unitAnim.InAttackLock;
        bool inRange = target != null && IsInBasicAttackRange(target);
        bool engaged = target != null && GetCombatDist(this, target) <= GameConfig.GetCombatScreenWidth();
        Debug.Log($"[ProbeSlide] lock={lockOn} vx={vx:0.000} inRange={inRange} engaged={engaged} tgtNull={target == null} manual={_manualMove}");
    }

    protected override void Die(bool isCritKill = false)
    {
        base.Die(isCritKill);
        if (BattleManager.Instance != null)
            BattleManager.Instance.OnHeroDead();
        else
            Debug.LogWarning("[Hero] Die: BattleManager 为空，跳过 OnHeroDead");
    }

    /// <summary>
    /// Hero不回对象池，死亡动画播完后直接隐藏。
    /// 【2026-10-24】复活守卫：等待复活选择期间死亡回收定时器到期，若玩家已复活（血量>0）
    /// 则不隐藏 —— 否则复活后本体被延迟回收藏掉。
    /// </summary>
    protected override void OnDeathRelease()
    {
        if (currentHp <= 0f)
            gameObject.SetActive(false);
    }
}