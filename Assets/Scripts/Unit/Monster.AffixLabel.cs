using UnityEngine;
using UnityEngine.Rendering;

// 怪物单位 partial —— 2026-11-05 新增
// 职责：表定词条怪（monster_stats.affix 列 → MonsterAffix.AttachFixed）头顶词条标签。
// 手法 1:1 复用 Monster.StackLabel.cs 的 TextMesh + 四向描边（挂 Body 防镜像反字、防负 scale）。
// 只对「表定词缀怪」（VisualTinted）显示；精英 / Boss 的掷骰词缀走屏幕 Boss 血条后缀，头顶不加。
public partial class Monster
{
    private Transform _affixLabelRoot;
    private TextMesh _affixLabel;
    private MeshRenderer _affixLabelRenderer;
    private TextMesh[] _affixOutlineLabels;
    private MeshRenderer[] _affixOutlineRenderers;

    /// <summary>词条字符大小（叠怪标签 charSize≈0.084，词条要一眼认出取 0.12；主人实测可调）。</summary>
    const float AffixLabelCharSize = 0.12f;
    /// <summary>词条标签在精灵顶再抬高 0.34，避开同位置的叠怪标签 ×N（叠怪标签是 top+0.12）。</summary>
    const float AffixLabelTopPad = 0.34f;
    /// <summary>点阵字体字号必须 12 的倍数（fusion-pixel 12px 硬规则，优先 24）。</summary>
    const int AffixLabelFontSize = 24;

    static readonly Vector2[] AffixOutlineDirs =
    {
        new Vector2(-1f, 0f), new Vector2(1f, 0f),
        new Vector2(0f, -1f), new Vector2(0f, 1f)
    };

    /// <summary>
    /// 【2026-11-05 主人拍板「词条怪的头顶加上词条」】Monster.Init 词缀挂接后调用：
    /// 表定词缀怪显示头顶词条名（如「铁壁」，多词条「铁壁·荆棘」），其余怪自动隐藏。
    /// 颜色用词缀 Tint（与染色/血条后缀同源），黑描边保证浅色怪图上看得清。
    /// </summary>
    public void RefreshAffixHeadLabel()
    {
        var affix = MonsterAffix.Get(this);
        bool show = affix != null && affix.VisualTinted && affix.Count > 0;
        if (!show)
        {
            HideAffixHeadLabel();
            return;
        }

        EnsureAffixHeadLabel();
        string label = affix.Suffix.TrimStart('·');
        var font = GameFonts.GetChinese();
        if (font != null)
        {
            // 点阵字体按需扩字符集（词缀名是动态中文，必须先请求再渲染，否则缺字）
            font.RequestCharactersInTexture(label, AffixLabelFontSize, FontStyle.Normal);
            _affixLabel.font = font;
            if (_affixLabelRenderer != null && font.material != null)
                _affixLabelRenderer.sharedMaterial = font.material;
            if (_affixOutlineLabels != null)
            {
                for (int i = 0; i < _affixOutlineLabels.Length; i++)
                {
                    var o = _affixOutlineLabels[i];
                    if (o == null) continue;
                    o.font = font;
                    if (_affixOutlineRenderers != null && i < _affixOutlineRenderers.Length
                        && _affixOutlineRenderers[i] != null && font.material != null)
                        _affixOutlineRenderers[i].sharedMaterial = font.material;
                }
            }
        }
        _affixLabel.text = label;
        _affixLabel.color = affix.FirstTint;   // 与染色 / 血条后缀同源的词缀色
        SyncAffixOutlineText(label);
        RefreshAffixLabelLayout();
        _affixLabelRoot.gameObject.SetActive(true);
    }

    public void HideAffixHeadLabel()
    {
        if (_affixLabelRoot != null) _affixLabelRoot.gameObject.SetActive(false);
    }

    void SyncAffixOutlineText(string label)
    {
        if (_affixOutlineLabels == null) return;
        for (int i = 0; i < _affixOutlineLabels.Length; i++)
        {
            if (_affixOutlineLabels[i] != null)
                _affixOutlineLabels[i].text = label;
        }
    }

