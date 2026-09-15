using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 局内构筑 HUD：本局战力 + 已获技能（星级/冷却）+ 出战后佣兵。
/// 运行时建树（不依赖预制体），挂在 BattleUI 下；只读 <see cref="RunLoadout"/> 与 <see cref="SkillSystem"/>。
/// </summary>
public class RunSkillBarUI : MonoBehaviour
{
    public static RunSkillBarUI Instance { get; private set; }

    const float ChipW = 176f;
    const float ChipH = 40f;

    Text _powerText;
    Text _expText;
    Image _expFill;
    RectTransform _chipRoot;
    readonly List<Chip> _skillChips = new List<Chip>();
    readonly List<Chip> _mercChips = new List<Chip>();

    class Chip
    {
        public Image Bg;
        public Image CdFill;
        /// <summary>V6：本技能自己的充能条填充（技能能量改为每槽一条）。</summary>
        public Image EnergyFill;
        public Text Label;
        public string SkillId;
        public float Cooldown;
        public int Index;
    }

    /// <summary>释放优先级序号 ①②③④ —— 槽序就是自动释放的优先级。</summary>
    static string OrderMark(int index)
    {
        switch (index)
        {
            case 0: return "①";
            case 1: return "②";
            case 2: return "③";
            case 3: return "④";
            default: return (index + 1) + ".";
        }
    }

