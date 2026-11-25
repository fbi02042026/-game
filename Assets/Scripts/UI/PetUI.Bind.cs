using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>PetUI 节点绑定 + 点击注册。只读 prefab 节点，不改任何位置/尺寸/颜色。</summary>
public partial class PetUI
{
    // 列表
    Transform _petList;
    readonly List<Transform> _rowGos = new List<Transform>();
    readonly List<Image> _rowPortraits = new List<Image>();
    readonly List<Image> _rowBgs = new List<Image>();
    readonly List<GameObject> _rowLocks = new List<GameObject>();
    readonly List<GameObject> _rowSelects = new List<GameObject>();
    readonly List<Button> _rowBtns = new List<Button>();

    // 详情
    Transform _skinSlot, _skinPortrait, _skinLock, _skinName;
    Transform _nameText, _petArt;
    // 喂养表情粒子（主人 11-25 手工搭的 Stage/biaoqing，7 个 Emoji* 粒子组）
    Transform _biaoqing;
    ParticleSystem[] _biaoqingAll = System.Array.Empty<ParticleSystem>();
    readonly List<ParticleSystem[]> _biaoqingGroups = new List<ParticleSystem[]>();
    Transform _deployBtn, _deployLabel;
    Image _feedCostIcon;      // 喂养按钮上方的金币图标
    Text _feedCostText;       // 喂养消耗数字
    Transform _powerText, _hpText, _atkText, _defText;
    readonly Transform[] _stars = new Transform[4];
    Color _starLitColor = new Color(1f, 0.84f, 0.3f, 1f);   // 点亮色基准，Bind 时从 prefab Star 原色覆盖
    Image _skillIcon;
    Text _skillInfo, _skillTitle;
    // 好感（3 颗心 + 各自「空心」）
    readonly Image[] _heartFills = new Image[3];
    readonly Image[] _heartEmpties = new Image[3];
    Color _heartPrefabColor = Color.white;
    Color _emptyPrefabColor = Color.white;

    void BindAll()
    {
        _panel = FindDeep(transform, "Root/Panel");
        if (_panel == null) { Debug.LogError(TAG + "缺 Root/Panel（结构被改过？）"); return; }

        _closeBtn = GetButton(FindDeep(_panel, "CloseBtn"));
        _petList = FindDeep(_panel, "PetList");

        _skinSlot = FindDeep(_panel, "Stage/皮肤");
        _skinPortrait = FindDeep(_panel, "Stage/皮肤/Slot0/Portrait");
        _skinLock = FindDeep(_panel, "Stage/皮肤/LockIcon");
        _skinName = FindDeep(_panel, "Stage/皮肤/NameText");
        _nameText = FindDeep(_panel, "Stage/NamePlate/NameText");
        _petArt = FindDeep(_panel, "Stage/PetArt");
        _biaoqing = FindDeep(_panel, "Stage/biaoqing");
        BindBiaoqingFx();
        _deployBtn = FindDeep(_panel, "Stage/DeployBtn");
        _deployLabel = FindDeep(_panel, "Stage/DeployBtn/Label");
        // 喂养消耗：在 DeployBtn 上挂金币图标+数字（prefab 无此节点，运行时新建，不动现有节点）
        EnsureFeedCostBadge();

        _hpText = FindDeep(_panel, "InfoPanel/HpText");
        _atkText = FindDeep(_panel, "InfoPanel/AtkText");
        _defText = FindDeep(_panel, "InfoPanel/DefText");
        _powerText = FindDeep(_panel, "InfoPanel/PowerText");
        for (int i = 0; i < 4; i++) _stars[i] = FindDeep(_panel, "InfoPanel/Star" + i);
        var star0Img = GetImage(_stars[0]);
        if (star0Img != null) _starLitColor = star0Img.color;   // 点亮色 = prefab 原色

        _skillTitle = GetText(FindDeep(_panel, "SkillPanel/SkillTitle"));
        _skillInfo = GetText(FindDeep(_panel, "SkillPanel/SkillInfo"));
        _skillIcon = GetImage(FindDeep(_panel, "SkillPanel/SkillSlot0"));

        var favor = FindDeep(_panel, "Stage/好感进度");
        for (int i = 0; i < 3; i++)
        {
            var heart = FindDeep(favor, "爱心" + (i + 1));
            _heartFills[i] = GetImage(heart);
            _heartEmpties[i] = GetImage(FindDeep(heart, "空心"));
        }
        if (_heartFills[0] != null) _heartPrefabColor = _heartFills[0].color;
        if (_heartEmpties[0] != null) _emptyPrefabColor = _heartEmpties[0].color;

        // 缺节点一次性明确报错（不静默兜底）
        if (_petList == null) Debug.LogError(TAG + "缺 PetList");
        if (_nameText == null) Debug.LogError(TAG + "缺 Stage/NamePlate/NameText");
        if (_petArt == null) Debug.LogError(TAG + "缺 Stage/PetArt");
        if (_deployBtn == null) Debug.LogError(TAG + "缺 Stage/DeployBtn");
        if (_heartFills[0] == null || _heartEmpties[0] == null)
            Debug.LogError(TAG + "缺 好感进度/爱心1 或 空心（进度条无法显示，结构被改过？）");
    }

