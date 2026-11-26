using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 宠物测试开关（2026-11-21）：一键全解锁 5 只本体 + 5 款皮肤，方便逐只测试界面/技能/战斗出场。
///
/// 真源 = SaveData.petDebugAllUnlocked；打开时 IsPetUnlocked 恒真 → 列表/详情/皮肤切换/喂养/
/// 战斗出场（BattleManager.PetDeploy 也读 IsPetUnlocked）全部自动生效，不往 petUnlocked 写脏数据，
/// 关掉即回到真实解锁状态。
///
/// 【2026-11-26 主人「把这个开关去掉」】界面上的「全解锁 开/关」按钮及其建树代码已删净；
/// 测试入口保留 F8 热键与 Tools ▸ 宠物全解锁勾选（均仅编辑器/开发包，正式包不编译）。
/// 仅在编辑器与开发包编译；正式包不含此代码（避免玩家白嫖）。
/// </summary>
public partial class PetUI
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
