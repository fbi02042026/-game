using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 节点绑定：从预制体取节点与组件（只认既有节点，取不到置空由 Fill 判空报错）。
/// 【2026-10-23 主人拍板】原 MercObtainPopupUI.cs 已 929 行（项目约定 >600 行必须拆），
/// 按职责切成 4 个 partial，本文件是其中之一。代码为原样搬运，逻辑未改。
/// </summary>
public partial class MercObtainPopupUI : MonoBehaviour
{
    void Bind()
    {
        _root = transform.Find("Root")?.gameObject;
        if (_root == null)
        {
            Debug.LogError("[MercObtainPopup] 预制体缺少节点 Root（后续 Fill 会跳过，onClosed 收尾）");
            return;
        }

        _dimBtn = _root.transform.Find("Dim")?.GetComponent<Button>();
        _keepBtn = _root.transform.Find("Panel/KeepButton")?.GetComponent<Button>();
        if (_keepBtn != null)
        {
            _keepRt = _keepBtn.transform as RectTransform;
            if (_keepRt != null) _keepBasePos = _keepRt.anchoredPosition;   // 预制体并排态原位（x=-122）
            else Debug.LogError("[MercObtainPopup] KeepButton 不是 RectTransform，查看模式无法居中");
        }
        // 【2026-10-18 主人拍板】「替换」钮 = 预制体自带节点（主人摆的 tihaunButton），
        // 只绑不改：显隐走 ApplyReplaceButton（酒馆招募双钮 / 其余入口单确认钮），点击回调在 Wire 里接。
        _replaceBtn = _root.transform.Find("Panel/tihaunButton")?.GetComponent<Button>();
        if (_replaceBtn == null)
            Debug.LogError("[MercObtainPopup] 缺少节点 Panel/tihaunButton（酒馆招募的「替换」钮不显示也不可点）");

        // 【2026-10-22 主人拍板】「进阶」钮 = 预制体自带节点（主人摆的 Panel/进阶）：
        // 节点没有 Button 组件就运行时 AddComponent 补上（先例允许，几何不动）；
        // 节点整个缺失只 LogError 不兜底建（同 tihaunButton 口径）。显隐在 Fill 里按已拥有刷。
        var ascendTf = _root.transform.Find("Panel/进阶");
        if (ascendTf == null)
            Debug.LogError("[MercObtainPopup] 缺少节点 Panel/进阶（进阶入口不可用）");
        else
        {
            _ascendBtn = ascendTf.GetComponent<Button>();
            if (_ascendBtn == null) _ascendBtn = ascendTf.gameObject.AddComponent<Button>();
        }

        // 招募购买模式要改文案的三个节点（2026-10-19 主人拍板：按钮换成 购买/取消、标题改佣兵信息）
        _keepLabel = _root.transform.Find("Panel/KeepButton/Label")?.GetComponent<Text>();
        _replaceLabel = _root.transform.Find("Panel/tihaunButton/Label")?.GetComponent<Text>();
        _titleText = _root.transform.Find("Panel/biaoti")?.GetComponent<Text>();
        if (_keepLabel != null && !string.IsNullOrEmpty(_keepLabel.text)) _keepLabelText0 = _keepLabel.text;
        if (_replaceLabel != null && !string.IsNullOrEmpty(_replaceLabel.text)) _replaceLabelText0 = _replaceLabel.text;
        if (_titleText != null && !string.IsNullOrEmpty(_titleText.text)) _titleText0 = _titleText.text;
        _keepLabelColor0 = _keepLabel != null ? _keepLabel.color : Color.white;

        // 阵亡复活模式的「护符 icon + 拥有数量」提示行（复活按钮上方；运行时补节点，预制体不动）
        EnsureReviveCharmHint();

        _icon = _root.transform.Find("Panel/zuo/Iconmask/Icon")?.GetComponent<Image>();
        _nameText = _root.transform.Find("Panel/zuo/namebg/Name")?.GetComponent<Text>();
        _nameBg = _root.transform.Find("Panel/zuo/namebg")?.GetComponent<Image>();
        _zuoImg = _root.transform.Find("Panel/zuo")?.GetComponent<Image>();
        _role = _root.transform.Find("Panel/Role")?.GetComponent<Image>();

        // 满星节点：预制体只摆了 3 个（满星1/满星2/满星3）
        _stars = new GameObject[MaxStarNodes];
        var rating = _root.transform.Find("Panel/you/Rating");
        if (rating == null)
            Debug.LogError("[MercObtainPopup] 缺少节点 Panel/you/Rating（星级将不显示）");
        else
        {
            _stars[0] = rating.Find("满星1")?.gameObject;
            _stars[1] = rating.Find("满星2")?.gameObject;
            _stars[2] = rating.Find("满星3")?.gameObject;

            // 【2026-10-14 主人拍板】星级要 1 颗 / 2 颗 / 3 颗都居中 —— 记下满星布局原始位置，
            // Fill 时按激活数量整组平移（见 CenterActiveStars）。
            _starBasePos = new Vector2[MaxStarNodes];
            for (int i = 0; i < MaxStarNodes; i++)
            {
                var srt = _stars[i] != null ? _stars[i].GetComponent<RectTransform>() : null;
                if (srt != null) _starBasePos[i] = srt.anchoredPosition;
            }
        }

        // 七对属性（Root 整行 + 值 Text；Label 静态文本不碰）
        _attrHpRoot = _root.transform.Find("Panel/you/AttrPanel/AttrHpRoot")?.gameObject;
        _attrHp = _root.transform.Find("Panel/you/AttrPanel/AttrHpRoot/AttrHp")?.GetComponent<Text>();
        _attrAtkRoot = _root.transform.Find("Panel/you/AttrPanel/AttrAtkRoot")?.gameObject;
        _attrAtk = _root.transform.Find("Panel/you/AttrPanel/AttrAtkRoot/AttrAtk")?.GetComponent<Text>();
        _attrMagicAtkRoot = _root.transform.Find("Panel/you/AttrPanel/AttrMagicAtkRoot")?.gameObject;
        _attrMagicAtk = _root.transform.Find("Panel/you/AttrPanel/AttrMagicAtkRoot/AttrMagicAtk")?.GetComponent<Text>();
        _attrDefRoot = _root.transform.Find("Panel/you/AttrPanel/AttrDefRoot")?.gameObject;
        _attrDef = _root.transform.Find("Panel/you/AttrPanel/AttrDefRoot/AttrDef")?.GetComponent<Text>();
        _attrMagicDefRoot = _root.transform.Find("Panel/you/AttrPanel/AttrMagicDefRoot")?.gameObject;
        _attrMagicDef = _root.transform.Find("Panel/you/AttrPanel/AttrMagicDefRoot/AttrMagicDef")?.GetComponent<Text>();
        _attrSpdRoot = _root.transform.Find("Panel/you/AttrPanel/AttrSpdRoot")?.gameObject;
        _attrSpd = _root.transform.Find("Panel/you/AttrPanel/AttrSpdRoot/AttrSpd")?.GetComponent<Text>();
        _attrCritRoot = _root.transform.Find("Panel/you/AttrPanel/AttrCritRoot")?.gameObject;
        _attrCrit = _root.transform.Find("Panel/you/AttrPanel/AttrCritRoot/AttrCrit")?.GetComponent<Text>();

        _skillRoot = _root.transform.Find("Panel/you/Skill")?.gameObject;
        _skillName = _root.transform.Find("Panel/you/Skill/SkillName")?.GetComponent<Text>();
        _skillInfo = _root.transform.Find("Panel/you/Skill/Skillxinxi")?.GetComponent<Text>();
        _skillIcon = _root.transform.Find("Panel/you/Skill/SkillIcon")?.GetComponent<Image>();
    }

}
