using System;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;

/// <summary>
/// 【2026-11-02 主人拍板「接上你说的那个没做的功能」】微信小游戏转发桥（C# 门面 → jslib → 微信官方 wx 接口）。
///
/// 这是「分享拉新」里**唯一零合规风险**的那一条：玩家自己从右上角菜单转发，项目不给任何奖励，
/// 不构成运营规范 2.5.1 的「利益诱导分享」。分享得钻那套已于同日整体下线（见 ShareService 类头）。
///
/// 设计（照 <see cref="WeChatAd"/> 同款桥接模式）：
/// · 仅 <c>UNITY_WECHATMINIGAME</c> 构建内走真实调用；编辑器/安卓构建 Available=false，调用直接返回。
/// · 启动时 <see cref="WeChatShareBootstrap"/> 自动 SetupShareMenu 一次（幂等），玩家右上角「...」→ 转发
///   出去的卡片带自定义标题 + 5:4 封面 + query 归因参数。
/// · 封面：优先取 <see cref="CoverResPath"/>（主人 2026-11-02 给的 5:4 宣传图 980×785）→ 降采样 500×400
///   → EncodeToJPG → jslib 写进 wx.env.USER_DATA_PATH → 该路径当 imageUrl。
///   取不到才退回 <see cref="ShareService.ShareImageResPath"/>（竖图 940×1672）居中裁 5:4 兜底。
///   任一步失败只 LogError，退化为「不传图」（微信用小游戏默认图），不阻塞启动。
/// · 主动转发 <see cref="Share"/> 备着（将来若要加「分享给好友」按钮走它），当前无入口。
///
/// 【口径】wx.shareAppMessage 的 success 只代表「调起了转发界面」——微信 2021 年后取消了「是否真的发出去」
/// 的判定，所以本类**只回传调起成败**，不做也不该做「分享成功发奖」。
/// </summary>
public static class WeChatShare
{
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

    /// <summary>转发归因参数（只用于统计来源，不发奖、不判定成功）。</summary>
    public const string DefaultQuery = "from=menu";

    /// <summary>
    /// 转发封面（5:4，主人 2026-11-02 提供的宣传图 980×785）。
    /// 微信转发卡片按 5:4 显示，用这张**不用裁**，只做降采样；取不到才退回竖图裁剪兜底。
    /// </summary>
    public const string CoverResPath = "UI/Art/分享/分享封面5x4";

    /// <summary>
    /// 打开右上角菜单转发 + 设置卡片内容（幂等：重复调只更新标题/query）。
    /// title 建议直接传 <see cref="ShareService.ShareText"/>（单一出处，文案不带任何奖励字样）。
    /// </summary>
    public static void SetupShareMenu(string title, string query)
    {
        if (!Available) return;
#if UNITY_WECHATMINIGAME
        try
        {
            PrepareCoverOnce();
            if (WXShareSetupMenu(title ?? "", query ?? "") == 0)
                Debug.LogError("[WeChatShare] 转发菜单设置失败（jslib 返回 0，看控制台 console.error）");
        }
        catch (Exception e)
        {
            Debug.LogError("[WeChatShare] 设置转发菜单异常: " + e);
        }
#endif
    }

    /// <summary>
    /// 主动拉起转发（有按钮入口时才用；当前项目无入口）。
    /// onDone(true) 只代表「转发界面调起成功」，不代表玩家发出去了。
    /// </summary>
    public static void Share(Action<bool> onDone)
    {
        if (!Available)
        {
            onDone?.Invoke(false);
            return;
        }
#if UNITY_WECHATMINIGAME
        try
        {
            _pending = onDone;
            IntPtr cb = Marshal.GetFunctionPointerForDelegate((Action<int>)OnShareDone);
            WXShareAppMessage(cb);
        }
        catch (Exception e)
        {
            Debug.LogError("[WeChatShare] 主动转发异常: " + e);
            _pending = null;
            onDone?.Invoke(false);
        }
#endif
    }

#if UNITY_WECHATMINIGAME
    [DllImport("__Internal")]
    private static extern int WXShareSetupMenu(string title, string query);

    [DllImport("__Internal")]
    private static extern int WXShareSetCover(IntPtr data, int length);

    [DllImport("__Internal")]
    private static extern void WXShareAppMessage(IntPtr onDone);

    [MonoPInvokeCallback(typeof(Action<int>))]
    private static void OnShareDone(int ok)
    {
        var cb = _pending;
        _pending = null;
        try { cb?.Invoke(ok == 1); }
        catch (Exception e) { Debug.LogError("[WeChatShare] 转发回调异常: " + e); }
    }

    [NonSerialized]
    private static Action<bool> _pending;

    static bool _coverTried;

