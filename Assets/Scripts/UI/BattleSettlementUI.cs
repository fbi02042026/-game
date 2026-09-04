using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗结算：通关 / 撤离 / 阵亡统一展示统计与奖励格。
/// 预制体：Resources/Prefabs/Battle/BattleSettlement
/// </summary>
public class BattleSettlementUI : MonoBehaviour
{
    public const string PrefabPath = "Prefabs/Battle/BattleSettlement";
    const int MaxRewardCells = 16;
    const string ArtRoot = "Assets/Art/UI/战斗结算/";

    public static BattleSettlementUI Instance { get; private set; }

    public GameObject root;
    public Image panel;
    public Text titleText;
    public Text subtitleText;
    public Image portraitImage;
    public Text nameText;
    public Text valueDamage;
    public Text valueKill;
    public Text valueCrit;
    public Text valueCombo;
    public Text valueTaken;
    public Text valueHeal;
    public Transform rewardsGrid;
    public Button confirmButton;
    public Text confirmLabel;
    public GameObject rewardCellPrefab;

    readonly List<GameObject> _spawnedCells = new List<GameObject>();
    Action _onConfirm;
    BattleRunStats _stats;

    public static void Show(BattleRunStats stats, Action onConfirm)
    {
        Ensure().Open(stats, onConfirm);
    }

    public static BattleSettlementUI Ensure()
    {
        if (Instance != null) return Instance;
        var prefab = Resources.Load<GameObject>(PrefabPath);
        GameObject go;
        if (prefab != null)
        {
            go = Instantiate(prefab);
            go.name = "BattleSettlement";
        }
        else
        {
            Debug.LogWarning($"[BattleSettlement] 未找到 {PrefabPath}，临时代码搭壳");
            go = new GameObject("BattleSettlement", typeof(RectTransform));
            BuildHierarchy(go);
        }
        DontDestroyOnLoad(go);
        return go.GetComponent<BattleSettlementUI>() ?? go.AddComponent<BattleSettlementUI>();
    }

