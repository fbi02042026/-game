using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 战斗飘字（世界空间 TextMesh）：
/// · 我方受击：前缀「-」+ 白字，瞬间闪大 → 往左滑 → 停 1s → 上飘消失
/// · 敌方受击：红字，往右滑；暴击后缀「!」、亮紫
/// · 闪避：前缀 dodgePrefix +「闪避」
/// </summary>
public class DamageTextSystem : Singleton<DamageTextSystem>
{
    public enum TextKind
    {
        OutNormal,
        OutCrit,
        InNormal,
        InCrit,
        Heal,
        Dodge,
        Gold
    }

    enum MotionPhase { Pop, Slide, Hold, Rise }

    [Header("敌方普伤 — 打在敌人身上（比己方略小 10%）")]
    public Color outNormalColor = new Color(1f, 0.22f, 0.22f, 1f);
    public int outNormalFontSize = 44;
    public float outNormalScale = 1.08f;

    [Header("我方普伤 — 打在我方身上")]
    public Color inNormalColor = new Color(1f, 1f, 1f, 1f);
    public int inNormalFontSize = 46;
    public float inNormalScale = 1.12f;

    [Header("暴击 — 亮紫（敌方暴击另 ×0.9）")]
    public Color critColor = new Color(0.78f, 0.36f, 1f, 1f);
    public int critFontSize = 58;
    public float critScale = 1.3f;
    const float EnemyTextMul = 0.9f;

    [Header("前缀 / 后缀")]
    public string incomingPrefix = "-";
    public string critSuffix = "!";
    public string dodgePrefix = "·";

    [Header("闪避")]
    public Color dodgeColor = new Color(0.62f, 0.72f, 0.86f, 1f);

    [Header("治疗 / 金币")]
    public Color healColor = new Color(0.35f, 1f, 0.59f, 1f);
    public int healFontSize = 34;
    public float healScale = 1.05f;
    public Color goldColor = new Color(1f, 0.85f, 0.29f, 1f);

    [Header("动画 — 闪出 / 滑出 / 停 / 上飘")]
    public float popDuration = 0.05f;
    public float popOvershootNormal = 1.42f;
    public float popOvershootCrit = 1.68f;
    public float popFlashStrength = 0.55f;
    public float slideDuration = 0.11f;
    public float holdDuration = 1f;
    public float riseDuration = 0.42f;
    public float riseSpeed = 1.65f;
    public float slideDistanceNormal = 0.72f;
    public float slideDistanceCrit = 1.02f;
    public float slideDistanceDodge = 0.42f;

    [Header("散开范围（多段伤害）")]
    public float spreadRadiusX = 0.44f;
    public float spreadRadiusY = 0.3f;
    public float worldTextScale = 1.32f;

    [Header("通用")]
    public GameObject damageTextPrefab;

    Queue<DamageTextInstance> _pool = new Queue<DamageTextInstance>();
    List<DamageTextInstance> _active = new List<DamageTextInstance>();
    static float _nextTextScaleMul = 1f;

    /// <summary>连杀等临时放大下一跳飘字（用后自动复位 1）。</summary>
    public static void SetNextTextScaleMul(float mul)
    {
        _nextTextScaleMul = Mathf.Max(1f, mul);
    }

    class DamageTextInstance
    {
        public GameObject go;
        public TextMesh textMesh;
        public MeshRenderer meshRenderer;
        public TextKind kind;
        public MotionPhase phase;
        public float phaseTime;
        public float baseScale;
        public float popFromScale;
        public float slideDistance;
        public float slideDir;
        public Vector3 origin;
        public Vector3 anchor;
        public Color baseColor;
        public Color flashColor;
        public float totalDuration;
        public float timer;
        public TextMesh outlineMesh;
    }

    public void SpawnDamageText(Vector3 pos, int damage, bool isCrit, bool victimIsAlly)
    {
        TextKind kind = isCrit
            ? (victimIsAlly ? TextKind.InCrit : TextKind.OutCrit)
            : (victimIsAlly ? TextKind.InNormal : TextKind.OutNormal);
        string text = FormatDamageText(damage, kind);
        SpawnDirectional(pos, text, kind, victimIsAlly);
    }

    string FormatDamageText(int damage, TextKind kind)
    {
        string num = damage.ToString();
        bool incoming = kind == TextKind.InNormal || kind == TextKind.InCrit;
        bool crit = kind == TextKind.OutCrit || kind == TextKind.InCrit;
        if (incoming && !string.IsNullOrEmpty(incomingPrefix))
            num = incomingPrefix + num;
        if (crit && !string.IsNullOrEmpty(critSuffix))
            num = num + critSuffix;
        return num;
    }

    public void SpawnHealText(Vector3 pos, int amount)
    {
        SpawnDirectional(pos, $"+{amount}", TextKind.Heal, victimIsAlly: true, forceUp: true);
    }

