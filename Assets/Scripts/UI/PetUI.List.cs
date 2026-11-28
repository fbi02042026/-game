using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 宠物列表：本体+皮肤共 10 条，prefab 摆 5 行作模板，运行时克隆补足并套 ScrollRect 可滚动。
/// 克隆行严格复制模板结构（含位置/尺寸），只改头像图与锁/选中态——不动模板节点位置。
/// </summary>
public partial class PetUI
{
    int _selectedIndex = 0;
    ScrollRect _scroll;
    RectTransform _viewport, _content;
    float _rowStep;   // 行间距从 prefab 模板行现读（Slot1~Slot4 实测 130），不写死、不改模板行位置
    readonly List<GameObject> _rowDeployBadges = new List<GameObject>();   // 列表头像「上阵」红条角标（11-25 主人）

    void SetupList()
    {
        if (_petList == null) return;
        // 采集 prefab 现成的 5 行
        _rowGos.Clear(); _rowPortraits.Clear(); _rowBgs.Clear();
        _rowLocks.Clear(); _rowSelects.Clear(); _rowBtns.Clear(); _rowDeployBadges.Clear();
        for (int i = 0; i < _petList.childCount; i++)
        {
            var c = _petList.GetChild(i);
            if (c.name != "Slot" + i) continue;
            RegisterRow(c);
        }
        if (_rowGos.Count == 0) { Debug.LogError(TAG + "PetList 下无 Slot0.. 模板行"); return; }

        // 行距真源：模板 Slot2→Slot3 的 y 差（Slot0 是放大选中行、偏移不同，不能拿它算）
        _rowStep = 130f;
        if (_rowGos.Count >= 4)
        {
            var a = _rowGos[2] as RectTransform;
            var b = _rowGos[3] as RectTransform;
            if (a != null && b != null && Mathf.Abs(a.anchoredPosition.y - b.anchoredPosition.y) > 1f)
                _rowStep = Mathf.Abs(a.anchoredPosition.y - b.anchoredPosition.y);
        }

        // 主人 11-21 明确：左侧只显示 5 只本体，皮肤不单列（走右上角皮肤槽）。
        // prefab 已摆 5 行模板，正好够用——不克隆、不动任何模板行位置。
        EnsureScroll();
    }

    /// <summary>列表头像「上阵」红条角标：底图复用每日登录「限定字底」+ 金黄字，运行时建树不碰 prefab（11-25）。</summary>
    static GameObject EnsureDeployBadge(Transform portrait)
    {
        if (portrait == null) return null;
        var go = new GameObject("DeployBadge", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(portrait, false);
        var rt = go.GetComponent<RectTransform>();
        // 【2026-11-25 主人「上阵的字左右反了，放大10%放到图标右上角」】
        // 父节点 Portrait 自带 localScale.x=-1（头像朝向用），角标挂其下被连带镜像 → 字反。
        // ① 本地 (0,1) 锚点 = 视觉右上角，往内缩 6px；② 整体 ×1.1；③ 角标根 x=-1 反向抵消父镜像，字正立。
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(6f, -6f);
        rt.sizeDelta = new Vector2(64f * 1.1f, 34f * 1.1f);
        go.transform.localScale = new Vector3(-1f, 1f, 1f);
        var img = go.GetComponent<Image>();
        img.sprite = Resources.Load<Sprite>("UI/Art/每日登录/每日登录_0003_限定字底");
        img.color = Color.white;   // 底图自带红色，不叠 tint
        img.raycastTarget = false;

        var txtGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        txtGo.transform.SetParent(go.transform, false);
        var trt = txtGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        var txt = txtGo.GetComponent<Text>();
        txt.text = "上阵";
        txt.font = GameFonts.GetChinese();
        txt.fontSize = 20;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = new Color(1f, 0.9f, 0.5f, 1f);   // 金黄字（同「限定」角标）
        txt.raycastTarget = false;
        return go;
    }

    void RegisterRow(Transform row)
    {
        var portraitT = FindDeep(row, "Slot0/Portrait");
        var deployBadge = EnsureDeployBadge(portraitT);
        var bg = GetImage(row);
        var lockGo = FindDeep(row, "LockIcon")?.gameObject;
        var selGo = FindDeep(row, "选中")?.gameObject;
        var btn = GetButton(row) ?? row.gameObject.AddComponent<Button>();

        _rowGos.Add(row);
        _rowPortraits.Add(GetImage(portraitT));
        _rowBgs.Add(bg);
        _rowLocks.Add(lockGo);
        _rowSelects.Add(selGo);
        _rowBtns.Add(btn);
        _rowDeployBadges.Add(deployBadge);

        int idx = _rowBtns.Count - 1;
        // 【2026-11-28】每行挂「可解锁」红点（键 = pet.unlock.<bodyId>）；行顺序与 PetDefs.Bodies 一致。
        var bodies = PetDefs.Bodies;
        if (idx < bodies.Count && bodies[idx] != null)
            RedDot.Bind(row, RedDot.PetUnlockPrefix + bodies[idx].id);
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => Select(idx));

        if (selGo != null) selGo.SetActive(false);
    }

