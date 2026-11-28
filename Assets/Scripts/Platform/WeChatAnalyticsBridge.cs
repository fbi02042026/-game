using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
#if UNITY_WECHATMINIGAME
using WeChatWASM;
#endif

/// <summary>
/// 微信小游戏「We 分析」自定义事件上报桥（C# 门面 → 官方 WX SDK）。
///
/// 【2026-11-27 接入】此前本类是占位 + TODO（只有 Debug.Log），原因是注释误判「没有 SDK 封装」。
/// 实测官方包 com.qq.weixin.minigame 已提供 WX.ReportEvent(string, Dictionary&lt;string,string&gt;)，
/// 底层即 wx.reportEvent(eventId, object)（需基础库 2.14.4）。项目里 WeChatAd.cs:114 已在用同一 SDK 的
/// WX.CreateRewardedVideoAd，故本类照同一模式直调官方 API，**不再新增 jslib**
/// （WeChatAd 的 jslib 也已于 11-02 退役删除，避免自写桥与官方桥打架）。
///
/// 设计（沿用 WeChatAd 口径）：
/// - 仅微信小游戏构建（UNITY_WECHATMINIGAME）真正上报；编辑器/安卓/聚光灯包一律只走日志。
/// - UseWeChatReport 默认 false，由 Analytics.Init 在微信构建置 true；保留开关便于一键关上报。
/// - 所有上报 try/catch 包住，禁止因埋点抛异常中断游戏（Analytics 门面纪律第 2 条）。
/// - WX.ReportEvent 的 data 只收 Dictionary&lt;string,string&gt;，故把 Analytics 传来的扁平 JSON
///   就地解析成字典；不引第三方库、不动 Analytics 门面接口。
/// </summary>
public static class WeChatAnalyticsBridge
{
    /// <summary>是否已接入真实微信上报（微信构建下由 Analytics.Init 置 true）。</summary>
    public static bool UseWeChatReport { get; set; }

    /// <summary>是否处于微信小游戏运行时（编译期宏，非微信构建恒 false）。</summary>
    public static bool Available
    {
        get
        {
#if UNITY_WECHATMINIGAME
            return true;
#else
            return false;
#endif
        }
    }

    /// <summary>
    /// 上报一条事件。jsonProps 为 Analytics 拼好的扁平 JSON 对象字符串。
    /// 所有上报都 try/catch 包住，禁止因埋点抛异常中断游戏。
    /// </summary>
    public static void ReportEvent(string eventName, string jsonProps)
    {
        if (SpotlightBuild.Enabled)
        {
            Debug.Log($"[WeChatAnalyticsBridge] Spotlight：跳过上报 {eventName}");
            return;
        }

#if UNITY_WECHATMINIGAME
        if (UseWeChatReport && Available)
        {
            try
            {
                var props = ParseProps(jsonProps);
                WX.ReportEvent(eventName, props);
                return;
            }
            catch (Exception e)
            {
                // 埋点失败不能中断游戏；只打警告，仍落日志便于排查。
                Debug.LogWarning($"[WeChatAnalyticsBridge] 上报失败（已吞掉）: {eventName} {e.Message}");
            }
        }
#endif

        // 非微信平台 / 未开启真实上报：走日志，方便联调，不报错。
        Debug.Log($"[Analytics] {eventName} {jsonProps}");
    }

    /// <summary>
    /// 把 Analytics 生成的扁平 JSON（形如 {"chapter":1,"win":true,"placement":"revive"}）
    /// 解析成 WX.ReportEvent 需要的 Dictionary&lt;string,string&gt;。
    /// 只处理本工程 BuildPropsJson 产出的四种值：string / bool / number / null；
    /// 不引 JsonUtility（它不能解析动态 key 的字典），不引第三方库。
    /// 解析失败抛异常，由 ReportEvent 的 try/catch 吞掉并打警告（不静默成功）。
    /// </summary>
    static Dictionary<string, string> ParseProps(string json)
    {
        var result = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(json) || json == "{}") return result;

        int i = 0;
        SkipWs(json, ref i);
        if (json[i++] != '{') throw new FormatException("事件属性不是 JSON 对象");
        SkipWs(json, ref i);
        if (i < json.Length && json[i] == '}') return result;

        while (i < json.Length)
        {
            SkipWs(json, ref i);
            string key = ReadString(json, ref i);
            SkipWs(json, ref i);
            if (json[i++] != ':') throw new FormatException($"键 {key} 后缺少冒号");
            SkipWs(json, ref i);
            result[key] = ReadValue(json, ref i);
            SkipWs(json, ref i);
            if (i >= json.Length) throw new FormatException("JSON 对象未闭合");
            char c = json[i++];
            if (c == '}') break;
            if (c != ',') throw new FormatException($"键 {key} 后应为逗号或右括号");
        }
        return result;
    }

    static string ReadValue(string s, ref int i)
    {
        if (i >= s.Length) throw new FormatException("值缺失");
        if (s[i] == '"') return ReadString(s, ref i);
        int start = i;
        while (i < s.Length && s[i] != ',' && s[i] != '}' && !char.IsWhiteSpace(s[i])) i++;
        string raw = s.Substring(start, i - start);
        if (raw.Length == 0) throw new FormatException("空值");
        // bool 保持小写以对齐 JSON 原貌；数字原样返回字符串（We 分析按字符串维度收）。
        return raw;
    }

    static string ReadString(string s, ref int i)
    {
        if (i >= s.Length || s[i++] != '"') throw new FormatException("期望字符串");
        var sb = new System.Text.StringBuilder();
        while (i < s.Length)
        {
            char c = s[i++];
            if (c == '"') return sb.ToString();
            if (c != '\\') { sb.Append(c); continue; }
            if (i >= s.Length) throw new FormatException("转义序列不完整");
            char e = s[i++];
            switch (e)
            {
                case '"': sb.Append('"'); break;
                case '\\': sb.Append('\\'); break;
                case '/': sb.Append('/'); break;
                case 'n': sb.Append('\n'); break;
                case 'r': sb.Append('\r'); break;
                case 't': sb.Append('\t'); break;
                case 'u':
                    if (i + 4 > s.Length) throw new FormatException("Unicode 转义不完整");
                    sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                    i += 4;
                    break;
                default: throw new FormatException($"未知转义 \\{e}");
            }
        }
        throw new FormatException("字符串未闭合");
    }

    static void SkipWs(string s, ref int i)
    {
        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
    }
}
