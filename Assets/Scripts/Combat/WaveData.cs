using UnityEngine;

/// <summary>
/// 一波刷怪参数。正式关由 <see cref="WavePlanner"/> 按表铺波；
/// 引导步进也走同一结构（锚点 / 左右进场 / stagger），不再另开刷怪轨。
/// </summary>
[System.Serializable]
public class WaveData
{
    public float triggerX;           // 无刷怪点时的兜底触发 X
    public Transform spawnAnchor;    // 场景 MonsterSpawn_*（世界坐标，不跟 Ground 逻辑）
    public int monsterCount;
    public bool isBossWave;
    public bool spawned;
    public int aliveCount;
    /// <summary>有效时（&gt; -900）作为交战/夹击锚点 X。</summary>
    public float engageAnchorX = -999f;
    /// <summary>左右交替进场（宝箱/佣兵埋伏）。普通波保持右侧。</summary>
    public bool bilateralEnter;
    /// <summary>围着锚点散开（佣兵围殴）。</summary>
    public bool aroundAnchor;
    public UnitBase forcedTarget;
    /// <summary>&lt;0 用规则包默认间隔；0=同一帧出齐（埋伏）。</summary>
    public float staggerOverride = -1f;
    /// <summary>
    /// V3.0：在原型自带精英额度之外追加的精英只数（精英关每波 +1）。
    /// 占本波最后 N 个名额，不是额外追加。
    /// </summary>
    public int eliteBonus;
    /// <summary>
    /// 波次原型 id（wave_archetype.csv）。空 = 不接管，走旧的「奇偶近远 + 均分人数」逻辑。
    /// 由 <see cref="StageModeTable"/> 抽到的模式铺进正式关；引导步进不填。
    /// </summary>
    public string archetypeId = "";
    /// <summary>
    /// 关卡事件层 V1.0：本波挂载的事件 id（空 = 无事件）。
    /// 仅在 WavePlanner.PlanStageEvents 按章概率命中时填写；关闭事件层时恒为空。
    /// </summary>
    public string stageEventId = "";
    /// <summary>事件预告文案（出波前随波次播报一同显示）。</summary>
    public string stageEventTelegraph = "";
    /// <summary>事件预效果是否已结算（防止 SpawnNextPendingWave 重入时重复触发）。</summary>
    public bool stageEventApplied;
    /// <summary>SUPPLY 事件：出兵前全队回血比例（0 = 无）。</summary>
    public float stageEventHealPct;
    /// <summary>HAZARD 事件：英雄移速惩罚比例 0~1（0 = 无）。</summary>
    public float stageEventMovePenalty;
    public bool HasEngageAnchor => engageAnchorX > -900f;

    /// <summary>本波原型；未接管时为 null。</summary>
    public WaveArchetypeTable.Archetype Archetype =>
        string.IsNullOrEmpty(archetypeId) ? null : WaveArchetypeTable.Get(archetypeId);
}
