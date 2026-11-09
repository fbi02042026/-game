using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 《跑酷小游戏》V1 —— T4 佣兵队列（EndlessRunGame 分部，2026-11-09）。
///
/// 规格来源：Docs/《跑酷小游戏》_待办任务清单.md §2.2 B 方案定稿 / §6 / §10，
/// 数值全部走 RunTrackTable（真源 run_track.csv，key 见 ReadQueueTuning，不造新 key）。
///
/// 【tick】只挂 FixedUpdate（T2 已有 Update、T3 已有 LateUpdate）；跳信号 = IsAirborne 上升沿
/// （自缓存 _qWasAirborne，不读 T3 的 _wasAirborne）；不调用任何输入 API，不改 T2/T3。
/// 【跟随】跳是全队指令：按 follow_jump_delay × 序号延迟复现玩家轨迹（与 T2 UpdateJump 同款 sin 解析式）；
/// x 用固定槽位 + 阻尼弹簧 cur=Lerp(cur,target,1-exp(-12dt))。
/// 【0.7】渲染缩放只进佣兵 transform.localScale；LogicX / 碰撞 / 射程 / 槽位 x 一律逻辑 px。
/// 【边界】佣兵只在内存：不写佣兵招募存档字段、不调任何钱包 / 存档 API；
/// 结算 / HUD / 入口 / 存档交 T5~T7，本分部只暴露 PlayerHp / PlayerDied / EnqueueMerc()。
/// 立绘接入是待确认项：佣兵用空 SpriteRenderer 槽位，不找图 / 不造图 / 不写占位兜底。
/// </summary>
public partial class EndlessRunGame
{
    // ============================================================
    // 对外契约（T6 消费；只置位事件，不写结算 UI / 不存盘）
    // ============================================================

    /// <summary>玩家血量（player_hp，初始 3），归零触发 PlayerDied。</summary>
    public int PlayerHp { get; private set; }

    /// <summary>玩家死亡事件：T4 只置位一次，结算 UI / 存盘交 T6。</summary>
    public event System.Action PlayerDied;

    /// <summary>获得佣兵的唯一入口（本期无调用方，机制先就位；不写测试按钮 / 不自动塞佣兵）。</summary>
    public void EnqueueMerc()
    {
        if (!_qReady || _dead) return;
        if (SuppressWaveSpawn) return;              // 天降窗口未结束，不叠加第二只

        if (_mercs.Count < 2)
        {
            // 有空槽：直接天降入队，落地自动生效（验收场景①）。
            var m = CreateMerc();
            m.anim = MercAnim.Drop;
            m.animT = 0f;
            m.curX = SlotX(_mercs.Count);           // 落自己的槽位
            m.dropFromY = _groundTopYPx + _jumpHeightPx;
            m.go.SetActive(true);
            _mercs.Add(m);
            SuppressWaveSpawn = true;               // 天降窗口置位，落地复位
        }
        else
        {
            // 满员：待定天降（前 1 槽上方），0.35s 内跳=顶替最老、不跳=放弃（验收场景②）。
            _pending = CreateMerc();
            _pending.anim = MercAnim.PendingDrop;
            _pending.animT = 0f;
            _pending.curX = SlotX(0);               // 新佣兵落被顶替槽位（前 1）
            _pending.dropFromY = _groundTopYPx + _jumpHeightPx;
            _pending.go.SetActive(true);
            SuppressWaveSpawn = true;               // 窗口内不刷怪波；跳=顶替 / 不跳=放弃
        }
    }

    // ============================================================
    // 数据类型 / 状态
    // ============================================================

    enum MercAnim { None, Drop, PendingDrop, ReplaceJumpOut, KnockFly }

    /// <summary>一只佣兵：渲染槽位 + 队列动画状态 + 攻击 CD（判定全用逻辑 px）。</summary>
    class Merc
    {
        public GameObject go;
        public SpriteRenderer sr;
        public MercAnim anim;
        public float animT;
        public float curX;        // 槽位 x（逻辑 px，弹簧跟随，不含 0.7）
        public float refillFrom;  // 补位起点 x
        public float refillT;     // 补位剩余秒
        public float atkCd;       // 攻击 CD 剩余秒
        public float dropFromY;   // 天降起点 y
    }

