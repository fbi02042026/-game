using System;
using UnityEngine;

/// <summary>
/// 开箱/整理装备模式：禁刷怪、关摇杆、背包可拖拽、「整理」变「确定」。
/// </summary>
public static class BattleLootMode
{
    public static bool Active { get; private set; }
    public static event Action<bool> Changed;

    static Action _onConfirm;

    public static void Enter(Action onConfirm = null)
    {
        _onConfirm = onConfirm;
        if (Active) return;
        Active = true;
        Changed?.Invoke(true);
        BattleUI.Instance?.RefreshLootModeChrome();
        BattleJoystick.Instance?.SetVisible(false);
    }

    public static void Exit()
    {
        if (!Active) return;
        Active = false;
        _onConfirm = null;
        Changed?.Invoke(false);
        BattleUI.Instance?.RefreshLootModeChrome();
        bool showStick = BattleManager.Instance != null
            && BattleManager.Instance.isInBattle
            && BattleManager.Instance.UnitsCanAct;
        BattleJoystick.Instance?.SetVisible(showStick);
    }

    public static void Confirm()
    {
        var cb = _onConfirm;
        Exit();
        cb?.Invoke();
    }
}
