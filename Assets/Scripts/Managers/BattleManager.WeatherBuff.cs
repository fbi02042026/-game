using UnityEngine;

/// <summary>
/// BattleManager partial —— 天气 debuff 挂摘（《战斗昼夜天气》清单 §2 / T5）。
/// 仿 BattleManager.BossBlessing.cs 的 _bossStageTempBuff 手法：
/// 独立 <see cref="AttrBonusData"/> 引用字段，进关挂、换关按引用精确摘除（主角+佣兵 RecalcAttr）。
/// 🔴 绝不 tempBuffs.Clear() —— 石像赐福 / 局内强化卡是整局 buff，不能误删。
/// 数值口径（§2 定稿）：雪=我方移速 -5%、雨=我方攻速 -5%、晴=无；引导局由 T2 强制晴，天然无 debuff。
/// 挂摘时机与 T2 的 BattleAtmosphere 相位切换对齐：LoadStage 调 BindWeatherBuffToAtmosphere 订阅
/// OnChanged（幂等，换实例自动改订），ApplyForStage 每次触发即「先摘后挂」。
/// </summary>
public partial class BattleManager
{
    /// <summary>
    /// 本关天气 debuff 那一条（AttrBonusData 是 class，换关按引用精确摘除）。
    /// 只记这一条，绝不整份 tempBuffs.Clear() —— 石像赐福 / 强化卡是整局 buff，不能误删。
    /// </summary>
    AttrBonusData _weatherStageTempBuff;

    /// <summary>当前已订阅的氛围实例（退战重进换实例时自动改订，避免重复订阅/漏订）。</summary>
    BattleAtmosphere _weatherAtmosphere;

    /// <summary>
    /// LoadStage 调一次（T5 挂点，紧邻 RevertBossStageBlessing）：订阅 BattleAtmosphere.OnChanged
    /// 并立即同步当前天气。幂等：同一氛围实例只订一次；场景重进后实例更换自动改订旧的。
    /// 同时负责装配 T3/T6 表现控制器（DayNightOverlay / WeatherVfxController，懒创建、随场景销毁）。
    /// </summary>
    public void BindWeatherBuffToAtmosphere()
    {
        // 表现控制器装配（T3 遮罩 / T6 粒子）：Instance 懒创建，Bind 内部自订阅 OnChanged
        DayNightOverlay.Instance?.Bind(BattleAtmosphere.Instance);
        WeatherVfxController.Instance?.Bind(BattleAtmosphere.Instance);

        var atmo = BattleAtmosphere.Instance;
        if (atmo == null) return;
        if (_weatherAtmosphere != atmo)
        {
            if (_weatherAtmosphere != null) _weatherAtmosphere.OnChanged -= OnAtmosphereChanged;
            _weatherAtmosphere = atmo;
            atmo.OnChanged += OnAtmosphereChanged;
        }
        ApplyWeatherBuff();
    }

    void OnAtmosphereChanged(DayPhase phase, WeatherType weather)
    {
        ApplyWeatherBuff(weather);
    }

    /// <summary>§3 契约签名（无参）：按当前氛围天气挂/摘 debuff。读不到氛围时按晴处理（不挂）。</summary>
    public void ApplyWeatherBuff()
    {
        var atmo = BattleAtmosphere.Instance;
        ApplyWeatherBuff(atmo != null ? atmo.Weather : WeatherType.Clear);
    }

    /// <summary>
    /// 按天气挂我方 debuff：雪=AttrType.MoveSpeed -5%、雨=AttrType.AttackSpeed -5%、晴=无。
    /// 先按引用摘除上一条再挂新的（换关不残留、同关重复调用不叠加）；主角+佣兵 RecalcAttr。
    /// </summary>
    public void ApplyWeatherBuff(WeatherType weather)
    {
        ClearWeatherBuff();
        if (weather != WeatherType.Snow && weather != WeatherType.Rain) return;

        var buff = new AttrBonusData
        {
            attrType = weather == WeatherType.Snow ? AttrType.MoveSpeed : AttrType.AttackSpeed,
            value = -0.05f,
            isPercent = true
        };
        tempBuffs.Add(buff);
        _weatherStageTempBuff = buff;   // 记下本关这一条，换关时按引用撤销
        RecalcTeamAttrForWeatherBuff();
        Debug.Log($"[BattleManager] 天气 debuff 已挂：{weather} → {buff.attrType} -5%（独立引用，换关精确摘除）");
    }

    /// <summary>
    /// 按引用精确摘除天气 debuff（绝不 tempBuffs.Clear()）。
    /// StartNewRun 可能已整份 Clear（新一局）：Remove 返回 false 就不必重算，只把引用清掉。
    /// </summary>
    public void ClearWeatherBuff()
    {
        if (_weatherStageTempBuff == null) return;
        if (tempBuffs.Remove(_weatherStageTempBuff))
            RecalcTeamAttrForWeatherBuff();
        _weatherStageTempBuff = null;
    }

    /// <summary>主角 + 全体存活佣兵重算属性（与 ApplyTeamTempBuff 同款口径；阵亡的不重算，复活链会重新带上）。</summary>
    void RecalcTeamAttrForWeatherBuff()
    {
        if (Hero.Instance != null) Hero.Instance.RecalcAttr();
        var mercs = MercenaryManager.Instance != null ? MercenaryManager.Instance.GetActiveMercs() : null;
        if (mercs != null)
        {
            for (int i = 0; i < mercs.Count; i++)
            {
                var m = mercs[i];
                if (m == null || m.isDead || m.attr == null) continue;
                m.RecalcAttr();
            }
        }
        BattleUI.Instance?.ForceRefreshLiveBars();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        // BattleManager 是 DDOL，本方法只在退应用时走；退订氛围事件 + 摘 debuff 兜底
        if (_weatherAtmosphere != null) _weatherAtmosphere.OnChanged -= OnAtmosphereChanged;
        _weatherAtmosphere = null;
        ClearWeatherBuff();
    }
}