    // ---- 数值（RunTrackTable）----
    int _qPlayerHpMax;
    float _qDropSec, _qReplaceSec, _qRefillSec, _qInvSec, _qNoProtSec;
    float _qFollowDelay, _qAtkCd, _qMeleeRange, _qRangedRange, _qSlotGap, _qRenderScale;
    bool _qReady;

    // ---- 运行时 ----
    readonly List<Merc> _mercs = new List<Merc>();   // index 0 = 最前（挡刀位 / FIFO 最老）
    Merc _pending;                          // 满员天降待定佣兵（跳=顶替 / 不跳=放弃）
    bool _qWasAirborne;                     // 本分部自己的跳信号缓存（不读 T3 的）
    float _qPrevMeters;                     // 里程回落检测 → 重开复位队列
    float _playerInvT, _noProtT, _playerAtkCd;
    bool _dead;
    Segment _lastGapSeg;                    // 掉沟去重：同一 Gap 段只结算一次

    // ============================================================
    // 初始化 / 重开
    // ============================================================

    void InitQueue()
    {
        ReadQueueTuning();
        ResetQueue();
        _qReady = true;
    }

    void ReadQueueTuning()
    {
        _qPlayerHpMax  = RunTrackTable.GetInt("player_hp", 3);
        _qDropSec      = RunTrackTable.GetFloat("merc_drop_time", 0.35f);
        _qReplaceSec   = RunTrackTable.GetFloat("merc_replace_jump_time", 0.4f);
        _qRefillSec    = RunTrackTable.GetFloat("queue_refill_time", 0.25f);
        _qInvSec       = RunTrackTable.GetFloat("hit_invincible_time", 0.35f);
        _qNoProtSec    = RunTrackTable.GetFloat("no_protection_time", 0.3f);
        _qFollowDelay  = RunTrackTable.GetFloat("follow_jump_delay", 0.10f);
        _qAtkCd        = RunTrackTable.GetFloat("attack_cd", 0.62f);
        _qMeleeRange   = RunTrackTable.GetFloat("melee_range", 96f);
        _qRangedRange  = RunTrackTable.GetFloat("ranged_range", 288f);
        _qSlotGap      = RunTrackTable.GetFloat("wave_group_gap", 130f);
        _qRenderScale  = RunTrackTable.GetFloat("render_scale", 0.7f);
    }

    /// <summary>开局 / 重开（里程回落检测触发）：清佣兵、回满血、清全部计时器。</summary>
    void ResetQueue()
    {
        for (int i = 0; i < _mercs.Count; i++) DestroyMerc(_mercs[i]);
        _mercs.Clear();
        if (_pending != null) { DestroyMerc(_pending); _pending = null; }
        PlayerHp = _qPlayerHpMax;
        _dead = false;
        _playerInvT = _noProtT = _playerAtkCd = 0f;
        _qWasAirborne = false;
        _lastGapSeg = null;
        SuppressWaveSpawn = false;
    }

    void DestroyMerc(Merc m)
    {
        if (m != null && m.go != null) Object.Destroy(m.go);
    }

    Merc CreateMerc()
    {
        var go = new GameObject("RunMerc");
        go.transform.SetParent(_worldRoot.transform, false);
        go.layer = _worldLayer;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 6;
        // 立绘接入是待确认项：空 SpriteRenderer 槽位（sprite 留 null），不兜底。
        // 0.7 唯一落点（渲染 localScale），与 T2 玩家 / T3 怪同做法。
        go.transform.localScale = new Vector3(_qRenderScale, _qRenderScale, 1f);
        return new Merc { go = go, sr = sr, anim = MercAnim.None, atkCd = 0f, refillT = 0f };
    }

    /// <summary>槽位 x：玩家 x − wave_group_gap ×（槽位序号+1），index 0 最前。</summary>
    float SlotX(int idx)
    {
        return _playerXPx - _qSlotGap * (idx + 1);
    }