    public void SpawnDodgeText(Vector3 pos, bool victimIsAlly)
    {
        string prefix = string.IsNullOrEmpty(dodgePrefix) ? "" : dodgePrefix;
        SpawnDirectional(pos, prefix + "闪避", TextKind.Dodge, victimIsAlly);
    }

    public void SpawnGoldText(Vector3 pos, int amount)
    {
        SpawnDirectional(pos, $"+{amount}金", TextKind.Gold, victimIsAlly: false, forceUp: true);
    }

    static bool IsCritDamage(TextKind kind) => kind == TextKind.OutCrit || kind == TextKind.InCrit;

    float ResolvePopOvershootNormal() =>
        GameConfig.COMBAT_JUICE_DAMAGE_TEXT_BOOST ? 1.55f : popOvershootNormal;

    float ResolvePopOvershootCrit() =>
        GameConfig.COMBAT_JUICE_DAMAGE_TEXT_BOOST ? 1.82f : popOvershootCrit;

    float ResolveSlideDistanceNormal() =>
        GameConfig.COMBAT_JUICE_DAMAGE_TEXT_BOOST ? 0.85f : slideDistanceNormal;

    float ResolveSlideDistanceCrit() =>
        GameConfig.COMBAT_JUICE_DAMAGE_TEXT_BOOST ? 1.15f : slideDistanceCrit;

    float ResolvePopFlashStrength() =>
        GameConfig.COMBAT_JUICE_DAMAGE_TEXT_BOOST ? 0.72f : popFlashStrength;

    void SpawnDirectional(Vector3 pos, string text, TextKind kind, bool victimIsAlly, bool forceUp = false)
    {
        GetStyle(kind, out Color color, out int fontSize, out float scale);

        DamageTextInstance inst = GetOrCreateInstance();
        float ox = Random.Range(-spreadRadiusX, spreadRadiusX);
        float oy = Random.Range(0f, spreadRadiusY);
        if (IsCritDamage(kind))
        {
            ox *= 1.25f;
            oy *= 1.15f;
        }
        else if (kind == TextKind.Dodge)
        {
            ox *= 0.55f;
            oy *= 0.65f;
        }

        inst.origin = pos + new Vector3(ox, oy, 0f);
        inst.anchor = inst.origin;
        inst.go.transform.position = inst.origin;
        inst.textMesh.text = text;

        Font font = (kind == TextKind.Dodge || kind == TextKind.Gold || kind == TextKind.InNormal || kind == TextKind.InCrit)
            ? GameFonts.GetChinese()
            : GameFonts.GetNumber();
        if (font != null)
        {
            inst.textMesh.font = font;
            if (inst.meshRenderer != null && font.material != null)
                inst.meshRenderer.sharedMaterial = font.material;
        }

        inst.textMesh.fontSize = fontSize;
        inst.textMesh.fontStyle = FontStyle.Bold;
        inst.textMesh.color = color;
        EnsureDamageChrome(inst, text, fontSize);
        inst.baseColor = color;
        inst.flashColor = kind == TextKind.InNormal || kind == TextKind.InCrit
            ? Color.Lerp(color, Color.white, ResolvePopFlashStrength())
            : Color.Lerp(color, Color.white, ResolvePopFlashStrength() * 0.85f);
        inst.baseScale = scale * worldTextScale * _nextTextScaleMul;
        _nextTextScaleMul = 1f;
        float popMul = IsCritDamage(kind) ? ResolvePopOvershootCrit() : ResolvePopOvershootNormal();
        inst.popFromScale = inst.baseScale * popMul;
        inst.kind = kind;
        inst.phase = MotionPhase.Pop;
        inst.phaseTime = 0f;

        if (forceUp)
            inst.slideDir = 0f;
        else
            inst.slideDir = victimIsAlly ? -1f : 1f;

        inst.slideDistance = IsCritDamage(kind) ? ResolveSlideDistanceCrit()
            : kind == TextKind.Dodge ? slideDistanceDodge
            : ResolveSlideDistanceNormal();

        inst.totalDuration = popDuration + slideDuration + holdDuration + riseDuration;
        inst.timer = inst.totalDuration;

        inst.go.transform.localScale = Vector3.one * inst.popFromScale;
        inst.textMesh.color = inst.flashColor;

        if (inst.meshRenderer != null)
        {
            inst.meshRenderer.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            inst.meshRenderer.sortingOrder = GameConfig.SORT_VFX + 5;
        }

        inst.go.SetActive(true);
        _active.Add(inst);
    }

