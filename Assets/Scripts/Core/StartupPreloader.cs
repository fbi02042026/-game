using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 启动期预加载：趁 Loading 界面把常用资源先 Load 进内存，
/// 之后第一次打开界面不会再因为临时 Resources.Load 卡一帧。
/// 只做“加载”，不改任何 UI、不碰存档数据。
/// 每条 yield 一帧，外层把进度写进 Loading 界面。
/// </summary>
public static class StartupPreloader
{
    /// <summary>城镇页 prefab（与 TownHubController / CodexInfoPopup 里的加载路径保持一致）。</summary>
    static readonly string[] TownPrefabs =
    {
        "Prefabs/Town/CharacterUI",
        "Prefabs/Town/TavernUI",
        "Prefabs/Town/AdventureUI",
        "Prefabs/Town/AdventureLogUI",
        "Prefabs/Town/CodexInfoPopup",
    };

    /// <summary>日志页常用底图/插图。</summary>
    static readonly string[] LogFrames = { "内容底", "标签底", "字底", "图层 1", "普通边框", "boss边框" };
    static readonly string[] LogIllusts = { "主线", "支线", "怪物", "佣兵", "成就", "世界" };

    public static IEnumerator Run(Action<float, string> report)
    {
        var steps = new List<KeyValuePair<string, Action>>();

        steps.Add(Step("字体", () =>
        {
            GameFonts.GetChinese();
            GameFonts.GetNumber();
        }));

        steps.Add(Step("界面底图", () =>
        {
            for (int i = 0; i < LogFrames.Length; i++)
                UiKeyedBackgrounds.LogFrame(LogFrames[i]);
        }));

        steps.Add(Step("标签插图", () =>
        {
            for (int i = 0; i < LogIllusts.Length; i++)
            {
                UiKeyedBackgrounds.LogTabIllust(LogIllusts[i]);
                UiKeyedBackgrounds.LogTabSidebarIcon(LogIllusts[i]);
            }
        }));

        steps.Add(Step("图标", () =>
        {
            var dot = RedDot.Sprite;            // 红点（会按需内部加载）
            if (dot == null) dot = null;        // 占位：此处只为触发一次加载
            Resources.Load<Sprite>("UI/Common/红点");
            Resources.Load<Sprite>("UI/Common/锁");
        }));

        steps.Add(Step("城镇界面", () =>
        {
            for (int i = 0; i < TownPrefabs.Length; i++)
                Resources.Load<GameObject>(TownPrefabs[i]);
        }));

        int total = steps.Count;
        LoadingFlavorText.Reset();
        for (int i = 0; i < total; i++)
        {
            // 进度照常上报；文案换成「裂隙杂记」（剧情 / 佣兵向的小段子），
            // steps[i].Key 仍只用于失败日志，不给玩家看。
            if (report != null)
                report(i / (float)total, LoadingFlavorText.Next());
            yield return null;
            try
            {
                steps[i].Value();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[StartupPreloader] {steps[i].Key} 预加载失败：{ex.Message}");
            }
        }

        if (report != null)
            report(1f, LoadingFlavorText.Next());
        yield return null;
    }

    static KeyValuePair<string, Action> Step(string name, Action act)
    {
        return new KeyValuePair<string, Action>(name, act);
    }
}