    void EnsureScroll()
    {
        // 已有 ScrollRect 直接用
        _scroll = _petList.GetComponent<ScrollRect>();
        if (_scroll != null)
        {
            _viewport = _scroll.viewport;
            _content = _scroll.content;
            return;
        }
        // 运行时补：PetList 作 Viewport 裁剪，新建 Content 装所有行（不移动任何行，只换父级）
        _viewport = _petList as RectTransform;
        if (_viewport == null) return;
        var mask = _viewport.gameObject.GetComponent<RectMask2D>() ?? _viewport.gameObject.AddComponent<RectMask2D>();

        var contentGo = new GameObject("Content", typeof(RectTransform));
        _content = contentGo.transform as RectTransform;
        _content.SetParent(_viewport, false);
        // Content 继承 PetList 的锚点/pivot，让所有行本地坐标不变
        _content.anchorMin = _viewport.anchorMin;
        _content.anchorMax = _viewport.anchorMax;
        _content.pivot = _viewport.pivot;
        _content.anchoredPosition = Vector2.zero;
        _content.sizeDelta = Vector2.zero;

        var rows = new List<Transform>(_rowGos);
        foreach (var r in rows) r.SetParent(_content, false);

        // Content 高度按模板首行到末行的实际跨度算（不写死倍率，保证滚到底）
        float minY = 0f, maxY = 0f;
        foreach (var r in rows)
        {
            var rtr = r as RectTransform;
            if (rtr == null) continue;
            minY = Mathf.Min(minY, rtr.anchoredPosition.y);
            maxY = Mathf.Max(maxY, rtr.anchoredPosition.y);
        }
        _content.sizeDelta = new Vector2(0f, (maxY - minY) + _rowStep * 2f);

        _scroll = _viewport.gameObject.AddComponent<ScrollRect>();
        _scroll.viewport = _viewport;
        _scroll.content = _content;
        _scroll.horizontal = false;
        _scroll.vertical = true;
        _scroll.movementType = ScrollRect.MovementType.Clamped;
    }

    void SelectDefault()
    {
        // 默认选中上阵的；没有则选第一只已解锁；再没有选第 0 条
        var save = SaveSystem.Instance?.Data;
        string deployed = save != null ? save.petDeployedId : "";
        // 上阵的可能是皮肤 id，先归一到本体
        var d = PetDefs.Get(deployed);
        if (d != null && PetDefs.IsSkin(d)) deployed = d.skinOf;
        int idx = 0;
        var bodies = PetDefs.Bodies;
        for (int i = 0; i < bodies.Count; i++)
            if (!string.IsNullOrEmpty(deployed) && bodies[i].id == deployed) { idx = i; break; }
        Select(idx);
    }

    void Select(int idx)
    {
        if (idx < 0 || idx >= _rowGos.Count) return;
        var save = SaveSystem.Instance?.Data;
        var clicked = idx < PetDefs.Bodies.Count ? PetDefs.Bodies[idx] : null;
        // 2026-11-25 主人拍板：未解锁宠物也允许被选中（原「只弹提示、不切选中」导致
        // DeployBtn 的解锁分支永远走不到，玩家金币够也解不了锁）。
        // 保留一次条件 toast（点图标提示解锁条件），但继续走选中/刷详情，按钮显示「解锁」。
        // 真源 IsPetUnlocked：测试全解锁开关打开时恒真，调试期照旧可选。
        if (clicked != null && save != null && !save.IsPetUnlocked(clicked.id))
            ShowUnlockHint(clicked, save);
        _selectedIndex = idx;
        for (int i = 0; i < _rowSelects.Count; i++)
            if (_rowSelects[i] != null) _rowSelects[i].SetActive(i == idx);
        RefreshDetail();
    }

    PetDefs.Def SelectedDef => _selectedIndex >= 0 && _selectedIndex < PetDefs.Bodies.Count
        ? PetDefs.Bodies[_selectedIndex] : null;

    void RefreshList()
    {
        var save = SaveSystem.Instance?.Data;
        var bodies = PetDefs.Bodies;
        // 上阵角标口径：左侧只列本体，上阵的是皮肤时归一到本体；真源 petDeployedId。
        var deployedDef = PetDefs.Get(save != null ? save.petDeployedId : null);
        string deployedBodyId = deployedDef == null ? null : (PetDefs.IsSkin(deployedDef) ? deployedDef.skinOf : deployedDef.id);
        for (int i = 0; i < _rowGos.Count && i < bodies.Count; i++)
        {
            var def = bodies[i];
            var spr = PetDefs.LoadAvatar(def.id);
            if (_rowPortraits[i] != null) _rowPortraits[i].sprite = spr;
            // 11-22 列表图标框底按稀有度上色，色值 = 详情名字色同一出口（RarityPalette）。
            // 只换底框 Image.color，不碰头像图/节点尺寸/锚点；未解锁宠物框同样按自身稀有度显示。
            if (_rowBgs[i] != null) _rowBgs[i].color = QualityNameColor(def.qualityStar);
            bool unlocked = save != null && save.IsPetUnlocked(def.id);
            if (_rowLocks[i] != null) _rowLocks[i].SetActive(!unlocked);
            // 主人 11-21：未获得/未解锁的头像显示暗色（置灰），已解锁恢复原色
            if (_rowPortraits[i] != null)
                _rowPortraits[i].color = unlocked ? Color.white : new Color(0.35f, 0.35f, 0.4f, 1f);
            if (_rowDeployBadges[i] != null)
                _rowDeployBadges[i].SetActive(!string.IsNullOrEmpty(deployedBodyId) && def.id == deployedBodyId);
        }
    }
}
