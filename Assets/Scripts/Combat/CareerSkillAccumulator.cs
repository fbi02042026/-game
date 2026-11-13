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
        switch (job)
        {
            case PlayerJobId.SwordShield:
                _skillId = CareerSkillExecutor.ChargeSkillId;
                _need = GameConfig.CAREER_CHARGE_HITS;
                break;
            case PlayerJobId.Ranger:
                _skillId = CareerSkillExecutor.ScatterSkillId;
                _need = GameConfig.CAREER_SCATTER_HITS;
                break;
            case PlayerJobId.Mage:
                _skillId = CareerSkillExecutor.OrbSkillId;
                _need = GameConfig.CAREER_ORB_HITS;
                break;
            default:
                _skillId = null;
                _need = 0;
                break;
        }

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
        var bm = BattleManager.Instance;
        if (bm == null || !bm.isInBattle || !bm.UnitsCanAct) return;
        if (BattleLootMode.Active) return;
        if (_charge < _need || Time.time < _nextCastAt) return;
        if (!CareerSkillExecutor.HasVisibleEnemy()) return;

        Debug.Log($"[CareerSkill] 攒满，准备释放 {_skillId}（{_charge}/{_need}）");
        if (!CareerSkillExecutor.Cast(_runner, _job, _skillId)) return;
        _charge = 0;
        _nextCastAt = Time.time + _cooldown;
    }
}
