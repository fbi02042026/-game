using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 通用分享界面（2026-10-25 主人拍板 v2：不绑战斗数据，谁点都一样）。
///
/// 预制体：<c>Resources/Prefabs/Town/分享</c>（主人手工搭 2026-10-25 晚）：
/// 分享(Canvas ScreenSpaceCamera + Scaler 720×1280 + Raycaster) → Root
/// → Dim(全屏钮=关闭) / 界面(底图) / 按钮(Image+Button+子Text「立即分享」) / Info(提示文本)。
///
/// 行为：
/// · 打开即报 #share_entry_show(#scene=share_panel)；
/// · 「按钮」→ ShareService.TryShareAndReward("share_panel")（发钻/日键在 ShareService 唯一出口）；
/// · 当日已领（主人拍板：Info 改已领文案 + 按钮变灰禁点）；发奖是同步的，点完立即刷新；
/// · Dim → 关闭自毁。
///
/// 铁律合规：UICanvasSetup.ApplyPopup 修根 scale=0 / 相机为空 / 宽对齐 / override 层级；
/// 不写任何布局尺寸（铁律 20）；节点认不出 LogError 不静默（铁律 14）；字体/文案排版以预制体为准。
/// </summary>
public class SharePanelUI : MonoBehaviour
{
    /// <summary>预制体路径 —— 与实际位置必须同值，改一边忘另一边 = 运行时加载不到。</summary>
    public const string PrefabPath = "Prefabs/Town/分享";

    /// <summary>已领提示文案（主人拍板：领了后 Info 改已领 + 按钮变灰）。</summary>
    const string ClaimedInfoText = "今日已领，明天再来";

    /// <summary>已领态压灰系数（按钮 transition=None，Button.interactable 置灰不生效，手动压 Image 色）。</summary>
    static readonly Color ClaimedBtnColor = new Color(0.55f, 0.55f, 0.55f, 1f);

    Button _shareBtn;
    Image _shareBtnImg;
    Text _infoText;
    bool _claimed;

    /// <summary>打开通用分享界面。预制体缺失 → LogError 不弹（不静默，铁律 14）。</summary>
    public static void Show()
    {
        if (!Application.isPlaying) return;
        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[SharePanelUI] 找不到预制体 Resources/{PrefabPath}，分享界面不弹");
            return;
        }
        var go = UnityEngine.Object.Instantiate(prefab);
        go.AddComponent<SharePanelUI>();
    }

    void Awake()
    {
        // 同 BattleResultScreen 四件套：绑相机 + 归一根 scale + 宽对齐 + override 层级（UiSort.SharePanel=987）
        UICanvasSetup.ApplyPopup(GetComponent<Canvas>(), GameConfig.UiSort.SharePanel);

        var rootT = transform.Find("Root");
        if (rootT == null)
        {
            Debug.LogError("[SharePanelUI] 预制体缺 Root 节点（结构被改过？），分享界面关闭");
            Destroy(gameObject);
            return;
        }

        var dimT = rootT.Find("Dim");
        var dimBtn = dimT != null ? dimT.GetComponent<Button>() : null;
        if (dimBtn == null)
        {
            Debug.LogError("[SharePanelUI] 缺 Dim 或其上没有 Button（结构被改过？），无法点空白关闭");
        }
        else
        {
            dimBtn.onClick.RemoveAllListeners();
            dimBtn.onClick.AddListener(Close);
        }

        var btnT = rootT.Find("按钮");
        _shareBtn = btnT != null ? btnT.GetComponent<Button>() : null;
        _shareBtnImg = btnT != null ? btnT.GetComponent<Image>() : null;
        if (_shareBtn == null)
        {
            Debug.LogError("[SharePanelUI] 缺「按钮」节点或其上没有 Button（结构被改过？），立即分享不可点");
        }
        else
        {
            _shareBtn.onClick.RemoveAllListeners();
            _shareBtn.onClick.AddListener(OnShareClicked);
        }

        var infoT = rootT.Find("Info");
        _infoText = infoT != null ? infoT.GetComponent<Text>() : null;
        if (_infoText == null)
        {
            Debug.LogError("[SharePanelUI] 缺 Info 节点或其上没有 Text（结构被改过？），已领态不显示");
        }

        RefreshClaimedState();
        GameAnalytics.ReportShareEntryShow("share_panel");   // 埋点：分享界面曝光
    }

    void OnShareClicked()
    {
        if (_claimed) return;   // 已领态按钮已禁点，这里再拦一道（防程序集旧引用/外部调用）
        ShareService.TryShareAndReward("share_panel");
        RefreshClaimedState();   // 发奖是同步的，回来立即刷新已领态（Info 换文案 + 按钮变灰）
    }

    /// <summary>已领态唯一出口：判定走 ShareService.IsRewardClaimedToday（同语义单一出口）；
    /// Info 换已领文案，按钮禁点 + 压灰。未领态不动 prefab 原样（白图 + Info 原文案）。</summary>
    void RefreshClaimedState()
    {
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        _claimed = ShareService.IsRewardClaimedToday(data);
        if (!_claimed) return;

        if (_infoText != null) _infoText.text = ClaimedInfoText;
        if (_shareBtn != null) _shareBtn.interactable = false;
        if (_shareBtnImg != null) _shareBtnImg.color = ClaimedBtnColor;
    }

    void Close()
    {
        Destroy(gameObject);
    }
}
