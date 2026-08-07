using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 伤害飘字：用数字表现区分普通伤害 / 暴击 / 治疗（不再用暴击特效）。
/// 颜色、字号、浮速、弹出动画均分开。
/// </summary>
public class DamageTextSystem : Singleton<DamageTextSystem>
{
    public enum TextKind
    {
        NormalDamage,
        CritDamage,
        Heal,
        Dodge,
        Gold
    }

    [Header("普通伤害")]
    public Color normalColor = new Color(1f, 1f, 1f, 1f);
    public int normalFontSize = 32;
    public float normalScale = 1f;
    public float normalFloatSpeed = 1.2f;
    public float normalDuration = 0.7f;
    public float normalPopScale = 1.15f;   // 出现瞬间略放大

    [Header("暴击伤害")]
    public Color critColor = new Color(1f, 0.35f, 0.15f, 1f); // 橙红
    public int critFontSize = 48;
    public float critScale = 1.55f;
    public float critFloatSpeed = 1.85f;  // 更快冲上去
    public float critDuration = 1.0f;
    public float critPopScale = 1.9f;     // 更夸张弹出
    public string critPrefix = "";       // 可改成 "暴击 " 或 "CRIT "

    [Header("治疗")]
    public Color healColor = new Color(0.35f, 1f, 0.45f, 1f);
    public int healFontSize = 34;
    public float healScale = 1.1f;
    public float healFloatSpeed = 0.75f;  // 更慢、更柔
    public float healDuration = 1.1f;
    public float healPopScale = 1.25f;

    [Header("闪避 / 金币")]
    public Color dodgeColor = new Color(0.7f, 0.7f, 0.75f, 1f);
    public Color goldColor = new Color(1f, 0.85f, 0.3f, 1f);

    [Header("通用")]
    public GameObject damageTextPrefab;
    public float randomOffsetX = 0.35f;
    public float randomOffsetY = 0.2f;
    /// <summary>世界空间飘字基准字号缩放</summary>
    public float worldTextScale = 1.1f;

    private Queue<DamageTextInstance> _pool = new Queue<DamageTextInstance>();
    private List<DamageTextInstance> _active = new List<DamageTextInstance>();

    private class DamageTextInstance
    {
        public GameObject go;
        public TextMesh textMesh;
        public MeshRenderer meshRenderer;
        public float timer;
        public float duration;
        public Vector3 velocity;
        public float baseScale;
        public float popScale;
        public Color baseColor;
        public TextKind kind;
    }

    public void SpawnDamageText(Vector3 pos, int damage, bool isCrit)
    {
        if (isCrit)
        {
            string t = string.IsNullOrEmpty(critPrefix) ? damage.ToString() : critPrefix + damage;
            SpawnText(pos, t, TextKind.CritDamage);
        }
        else
        {
            SpawnText(pos, damage.ToString(), TextKind.NormalDamage);
        }
    }

    public void SpawnHealText(Vector3 pos, int amount)
    {
        SpawnText(pos, $"+{amount}", TextKind.Heal);
    }

    public void SpawnDodgeText(Vector3 pos)
    {
        SpawnText(pos, "闪避", TextKind.Dodge);
    }

    public void SpawnGoldText(Vector3 pos, int amount)
    {
        SpawnText(pos, $"+{amount}金", TextKind.Gold);
    }

    void SpawnText(Vector3 pos, string text, TextKind kind)
    {
        GetStyle(kind, out Color color, out int fontSize, out float scale,
            out float floatSpd, out float duration, out float pop);

        DamageTextInstance inst = GetOrCreateInstance();
        float ox = Random.Range(-randomOffsetX, randomOffsetX);
        float oy = Random.Range(0f, randomOffsetY);
        // 暴击左右散开更大；治疗更贴身偏上
        if (kind == TextKind.CritDamage) ox *= 1.4f;
        if (kind == TextKind.Heal) { ox *= 0.5f; oy += 0.25f; }

        inst.go.transform.position = pos + new Vector3(ox, oy, 0f);
        inst.textMesh.text = text;
        // 数字飘字用 PixelFont；含中文的（闪避 / 金）用 fusion-pixel
        Font font = (kind == TextKind.Dodge || kind == TextKind.Gold)
            ? GameFonts.GetChinese()
            : GameFonts.GetNumber();
        if (font != null)
        {
            inst.textMesh.font = font;
            if (inst.meshRenderer != null && font.material != null)
                inst.meshRenderer.sharedMaterial = font.material;
        }
        inst.textMesh.fontSize = fontSize;
        inst.textMesh.color = color;
        inst.baseColor = color;
        inst.baseScale = scale * worldTextScale;
        inst.popScale = pop;
        inst.duration = duration;
        inst.timer = duration;
        inst.kind = kind;

        // 初始：弹出放大，再收回
        inst.go.transform.localScale = Vector3.one * (scale * pop);

        // 速度：暴击冲得快且略抖；治疗稳；普伤中等
        float vx;
        float vy = floatSpd;
        switch (kind)
        {
            case TextKind.CritDamage:
                vx = Random.Range(-0.55f, 0.55f);
                vy += Random.Range(0.2f, 0.5f);
                break;
            case TextKind.Heal:
                vx = Random.Range(-0.12f, 0.12f);
                break;
            default:
                vx = Random.Range(-0.25f, 0.25f);
                break;
        }
        inst.velocity = new Vector3(vx, vy, 0f);

        if (inst.meshRenderer != null)
        {
            inst.meshRenderer.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            inst.meshRenderer.sortingOrder = GameConfig.SORT_VFX + 5;
        }

        inst.go.SetActive(true);
        _active.Add(inst);
    }