    public static RunSkillBarUI Ensure()
    {
        if (Instance != null) return Instance;
        Transform parent = BattleUI.Instance != null ? BattleUI.Instance.transform : null;
        if (parent == null)
        {
            var canvas = Object.FindObjectOfType<Canvas>();
            parent = canvas != null ? canvas.transform : null;
        }
        if (parent == null) return null;

        var go = new GameObject("RunSkillBarUI", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.AddComponent<RunSkillBarUI>();
    }

    public static void Refresh()
    {
        if (Instance == null) return;
        Instance.Rebuild();
    }

    void Awake()
    {
        Instance = this;
        Build();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Build()
    {
        var root = GetComponent<RectTransform>();
        root.anchorMin = new Vector2(0f, 0.5f);
        root.anchorMax = new Vector2(0f, 0.5f);
        root.pivot = new Vector2(0f, 0.5f);
        root.anchoredPosition = new Vector2(10f, 40f);
        root.sizeDelta = new Vector2(ChipW + 12f, 492f);

        var bg = MakeImage("Bg", root, new Color(0.10f, 0.09f, 0.13f, 0.72f));
        Stretch(bg.rectTransform);

        var title = MakeText("Title", root, "本局构筑", 22, new Color(0.98f, 0.88f, 0.62f));
        Anchor(title.rectTransform, 0.5f, 1f, 0.5f, 1f, 0.5f, 1f, 0f, -18f, ChipW, 28f);

        // 战力（等级系统已停用，只展示本局战力）
        _powerText = MakeText("Power", root, "战力 0", 24, new Color(1f, 0.82f, 0.36f));
        Anchor(_powerText.rectTransform, 0.5f, 1f, 0.5f, 1f, 0.5f, 1f, 0f, -48f, ChipW, 30f);

        // 经验条（满格 → 升级 → 弹抽卡）
        var expBg = MakeImage("ExpBg", root, new Color(0.06f, 0.06f, 0.09f, 0.95f));
        Anchor(expBg.rectTransform, 0.5f, 1f, 0.5f, 1f, 0.5f, 1f, 0f, -70f, ChipW - 16f, 9f);
        _expFill = MakeImage("ExpFill", expBg.transform, new Color(0.36f, 0.74f, 1f, 1f));
        _expFill.rectTransform.anchorMin = new Vector2(0f, 0f);
        _expFill.rectTransform.anchorMax = new Vector2(0f, 1f);
        _expFill.rectTransform.offsetMin = Vector2.zero;
        _expFill.rectTransform.offsetMax = Vector2.zero;

        _expText = MakeText("ExpText", root, "EXP 0/70 · 升级抽卡", 16, new Color(0.72f, 0.80f, 0.92f));
        Anchor(_expText.rectTransform, 0.5f, 1f, 0.5f, 1f, 0.5f, 1f, 0f, -86f, ChipW, 20f);

        _chipRoot = MakeRect("Chips", root);
        Anchor(_chipRoot, 0.5f, 1f, 0.5f, 1f, 0.5f, 1f, 0f, -112f, ChipW, 360f);
        var layout = _chipRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        gameObject.SetActive(false);
    }

    // ============================================================
    // 重建
    // ============================================================

    void Rebuild()
    {
        if (_chipRoot == null) return;
        // V6：教程局也要显示 —— 教程里有「拖拽调整释放顺序」这一步，看不到技能槽就没法学。
        if (!RunLoadout.IsActive)
        {
            gameObject.SetActive(false);
            return;
        }
        gameObject.SetActive(true);

        for (int i = _chipRoot.childCount - 1; i >= 0; i--)
            Destroy(_chipRoot.GetChild(i).gameObject);
        _skillChips.Clear();
        _mercChips.Clear();

        var job = PlayerJobDefs.GetSelected();
        var ids = RunLoadout.SkillIds();
        for (int i = 0; i < ids.Count; i++)
        {
            string id = ids[i];
            var def = PlayerSkillDefs.GetById(id);
            var active = RunDraftDirector.BuildRunSkill(id, job);
            int star = RunLoadout.StarOf(id);
            bool affinity = SkillDraftMeta.IsAffinity(id, job);
            var tag = SkillDraftMeta.Tag(id);

            string name = def != null ? def.displayName : id;
            // V6：序号即自动释放优先级（多个技能同时就绪时，① 先放）
            string label = $"{OrderMark(i)}{name} ★{star}";
            if (affinity) label += " ▲";

            var chip = BuildChip(
                label,
                SkillRarityUtil.Tint(SkillDraftMeta.Rarity(id)),
                SynergyTagUtil.Tint(tag),
                active != null ? active.cooldown : 0f);
            chip.SkillId = active != null ? active.skillId : id;
            chip.Cooldown = active != null ? active.cooldown : 0f;
            chip.Index = i;
            _skillChips.Add(chip);
        }

        var mercs = RunLoadout.Mercs();
        for (int i = 0; i < mercs.Count; i++)
        {
            var m = mercs[i];
            if (m == null) continue;
            string label = $"{m.displayName} Lv{m.level} ★{m.star}";
            var chip = BuildChip(label, new Color(0.42f, 0.72f, 0.94f), new Color(0.42f, 0.72f, 0.94f), 0f);
            _mercChips.Add(chip);
        }

        UpdateLevelAndPower();
    }

    Chip BuildChip(string label, Color rarityTint, Color tagTint, float cooldown)
    {
        var chipRoot = MakeImage("Chip", _chipRoot, new Color(0.18f, 0.17f, 0.22f, 1f));
        chipRoot.rectTransform.sizeDelta = new Vector2(ChipW, ChipH);
        var le = chipRoot.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = ChipW;
        le.preferredHeight = ChipH;

        var tagBar = MakeImage("TagBar", chipRoot.transform, tagTint);
        Anchor(tagBar.rectTransform, 0f, 0f, 0f, 1f, 0f, 0.5f, 0f, 0f, 5f, 0f);
        tagBar.rectTransform.offsetMin = new Vector2(0f, 0f);
        tagBar.rectTransform.offsetMax = new Vector2(5f, 0f);

        // 冷却遮罩：无 sprite 时 Image.filled 不生效，改用 anchor 宽度做填充
        var cd = MakeImage("Cd", chipRoot.transform, new Color(0f, 0f, 0f, 0.55f));
        cd.rectTransform.anchorMin = new Vector2(0f, 0f);
        cd.rectTransform.anchorMax = new Vector2(0f, 1f);
        cd.rectTransform.offsetMin = Vector2.zero;
        cd.rectTransform.offsetMax = Vector2.zero;
        cd.rectTransform.localScale = Vector3.one;

        var text = MakeText("Label", chipRoot.transform, label, 18, rarityTint);
        Stretch(text.rectTransform);
        text.alignment = TextAnchor.MiddleCenter;

        // V6：本技能自己的能量条（底部细条）。2026-09-15 改纯冷却制后默认隐藏，只在建的时候设一次。
        var energyBg = MakeImage("EnergyBg", chipRoot.transform, new Color(0.08f, 0.08f, 0.12f, 0.95f));
        energyBg.gameObject.SetActive(GameConfig.PLAYER_SKILL_USE_ENERGY);
        var ebRt = energyBg.rectTransform;
        ebRt.anchorMin = new Vector2(0f, 0f);
        ebRt.anchorMax = new Vector2(1f, 0f);
        ebRt.pivot = new Vector2(0f, 0f);
        ebRt.anchoredPosition = new Vector2(3f, 2f);
        ebRt.sizeDelta = new Vector2(-6f, 5f);

        var energyFill = MakeImage("EnergyFill", energyBg.transform, new Color(0.98f, 0.78f, 0.28f, 1f));
        Stretch(energyFill.rectTransform);

        return new Chip { Bg = chipRoot, CdFill = cd, EnergyFill = energyFill, Label = text, Cooldown = cooldown };
    }

    void UpdateLevelAndPower()
    {
        if (_powerText != null)
            _powerText.text = $"战力 {RunLoadout.TotalPower()}";

        // 等级/经验已移除：隐藏经验条与「升级抽卡」文案
        if (_expText != null)
        {
            _expText.gameObject.SetActive(false);
            _expText.text = "";
        }
        if (_expFill != null && _expFill.transform.parent != null)
            _expFill.transform.parent.gameObject.SetActive(false);
    }

    void Update()
    {
        if (_chipRoot == null || !RunLoadout.IsActive) return;

        // 冷却遮罩：ratio=1 全遮，冷却恢复时从左往右揭开
        var sys = SkillSystem.Instance;
        var bm = BattleManager.Instance;
        if (sys != null)
        {
            for (int i = 0; i < _skillChips.Count; i++)
            {
                var c = _skillChips[i];
                if (c.CdFill != null)
                {
                    float ratio = sys.GetPlayerSkillCooldownRatio(c.Index);
                    SetCdFill(c.CdFill.rectTransform, ratio);
                }
                // 纯冷却制下能量条已整体隐藏（PLAYER_SKILL_USE_ENERGY 置 true 可退回）
                if (GameConfig.PLAYER_SKILL_USE_ENERGY && c.EnergyFill != null && bm != null)
                {
                    float e = bm.GetPlayerSkillEnergy(c.Index);
                    SetCdFill(c.EnergyFill.rectTransform, e);
                }
            }
        }

        // 等级/经验/战力逐帧可能变（升级、抽卡），低频刷新文本
        _powerTimer += Time.unscaledDeltaTime;
        if (_powerTimer >= 0.25f)
        {
            _powerTimer = 0f;
            UpdateLevelAndPower();
        }
    }

    float _powerTimer;

    // ============================================================
    // UI 小工具
    // ============================================================

    static Image MakeImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static RectTransform MakeRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static Text MakeText(string name, Transform parent, string text, int size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;
        var f = GameFonts.GetChinese();
        if (f != null) t.font = f;
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>按比例设置冷却遮罩宽度（左对齐，ratio=1 覆盖整条）。</summary>
    static void SetCdFill(RectTransform rt, float ratio)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void Anchor(RectTransform rt, float aminX, float aminY, float amaxX, float amaxY,
        float px, float py, float x, float y, float w, float h)
    {
        rt.anchorMin = new Vector2(aminX, aminY);
        rt.anchorMax = new Vector2(amaxX, amaxY);
        rt.pivot = new Vector2(px, py);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }
}
