using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 宠物测试开关（2026-11-21）：一键全解锁 5 只本体 + 5 款皮肤，方便逐只测试界面/技能/战斗出场。
///
/// 真源 = SaveData.petDebugAllUnlocked；打开时 IsPetUnlocked 恒真 → 列表/详情/皮肤切换/喂养/
/// 战斗出场（BattleManager.PetDeploy 也读 IsPetUnlocked）全部自动生效，不往 petUnlocked 写脏数据，
/// 关掉即回到真实解锁状态。
///
/// 按钮运行时建在 PetUI Canvas 根节点右上角（不进 Panel，不动任何现有节点位置/尺寸）。
/// 仅在编辑器与开发包编译；正式包不含此代码（避免玩家白嫖）。
/// </summary>
public partial class PetUI
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    Transform _debugToggle;
    Text _debugToggleLabel;

    void EnsureDebugToggle()
    {
        if (_debugToggle != null) return;

        var go = new GameObject("PetDebugToggle",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);   // 挂 Canvas 根：不挤占 Panel 内任何现有节点
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-16f, -16f);
        rt.sizeDelta = new Vector2(200f, 60f);

        var bg = go.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.72f);
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = bg;
        btn.onClick.AddListener(OnToggleDebugAllUnlock);

        var txtGo = new GameObject("Label",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        var trt = (RectTransform)txtGo.transform;
        trt.SetParent(rt, false);
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        _debugToggleLabel = txtGo.GetComponent<Text>();
        _debugToggleLabel.font = GameFonts.GetChinese();
        _debugToggleLabel.fontSize = 24;
        _debugToggleLabel.alignment = TextAnchor.MiddleCenter;
        _debugToggleLabel.raycastTarget = false;

        _debugToggle = rt;
        RefreshDebugToggleLabel();
    }

    void RefreshDebugToggleLabel()
    {
        if (_debugToggleLabel == null) return;
        var save = SaveSystem.Instance?.Data;
        bool on = save != null && save.petDebugAllUnlocked;
        _debugToggleLabel.text = on ? "全解锁 开" : "全解锁 关";
        _debugToggleLabel.color = on ? new Color(1f, 0.86f, 0.32f, 1f) : new Color(1f, 1f, 1f, 0.85f);
    }

    /// <summary>切换测试全解锁：开 → 10 只全部可用；关 → 回到真实存档解锁状态。</summary>
    void OnToggleDebugAllUnlock()
    {
        var save = SaveSystem.Instance?.Data;
        if (save == null) return;

        save.petDebugAllUnlocked = !save.petDebugAllUnlocked;

        // 关掉时若上阵的是「靠测试开关才解锁」的宠物，回落到默认狗，避免下局出场被拒
        if (!save.petDebugAllUnlocked)
        {
            var deployedDef = PetDefs.Get(save.petDeployedId);
            string bodyId = deployedDef != null && PetDefs.IsSkin(deployedDef) ? deployedDef.skinOf : save.petDeployedId;
            if (!string.IsNullOrEmpty(bodyId) && !save.IsPetUnlocked(save.petDeployedId))
            {
                save.petDeployedId = "dog";
                Debug.Log("[PetUI] 关闭测试全解锁：上阵宠物 " + bodyId + " 未真实解锁，已回落 dog");
            }
        }

        SaveSystem.Instance.Save();
        RefreshDebugToggleLabel();
        RefreshAll();
        GlobalToastUI.Show(save.petDebugAllUnlocked
            ? "测试模式：全部宠物/皮肤已解锁"
            : "测试模式已关闭，回到真实解锁状态");
    }

    void DebugHotkey()
    {
        // 编辑器里用 F8 快速切换（F9 已被邮件界面调试占用）
        if (Input.GetKeyDown(KeyCode.F8)) OnToggleDebugAllUnlock();
    }
#endif
}