    // ============================================================
    // 每帧 tick（只挂 FixedUpdate；T2 有 Update、T3 有 LateUpdate）
    // ============================================================

    void FixedUpdate()
    {
        if (!_built || !_playing || _appPaused) return;
        if (!_qReady) InitQueue();

        // 重开信号：里程回落（T2 ResetRun 把 Meters 归零），零改主文件。
        if (_qPrevMeters > 0f && Meters < _qPrevMeters) ResetQueue();
        _qPrevMeters = Meters;

        // 跳信号：IsAirborne 上升沿（唯一跳来源）。
        if (IsAirborne && !_qWasAirborne) OnQueueJumpEdge();
        _qWasAirborne = IsAirborne;

        float dt = Time.fixedDeltaTime;
        TickTimers(dt);
        TickFollowX(dt);
        TickMercAnims(dt);

        if (!IsSafeWindow)          // 黑屏安全窗口：零攻击零碰撞
        {
            TickGap();              // 掉沟：空中不判（跳过沟）
            TickCollision();        // 碰撞：空中不判
            TickAttack(dt);         // 攻击：空中不出手（函数内再闸一次）
        }
    }

    void OnQueueJumpEdge()
    {
        // 跳=换人：满员天降窗口内起跳 → 最老（前 1 槽）往后跳出，待定佣兵落前 1 槽。
        if (_pending == null) return;
        var old = _mercs[0];
        old.anim = MercAnim.ReplaceJumpOut;
        old.animT = 0f;
        _pending.anim = MercAnim.Drop;   // animT 保留：总窗口仍是 0.35s
        _mercs.Insert(0, _pending);
        _pending = null;
        // SuppressWaveSpawn 由这次落地完成时复位。
    }

    void TickTimers(float dt)
    {
        if (_playerInvT > 0f) _playerInvT = Mathf.Max(0f, _playerInvT - dt);
        if (_noProtT > 0f) _noProtT = Mathf.Max(0f, _noProtT - dt);
        if (_playerAtkCd > 0f) _playerAtkCd = Mathf.Max(0f, _playerAtkCd - dt);
    }

    // ============================================================
    // 跟随：x 弹簧 + y 解析式
    // ============================================================

    void TickFollowX(float dt)
    {
        for (int i = 0; i < _mercs.Count; i++)
        {
            var m = _mercs[i];
            if (m.anim != MercAnim.None && m.anim != MercAnim.Drop) continue;   // 出场动画期 x 不跟
            float target = SlotX(i);
            if (m.refillT > 0f)
            {
                // 补位：0.25s 平滑到位（不瞬移）
                m.refillT -= dt;
                float p = 1f - Mathf.Max(0f, m.refillT) / _qRefillSec;
                m.curX = Mathf.Lerp(m.refillFrom, target, p);
            }
            else
            {
                // 阻尼弹簧（§2.2 定稿公式，逐字）：cur = Lerp(cur, target, 1 - exp(-12·dt))
                m.curX = Mathf.Lerp(m.curX, target, 1f - Mathf.Exp(-12f * dt));
            }
        }
    }

    /// <summary>跟跳 y：与 T2 UpdateJump 同款 sin 解析式，按 0.10s × 序号延迟复现玩家轨迹。</summary>
    float FollowY(float delay)
    {
        float t = _jumpT - delay;
        if (t >= 0f && t <= _jumpAirSec)
            return _groundTopYPx + _jumpHeightPx * Mathf.Sin(Mathf.PI * t / Mathf.Max(0.0001f, _jumpAirSec));
        return _groundTopYPx;
    }

    // ============================================================
    // 佣兵动画 / 出队
    // ============================================================