    /// <summary>封面只准备一次（图不会变；每次启动重写一遍沙箱文件）。失败不重试，避免反复编码拖启动。</summary>
    static void PrepareCoverOnce()
    {
        if (_coverTried) return;
        _coverTried = true;

        // 【2026-11-02】优先用主人给的 5:4 宣传图（980×785，本来就是 5:4，不用裁）
        var tex = Resources.Load<Texture2D>(CoverResPath);
        if (tex == null)
        {
            Debug.LogWarning("[WeChatShare] 5:4 封面取不到：Resources/" + CoverResPath + "，退回竖图裁剪");
            tex = Resources.Load<Texture2D>(ShareService.ShareImageResPath);
        }
        if (tex == null)
        {
            Debug.LogError("[WeChatShare] 转发封面一张都取不到（" + CoverResPath + " / " +
                           ShareService.ShareImageResPath + "），转发卡片将用微信默认图");
            return;
        }

        byte[] jpg;
        try
        {
            jpg = BuildFiveByFourJpg(tex);
        }
        catch (Exception e)
        {
            Debug.LogError("[WeChatShare] 封面裁成 5:4 / 编码失败（meta isReadable 没开？）：" + e.Message);
            return;
        }
        if (jpg == null || jpg.Length == 0)
        {
            Debug.LogError("[WeChatShare] 封面编码结果为空，转发卡片将用微信默认图");
            return;
        }

        IntPtr ptr = Marshal.AllocHGlobal(jpg.Length);
        try
        {
            Marshal.Copy(jpg, 0, ptr, jpg.Length);
            if (WXShareSetCover(ptr, jpg.Length) == 0)
                Debug.LogError("[WeChatShare] 封面写微信沙箱失败，转发卡片将用微信默认图");
        }
        catch (Exception e)
        {
            Debug.LogError("[WeChatShare] 封面传给 jslib 失败: " + e.Message);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    /// <summary>
    /// 居中裁成 5:4 再编码 JPG。微信转发卡片显示比例固定 5:4，
    /// 直接把 940×1672 的竖图传上去会被横切掉大半，所以先裁再传。
    /// 裁完再降采样到 <see cref="CoverTargetWidth"/>（微信卡片实际显示很小，500×400 足够，
    /// 顺带把启动时的编码开销从 ~300ms 压到几十 ms）。
    /// </summary>
    static byte[] BuildFiveByFourJpg(Texture2D src)
    {
        int w = src.width;
        int h = Mathf.RoundToInt(w * 4f / 5f);   // 以宽为基准算高
        if (h > src.height)                       // 图太矮 → 改以高为基准算宽
        {
            h = src.height;
            w = Mathf.RoundToInt(h * 5f / 4f);
        }
        int x0 = Mathf.Max(0, (src.width - w) / 2);
        int y0 = Mathf.Max(0, (src.height - h) / 2);

        // 一次读回整块（逐像素 GetPixel 会反复 GPU 回读，很慢）
        var block = src.GetPixels(x0, y0, w, h);

        int step = Mathf.Max(1, Mathf.RoundToInt(w / (float)CoverTargetWidth));
        int outW = Mathf.Max(1, w / step);
        int outH = Mathf.Max(1, h / step);
        var dst = new Color32[outW * outH];
        for (int j = 0; j < outH; j++)
        {
            int rowSrc = (j * step) * w;
            int rowDst = j * outW;
            for (int i = 0; i < outW; i++)
                dst[rowDst + i] = block[rowSrc + i * step];
        }

        var outTex = new Texture2D(outW, outH, TextureFormat.RGB24, false);
        try
        {
            outTex.SetPixels32(dst);
            outTex.Apply();
            return outTex.EncodeToJPG(85);
        }
        finally
        {
            UnityEngine.Object.Destroy(outTex);
        }
    }

    /// <summary>转发封面目标宽度（5:4 → 500×400，微信官方示例尺寸）。</summary>
    const int CoverTargetWidth = 500;
#endif
}

/// <summary>
/// 转发桥启动接线（2026-11-02）：游戏起来就把右上角菜单转发打开、卡片内容设好。
/// 用 <c>RuntimeInitializeOnLoadMethod</c> 而不是塞进某个界面 —— 与登录/主城/战斗场景解耦，
/// 玩家在任何界面点右上角转发都能拿到同一张卡片。幂等，重复调用无副作用。
/// </summary>
public static class WeChatShareBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        if (!WeChatShare.Available) return;
        // 文案单一出处 = ShareService.ShareText（改文案只改那一处；该文案不含任何奖励字样，合规）
        WeChatShare.SetupShareMenu(ShareService.ShareText, WeChatShare.DefaultQuery);
    }
}
