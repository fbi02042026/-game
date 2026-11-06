using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SDK 开关登记表（Editor-only）。唯一真值 = 各平台组的 Scripting Define Symbol，
/// 菜单只做读写界面，不另存状态（见 Docs/SDK统一开关规范.md 2.1 / 2.2）。
/// </summary>
public static class SdkToggles
{
    /// <summary>一条记录：Id / Define 宏名 / 生效平台组 / 默认值 / 菜单名。</summary>
    public sealed class Entry
    {
        public readonly string Id;
        public readonly string Define;
        public readonly BuildTargetGroup Group;
        public readonly bool DefaultOn;
        public readonly string MenuPath;

        public Entry(string id, string define, BuildTargetGroup group, bool defaultOn, string menuPath)
        {
            Id = id;
            Define = define;
            Group = group;
            DefaultOn = defaultOn;
            MenuPath = menuPath;
        }
    }

    const string TapTapMenu = "Tools/SDK/TapTap SDK";
    const string PrintMenu = "Tools/SDK/打印当前开关状态";

    // 首条 = TapTap / TAPSDK_ENABLED / Android / 开 / 菜单名；新 SDK 只加一条记录。
    static readonly Entry[] Table =
    {
        new Entry("TapTap", "TAPSDK_ENABLED", BuildTargetGroup.Android, true, TapTapMenu),
    };

    static readonly IReadOnlyList<Entry> ReadOnlyTable = Array.AsReadOnly(Table);

    /// <summary>公开只读枚举（供 CliAndroidBuild 遍历打印）；每项含 Define 与 Group。</summary>
    public static IReadOnlyList<Entry> All { get { return ReadOnlyTable; } }

    // 菜单三件套：MenuItem + validate（Menu.SetChecked）+ 执行函数。
    // 状态真值只从 PlayerSettings 的平台组 define 读写，不另存本机偏好 / 资产文件 / 独立 json 等第二份状态。
    [MenuItem(TapTapMenu)]
    public static void ToggleTapTap()
    {
        Toggle(Table[0]);
    }

    [MenuItem(TapTapMenu, true)]
    public static bool ToggleTapTapValidate()
    {
        return Validate(Table[0]);
    }

    static void Toggle(Entry e)
    {
        string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(e.Group);
        bool wasOn = HasDefine(defines, e.Define);
        string next = wasOn ? RemoveDefine(defines, e.Define) : AddDefine(defines, e.Define);
        if (next != defines)
            PlayerSettings.SetScriptingDefineSymbolsForGroup(e.Group, next);
        bool on = !wasOn;
        Menu.SetChecked(e.MenuPath, on);
        Debug.Log("[SdkToggles] " + e.Id + " / " + e.Define + "=" + (on ? "ON" : "OFF") + " (" + e.Group + ")");
    }

    static bool Validate(Entry e)
    {
        bool on = HasDefine(PlayerSettings.GetScriptingDefineSymbolsForGroup(e.Group), e.Define);
        Menu.SetChecked(e.MenuPath, on);
        return true;
    }

    [MenuItem(PrintMenu)]
    public static void PrintAll()
    {
        foreach (var e in Table)
        {
            bool on = HasDefine(PlayerSettings.GetScriptingDefineSymbolsForGroup(e.Group), e.Define);
            Debug.Log("[SdkToggles] " + e.Id + " / " + e.Define + "=" + (on ? "ON" : "OFF")
                      + " (" + e.Group + ", 默认" + (e.DefaultOn ? "开" : "关") + ")");
        }
    }

    /// <summary>
    /// 按包型批量写 define：以登记表全部组为对齐范围，enabled 为空即全部关闭——
    /// 登记表里属于 enabled 的 define 保证加上，其余登记的 define 全部删掉。
    /// 真值仍是该组的 Scripting Define Symbol，不另存状态；内容无变化时跳过写入以免触发重编译。
    /// </summary>
    public static void ApplyDefinesForProfile(IReadOnlyList<Entry> enabled)
    {
        var groups = new HashSet<BuildTargetGroup>();
        foreach (var e in Table)
            groups.Add(e.Group);

        foreach (var group in groups)
        {
            string before = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
            string after = before;
            foreach (var e in Table)
            {
                if (e.Group != group) continue;
                bool shouldOn = false;
                foreach (var want in enabled)
                {
                    if (want.Group == group && want.Define == e.Define)
                    {
                        shouldOn = true;
                        break;
                    }
                }
                after = shouldOn ? AddDefine(after, e.Define) : RemoveDefine(after, e.Define);
            }
            if (after != before)
                PlayerSettings.SetScriptingDefineSymbolsForGroup(group, after);
        }
    }

    static bool HasDefine(string defines, string symbol)
    {
        if (string.IsNullOrEmpty(defines)) return false;
        return defines.Split(';').Any(d => d.Trim() == symbol);
    }

    static string AddDefine(string defines, string symbol)
    {
        if (HasDefine(defines, symbol)) return defines;
        return string.IsNullOrEmpty(defines) ? symbol : defines + ";" + symbol;
    }

    static string RemoveDefine(string defines, string symbol)
    {
        if (string.IsNullOrEmpty(defines)) return "";
        return string.Join(";", defines.Split(';')
            .Where(d => !string.IsNullOrEmpty(d.Trim()) && d.Trim() != symbol));
    }
}