    void GetStyle(TextKind kind, out Color color, out int fontSize, out float scale,
        out float floatSpd, out float duration, out float pop)
    {
        switch (kind)
        {
            case TextKind.CritDamage:
                color = critColor; fontSize = critFontSize; scale = critScale;
                floatSpd = critFloatSpeed; duration = critDuration; pop = critPopScale;
                break;
            case TextKind.Heal:
                color = healColor; fontSize = healFontSize; scale = healScale;
                floatSpd = healFloatSpeed; duration = healDuration; pop = healPopScale;
                break;
            case TextKind.Dodge:
                color = dodgeColor; fontSize = 28; scale = 0.9f;
                floatSpd = 1.0f; duration = 0.65f; pop = 1.1f;
                break;
            case TextKind.Gold:
                color = goldColor; fontSize = 30; scale = 0.95f;
                floatSpd = 1.1f; duration = 0.85f; pop = 1.2f;
                break;
            default:
                color = normalColor; fontSize = normalFontSize; scale = normalScale;
                floatSpd = normalFloatSpeed; duration = normalDuration; pop = normalPopScale;
                break;
        }
    }

    DamageTextInstance GetOrCreateInstance()
    {
        if (_pool.Count > 0)
            return _pool.Dequeue();

        if (damageTextPrefab != null)
        {
            GameObject go = Instantiate(damageTextPrefab, transform);
            TextMesh tm = go.GetComponent<TextMesh>() ?? go.GetComponentInChildren<TextMesh>();
            return new DamageTextInstance
            {
                go = go,
                textMesh = tm,
                meshRenderer = go.GetComponent<MeshRenderer>()
            };
        }

        GameObject fallbackGo = new GameObject("DamageText");
        fallbackGo.transform.SetParent(transform);
        TextMesh fallbackTm = fallbackGo.AddComponent<TextMesh>();
        fallbackTm.anchor = TextAnchor.MiddleCenter;
        fallbackTm.alignment = TextAlignment.Center;
        fallbackTm.font = GameFonts.GetNumber();
        fallbackTm.fontSize = 36;
        fallbackTm.characterSize = 0.075f;
        fallbackTm.color = Color.white;
        MeshRenderer mr = fallbackGo.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            if (fallbackTm.font != null && fallbackTm.font.material != null)
                mr.sharedMaterial = fallbackTm.font.material;
            mr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            mr.sortingOrder = GameConfig.SORT_VFX + 5;
        }
        return new DamageTextInstance { go = fallbackGo, textMesh = fallbackTm, meshRenderer = mr };
    }

    void Update()
    {
        float dt = Time.deltaTime;
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            DamageTextInstance inst = _active[i];
            inst.timer -= dt;
            float life = 1f - Mathf.Clamp01(inst.timer / Mathf.Max(0.01f, inst.duration));

            // 位移：暴击后期减速更明显；治疗匀速上浮
            float speedMul = 1f;
            if (inst.kind == TextKind.CritDamage)
                speedMul = Mathf.Lerp(1.25f, 0.55f, life);
            else if (inst.kind == TextKind.Heal)
                speedMul = Mathf.Lerp(0.85f, 1.05f, life);

            inst.go.transform.position += inst.velocity * (dt * speedMul);

            // 缩放动画：前 20% 从 pop 收到 base，之后保持；暴击再轻微脉动
            float scale;
            if (life < 0.2f)
            {
                float t = life / 0.2f;
                scale = Mathf.Lerp(inst.baseScale * inst.popScale, inst.baseScale, t);
            }
            else
            {
                scale = inst.baseScale;
                if (inst.kind == TextKind.CritDamage)
                    scale *= 1f + 0.06f * Mathf.Sin(life * 28f);
            }
            inst.go.transform.localScale = Vector3.one * scale;

            // 渐隐：治疗更晚淡；暴击末段快淡
            float fadeStart = inst.kind == TextKind.Heal ? 0.45f : (inst.kind == TextKind.CritDamage ? 0.55f : 0.5f);
            float alpha = life < fadeStart ? 1f : 1f - (life - fadeStart) / (1f - fadeStart);
            Color c = inst.baseColor;
            c.a = Mathf.Clamp01(alpha) * inst.baseColor.a;
            inst.textMesh.color = c;

            if (inst.timer <= 0f)
            {
                inst.go.SetActive(false);
                _active.RemoveAt(i);
                _pool.Enqueue(inst);
            }
        }
    }
}
