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
            // 【2026-10-24 主人拍板】底图（挂发光材质球用）+ authored 缩放（呼吸动效的基准，还原也回它）
            _ascendImg = ascendTf.GetComponent<Image>();
            if (ascendTf is RectTransform ascendRt) _ascendBaseScale = ascendRt.localScale;

            // 【2026-10-25 主人需求】「指引」节点 = 主人手工摆进预制体的 Panel/进阶/指引：
            // 可升星时随进阶钮显示 + 缓慢上下浮动（见 ApplyGuideNode）。缺失只报错不兜底建。
            var guideTf = ascendTf.Find("指引");
            if (guideTf == null)
                Debug.LogError("[MercObtainPopup] 缺少节点 Panel/进阶/指引（进阶指引不显示，不影响其它功能）");
            else
            {
                _guideNode = guideTf.gameObject;
                _guideRt = guideTf as RectTransform;
                if (_guideRt != null) _guideBasePos = _guideRt.localPosition;   // authored 原位 = 浮动基准

                // 【2026-11-08】进阶指引气泡：文字去掉末字「略」+ 字号缩小，不再撑出气泡/屏幕。
                // 只改文案与 fontSize，预制体几何（sizeDelta / 位置 / 缩放）一个值不动。
                var guideLabel = guideTf.Find("Text (Legacy)")?.GetComponent<Text>();
                if (guideLabel == null)
                    Debug.LogError("[MercObtainPopup] 缺少节点 Panel/进阶/指引/Text (Legacy)（进阶提示文案）");
                else
                {
                    guideLabel.text = "可进阶";
                    guideLabel.fontSize = 24;
                }
            }
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
        // 【2026-11-08 主人需求】购买钮货币图标：运行时补建/绑定 KeepButton/icon（默认隐藏，仅招募购买模式显示）
        EnsureBuyIcon();

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

    /// <summary>
    /// 购买钮货币图标（2026-11-08 主人需求「购买按钮上放上卷轴的图标 跟其他放卷轴的位置一样」）：
    /// 先找 KeepButton/icon 现成节点，找不到才运行时补建；默认隐藏，仅招募购买模式由 ApplyBuyIcon 显示。
    /// <para>⚠️ 补建分支里的几何是【新建节点自身】的初始几何，照抄主人摆好的招募卡 ConfirmButton1/icon
    /// （sizeDelta 100×100、anchoredPosition(-38.1,0.7)、localScale 0.3、raycastTarget=false），
    /// 不是修改任何既有节点布局；KeepButton/Label 的几何与颜色一律不碰。</para>
    /// </summary>
    void EnsureBuyIcon()
    {
        if (_buyIcon != null) return;   // 幂等：已绑定/补建过就不再动
        if (_keepBtn == null)
        {
            Debug.LogError("[MercObtainPopup] KeepButton 为空，购买钮货币图标挂不上（fail closed，不静默）");
            return;
        }

        var t = _keepBtn.transform.Find(BuyIconNode);
        if (t != null)
        {
            _buyIcon = t.GetComponent<Image>();
            if (_buyIcon == null)
                Debug.LogError("[MercObtainPopup] KeepButton/" + BuyIconNode + " 节点缺 Image 组件（购买钮图标不可用）");
        }
        else
        {
            // 主人本次只手工摆了招募卡的遮罩，没在预制体摆购买钮的图标节点 → 运行时补建。
            var go = new GameObject(BuyIconNode, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(_keepBtn.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(-38.1f, 0.7f);
            rt.sizeDelta = new Vector2(100f, 100f);
            rt.localScale = new Vector3(0.3f, 0.3f, 0.3f);

            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;   // 不写 Image.type
            _buyIcon = img;
        }

        // 默认隐藏：只在招募购买模式显示，普通详情 / 复活模式一律隐藏。
        if (_buyIcon != null) _buyIcon.gameObject.SetActive(false);
    }

}
