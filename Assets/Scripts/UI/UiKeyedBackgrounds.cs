using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 城镇/日志等多背景图统一管理：按 key 从 Resources 换图，避免各页各自硬编码路径。
/// 约定目录：
///   Resources/UI/AdventureLog/插图/{主线|支线|怪物|佣兵|成就|世界}
///   Resources/UI/AdventureLog/Frames/{内容底|普通边框|boss边框|...}
///   Resources/UI/Adventure/{章节背景等，可扩展}
/// </summary>
public static class UiKeyedBackgrounds
{
    public const string AdventureLogIllust = "UI/AdventureLog/插图";
    public const string AdventureLogTabIcons = "UI/AdventureLog/TabIcons";
    public const string AdventureLogFrames = "UI/AdventureLog/Frames";
    public const string AdventurePages = "UI/Adventure";

    static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

    public static Sprite Load(string resourcesFolder, string fileNameNoExt)
    {
        if (string.IsNullOrEmpty(resourcesFolder) || string.IsNullOrEmpty(fileNameNoExt))
            return null;
        string key = resourcesFolder + "/" + fileNameNoExt;
        if (Cache.TryGetValue(key, out var hit) && hit != null)
            return hit;

        var sp = Resources.Load<Sprite>(key);
        if (sp == null)
        {
            var tex = Resources.Load<Texture2D>(key);
            if (tex != null)
                sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f), 100f);
        }
        if (sp != null) Cache[key] = sp;
        return sp;
    }

    public static Sprite LogTabIllust(string tabName) =>
        Load(AdventureLogIllust, tabName);

    /// <summary>侧栏 Tab 小图标（怪物-1 / 佣兵-1 规格）。</summary>
    public static Sprite LogTabSidebarIcon(string tabName) =>
        Load(AdventureLogTabIcons, tabName);

    public static Sprite LogFrame(string fileNameNoExt) =>
        Load(AdventureLogFrames, fileNameNoExt);

    /// <summary>把 Image 换成指定资源；找不到则保持原样。</summary>
    public static bool Apply(Image target, string resourcesFolder, string fileNameNoExt,
        bool preserveAspect = true)
    {
        if (target == null) return false;
        var sp = Load(resourcesFolder, fileNameNoExt);
        if (sp == null) return false;
        target.sprite = sp;
        target.color = Color.white;
        target.enabled = true;
        target.preserveAspect = preserveAspect;
        return true;
    }

    // 2026-11-01 主人报「预制体我已经是拉宽的，但是你代码给我改了」：旧版走 Apply 默认
    // 强制 preserveAspect=true，每次切页签都覆盖预制体里的比例设置，宽框里的图被等比压窄。
    // 改为只换图/颜色，preserveAspect 一律按预制体原样（「以预制体为准」）。
    public static bool ApplyLogTabIllust(Image target, string tabName)
    {
        if (target == null) return false;
        var sp = Load(AdventureLogIllust, tabName);
        if (sp == null) return false;
        target.sprite = sp;
        target.color = Color.white;
        target.enabled = true;
        return true;
    }

    public static bool ApplyLogFrame(Image target, string fileNameNoExt, bool preserveAspect = false) =>
        Apply(target, AdventureLogFrames, fileNameNoExt, preserveAspect);
}