    void Awake()
    {
        Instance = this;
        BindRefs();
        Wire();
        if (root != null) root.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void BindRefs()
    {
        if (root == null)
            root = transform.Find("Root")?.gameObject ?? gameObject;
        if (panel == null)
            panel = FindDeep(root.transform, "Panel")?.GetComponent<Image>();
        if (titleText == null)
            titleText = FindDeep(root.transform, "Title")?.GetComponent<Text>();
        if (subtitleText == null)
            subtitleText = FindDeep(root.transform, "Subtitle")?.GetComponent<Text>();
        if (portraitImage == null)
            portraitImage = ResolvePortraitImage(root.transform);
        if (nameText == null)
            nameText = FindDeep(root.transform, "NameText")?.GetComponent<Text>();
        if (valueDamage == null)
            valueDamage = FindDeep(root.transform, "StatRow_Damage")?.Find("Value")?.GetComponent<Text>()
                ?? FindDeep(root.transform, "Value_Damage")?.GetComponent<Text>();
        if (valueKill == null)
            valueKill = FindDeep(root.transform, "StatRow_Kill")?.Find("Value")?.GetComponent<Text>()
                ?? FindDeep(root.transform, "Value_Kill")?.GetComponent<Text>();
        if (valueCrit == null)
            valueCrit = FindDeep(root.transform, "StatRow_Crit")?.Find("Value")?.GetComponent<Text>()
                ?? FindDeep(root.transform, "Value_Crit")?.GetComponent<Text>();
        if (valueCombo == null)
            valueCombo = FindDeep(root.transform, "StatRow_Combo")?.Find("Value")?.GetComponent<Text>()
                ?? FindDeep(root.transform, "Value_Combo")?.GetComponent<Text>();
        if (valueTaken == null)
            valueTaken = FindDeep(root.transform, "StatRow_Taken")?.Find("Value")?.GetComponent<Text>()
                ?? FindDeep(root.transform, "Value_Taken")?.GetComponent<Text>();
        if (valueHeal == null)
            valueHeal = FindDeep(root.transform, "StatRow_Heal")?.Find("Value")?.GetComponent<Text>()
                ?? FindDeep(root.transform, "Value_Heal")?.GetComponent<Text>();
        if (rewardsGrid == null)
            rewardsGrid = FindDeep(root.transform, "RewardsGrid");
        if (confirmButton == null)
            confirmButton = FindDeep(root.transform, "ConfirmButton")?.GetComponent<Button>();
        if (confirmLabel == null && confirmButton != null)
            confirmLabel = confirmButton.GetComponentInChildren<Text>(true);
        if (rewardCellPrefab == null)
        {
            var tmpl = FindDeep(root.transform, "RewardCellTemplate");
            if (tmpl != null) rewardCellPrefab = tmpl.gameObject;
        }
    }

    void Wire()
    {
        if (confirmButton == null) return;
        confirmButton.onClick.RemoveAllListeners();
        confirmButton.onClick.AddListener(OnConfirm);
    }

    void Open(BattleRunStats stats, Action onConfirm)
    {
        BindRefs();
        Wire();
        _stats = stats ?? new BattleRunStats();
        _onConfirm = onConfirm;
        Time.timeScale = 0f;

        if (titleText != null)
            titleText.text = "战斗结算";

        if (subtitleText != null)
        {
            string kind = _stats.IsDeath ? "阵亡" : (_stats.IsVictory ? "通关" : "撤离成功");
            subtitleText.text = string.IsNullOrEmpty(_stats.StageTitle)
                ? kind
                : _stats.StageTitle + " · " + kind;
        }

        ApplyPortraitAndName();
        ApplyStatValues();
        PopulateRewards();
        ShowMvpToast();

        if (confirmLabel != null)
            confirmLabel.text = "确定";

        var canvas = GetComponent<Canvas>();
        if (canvas != null)
            UICanvasSetup.RefreshPopup(canvas, GameConfig.UiSort.BattleSettlement);

        FitPanelUniformScale();

        if (root != null)
        {
            root.SetActive(true);
            transform.SetAsLastSibling();
        }
        GameFonts.ApplyToHierarchy(transform);
    }

    const float PanelFitPadding = 40f;

    /// <summary>
    /// 按设计分辨率整体等比缩放 Panel，只改 localScale，不改预制体布局坐标。
    /// </summary>
    void FitPanelUniformScale()
    {
        if (panel == null && root != null)
            panel = FindDeep(root.transform, "Panel")?.GetComponent<Image>();
        if (panel == null) return;

        var rt = panel.rectTransform;
        if (rt == null) return;

        float designW = Mathf.Max(1f, GameConfig.DESIGN_WIDTH);
        float designH = Mathf.Max(1f, GameConfig.DESIGN_HEIGHT);
        float pad = PanelFitPadding * 2f;
        float maxW = Mathf.Max(1f, designW - pad);
        float maxH = Mathf.Max(1f, designH - pad);

        // 以预制体设计尺寸为准（scale=1 时的 sizeDelta / rect）
        Vector2 designSize = rt.sizeDelta;
        if (designSize.x < 1f || designSize.y < 1f)
        {
            var r = rt.rect;
            designSize = new Vector2(Mathf.Abs(r.width), Mathf.Abs(r.height));
        }
        if (designSize.x < 1f || designSize.y < 1f) return;

        float sx = maxW / designSize.x;
        float sy = maxH / designSize.y;
        float s = Mathf.Min(1f, Mathf.Min(sx, sy));
        if (s < 0.05f) s = 0.05f;
        rt.localScale = new Vector3(s, s, 1f);
    }

    /// <summary>
    /// PortraitHost 可直接挂 Image，或 Host 作 Mask、子节点才是立绘图。
    /// 不关 Mask；优先绑名为 Portrait / PortraitImage 的子 Image。
    /// </summary>
    static Image ResolvePortraitImage(Transform root)
    {
        var host = FindDeep(root, "PortraitHost");
        if (host == null) return null;

        Transform named = host.Find("Portrait") ?? host.Find("PortraitImage");
        if (named != null)
        {
            var namedImg = named.GetComponent<Image>();
            if (namedImg != null) return namedImg;
        }

        for (int i = 0; i < host.childCount; i++)
        {
            var child = host.GetChild(i);
            if (child == null) continue;
            var img = child.GetComponent<Image>();
            if (img != null) return img;
        }

        return host.GetComponent<Image>();
    }

    static readonly string[] PlayerMvpLines =
    {
        "本场最佳：还是我。",
        "本场最佳：裂缝里就认这个。",
        "本场最佳：记一笔，回酒馆吹。"
    };

    void ShowMvpToast()
    {
        if (_stats == null) return;
        string mvpKey = string.IsNullOrEmpty(_stats.MvpKey)
            ? BattleRunStats.PlayerMvpKey
            : _stats.MvpKey;
        string line;
        if (mvpKey == BattleRunStats.PlayerMvpKey)
        {
            int idx = Mathf.Abs(_stats.Chapter) % PlayerMvpLines.Length;
            line = PlayerMvpLines[idx];
        }
        else
            line = MercLineTable.Pick(mvpKey, MercLineTable.Scene.Mvp);

        if (string.IsNullOrEmpty(line)) return;
        if (UIManager.Instance != null)
            UIManager.Instance.ShowToast(line);
        else
            GlobalToastUI.Show(line);
    }

    void ApplyPortraitAndName()
    {
        if (_stats == null) return;
        string display = !string.IsNullOrEmpty(_stats.MvpDisplayName)
            ? _stats.MvpDisplayName
            : "冒险者";
        int ch = _stats.Chapter > 0 ? _stats.Chapter : 1;
        if (nameText != null)
            nameText.text = $"第{ch}章 · {display}";

        if (portraitImage == null)
            portraitImage = ResolvePortraitImage(root != null ? root.transform : transform);
        if (portraitImage == null) return;

        Sprite sp = null;
        string mvpKey = string.IsNullOrEmpty(_stats.MvpKey)
            ? BattleRunStats.PlayerMvpKey
            : _stats.MvpKey;
        if (mvpKey == BattleRunStats.PlayerMvpKey)
            sp = StoryPortraits.Get(StoryPortraits.Player);
        else
            sp = MercPortraitSprites.GetStand(mvpKey);

        if (sp == null)
            sp = StoryPortraits.Get(StoryPortraits.Player);

        if (sp != null)
        {
            portraitImage.sprite = sp;
            portraitImage.color = Color.white;
            portraitImage.preserveAspect = true;
            portraitImage.enabled = true;
            portraitImage.gameObject.SetActive(true);
        }
        else
            portraitImage.enabled = false;
    }

    void ApplyStatValues()
    {
        SetValue(valueDamage, FormatInt(_stats.DamageDealt));
        SetValue(valueKill, _stats.KillCount.ToString());
        SetValue(valueCrit, _stats.CritCount.ToString());
        SetValue(valueCombo, _stats.MaxKillCombo.ToString());
        SetValue(valueTaken, FormatInt(_stats.DamageTaken));
        SetValue(valueHeal, FormatInt(_stats.HealingReceived));
    }

    static string FormatInt(float v) => Mathf.RoundToInt(v).ToString("N0");

    static void SetValue(Text t, string v)
    {
        if (t != null) t.text = v ?? "0";
    }

    void PopulateRewards()
    {
        ClearSpawnedCells();
        if (rewardsGrid == null) return;

        var cells = BuildRewardCells(_stats);
        int n = Mathf.Min(cells.Count, MaxRewardCells);
        for (int i = 0; i < n; i++)
            SpawnRewardCell(cells[i]);
        if (cells.Count > MaxRewardCells)
            SpawnRewardCell(new SettlementRewardCell
            {
                label = "更多",
                count = cells.Count - MaxRewardCells,
                frameColor = new Color(0.4f, 0.35f, 0.45f, 1f)
            });
    }

    static List<SettlementRewardCell> BuildRewardCells(BattleRunStats s)
    {
        var list = new List<SettlementRewardCell>();
        if (s == null) return list;

        if (!s.IsDeath && s.GoldGained > 0)
            list.Add(MakeCell("金币", (int)s.GoldGained, LoadUiIcon("gold"), new Color(0.85f, 0.7f, 0.25f)));
        if (s.DiamondGained > 0)
            list.Add(MakeCell("钻石", s.DiamondGained, LoadUiIcon("diamond"), new Color(0.35f, 0.55f, 0.9f)));
        if (s.TalentGained > 0)
            list.Add(MakeCell("天赋石", s.TalentGained, LoadUiIcon("talent"), new Color(0.55f, 0.4f, 0.75f)));
        if (s.EnchantStoneDelta > 0)
            list.Add(MakeCell("附魔石", s.EnchantStoneDelta, LoadUiIcon("enchant"), new Color(0.45f, 0.55f, 0.85f)));
        if (s.DecomposeMatDelta > 0)
            list.Add(MakeCell("强化材料", s.DecomposeMatDelta, LoadUiIcon("mat"), new Color(0.5f, 0.55f, 0.45f)));

        if (GridBackpackSystem.Instance != null)
        {
            var equips = GridBackpackSystem.Instance.GetAllItemsForLegacy();
            for (int i = 0; i < equips.Count; i++)
            {
                var eq = equips[i];
                if (eq == null) continue;
                Sprite icon = eq.icon;
                if (icon == null && eq.template != null)
                    icon = EquipIcons.Get(eq.template.iconFileName);
                list.Add(new SettlementRewardCell
                {
                    label = string.IsNullOrEmpty(eq.equipName) ? "装备" : eq.equipName,
                    count = 1,
                    icon = icon,
                    frameColor = RarityFrameColor(eq.rarity)
                });
            }
        }
        else if (s.EquipCount > 0)
        {
            list.Add(MakeCell("装备", s.EquipCount, null, new Color(0.55f, 0.45f, 0.25f)));
        }

        return list;
    }

    static SettlementRewardCell MakeCell(string label, int count, Sprite icon, Color frame)
    {
        return new SettlementRewardCell
        {
            label = label,
            count = count,
            icon = icon,
            frameColor = frame
        };
    }

    static Color RarityFrameColor(Rarity r)
    {
        switch (r)
        {
            case Rarity.Legendary: return new Color(0.85f, 0.55f, 0.15f);
            case Rarity.Epic: return new Color(0.65f, 0.35f, 0.85f);
            case Rarity.Rare: return new Color(0.3f, 0.5f, 0.9f);
            case Rarity.Uncommon: return new Color(0.3f, 0.7f, 0.35f);
            default: return new Color(0.55f, 0.55f, 0.55f);
        }
    }

    static Sprite LoadUiIcon(string key)
    {
        // 可选 Resources；没有就空图标
        return Resources.Load<Sprite>("UI/Settlement/" + key);
    }

    void SpawnRewardCell(SettlementRewardCell data)
    {
        GameObject go;
        if (rewardCellPrefab != null)
        {
            go = Instantiate(rewardCellPrefab, rewardsGrid, false);
            go.name = "RewardCell";
            go.SetActive(true);
        }
        else
        {
            go = CreateRewardCellRuntime(rewardsGrid, data);
        }
        _spawnedCells.Add(go);

        var frame = go.GetComponent<Image>();
        if (frame != null) frame.color = data.frameColor;

        var iconTf = FindDeep(go.transform, "Icon");
        var icon = iconTf != null ? iconTf.GetComponent<Image>() : null;
        if (icon != null)
        {
            if (data.icon != null)
            {
                icon.sprite = data.icon;
                icon.color = Color.white;
                icon.enabled = true;
            }
            else
            {
                icon.color = new Color(0.3f, 0.28f, 0.35f, 1f);
            }
            icon.preserveAspect = true;
        }

        var countTf = FindDeep(go.transform, "Count");
        var count = countTf != null ? countTf.GetComponent<Text>() : null;
        if (count != null)
            count.text = data.count > 1 ? data.count.ToString() : "";
    }

    void ClearSpawnedCells()
    {
        for (int i = 0; i < _spawnedCells.Count; i++)
        {
            if (_spawnedCells[i] != null)
                Destroy(_spawnedCells[i]);
        }
        _spawnedCells.Clear();
    }

    void OnConfirm()
    {
        ClearSpawnedCells();
        if (root != null) root.SetActive(false);
        Time.timeScale = 1f;
        var cb = _onConfirm;
        _onConfirm = null;
        cb?.Invoke();
    }

    public static void BuildHierarchy(GameObject host)
    {
        var canvas = host.GetComponent<Canvas>() ?? host.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.BattleSettlement);
        if (host.GetComponent<GraphicRaycaster>() == null)
            host.AddComponent<GraphicRaycaster>();
        var ui = host.GetComponent<BattleSettlementUI>() ?? host.AddComponent<BattleSettlementUI>();

        var root = Mk(host.transform, "Root");
        Stretch(root);

        var dim = Mk(root, "Dim");
        Stretch(dim);
        dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

        var panel = Mk(root, "Panel");
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(680f, 1100f);
        var panelImg = panel.gameObject.AddComponent<Image>();
        panelImg.color = new Color(0.1f, 0.08f, 0.12f, 0.98f);
        var bgSp = LoadSettlementArt("bg");
        if (bgSp != null)
        {
            panelImg.sprite = bgSp;
            panelImg.color = Color.white;
            panelImg.type = Image.Type.Sliced;
        }

        Label(panel, "Title", "战斗结算", 34, new Vector2(0f, -28f), new Vector2(400f, 48f));
        Label(panel, "Subtitle", "通关", 20, new Vector2(0f, -72f), new Vector2(500f, 32f));

        var portrait = Mk(panel, "PortraitHost");
        portrait.anchorMin = portrait.anchorMax = new Vector2(0f, 1f);
        portrait.pivot = new Vector2(0f, 1f);
        portrait.anchoredPosition = new Vector2(28f, -110f);
        portrait.sizeDelta = new Vector2(240f, 420f);
        var pImg = portrait.gameObject.AddComponent<Image>();
        pImg.color = new Color(0.2f, 0.18f, 0.24f, 1f);
        pImg.preserveAspect = true;

        var namePlate = Mk(panel, "NamePlate");
        namePlate.anchorMin = namePlate.anchorMax = new Vector2(0f, 1f);
        namePlate.pivot = new Vector2(0f, 1f);
        namePlate.anchoredPosition = new Vector2(28f, -540f);
        namePlate.sizeDelta = new Vector2(240f, 40f);
        namePlate.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.1f, 0.14f, 0.9f);
        Label(namePlate, "NameText", "Lv.1 冒险者", 22, Vector2.zero, new Vector2(240f, 40f), TextAnchor.MiddleCenter, true);

