using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 战斗飘字（世界空间 TextMesh）— 方向规则定死：
/// · 我方受击：前缀「-」+ 白字 → 往左滑（或按 hitVfxFacing 修正侧向攻击）
/// · 敌方受击：红字 → 始终往受害者后方（与怪物 GetVfxFacingDir 相反）
/// · 暴击：前缀「暴击」、亮紫
/// </summary>
public class DamageTextSystem : Singleton<DamageTextSystem>, ICombatBoundSingleton
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

    /// <summary>
    /// 伤害元素（2026-09-26 主人口径）：火焰伤害飘字橙色、冰霜伤害飘字蓝色。
    /// 只有 Fire / Ice 会覆盖飘字颜色，None 走原来的普伤/暴击配色。
    /// </summary>
    public enum DamageElement
    {
        None = 0,
        Fire = 1,
        Ice = 2
    }

    /// <summary>火焰伤害飘字：橙色。</summary>
    public static readonly Color FireTextColor = new Color(1f, 0.55f, 0.12f, 1f);
    /// <summary>冰霜伤害飘字：蓝色。</summary>
    public static readonly Color IceTextColor = new Color(0.42f, 0.80f, 1f, 1f);

    static Color ElementColor(DamageElement element)
    {
        return element == DamageElement.Ice ? IceTextColor : FireTextColor;
    }

    enum MotionPhase { Pop, Slide, Hold, Rise }

    [Header("敌方普伤 — 打在敌人身上（比己方略小 10%）")]
    public Color outNormalColor = new Color(1f, 0.22f, 0.22f, 1f);
    public int outNormalFontSize = 44;
    public float outNormalScale = 1.08f;

    [Header("我方普伤 — 打在我方身上")]
    public Color inNormalColor = new Color(1f, 1f, 1f, 1f);
    public int inNormalFontSize = 37;
    public float inNormalScale = 0.90f;

    [Header("暴击 — 亮紫（敌方暴击另 ×0.9）")]
    public Color critColor = new Color(0.78f, 0.36f, 1f, 1f);
    public int critFontSize = 58;
    public float critScale = 1.3f;
    const float EnemyTextMul = 0.9f;
    const float GlobalSizeMul = 0.9f;

    [Header("前缀 / 后缀")]
    public string incomingPrefix = "-";
    public string critPrefix = "暴击";
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
    public float spreadRadiusX = 0.22f;
    public float spreadRadiusY = 0.15f;
    public float worldTextScale = 1.188f;

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
        public SpriteRenderer goldIcon;   // Gold 专用：+N 右侧的抽奖币小图标（懒创建，池化复用）
        public Sprite goldIconSprite;     // 图标加载缓存（实例各存一份，防止回收后引用丢失）
    }

    public void SpawnDamageText(Vector3 pos, int damage, bool isCrit, bool victimIsAlly, int hitVfxFacing = 0)
    {
        SpawnDamageText(pos, damage, isCrit, victimIsAlly, hitVfxFacing, DamageElement.None);
    }

    /// <summary>
    /// 我方打出的暴击飘字节流计数。
    /// 【2026-10-10 主人反馈「暴击999 紫色太多」】我方暴击每 2 次只显示 1 次；
    /// 普通伤害 / 我方受击（含敌方暴击我方）不受影响，照常显示。
    /// </summary>
    static int _critShowCounter;

    /// <summary>
    /// 带元素色的伤害飘字：火 = 橙、冰 = 蓝。数字已含附加伤害（见 UnitBase.ResolveElementalBonus）。
    /// </summary>
    public void SpawnDamageText(Vector3 pos, int damage, bool isCrit, bool victimIsAlly, int hitVfxFacing, DamageElement element)
    {
        // 只砍「我方打敌方」的紫色暴击：敌方暴击我方（victimIsAlly）仍然每次都弹，警示信息不能省。
        if (isCrit && !victimIsAlly)
        {
            _critShowCounter++;
            if (_critShowCounter % 2 != 0) return;
        }

        TextKind kind = isCrit
            ? (victimIsAlly ? TextKind.InCrit : TextKind.OutCrit)
            : (victimIsAlly ? TextKind.InNormal : TextKind.OutNormal);
        string text = FormatDamageText(damage, kind);
        SpawnDirectional(pos, text, kind, victimIsAlly, hitVfxFacing: hitVfxFacing, element: element);
    }

    string FormatDamageText(int damage, TextKind kind)
    {
        string num = damage.ToString();
        bool incoming = kind == TextKind.InNormal || kind == TextKind.InCrit;
        bool crit = kind == TextKind.OutCrit || kind == TextKind.InCrit;
        if (incoming && !string.IsNullOrEmpty(incomingPrefix))
            num = incomingPrefix + num;
        if (crit && !string.IsNullOrEmpty(critPrefix))
            num = critPrefix + num;
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

    /// <summary>
    /// 击杀发局内抽奖币的飘字（BattleManager.OnMonsterDead 调）。
    /// 【2026-10-26 主人需求】「+10金」容易误读成局外金币 → 改成「+N」纯数字，
    /// 右侧跟一个缩小的抽奖币图标（icon_draft_coin，见 <see cref="EnsureGoldIcon"/>），
    /// 与顶栏 DraftDisplay 同图，看一眼就知道涨的是抽奖币。
    /// </summary>
    public void SpawnGoldText(Vector3 pos, int amount)
    {
        SpawnDirectional(pos, $"+{amount}", TextKind.Gold, victimIsAlly: false, forceUp: true);
    }

    static bool IsCritDamage(TextKind kind) => kind == TextKind.OutCrit || kind == TextKind.InCrit;

    /// <summary>飘字滑出方向（定死）：己方略左；敌方始终往受害者后方。</summary>
    static float ResolveDamageSlideDir(bool victimIsAlly, int victimFacingDir)
    {
        if (victimIsAlly)
        {
            if (victimFacingDir != 0)
                return victimFacingDir > 0 ? 1f : -1f;
            return -1f;
        }

        int face = victimFacingDir != 0 ? victimFacingDir : 1;
        return face > 0 ? -1f : 1f;
    }

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

    void SpawnDirectional(Vector3 pos, string text, TextKind kind, bool victimIsAlly, bool forceUp = false, int hitVfxFacing = 0, DamageElement element = DamageElement.None)
    {
        GetStyle(kind, out Color color, out int fontSize, out float scale);
        // 火 / 冰附加伤害：飘字整体换成橙 / 蓝（主人 2026-09-26 口径）
        if (element != DamageElement.None)
            color = ElementColor(element);

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

        Font font = (kind == TextKind.Dodge || kind == TextKind.Gold || kind == TextKind.InNormal || kind == TextKind.InCrit
            || kind == TextKind.OutCrit)
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
        // 抽奖币飘字带小图标；其他类型复用同实例时必须收掉，防止伤害/治疗飘字残留币图
        if (kind == TextKind.Gold) EnsureGoldIcon(inst);
        else if (inst.goldIcon != null) inst.goldIcon.enabled = false;
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
            inst.slideDir = ResolveDamageSlideDir(victimIsAlly, hitVfxFacing);

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

    /// <summary>
    /// 抽奖币飘字的右侧小图标（2026-10-26 主人需求「+10 后面带一个缩小的抽奖币图标」）。
    /// <para>· 挂在飘字节点下 → 自动跟随 Pop/Slide/Rise 动画与整体缩放；</para>
    /// <para>· 尺寸 ≈ 文字渲染高 × 0.85（主人要的「缩小」），按 TextMesh 的 mesh 局部包围盒
    ///   （与节点缩放无关、设 text 后同步可得）贴着文字右缘摆，字符数变化也不会叠字；</para>
    /// <para>· 量不到包围盒（异常）就不显示图标——绝不乱摆一个错位图标。</para>
    /// </summary>
    void EnsureGoldIcon(DamageTextInstance inst)
    {
        if (inst == null || inst.go == null || inst.textMesh == null) return;
        if (inst.goldIconSprite == null)
        {
            // 与 BattleUI.Binding 顶栏 DraftDisplay 同一条加载路径（Resources 直连，真机可用）；
            // 不走 CommonIconSprites（它的 Resources 根目录没有 Art 前缀副本，真机会落空）
            inst.goldIconSprite = Resources.Load<Sprite>("Art/UI/Icons/Common/icon_draft_coin");
            if (inst.goldIconSprite == null)
                Debug.LogError("[DamageTextSystem] 抽奖币图标取不到：Art/UI/Icons/Common/icon_draft_coin —— 飘字只出数字");
        }
        if (inst.goldIconSprite == null)
            return;   // 图取不到就不带图，文字本身仍然可读

        if (inst.goldIcon == null)
        {
            var iconGo = new GameObject("GoldIcon");
            iconGo.transform.SetParent(inst.go.transform, false);
            var sr = iconGo.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            sr.sortingOrder = GameConfig.SORT_VFX + 6;   // 文字 +5、描边 +4，币图盖在最上
            inst.goldIcon = sr;
        }

        var mf = inst.textMesh.GetComponent<MeshFilter>();
        var mb = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds();
        if (mb.extents.x <= 0.0001f || mb.extents.y <= 0.0001f)
        {
            inst.goldIcon.enabled = false;
            return;
        }

        float textHeight = mb.extents.y * 2f;
        float iconHeight = textHeight * 0.85f;
        float spriteHeight = inst.goldIconSprite.bounds.size.y;
        if (spriteHeight <= 0.0001f)
        {
            inst.goldIcon.enabled = false;
            return;
        }

        float s = iconHeight / spriteHeight;
        float iconHalfWidth = inst.goldIconSprite.bounds.size.x * s * 0.5f;
        float gap = textHeight * 0.18f;

        inst.goldIcon.sprite = inst.goldIconSprite;
        inst.goldIcon.transform.localScale = Vector3.one * s;
        // mesh.bounds 是局部空间：max.x = 文字右缘、center.y = 文字视觉中线（与 TextMesh anchor 无关）
        inst.goldIcon.transform.localPosition =
            new Vector3(mb.max.x + gap + iconHalfWidth, mb.center.y, 0f);
        inst.goldIcon.enabled = true;
    }

    void GetStyle(TextKind kind, out Color color, out int fontSize, out float scale)
    {
        switch (kind)
        {
            case TextKind.OutCrit:
                color = critColor;
                fontSize = Mathf.RoundToInt(critFontSize * EnemyTextMul * GlobalSizeMul);
                scale = critScale * EnemyTextMul * GlobalSizeMul;
                break;
            case TextKind.InCrit:
                color = critColor;
                fontSize = Mathf.RoundToInt(critFontSize * 0.8f * GlobalSizeMul);
                scale = critScale * 0.8f * GlobalSizeMul;
                break;
            case TextKind.InNormal:
                color = inNormalColor;
                fontSize = Mathf.RoundToInt(inNormalFontSize * GlobalSizeMul);
                scale = inNormalScale * GlobalSizeMul;
                break;
            case TextKind.Heal:
                color = healColor;
                fontSize = Mathf.RoundToInt(healFontSize * GlobalSizeMul);
                scale = healScale * GlobalSizeMul;
                break;
            case TextKind.Dodge:
                color = dodgeColor;
                fontSize = Mathf.RoundToInt(28 * GlobalSizeMul);
                scale = 0.88f * GlobalSizeMul;
                break;
            case TextKind.Gold:
                color = goldColor;
                fontSize = Mathf.RoundToInt(30 * GlobalSizeMul);
                scale = 0.98f * GlobalSizeMul;
                break;
            default:
                color = outNormalColor;
                fontSize = Mathf.RoundToInt(outNormalFontSize * GlobalSizeMul);
                scale = outNormalScale * GlobalSizeMul;
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
            if (inst.goldIcon != null)
            {
                var ic = inst.goldIcon.color;
                ic.a = c.a;   // 淡出节奏与文字完全一致（c.a 已含 baseColor.a）
                inst.goldIcon.color = ic;
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
