using UnityEngine;

/// <summary>
/// 职业自带技能攒槽器（2026-11-13 主人拍板）。
/// 独立通道：不占 RunLoadout 的 4 个抽卡技能槽、不进抽卡池、不做任何 UI。
/// 玩家自己每次普攻命中 +1，攒满且「战斗镜头内有存活怪」时自动释放；
/// 释放后清空再进一段短保底间隔（技能表 cooldown），与 4 槽的 PLAYER_SKILL_GCD 互不干涉。
/// 开局按 GameConfig.CAREER_SKILL_START_CHARGE_RATIO 送起步槽（30%）。
/// </summary>
public sealed class CareerSkillAccumulator
{
    string _skillId;
    PlayerJobId _job;
    int _need;
    int _charge;
    float _cooldown;
    float _nextCastAt;
    MonoBehaviour _runner;
    bool _firstHitLogged;

    /// <summary>本局是否已完成初始化（技能 id 与攒满阈值都在）。供宿主做订阅自愈判断。</summary>
    public bool IsReady => _need > 0 && !string.IsNullOrEmpty(_skillId);

    /// <summary>开局 / 换关重绑：职业决定攒满阈值与技能 id。</summary>
    public void ResetForBattle(MonoBehaviour runner, PlayerJobId job)
    {
        _runner = runner;
        _job = job;
        // 技能 id 与攒满阈值一律读职业真源 PlayerJobDefs.Def，战斗层不按职业枚举硬编码分叉。
        var def = PlayerJobDefs.Get(job);
        _skillId = def.CareerSkillId;
        _need = def.CareerSkillHits;

        _cooldown = 0f;
        var reg = SkillRegistry.Instance;
        var active = (reg != null && !string.IsNullOrEmpty(_skillId))
            ? reg.GetActiveSkill(_skillId) : null;
        if (active != null)
            _cooldown = Mathf.Max(0f, active.cooldown);
        else if (!string.IsNullOrEmpty(_skillId))
            Debug.LogError($"[CareerSkill] 取不到职业技能配置，攒槽器不生效: {_skillId}");

        _charge = Mathf.Clamp(
            Mathf.RoundToInt(_need * GameConfig.CAREER_SKILL_START_CHARGE_RATIO), 0, _need);
        _nextCastAt = 0f;
        _firstHitLogged = false;
    }

    /// <summary>订阅 Hero.OnAttack：闪避（damage=0）与打友军都不算攒槽。</summary>
    public void OnHeroBasicAttackHit(UnitBase target, float damage, bool isCrit)
    {
        if (_need <= 0 || string.IsNullOrEmpty(_skillId)) return;
        if (damage <= 0f || target == null || target.isAlly) return;
        if (_charge >= _need) return;
        _charge++;
        if (!_firstHitLogged)
        {
            _firstHitLogged = true;
            Debug.Log($"[CareerSkill] 首次普攻命中入账：{_skillId} {_charge}/{_need}");
        }
    }

    public void Tick()
    {
        if (_need <= 0 || string.IsNullOrEmpty(_skillId)) return;
        // 【2026-11-24 主人拍板】职业自带技能（剑盾冲锋/法师奥术弹/游侠弹幕等）保持原来的自动行为，
        // 不受技能栏「自动/手动」开关影响：两种模式下都按普攻攒槽→满了自动放。开关只控制 4 槽抽卡技能。
        var bm = BattleManager.Instance;
        if (bm == null || !bm.isInBattle || !bm.UnitsCanAct) return;
        if (BattleLootMode.Active) return;
        // 【2026-11-14 主人「不要冲锋的时候还在攻击」】冲锋协程还没跑完时不允许叠放第二次冲锋。
        if (CareerSkillExecutor.IsCharging) return;
        if (_charge < _need || Time.time < _nextCastAt) return;
        // 【2026-11-14 主人拍板「引导中不要触发剑盾的冲锋技能」】引导局是 TutorialDirector
        // 脚本化逐拍刷怪，破阵冲锋一次把整簇怪撞光会让那一拍空转（等清场/等精英直接跳空）。
        // 只挡 Charge 类（剑盾破阵冲锋）；判据走职业真源 CareerSkillKind，不按职业枚举硬编码。
        // 引导局的「看技能自动放」那一拍等的是 4 槽技能（RunLoadout / FillPlayerSkillEnergy），
        // 与职业自带技能无关，挡这里不会断引导。
        if (bm.IsTutorialRun && PlayerJobDefs.Get(_job).CareerSkillKind == CareerSkillKind.Charge) return;
        if (!CareerSkillExecutor.HasVisibleEnemy()) return;

        Debug.Log($"[CareerSkill] 攒满，准备释放 {_skillId}（{_charge}/{_need}）");
        if (!CareerSkillExecutor.Cast(_runner, _job, _skillId)) return;
        _charge = 0;
        _nextCastAt = Time.time + _cooldown;
    }
}