        var stats = Mk(panel, "StatsPanel");
        stats.anchorMin = stats.anchorMax = new Vector2(1f, 1f);
        stats.pivot = new Vector2(1f, 1f);
        stats.anchoredPosition = new Vector2(-24f, -110f);
        stats.sizeDelta = new Vector2(360f, 420f);

        Label(stats, "StatsHeader", "战斗统计", 24, new Vector2(0f, 0f), new Vector2(340f, 36f));

        float y = -44f;
        CreateStatRow(stats, "StatRow_Damage", "总伤害", "总伤害", ref y);
        CreateStatRow(stats, "StatRow_Kill", "击杀敌人", "击杀敌人", ref y);
        CreateStatRow(stats, "StatRow_Crit", "暴击次数", "暴击次数", ref y);
        CreateStatRow(stats, "StatRow_Combo", "最大连击", "最大连击数", ref y);
        CreateStatRow(stats, "StatRow_Taken", "承受伤害", "承受伤害", ref y);
        CreateStatRow(stats, "StatRow_Heal", "受到治疗", "受到治疗", ref y);

        Label(panel, "RewardsHeader", "获得奖励", 24, new Vector2(0f, -560f), new Vector2(600f, 36f));

        var grid = Mk(panel, "RewardsGrid");
        grid.anchorMin = new Vector2(0.5f, 0f);
        grid.anchorMax = new Vector2(0.5f, 0f);
        grid.pivot = new Vector2(0.5f, 0f);
        grid.anchoredPosition = new Vector2(0f, 120f);
        grid.sizeDelta = new Vector2(620f, 280f);
        var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
        gl.cellSize = new Vector2(88f, 88f);
        gl.spacing = new Vector2(10f, 10f);
        gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gl.constraintCount = 6;
        gl.childAlignment = TextAnchor.UpperCenter;

