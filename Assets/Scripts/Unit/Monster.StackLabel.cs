using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

// 怪物单位 partial —— 2026-10-17 由 Monster.cs 纯位移拆出（无逻辑改动）
// 职责：叠怪数量标签（×N）生成、排版、描边
public partial class Monster
{
    private Transform _stackLabelRoot;
    private TextMesh _stackLabel;
    private MeshRenderer _stackLabelRenderer;
    private TextMesh[] _stackOutlineLabels;
    private MeshRenderer[] _stackOutlineRenderers;

    const float StackLabelCharSizeBase = 0.28f;
    /// <summary>????????? 70%????30%???/summary>
    const float StackLabelSizeScale = 0.3f;
    static readonly Vector2[] StackOutlineDirs =
    {
        new Vector2(-1f, 0f), new Vector2(1f, 0f),
        new Vector2(0f, -1f), new Vector2(0f, 1f)
    };

    public void SetOverlapStackCount(int count)
    {
        if (!GameConfig.SHOW_MONSTER_STACK_LABEL || count <= 1)
        {
            if (_stackLabelRoot != null) _stackLabelRoot.gameObject.SetActive(false);
            return;
        }

        EnsureStackLabel();
        ApplyStackLabelFont();
        RefreshStackLabelLayout();
        _stackLabelRoot.gameObject.SetActive(true);
        string label = "x" + count;
        _stackLabel.text = label;
        SyncStackOutlineText(label);
    }

    void SyncStackOutlineText(string label)
    {
        if (_stackOutlineLabels == null) return;
        for (int i = 0; i < _stackOutlineLabels.Length; i++)
        {
            if (_stackOutlineLabels[i] != null)
                _stackOutlineLabels[i].text = label;
        }
    }

    void ApplyStackLabelFont()
    {
        if (_stackLabel == null) return;
        var font = GameFonts.GetNumber();
        if (font == null) return;
        font.RequestCharactersInTexture("x0123456789", _stackLabel.fontSize, FontStyle.Normal);

        _stackLabel.font = font;
        if (_stackLabelRenderer != null && font.material != null)
            _stackLabelRenderer.sharedMaterial = font.material;

        if (_stackOutlineLabels == null) return;
        for (int i = 0; i < _stackOutlineLabels.Length; i++)
        {
            var o = _stackOutlineLabels[i];
            if (o == null) continue;
            o.font = font;
            if (_stackOutlineRenderers != null && i < _stackOutlineRenderers.Length
                && _stackOutlineRenderers[i] != null && font.material != null)
                _stackOutlineRenderers[i].sharedMaterial = font.material;
        }
    }

    void RefreshStackLabelLayout()
    {
        if (_stackLabelRoot == null || _stackLabel == null) return;

        Transform body = GetBodyTransform() != null ? GetBodyTransform() : transform;
        float labelY = 0.85f;
        if (sr != null && sr.sprite != null)
        {
            var topLocal = body.InverseTransformPoint(sr.bounds.max);
            labelY = Mathf.Max(labelY, topLocal.y + 0.12f);
        }
        else if (_worldHpBar != null)
        {
            labelY = body.InverseTransformPoint(MoveRoot.position).y + 0.14f;
        }

        float rootAbs = Mathf.Max(0.01f, Mathf.Abs(body.lossyScale.y));
        _stackLabelRoot.localPosition = new Vector3(0f, labelY / rootAbs, 0f);
        // 防父节点万一带负 scale 时仍反字
        float fixX = body.lossyScale.x < 0f ? -1f : 1f;
        _stackLabelRoot.localScale = new Vector3(fixX, 1f, 1f);
        float charSize = StackLabelCharSizeBase * StackLabelSizeScale / rootAbs;
        _stackLabel.characterSize = charSize;

        if (_stackLabelRenderer != null)
        {
            _stackLabelRenderer.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            _stackLabelRenderer.sortingOrder = GameConfig.SORT_VFX + 30;
        }

        float outlineStep = charSize * 0.2f;
        if (_stackOutlineLabels == null) return;
        for (int i = 0; i < _stackOutlineLabels.Length; i++)
        {
            var o = _stackOutlineLabels[i];
            if (o == null) continue;
            o.characterSize = charSize;
            var dir = StackOutlineDirs[i];
            o.transform.localPosition = new Vector3(dir.x * outlineStep, dir.y * outlineStep, 0.002f);
            if (_stackOutlineRenderers != null && i < _stackOutlineRenderers.Length && _stackOutlineRenderers[i] != null)
            {
                _stackOutlineRenderers[i].sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
                _stackOutlineRenderers[i].sortingOrder = GameConfig.SORT_VFX + 29;
            }
        }
    }

    void EnsureStackLabel()
    {
        if (_stackLabel != null && _stackOutlineLabels != null && _stackOutlineLabels.Length > 0)
            return;
        if (_stackLabelRoot != null)
            Destroy(_stackLabelRoot.gameObject);
        _stackLabel = null;
        _stackLabelRenderer = null;
        _stackOutlineLabels = null;
        _stackOutlineRenderers = null;

        _stackLabelRoot = new GameObject("StackCount").transform;
        // 挂 Body（不参与 Visual 镜像），避免朝左时 ×N 反字
        Transform labelParent = GetBodyTransform() != null ? GetBodyTransform() : transform;
        _stackLabelRoot.SetParent(labelParent, false);

        _stackOutlineLabels = new TextMesh[StackOutlineDirs.Length];
        _stackOutlineRenderers = new MeshRenderer[StackOutlineDirs.Length];
        for (int i = 0; i < StackOutlineDirs.Length; i++)
        {
            var oGo = new GameObject("Outline" + i, typeof(TextMesh));
            oGo.transform.SetParent(_stackLabelRoot, false);
            var o = oGo.GetComponent<TextMesh>();
            o.text = "x2";
            o.fontSize = 64;
            o.anchor = TextAnchor.MiddleCenter;
            o.alignment = TextAlignment.Center;
            o.color = new Color(0.08f, 0.05f, 0.02f, 0.95f);
            o.richText = false;
            _stackOutlineLabels[i] = o;
            _stackOutlineRenderers[i] = oGo.GetComponent<MeshRenderer>();
        }

        var fillGo = new GameObject("Fill", typeof(TextMesh));
        fillGo.transform.SetParent(_stackLabelRoot, false);
        _stackLabel = fillGo.GetComponent<TextMesh>();
        _stackLabel.text = "x2";
        _stackLabel.fontSize = 64;
        _stackLabel.anchor = TextAnchor.MiddleCenter;
        _stackLabel.alignment = TextAlignment.Center;
        _stackLabel.color = new Color(1f, 0.92f, 0.35f, 1f);
        _stackLabel.richText = false;

        _stackLabelRenderer = fillGo.GetComponent<MeshRenderer>();
        ApplyStackLabelFont();
        RefreshStackLabelLayout();
    }

    void HideStackLabel()
    {
        if (_stackLabelRoot != null) _stackLabelRoot.gameObject.SetActive(false);
    }
}
