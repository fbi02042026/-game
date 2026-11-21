using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 【仅编辑器测试用】宠物「一键全解锁」的 Tools 勾选开关（2026-11-21 主人要「tools 的设置」）。
///
/// 作用对象 = 存档 player.dat 里的 petDebugAllUnlocked 字段（真源见 SaveData.IsPetUnlocked）。
/// 打开后进 Play，宠物界面右侧列表不再按真实解锁条件判定，5 只本体 + 5 款皮肤全部可用，
/// 可逐只上阵进战斗测技能/出场；关掉即回到真实解锁状态（因为从不往 petUnlocked 里写数据）。
///
/// 实现方式照 DevProgressMenu 的既定范式：
///   ① Play 中或即将进入 Play 时拒绝执行（内存 SaveSystem 会用旧档覆盖手工改动）；
///   ② 先做同目录时间戳备份，备份失败即中止、不动主档；
///   ③ 文本级改值，其余字符逐字原样保留；字段不存在时在顶层对象末尾补一个键（老档兼容）；
///   ④ 写盘后 SaveSystem.InstanceQuiet?.Load() 刷新残留内存单例，避免下一次 Save 冲掉；
///   ⑤ UTF-8 无 BOM 写回。
///
/// 注意：该字段与开关逻辑在运行时代码里被 #if UNITY_EDITOR || DEVELOPMENT_BUILD 包住，
/// Release 正式包不编译，即使存档里留着 true 也无效。
/// </summary>
public static class PetTestMenu
{
    // 直接挂在 Tools 第一层（原先在 Tools/宠物测试/ 子菜单里，容易找不到）。
    const string MenuRoot = "Tools/";
    const string UnlockMenu = MenuRoot + "宠物全解锁（勾选开关）";
    const string ShowMenu = MenuRoot + "宠物解锁状态";

    /// <summary>与 SaveData.petDebugAllUnlocked 同名，文本级改写用。</summary>
    const string Field = "petDebugAllUnlocked";

    [MenuItem(UnlockMenu, false, 200)]
    static void ToggleAllUnlock()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("宠物测试 / 无法执行",
                "请先退出 Play 再点这个菜单。\n" +
                "Play 中内存里的 SaveSystem 会用旧档把手工改过的 player.dat 覆盖回去。\n\n" +
                "（Play 中要临时全解锁：宠物界面右上角按钮，或按 F8。）", "知道了");
            return;
        }

        string path = Path.Combine(Application.persistentDataPath, "player.dat");
        if (!File.Exists(path))
        {
            Debug.LogError("[宠物测试] 找不到存档：" + path);
            EditorUtility.DisplayDialog("宠物测试 / 失败",
                "还没有存档文件：\n" + path + "\n\n先运行一次游戏生成存档，再回来设置。", "知道了");
            return;
        }

        bool current = ReadFlag(path);
        bool target = !current;

        string backupPath = path + ".bak_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        try
        {
            File.Copy(path, backupPath, true);
        }
        catch (Exception e)
        {
            Debug.LogError("[宠物测试] 备份失败，已中止，未改动主档：" + e);
            return;
        }

        string json = File.ReadAllText(path, Encoding.UTF8);
        string newJson = WriteFlag(json, target);
        if (newJson == null)
        {
            Debug.LogError("[宠物测试] 无法写入 " + Field + "（JSON 结构异常），已中止，未改动主档。");
            EditorUtility.DisplayDialog("宠物测试 / 失败",
                "player.dat 结构异常，未改动主档。\n备份：" + Path.GetFileName(backupPath), "知道了");
            return;
        }

        File.WriteAllText(path, newJson, new UTF8Encoding(false));
        SaveSystem.InstanceQuiet?.Load();   // 刷新残留内存单例（域未重载时）

        Debug.Log("[宠物测试] " + Field + " = " + target + "；备份：" + backupPath);
        EditorUtility.DisplayDialog("宠物测试 / 已写入",
            target
                ? "已开启「全部宠物解锁」。\n\n进 Play 后宠物界面 5 只本体 + 5 款皮肤全部可用，可直接上阵测战斗。\n（Release 正式包不编译此功能，放心。）"
                : "已关闭「全部宠物解锁」，回到真实解锁状态。\n\n备份：" + Path.GetFileName(backupPath),
            "知道了");
    }

    [MenuItem(UnlockMenu, true)]
    static bool ToggleAllUnlockValidate()
    {
        string path = Path.Combine(Application.persistentDataPath, "player.dat");
        Menu.SetChecked(UnlockMenu, File.Exists(path) && ReadFlag(path));
        return true;
    }

    [MenuItem(ShowMenu, false, 201)]
    static void ShowStatus()
    {
        string path = Path.Combine(Application.persistentDataPath, "player.dat");
        bool on = File.Exists(path) && ReadFlag(path);
        Debug.Log("[宠物测试] 存档 = " + path + "，" + Field + " = " + on);
        EditorUtility.DisplayDialog("宠物测试 / 当前状态",
            (on ? "全解锁：已开启" : "全解锁：未开启") + "\n\n" +
            "存档：" + path + "\n" +
            (File.Exists(path) ? "（字段未出现 = 关闭）" : "（存档不存在）"), "知道了");
    }

    /// <summary>读存档里该字段的值；字段不存在视为 false（JsonUtility 反序列化同口径）。</summary>
    static bool ReadFlag(string path)
    {
        try
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            string key = '"' + Field + "\":";
            int i = json.IndexOf(key, StringComparison.Ordinal);
            if (i < 0) return false;
            int v = i + key.Length;
            while (v < json.Length && json[v] == ' ') v++;
            return v < json.Length && json[v] == 't';   // true / false
        }
        catch (Exception e)
        {
            Debug.LogWarning("[宠物测试] 读存档失败：" + e.Message);
            return false;
        }
    }

    /// <summary>
    /// 把 src 里 petDebugAllUnlocked 改成 value；字段不存在则在顶层对象末尾补键。
    /// 返回新字符串；结构异常返回 null（不兜底、不静默）。
    /// </summary>
    static string WriteFlag(string src, bool value)
    {
        string literal = value ? "true" : "false";
        string key = '"' + Field + "\":";
        int i = src.IndexOf(key, StringComparison.Ordinal);

        if (i >= 0)
        {
            int v = i + key.Length;
            while (v < src.Length && src[v] == ' ') v++;
            int end = v;
            while (end < src.Length && src[end] != ',' && src[end] != '}') end++;
            if (end == v) return null;
            return src.Substring(0, v) + literal + src.Substring(end);
        }

        // 老档没有该字段：插到顶层对象最后一个 '}' 之前
        int close = src.LastIndexOf('}');
        if (close < 0) return null;
        bool needComma = true;
        int p = close - 1;
        while (p >= 0 && char.IsWhiteSpace(src[p])) p--;
        if (p >= 0 && src[p] == '{') needComma = false;   // 空对象，首个键不加逗号
        string insert = (needComma ? "," : "") + key + literal;
        return src.Substring(0, close) + insert + src.Substring(close);
    }
}