        var tmpl = CreateRewardCellRuntime(grid, new SettlementRewardCell { count = 0 });
        tmpl.name = "RewardCellTemplate";
        tmpl.SetActive(false);
        ui.rewardCellPrefab = tmpl;

        var btn = Mk(panel, "ConfirmButton");
        btn.anchorMin = btn.anchorMax = new Vector2(0.5f, 0f);
        btn.pivot = new Vector2(0.5f, 0f);
        btn.anchoredPosition = new Vector2(0f, 28f);
        btn.sizeDelta = new Vector2(320f, 72f);
        btn.gameObject.AddComponent<Image>().color = new Color(0.35f, 0.22f, 0.48f, 1f);
        btn.gameObject.AddComponent<Button>().transition = Selectable.Transition.ColorTint;
        Label(btn, "Label", "确定", 28, Vector2.zero, new Vector2(320f, 72f), TextAnchor.MiddleCenter, true);

        GameFonts.ApplyToHierarchy(host.transform);
    }

    static void CreateStatRow(Transform parent, string rowName, string label, string iconFile, ref float y)
    {
        var row = Mk(parent, rowName);
        row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f);
        row.pivot = new Vector2(0.5f, 1f);
        row.anchoredPosition = new Vector2(0f, y);
        row.sizeDelta = new Vector2(340f, 52f);

        var icon = Mk(row, "Icon");
        icon.anchorMin = icon.anchorMax = new Vector2(0f, 0.5f);
        icon.pivot = new Vector2(0f, 0.5f);
        icon.anchoredPosition = new Vector2(4f, 0f);
        icon.sizeDelta = new Vector2(40f, 40f);
        var iconImg = icon.gameObject.AddComponent<Image>();
        iconImg.preserveAspect = true;
        var sp = LoadSettlementArt(iconFile);
        if (sp != null)
        {
            iconImg.sprite = sp;
            iconImg.color = Color.white;
        }
        else
            iconImg.color = new Color(0.45f, 0.35f, 0.55f, 1f);

        Label(row, "Label", label, 22, new Vector2(-20f, 0f), new Vector2(160f, 40f), TextAnchor.MiddleLeft);
        var labelRt = FindDeep(row, "Label") as RectTransform;
        if (labelRt != null)
        {
            labelRt.anchorMin = labelRt.anchorMax = new Vector2(0f, 0.5f);
            labelRt.pivot = new Vector2(0f, 0.5f);
            labelRt.anchoredPosition = new Vector2(52f, 0f);
        }

        Label(row, "Value", "0", 24, new Vector2(0f, 0f), new Vector2(120f, 40f), TextAnchor.MiddleRight);
        var valueRt = FindDeep(row, "Value") as RectTransform;
        if (valueRt != null)
        {
            valueRt.anchorMin = valueRt.anchorMax = new Vector2(1f, 0.5f);
            valueRt.pivot = new Vector2(1f, 0.5f);
            valueRt.anchoredPosition = new Vector2(-8f, 0f);
            var vt = valueRt.GetComponent<Text>();
            if (vt != null) vt.font = GameFonts.GetNumber() ?? vt.font;
        }

        y -= 56f;
    }

    static GameObject CreateRewardCellRuntime(Transform parent, SettlementRewardCell data)
    {
        var cell = Mk(parent, "RewardCell");
        cell.sizeDelta = new Vector2(88f, 88f);
        var frame = cell.gameObject.AddComponent<Image>();
        frame.color = data != null ? data.frameColor : new Color(0.55f, 0.45f, 0.25f);

        var icon = Mk(cell, "Icon");
        Stretch(icon);
        icon.offsetMin = new Vector2(8f, 16f);
        icon.offsetMax = new Vector2(-8f, -8f);
        var iconImg = icon.gameObject.AddComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.color = new Color(0.25f, 0.22f, 0.3f, 1f);

        Label(cell, "Count", data != null && data.count > 0 ? data.count.ToString() : "", 18,
            Vector2.zero, new Vector2(80f, 24f), TextAnchor.LowerRight);
        var countRt = FindDeep(cell, "Count") as RectTransform;
        if (countRt != null)
        {
            countRt.anchorMin = countRt.anchorMax = new Vector2(1f, 0f);
            countRt.pivot = new Vector2(1f, 0f);
            countRt.anchoredPosition = new Vector2(-4f, 2f);
            countRt.sizeDelta = new Vector2(72f, 22f);
        }
        return cell.gameObject;
    }

    static Sprite LoadSettlementArt(string fileNameNoExt)
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + fileNameNoExt + ".png");
#else
        return Resources.Load<Sprite>("UI/Settlement/" + fileNameNoExt);
#endif
    }

    static RectTransform Mk(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static void Label(Transform parent, string name, string text, int size, Vector2 pos, Vector2 sizeDelta,
        TextAnchor align = TextAnchor.MiddleCenter, bool stretch = false)
    {
        var rt = Mk(parent, name);
        if (stretch)
        {
            Stretch(rt);
        }
        else
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;
        }
        var t = rt.gameObject.AddComponent<Text>();
        t.font = GameFonts.GetChinese();
        t.fontSize = size;
        t.color = new Color(0.95f, 0.85f, 0.55f, 1f);
        t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        t.text = text;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var f = FindDeep(root.GetChild(i), name);
            if (f != null) return f;
        }
        return null;
    }
}
