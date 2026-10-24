using UnityEngine;

/// <summary>
/// 头顶名牌：名牌刷新、字体/颜色、版面换算、低血预警、名牌跟随与回收。
/// 【2026-10-23 主人拍板】原 Mercenary.cs 已 1025 行（项目约定 >600 行必须拆），
/// 按职责切成 4 个 partial，本文件是其中之一。代码为原样搬运，逻辑未改。
/// </summary>
public partial class Mercenary : UnitBase
{
    void RefreshNameLabel()
    {
        if (string.IsNullOrEmpty(DisplayName))
        {
            HideNameLabel();
            return;
        }
        EnsureNameLabel();
        _nameLabelRoot.gameObject.SetActive(true);
        _nameLabel.text = DisplayName;
        ApplyNameLabelFont();
        if (_nameOutlineLabels != null)
        {
            for (int i = 0; i < _nameOutlineLabels.Length; i++)
            {
                if (_nameOutlineLabels[i] != null)
                    _nameOutlineLabels[i].text = DisplayName;
            }
        }
        RefreshNameLabelLayout();
    }

    void ApplyNameLabelFont()
    {
        if (_nameLabel == null) return;
        var font = GameFonts.GetChinese();
        _nameLabel.font = font;
        if (font != null)
        {
            font.RequestCharactersInTexture(DisplayName, _nameLabel.fontSize, _nameLabel.fontStyle);
            if (_nameLabelRenderer != null && font.material != null)
                _nameLabelRenderer.sharedMaterial = font.material;
            if (_nameOutlineLabels != null)
            {
                for (int i = 0; i < _nameOutlineLabels.Length; i++)
                {
                    if (_nameOutlineLabels[i] == null) continue;
                    _nameOutlineLabels[i].font = font;
                    if (_nameOutlineRenderers != null && i < _nameOutlineRenderers.Length
                        && _nameOutlineRenderers[i] != null && font.material != null)
                        _nameOutlineRenderers[i].sharedMaterial = font.material;
                }
            }
        }
        ApplyNameLabelColor();
    }

    void ApplyNameLabelColor()
    {
        if (_nameLabel == null) return;
        var rarity = RarityPalette.ResolveMercRarity(mercId);
        _nameLabel.color = RarityPalette.Get(rarity);
        if (_nameOutlineLabels == null) return;
        var outline = RarityPalette.Outline;
        for (int i = 0; i < _nameOutlineLabels.Length; i++)
        {
            if (_nameOutlineLabels[i] != null)
                _nameOutlineLabels[i].color = outline;
        }
    }

    void RefreshNameLabelLayout()
    {
        if (_nameLabelRoot == null || _nameLabel == null) return;
        float rootAbs = Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.y));
        float charSize = NameCharSizeBase / rootAbs;
        _nameLabel.characterSize = charSize;
        if (_nameLabelRenderer != null)
        {
            _nameLabelRenderer.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            _nameLabelRenderer.sortingOrder = GameConfig.SORT_VFX + 24;
        }
        float outlineStep = charSize * 0.22f;
        if (_nameOutlineLabels != null)
        {
            for (int i = 0; i < _nameOutlineLabels.Length; i++)
            {
                var o = _nameOutlineLabels[i];
                if (o == null) continue;
                o.characterSize = charSize;
                o.text = _nameLabel.text;
                var dir = NameOutlineDirs[i];
                o.transform.localPosition = new Vector3(dir.x * outlineStep, dir.y * outlineStep, 0.002f);
                if (_nameOutlineRenderers != null && i < _nameOutlineRenderers.Length && _nameOutlineRenderers[i] != null)
                {
                    _nameOutlineRenderers[i].sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
                    _nameOutlineRenderers[i].sortingOrder = GameConfig.SORT_VFX + 23;
                }
            }
        }
    }

    static Transform GetUnitLabelsRoot()
    {
        var bm = BattleManager.Instance;
        if (bm == null || bm.unitRoot == null) return null;
        Transform labels = bm.unitRoot.Find("UnitLabels");
        if (labels != null) return labels;
        var go = new GameObject("UnitLabels");
        labels = go.transform;
        labels.SetParent(bm.unitRoot, false);
        return labels;
    }

    Vector3 GetNameLabelWorldPos()
    {
        float yOff = 0.94f;
        return transform.position + new Vector3(0f, yOff, transform.position.z);
    }

    void LateUpdate()
    {
        SyncNameLabelTransform();
        TickLowHpWarn();
    }

    void TickLowHpWarn()
    {
        float maxHp = attr != null ? attr.GetAttr(AttrType.MaxHp) : 0f;
        float ratio = maxHp > 0.01f ? currentHp / maxHp : 1f;
        bool dead = isDead || currentHp <= 0f;
        if (unitAnim != null)
            unitAnim.TickLowHpFlash(ratio, dead);
    }

    void SyncNameLabelTransform()
    {
        if (_nameLabelRoot == null || !_nameLabelRoot.gameObject.activeSelf) return;
        _nameLabelRoot.position = GetNameLabelWorldPos();
        _nameLabelRoot.localScale = new Vector3(NameScaleMul, NameScaleMul, NameScaleMul);
    }

    void EnsureNameLabel()
    {
        if (_nameLabel != null) return;

        Transform parent = GetUnitLabelsRoot() ?? transform;
        _nameLabelRoot = new GameObject("MercName").transform;
        _nameLabelRoot.SetParent(parent, false);

        _nameOutlineLabels = new TextMesh[NameOutlineDirs.Length];
        _nameOutlineRenderers = new MeshRenderer[NameOutlineDirs.Length];
        for (int i = 0; i < NameOutlineDirs.Length; i++)
        {
            var oGo = new GameObject("Outline" + i, typeof(TextMesh));
            oGo.transform.SetParent(_nameLabelRoot, false);
            var o = oGo.GetComponent<TextMesh>();
            o.text = DisplayName ?? "";
            o.fontSize = 22;
            o.anchor = TextAnchor.MiddleCenter;
            o.alignment = TextAlignment.Center;
            o.fontStyle = FontStyle.Bold;
            o.color = RarityPalette.Outline;
            _nameOutlineLabels[i] = o;
            _nameOutlineRenderers[i] = oGo.GetComponent<MeshRenderer>();
        }

        var fillGo = new GameObject("Fill", typeof(TextMesh));
        fillGo.transform.SetParent(_nameLabelRoot, false);
        _nameLabel = fillGo.GetComponent<TextMesh>();
        _nameLabel.text = DisplayName ?? "";
        _nameLabel.fontSize = 22;
        _nameLabel.anchor = TextAnchor.MiddleCenter;
        _nameLabel.alignment = TextAlignment.Center;
        _nameLabel.fontStyle = FontStyle.Bold;

        _nameLabelRenderer = fillGo.GetComponent<MeshRenderer>();
        if (_nameLabelRenderer != null)
            _nameLabelRenderer.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;

        ApplyNameLabelFont();
        RefreshNameLabelLayout();
    }

    void HideNameLabel()
    {
        if (_nameLabelRoot != null)
            Destroy(_nameLabelRoot.gameObject);
        _nameLabelRoot = null;
        _nameLabel = null;
        _nameLabelRenderer = null;
        _nameOutlineLabels = null;
        _nameOutlineRenderers = null;
    }

    /// <summary>近战前排角色（剑盾/狂战/重武等）；弓手法师牧师为远程后排。</summary>
}