    void TickMercAnims(float dt)
    {
        for (int i = _mercs.Count - 1; i >= 0; i--)
        {
            var m = _mercs[i];
            if (m.atkCd > 0f) m.atkCd = Mathf.Max(0f, m.atkCd - dt);

            switch (m.anim)
            {
                case MercAnim.Drop:
                    m.animT += dt;
                    if (m.animT >= _qDropSec) { m.anim = MercAnim.None; SuppressWaveSpawn = false; }
                    break;
                case MercAnim.ReplaceJumpOut:
                case MercAnim.KnockFly:
                    m.animT += dt;
                    if (m.animT >= _qReplaceSec)
                    {
                        bool knocked = m.anim == MercAnim.KnockFly;
                        DestroyMerc(m);
                        _mercs.RemoveAt(i);
                        if (knocked)
                        {
                            _noProtT = _qNoProtSec;   // 佣兵消失后 0.3s 无保护
                            StartRefill();
                        }
                        continue;
                    }
                    break;
            }
            ApplyMercTransform(m, i);
        }

        if (_pending != null)
        {
            _pending.animT += dt;
            if (_pending.animT >= _qDropSec)
            {
                // 不跳=放弃：窗口内没有起跳，待定佣兵离场。
                DestroyMerc(_pending);
                _pending = null;
                SuppressWaveSpawn = false;
            }
            else
            {
                ApplyMercTransform(_pending, 0);
            }
        }
    }

    /// <summary>佣兵消失（弹飞）后空槽出现：站立佣兵 0.25s 平滑补位。</summary>
    void StartRefill()
    {
        for (int i = 0; i < _mercs.Count; i++)
        {
            var m = _mercs[i];
            if (m.anim != MercAnim.None) continue;
            if (m.refillT > 0f) continue;
            if (Mathf.Abs(m.curX - SlotX(i)) < 0.01f) continue;
            m.refillFrom = m.curX;
            m.refillT = _qRefillSec;
        }
    }

    void ApplyMercTransform(Merc m, int idx)
    {
        float x = m.curX;
        float y = _groundTopYPx;
        switch (m.anim)
        {
            case MercAnim.Drop:
            case MercAnim.PendingDrop:
                y = Mathf.Lerp(m.dropFromY, _groundTopYPx, Mathf.Clamp01(m.animT / _qDropSec));
                break;
            case MercAnim.ReplaceJumpOut:   // 被顶替：往后跳出消失（复用跳高做弧线）
                float kOut = m.animT / Mathf.Max(0.0001f, _qReplaceSec);
                x = m.curX - _qSlotGap * kOut;
                y = _groundTopYPx + _jumpHeightPx * Mathf.Sin(Mathf.PI * kOut);
                break;
            case MercAnim.KnockFly:         // 受击：往前弹飞（与跳出两种表现分开；时长复用 merc_replace_jump_time，表内无独立 key）
                float kFly = m.animT / Mathf.Max(0.0001f, _qReplaceSec);
                x = m.curX + _qSlotGap * 2f * kFly;
                break;
            case MercAnim.None:             // 站立：0.10s × 序号 延迟复现玩家跳轨迹
                y = FollowY(_qFollowDelay * (idx + 1));
                break;
        }
        // 判定 / 世界坐标只乘 INV_PX2U，绝不乘 0.7（0.7 已在 localScale）。
        m.go.transform.localPosition = new Vector3(x * INV_PX2U, y * INV_PX2U, 0f);
    }

    // ============================================================
    // 碰撞（受击 / 挡刀）
    // ============================================================

    void TickCollision()
    {
        if (IsAirborne) return;   // 空中不碰撞
        for (int i = 0; i < ActiveEnemies.Count; i++)
        {
            var e = ActiveEnemies[i];
            if (!e.IsAlive) continue;
            // 怪从右迎面；接触阈值用怪自己的 ColliderHalfW（无单位半宽表 key）。
            if (e.LogicX > _playerXPx + e.ColliderHalfW) continue;
            ResolveHit(e);
            break;   // 一 tick 只结算一次碰撞
        }
    }

