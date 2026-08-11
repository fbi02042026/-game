using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 项目字体：
/// - 中文：fusion-pixel（OFL-1.1，可商用）
/// - 数字/拉丁：PixelFont（SPUM Victor's Pixel Font）
/// </summary>
public static class GameFonts
{
    static Font _numberFont;
    static Font _chineseFont;

    /// <summary>数字、金币、血量等（原 PixelFont）</summary>
    public static Font GetNumber()
    {
        if (_numberFont != null) return _numberFont;
        _numberFont = Resources.Load<Font>("Fonts/PixelFont");
#if UNITY_EDITOR
        if (_numberFont == null)
        {
            _numberFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(
                "Assets/Resources/Fonts/PixelFont.ttf");
            if (_numberFont == null)
                _numberFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(
                    "Assets/SPUM/Core/Basic_Resources/Font/PixelFont.ttf");
        }
#endif
        if (_numberFont == null)
            _numberFont = GetChinese();
        return _numberFont;
    }

    /// <summary>中文正文/标题（fusion-pixel）</summary>
    public static Font GetChinese()
    {
        if (_chineseFont != null) return _chineseFont;

        // 优先 ttf，再 otf
        _chineseFont = Resources.Load<Font>("Fonts/fusion-pixel");
#if UNITY_EDITOR
        if (_chineseFont == null)
        {
            _chineseFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(
                "Assets/Resources/Fonts/fusion-pixel.ttf");
            if (_chineseFont == null)
                _chineseFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(
                    "Assets/Resources/Fonts/fusion-pixel.otf");
        }
#endif
        if (_chineseFont == null)
        {
            _chineseFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei", "微软雅黑", "SimHei" }, 64);
        }
#if UNITY_2022_1_OR_NEWER
        if (_chineseFont == null)
            _chineseFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
        if (_chineseFont == null)
            _chineseFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
        return _chineseFont;
    }

    /// <summary>兼容旧调用：默认中文</summary>
    public static Font GetDefault() => GetChinese();

    /// <summary>兼容旧调用</summary>
    public static Font GetChineseSafe() => GetChinese();

    /// <summary>
    /// 给一整棵 UI 树套字体：数字类控件用 PixelFont，其余用 fusion-pixel。
    /// </summary>
    public static void ApplyToHierarchy(Transform root)
    {
        if (root == null) return;
        Font cn = GetChinese();
        Font num = GetNumber();
        var texts = root.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            Text t = texts[i];
            if (t == null) continue;
            // 含中文的控件一律 fusion-pixel（即使名字像数值控件）
            bool useNum = IsNumberText(t) && !HasCjk(t.text);
            t.font = useNum ? num : cn;
        }
    }

    static bool IsNumberText(Text t)
    {
        string n = t.gameObject.name ?? "";
        // 常见数值控件名
        if (ContainsIgnoreCase(n, "Gold")) return true;
        if (ContainsIgnoreCase(n, "Stamina") || n.Contains("体力")) return true;
        if (ContainsIgnoreCase(n, "Regen")) return true;
        if (ContainsIgnoreCase(n, "HP") || ContainsIgnoreCase(n, "Hp")) return true;
        if (ContainsIgnoreCase(n, "Lan") || ContainsIgnoreCase(n, "Mana")) return true;
        if (ContainsIgnoreCase(n, "Energy")) return true;
        if (ContainsIgnoreCase(n, "Cooldown")) return true;
        if (ContainsIgnoreCase(n, "Level") || ContainsIgnoreCase(n, "Lv")) return true;
        if (ContainsIgnoreCase(n, "Damage") || ContainsIgnoreCase(n, "Dmg")) return true;
        if (ContainsIgnoreCase(n, "Count") || ContainsIgnoreCase(n, "Timer")) return true;
        if (ContainsIgnoreCase(n, "Countdown")) return true;
        if (ContainsIgnoreCase(n, "WaveTimer") || ContainsIgnoreCase(n, "ComboValue")) return true;
        if (ContainsIgnoreCase(n, "Progress") && !ContainsIgnoreCase(n, "Bar")) return true;
        if (ContainsIgnoreCase(n, "Talent") && ContainsIgnoreCase(n, "Text")) return true;
        if (ContainsIgnoreCase(n, "Enchant") && ContainsIgnoreCase(n, "Text")) return true;
        if (ContainsIgnoreCase(n, "Decompose") && ContainsIgnoreCase(n, "Text")) return true;
        if (ContainsIgnoreCase(n, "Diamond")) return true;
        if (ContainsIgnoreCase(n, "Stone")) return true;
        if (ContainsIgnoreCase(n, "Mat")) return true;
        // 纯数字内容也倾向数字字体
        string s = t.text;
        if (!string.IsNullOrEmpty(s))
        {
            bool onlyNum = true;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsDigit(c) || c == '/' || c == '%' || c == '+' || c == '-' || c == '.' || c == ',' || c == ' ' || c == 'x' || c == 'X')
                    continue;
                onlyNum = false;
                break;
            }
            if (onlyNum) return true;
        }
        return false;
    }

    static bool ContainsIgnoreCase(string hay, string needle)
    {
        return hay.IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static bool HasCjk(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c >= 0x4E00 && c <= 0x9FFF) return true;
            if (c >= 0x3400 && c <= 0x4DBF) return true;
        }
        return false;
    }
}