    void RefreshAffixLabelLayout()
    {
        if (_affixLabelRoot == null || _affixLabel == null) return;

        Transform body = GetBodyTransform() != null ? GetBodyTransform() : transform;
        float labelY = 0.85f + AffixLabelTopPad;
        if (sr != null && sr.sprite != null)
        {
            var topLocal = body.InverseTransformPoint(sr.bounds.max);
            labelY = Mathf.Max(labelY, topLocal.y + 0.12f + AffixLabelTopPad);
        }

        float rootAbs = Mathf.Max(0.01f, Mathf.Abs(body.lossyScale.y));
        _affixLabelRoot.localPosition = new Vector3(0f, labelY / rootAbs, 0f);
        // 防父节点负 scale 反字（与叠怪标签同口径）
        float fixX = body.lossyScale.x < 0f ? -1f : 1f;
        _affixLabelRoot.localScale = new Vector3(fixX, 1f, 1f);
        float charSize = AffixLabelCharSize / rootAbs;
        _affixLabel.characterSize = charSize;

        if (_affixLabelRenderer != null)
        {
            _affixLabelRenderer.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            _affixLabelRenderer.sortingOrder = GameConfig.SORT_VFX + 32;
        }

        float outlineStep = charSize * 0.2f;
        if (_affixOutlineLabels == null) return;
        for (int i = 0; i < _affixOutlineLabels.Length; i++)
        {
            var o = _affixOutlineLabels[i];
            if (o == null) continue;
            o.characterSize = charSize;
            var dir = AffixOutlineDirs[i];
            o.transform.localPosition = new Vector3(dir.x * outlineStep, dir.y * outlineStep, 0.002f);
            if (_affixOutlineRenderers != null && i < _affixOutlineRenderers.Length
                && _affixOutlineRenderers[i] != null)
            {
                _affixOutlineRenderers[i].sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
                _affixOutlineRenderers[i].sortingOrder = GameConfig.SORT_VFX + 31;
            }
        }
    }

    void EnsureAffixHeadLabel()
    {
        if (_affixLabel != null && _affixOutlineLabels != null && _affixOutlineLabels.Length > 0)
            return;
        if (_affixLabelRoot != null)
            Destroy(_affixLabelRoot.gameObject);
        _affixLabel = null;
        _affixLabelRenderer = null;
        _affixOutlineLabels = null;
        _affixOutlineRenderers = null;

        _affixLabelRoot = new GameObject("AffixLabel").transform;
        // 挂 Body（不参与 Visual 镜像），避免朝左时词条反字（同叠怪标签口径）
        Transform labelParent = GetBodyTransform() != null ? GetBodyTransform() : transform;
        _affixLabelRoot.SetParent(labelParent, false);

        _affixOutlineLabels = new TextMesh[AffixOutlineDirs.Length];
        _affixOutlineRenderers = new MeshRenderer[AffixOutlineDirs.Length];
        for (int i = 0; i < AffixOutlineDirs.Length; i++)
        {
            var oGo = new GameObject("Outline" + i, typeof(TextMesh));
            oGo.transform.SetParent(_affixLabelRoot, false);
            var o = oGo.GetComponent<TextMesh>();
            o.text = "铁壁";
            o.fontSize = AffixLabelFontSize;
            o.anchor = TextAnchor.MiddleCenter;
            o.alignment = TextAlignment.Center;
            o.color = new Color(0f, 0f, 0f, 0.9f);
            o.richText = false;
            _affixOutlineLabels[i] = o;
            _affixOutlineRenderers[i] = oGo.GetComponent<MeshRenderer>();
        }

        var fillGo = new GameObject("Fill", typeof(TextMesh));
        fillGo.transform.SetParent(_affixLabelRoot, false);
        _affixLabel = fillGo.GetComponent<TextMesh>();
        _affixLabel.text = "铁壁";
        _affixLabel.fontSize = AffixLabelFontSize;
        _affixLabel.anchor = TextAnchor.MiddleCenter;
        _affixLabel.alignment = TextAlignment.Center;
        _affixLabel.color = Color.white;   // RefreshAffixHeadLabel 里按词缀 Tint 覆盖
        _affixLabel.richText = false;
        _affixLabelRenderer = fillGo.GetComponent<MeshRenderer>();
    }
}