    void ResolveHit(RunEnemy e)
    {
        e.TakeDamage(e.Hp);   // 碰撞的怪同帧消亡

        // 挡刀：有站立佣兵且不在无保护期 → 最前佣兵往前弹飞、不扣血（验收场景③）。
        Merc shield = FirstStandingMerc();
        if (shield != null && _noProtT <= 0f)
        {
            shield.anim = MercAnim.KnockFly;
            shield.animT = 0f;
            return;
        }

        // 无佣兵（或无保护期）→ 玩家扣 1 + 无敌（验收场景④）；玩家无敌期内不再扣血。
        if (_playerInvT > 0f) return;
        PlayerHp -= 1;
        _playerInvT = _qInvSec;
        if (PlayerHp <= 0 && !_dead)
        {
            _dead = true;
            PlayerDied?.Invoke();
        }
    }

    /// <summary>最前的站立佣兵（天降 / 出场动画中不参与碰撞，不挡刀）。</summary>
    Merc FirstStandingMerc()
    {
        for (int i = 0; i < _mercs.Count; i++)
            if (_mercs[i].anim == MercAnim.None) return _mercs[i];
        return null;
    }

    // ============================================================
    // 掉沟
    // ============================================================

    /// <summary>
    /// 掉沟判定：读 T3 内嵌 _segments / SegKind.Gap（同类分部 private 可见），
    /// 玩家固定 x（_playerXPx）落在某 Gap 段 [left, right] 内 = 越过沟沿。
    /// 空中不判（跳过沟不算掉沟）；同一段用 _lastGapSeg 引用去重只结算一次；
    /// 掉沟直接扣玩家 1、不由佣兵挡，并给 hit_invincible_time 无敌（无敌期随卷速滚过沟到下一平台）。
    /// </summary>
    void TickGap()
    {
        if (IsAirborne) return;
        if (_playerInvT > 0f) return;
        for (int i = 0; i < _segments.Count; i++)
        {
            var s = _segments[i];
            if (s.kind != SegKind.Gap) continue;
            if (_playerXPx < s.left || _playerXPx > s.right) continue;
            if (_lastGapSeg == s) return;
            _lastGapSeg = s;
            PlayerHp -= 1;
            _playerInvT = _qInvSec;
            if (PlayerHp <= 0 && !_dead)
            {
                _dead = true;
                PlayerDied?.Invoke();
            }
            return;
        }
    }

    // ============================================================
    // 攻击
    // ============================================================

    /// <summary>
    /// 全队自动出手：玩家 + 各佣兵按各自射程与 attack_cd；只让射程内单位出手；
    /// 命中调 RunEnemy.TakeDamage（一刀清 Hp，CD 是唯一瓶颈）；空中 / 安全区不出手。
    /// </summary>
    void TickAttack(float dt)
    {
        if (IsAirborne) return;   // 空中不攻击（闸门）
        if (IsSafeWindow) return; // 双保险（外层已闸）

        if (_playerAtkCd <= 0f && TryAttack(_playerXPx, _qMeleeRange))
            _playerAtkCd = _qAtkCd;

        for (int i = 0; i < _mercs.Count; i++)
        {
            var m = _mercs[i];
            if (m.anim != MercAnim.None) continue;   // 天降 / 出场中不出手
            if (m.atkCd > 0f) continue;
            // 射程按槽位定：前槽（挡刀位）近战 / 后槽远程；佣兵近战远程归属无数据源，见回报存疑项。
            float range = (i == 0) ? _qMeleeRange : _qRangedRange;
            if (TryAttack(m.curX, range)) m.atkCd = _qAtkCd;
        }
    }

    /// <summary>射程内最近的一只迎面怪（LogicX ≥ 单位 x 且距离 ≤ 射程）；命中一刀清 HP。</summary>
    bool TryAttack(float unitX, float range)
    {
        RunEnemy best = null;
        for (int i = 0; i < ActiveEnemies.Count; i++)
        {
            var e = ActiveEnemies[i];
            if (!e.IsAlive) continue;
            float d = e.LogicX - unitX;
            if (d < 0f || d > range) continue;
            if (best == null || e.LogicX < best.LogicX) best = e;
        }
        if (best == null) return false;
        best.TakeDamage(best.Hp);
        return true;
    }
}
