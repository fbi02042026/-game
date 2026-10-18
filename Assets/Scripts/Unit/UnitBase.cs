using UnityEngine;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// 所有战斗单位基类：英雄、怪物、佣兵全部继承这个，自动复用索敌/移动/攻击逻辑
/// 横版：英雄往右走，怪物从右侧刷新
///
/// 动画状态通过 UnitAnimation 统一控制：
/// - 移动时播放 MOVE，停止时播放 IDLE
/// - 攻击时触发 ATTACK
/// - 受伤时触发 DAMAGED
/// - 死亡时播放 DEATH，动画结束后回收对象
/// </summary>
public abstract partial class UnitBase : MonoBehaviour
{
    [Header("基础组件")]
    public Rigidbody2D rb;
    public SpriteRenderer sr;
    public Animator anim;

    [Header("动画配置")]
    [Tooltip("死亡动画播放时长（秒），播完后才回收对象")]
    public float deathAnimDuration = 0.8f;

    [Header("发射点/受击点")]
    [Tooltip("法球/弹幕发射位置（留空则自动创建在身体中部偏上）")]
    public Transform firePoint;
    [Tooltip("受击位置（留空则自动创建在身体中心）")]
    public Transform hitPoint;
    [Tooltip("发射点相对于根节点的偏移")]
    public Vector3 firePointOffset = new Vector3(0.3f, 0.8f, 0f);
    [Tooltip("受击点相对于根节点的偏移")]
    public Vector3 hitPointOffset = new Vector3(0f, 0.8f, 0f);

    /// <summary>所有单位固定的地面Y坐标（由AutoGameInitializer从SpawnPoint读取）</summary>
    public static float GROUND_Y = -3.5f;
    /// <summary>相对站立线的上下偏移，用于加宽地面后的前后排站位。</summary>
    public float LaneY { get; private set; }
    public float FootY => GROUND_Y + LaneY;

    /// <summary>当前生效的车道对齐容错偏移（Y，世界单位）。未启用则为 0。
    /// 追击方要扣掉目标身上这份偏移，否则「我追你+偏移、你追我」会两边一起漂到车道边界。</summary>
    public float LaneAlignBiasY { get; private set; }
    /// <summary>当前生效的左右容错（世界单位，恒 ≥0：表示站位比射程再近多少）。</summary>
    public float LaneAlignBiasX { get; private set; }
    /// <summary>是否启用车道对齐容错（基类默认关；Hero 打开）。关=完全回退成严丝合缝对齐。</summary>
    protected virtual bool UseLaneAlignTolerance => false;
    /// <summary>当前这份偏移是为哪个目标抽的（换目标才重抽，绝不每帧重抽）。</summary>
    UnitBase _laneBiasOwner;

    /// <summary>敌方换道锚点：锁定目标那一刻自己所在的车道（换目标才重设）。</summary>
    float _laneChaseAnchorY;
    UnitBase _laneChaseAnchorOwner;

    /// <summary>最近一次造成伤害的来源（结算 MVP 击杀归属）。</summary>
    public UnitBase LastDamageSource { get; private set; }

    public AttrSystem attr = new AttrSystem();

    void BindAttrOwnerKind()
    {
        if (attr == null) attr = new AttrSystem();
        if (this is Hero) attr.SetOwnerKind(AttrOwnerKind.Player);
        else if (this is Mercenary) attr.SetOwnerKind(AttrOwnerKind.Merc);
        else if (this is Monster) attr.SetOwnerKind(AttrOwnerKind.Monster);
    }
    public float currentHp;
    protected float attackCd = 0;
    // 2026-10-09 主人拍板：站桩保持计时，进入攻击范围后短时(0.5s)不再并道，抗怪跑动抖动
    protected float _standHoldUntil = 0f;
    protected UnitBase target;
    /// <summary>当前战斗目标（只读，供技能 VFX 等外部查询）。</summary>
    public UnitBase CurrentTarget => target;
    public bool isDead => currentHp <= 0;
    public int facingDir = 1; // 1右 -1左
    public bool isAlly; // true己方 false敌方
    /// <summary>精灵默认是否朝右（SPUM预制体朝向不一致，需子类设置）</summary>
    public bool spriteDefaultFacesRight = true;

    /// <summary>统一动画控制器（自动桥接SPUM或原生Animator）</summary>
    protected UnitAnimation unitAnim;
    /// <summary>是否正在执行死亡流程（防止重复进入）</summary>
    protected bool _isDying = false;
}