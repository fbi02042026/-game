using System;
using UnityEngine;

/// <summary>
/// 激励视频统一门面 + 双通道判定（2026-11-25 主人拍板「双通道 · 先广告后钻石」；
/// 【2026-11-25 追加拍板】本项目是<b>安卓端</b>，微信端先不管——微信实现在 <see cref="WeChatAd"/>
/// 里原样保留，但本项目不启用、不填它的广告位 id）。
///
/// 定位：业务点位只问一句 <see cref="Ready"/>，不再各自抄一遍「平台闸 / 远程急停 / 有没有实现」。
/// 拉起仍然走 <see cref="RewardedAdBridge.ShowRewarded"/> 唯一出口，本类不改它、也不绕过它。
///
/// 双通道口径（本项目统一）：
/// · <see cref="Ready"/>=true  → 先走广告（免费）；
/// · Ready=false              → 原样走钻石（等于改动前的线上行为，**钻石通道永远保留**）。
/// 即：广告接不通时行为与现在 100% 一致，接得通时白送一条免费路，两头都不会把玩家卡死。
///
/// 一键开放（接安卓广告 SDK 时只改一处，业务代码零改动）：
/// 把 <see cref="PlatformCanPlay"/> 正式包分支的 <c>return false;</c> 换成
/// 「新 SDK 的就绪判断（初始化完成 + 广告位 id 非空）」一行即可 ——
/// <see cref="Ready"/> 自动翻 true，所有接了本门面的点位长出广告通道。
/// 临时全项目关广告：把 <see cref="Enabled"/> 置 false（运行期，不用重新出包）。
///
/// 平台边界（当前状态）：
/// 安卓正式包还没有真广告实现（TapADN 已拆除、微信端暂不启用）→ 正式包恒 false，
/// 点位保持纯钻石，绝不会出现「按钮写着看广告却永远拉不起来」。
/// 编辑器 / 开发包放行，走 <see cref="RewardedAdBridge"/> 的本地模拟，方便本地把整条链路验通。
/// </summary>
public static class AdGateway
{
    /// <summary>
    /// 广告业务总开关（运行期，改它不用重新出包）。false = 全项目立刻回纯钻石口径，点位一行都不用改。
    /// 注意与 <c>GameConfig.ADS_ENABLED</c> 分工：那个管「档位结构」（2免费→2广告→50/100钻），
    /// 是编译期常量；本开关只管「广告此刻能不能播」，两者互不替代。
    /// </summary>
    public static bool Enabled = true;

    /// <summary>点位名：佣兵招募刷新（后台按 placement 分桶统计）。</summary>
    public const string PlacementMercRefresh = "merc_refresh";

    /// <summary>点位名：背包格子解锁。</summary>
    public const string PlacementBagCell = "backpack_cell";

    /// <summary>
    /// 当前运行环境是否真的能播出一条激励视频（不含业务开关与远程急停，只判「有没有实现」）。
    /// </summary>
    static bool PlatformCanPlay
    {
        get
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 编辑器 / 开发包：RewardedAdBridge 走本地模拟（回调 true），链路可本地验通。
            // 口径与 RewardedAdBridge 内部的 #if UNITY_EDITOR || DEVELOPMENT_BUILD 保持一致。
            return true;
#else
            // 安卓正式包：真广告 SDK 尚未接入（TapADN 已拆除、微信端本项目暂不启用），
            // 恒 false = 正式包所有广告位保持纯钻石，与线上行为完全一致。
            // 【接广告时只改这里】SDK 接好后把 return false 换成新桥的就绪判断，例如：
            //   return TapAdBridge.IsReady;（或新 SDK 的「已初始化 && 广告位 id 非空」）
            // 改这一行 = 全项目广告通道一键开放，业务点位零改动。
            return false;
#endif
        }
    }

    /// <summary>
    /// 此刻能不能走广告通道。false = 业务必须原样走钻石（现状口径）。
    /// 四道闸全过才算 Ready：业务总开关 / 平台有实现 / 广告桥平台闸 / 远程急停。
    /// </summary>
    public static bool Ready
        => Enabled
        && PlatformCanPlay
        && !RewardedAdBridge.PlatformClosed
        && !AdKillSwitch.AdsOff;

    /// <summary>
    /// 拉起一次激励视频。
    /// · Ready=false → 直接回调 false，不弹任何提示（调用方自己决定走钻石通道，不要在这里抢话）；
    /// · Ready=true  → 先给「广告加载中…」反馈（拉起到展示可达数十秒，无提示会被当成没反应而狂点），
    ///   再走 <see cref="RewardedAdBridge.ShowRewarded"/>；重复点击由桥内 busy 守卫静默忽略。
    /// 回调保证至多一次（透传桥内的一次性包装）。
    /// </summary>
    public static void TryWatch(string placement, Action<bool> onDone)
    {
        if (!Ready)
        {
            // 「没接通」不算故障：埋点区分开，别和「玩家中途关掉」混在一个桶里
            Analytics.AdFail(placement, "not_ready");
            onDone?.Invoke(false);
            return;
        }

        UIManager.Instance?.ShowToast("广告加载中…");
        RewardedAdBridge.ShowRewarded(placement, onDone);
    }
}
