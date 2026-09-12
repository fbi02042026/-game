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
    public bool HasEngageAnchor => engageAnchorX > -900f;
}