    void BindClicks()
    {
        if (_closeBtn != null) _closeBtn.onClick.AddListener(Close);
        // 皮肤槽 prefab 只有 Image 没 Button（2026-11-21 点击无反应根因）：运行时补 Button + targetGraphic，不动节点位置/尺寸
        var skinBtn = GetButton(_skinSlot);
        if (skinBtn == null && _skinSlot != null)
        {
            skinBtn = _skinSlot.gameObject.AddComponent<Button>();
            var skinImg = GetImage(_skinSlot);
            skinBtn.targetGraphic = skinImg;
            if (skinImg != null) skinImg.raycastTarget = true;
        }
        if (skinBtn != null) skinBtn.onClick.AddListener(OnSkinClicked);
        else Debug.LogError(TAG + "皮肤槽补 Button 失败（_skinSlot 为空），点击购买皮肤不可用");
        var deploy = GetButton(_deployBtn);
        if (deploy != null) deploy.onClick.AddListener(OnDeployClicked);
    }

    /// <summary>
    /// 绑定喂养表情粒子：biaoqing 下每个顶层子节点（Emoji*）算一组，组内含其 ExtraFX 子粒子。
    /// prefab 里 playOnAwake=1，不处理的话面板每次打开会 7 组齐喷——
    /// 运行时一律关 playOnAwake 并清空，只在喂养成功后随机播一组（2026-11-25 主人需求）。
    /// </summary>
    void BindBiaoqingFx()
    {
        if (_biaoqing == null) { Debug.LogError(TAG + "缺 Stage/biaoqing（喂养表情粒子不会播放）"); return; }
        for (int i = 0; i < _biaoqing.childCount; i++)
        {
            var ps = _biaoqing.GetChild(i).GetComponentsInChildren<ParticleSystem>(true);
            if (ps != null && ps.Length > 0) _biaoqingGroups.Add(ps);
        }
        _biaoqingAll = _biaoqing.GetComponentsInChildren<ParticleSystem>(true);
        StopAllBiaoqingFx();   // 关 playOnAwake + 清实例化瞬间的齐喷残留（同帧内清掉，看不见）
    }

    /// <summary>停掉并清空全部表情粒子（含子孙），连喂/重开面板不留残留。</summary>
    void StopAllBiaoqingFx()
    {
        for (int i = 0; i < _biaoqingAll.Length; i++)
        {
            var ps = _biaoqingAll[i];
            if (ps == null) continue;
            ps.playOnAwake = false;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    /// <summary>喂养成功后随机播一组表情粒子：先清上一组，再整组 Play（含 ExtraFX 子粒子）。</summary>
    void PlayRandomBiaoqingFx()
    {
        if (_biaoqingGroups.Count == 0) return;
        var group = _biaoqingGroups[UnityEngine.Random.Range(0, _biaoqingGroups.Count)];
        StopAllBiaoqingFx();
        for (int i = 0; i < group.Length; i++)
            if (group[i] != null) group[i].Play(true);
    }

    static Text T(Transform t) => GetText(t);
    static Image I(Transform t) => GetImage(t);
}