    void EnsureDamageChrome(DamageTextInstance inst, string text, int fontSize)
    {
        if (inst == null || inst.go == null || inst.textMesh == null) return;
        inst.textMesh.fontStyle = FontStyle.Bold;

        if (inst.outlineMesh == null)
        {
            var og = new GameObject("Outline");
            og.transform.SetParent(inst.go.transform, false);
            og.transform.localPosition = new Vector3(0.04f, -0.04f, 0.02f);
            var tm = og.AddComponent<TextMesh>();
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.characterSize = inst.textMesh.characterSize;
            var mr = og.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
                mr.sortingOrder = GameConfig.SORT_VFX + 4;
            }
            inst.outlineMesh = tm;
        }
        inst.outlineMesh.font = inst.textMesh.font;
        inst.outlineMesh.fontSize = fontSize;
        inst.outlineMesh.fontStyle = FontStyle.Bold;
        inst.outlineMesh.text = text;
        inst.outlineMesh.color = new Color(0f, 0f, 0f, 1f);
        if (inst.outlineMesh.font != null && inst.outlineMesh.GetComponent<MeshRenderer>() != null
            && inst.outlineMesh.font.material != null)
            inst.outlineMesh.GetComponent<MeshRenderer>().sharedMaterial = inst.outlineMesh.font.material;
    }

    void GetStyle(TextKind kind, out Color color, out int fontSize, out float scale)
    {
        switch (kind)
        {
            case TextKind.OutCrit:
                color = critColor;
                fontSize = Mathf.RoundToInt(critFontSize * EnemyTextMul);
                scale = critScale * EnemyTextMul;
                break;
            case TextKind.InCrit:
                color = critColor; fontSize = critFontSize; scale = critScale;
                break;
            case TextKind.InNormal:
                color = inNormalColor; fontSize = inNormalFontSize; scale = inNormalScale;
                break;
            case TextKind.Heal:
                color = healColor; fontSize = healFontSize; scale = healScale;
                break;
            case TextKind.Dodge:
                color = dodgeColor; fontSize = 28; scale = 0.88f;
                break;
            case TextKind.Gold:
                color = goldColor; fontSize = 30; scale = 0.98f;
                break;
            default:
                color = outNormalColor; fontSize = outNormalFontSize; scale = outNormalScale;
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
        fallbackTm.fontSize = 42;
        fallbackTm.fontStyle = FontStyle.Bold;
        fallbackTm.characterSize = 0.08f;
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
            inst.phaseTime += dt;

            Vector3 pos = inst.anchor;
            float alpha = 1f;
            float scale = inst.baseScale;

            switch (inst.phase)
            {
                case MotionPhase.Pop:
                {
                    float u = popDuration <= 0.0001f ? 1f : Mathf.Clamp01(inst.phaseTime / popDuration);
                    float ease = 1f - (1f - u) * (1f - u);
                    scale = Mathf.Lerp(inst.popFromScale, inst.baseScale, ease);
                    inst.textMesh.color = Color.Lerp(inst.flashColor, inst.baseColor, ease);
                    if (inst.phaseTime >= popDuration)
                    {
                        inst.phase = MotionPhase.Slide;
                        inst.phaseTime = 0f;
                    }
                    break;
                }
                case MotionPhase.Slide:
                {
                    float u = slideDuration <= 0.0001f ? 1f : Mathf.Clamp01(inst.phaseTime / slideDuration);
                    float ease = 1f - (1f - u) * (1f - u);
                    if (inst.slideDir == 0f)
                        pos = inst.origin + Vector3.up * (inst.slideDistance * 0.35f * ease);
                    else
                        pos = inst.origin + Vector3.right * (inst.slideDir * inst.slideDistance * ease);
                    if (inst.phaseTime >= slideDuration)
                    {
                        inst.anchor = pos;
                        inst.phase = MotionPhase.Hold;
                        inst.phaseTime = 0f;
                    }
                    break;
                }
                case MotionPhase.Hold:
                    pos = inst.anchor;
                    if (inst.phaseTime >= holdDuration)
                    {
                        inst.phase = MotionPhase.Rise;
                        inst.phaseTime = 0f;
                    }
                    break;
                case MotionPhase.Rise:
                {
                    float u = riseDuration <= 0.0001f ? 1f : Mathf.Clamp01(inst.phaseTime / riseDuration);
                    pos = inst.anchor + Vector3.up * (riseSpeed * inst.phaseTime);
                    alpha = 1f - u;
                    break;
                }
            }

            inst.go.transform.position = pos;
            inst.go.transform.localScale = Vector3.one * scale;

            Color c = inst.textMesh.color;
            c.a = Mathf.Clamp01(alpha) * inst.baseColor.a;
            inst.textMesh.color = c;
            if (inst.outlineMesh != null)
            {
                var oc = inst.outlineMesh.color;
                oc.a = c.a;
                inst.outlineMesh.color = oc;
            }

            if (inst.timer <= 0f)
            {
                inst.go.SetActive(false);
                _active.RemoveAt(i);
                _pool.Enqueue(inst);
            }
        }
    }
}
