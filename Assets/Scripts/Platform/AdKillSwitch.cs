using System;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 远程广告急停开关（2026-10-28 主人拍板，商业化保险丝）。
/// 数据源 = Gitee 仓库里的 <c>ads.json</c>（主人手机/电脑改文件即全网生效，无需发版）：
/// <code>
/// {"ads_off": false}   正常开广告（默认；文件没配/URL 未填也按此）
/// {"ads_off": true}    急停：RewardedAdBridge 拒绝所有广告，玩家点广告档收「广告维护中」，
///                      免费/钻石档不受影响；埋点 ad_fail(reason=remote_disabled)
/// </code>
/// 行为铁律：
/// 1. URL 未配置（待填）→ 只读本地缓存，不报错（未接入 ≠ 故障），默认开广告；
/// 2. 拉取失败（无网/超时/解析失败）→ 保持现状（默认或上次缓存），绝不因配置通道故障关广告；
/// 3. 结果写 PlayerPrefs 缓存 —— 急停通常持续数小时以上，玩家重启游戏后依然生效；
/// 4. 后台异步拉，不阻塞启动；每次启动拉一次（急停粒度 = 对下次启动的玩家生效，已够用）。
/// </summary>
public static class AdKillSwitch
{
    /// <summary>ads.json 的 Gitee raw 直链（仓库→文件→「原始数据」复制）。
    /// 【2026-10-28 已接入】仓库 https://gitee.com/fbi0204/xiangsumaoxianliexizhiren（公开）；
    /// 急停 = 网页编辑该文件改 {"ads_off": true} 提交，对下次启动的玩家生效；改回 false 恢复。
    /// 注：gitee.com/raw 会 302 到 raw.giteeusercontent.com 签名 CDN 链接，UnityWebRequest 默认跟随重定向。</summary>
    public const string ConfigUrl = "https://gitee.com/fbi0204/xiangsumaoxianliexizhiren/raw/master/ads.json";

    const string CacheKey = "AdKillSwitch.AdsOff";
    const int TimeoutSeconds = 8;

    /// <summary>当前是否急停（同步读；启动早期 = 上次缓存值，后台拉取完成后更新为最新值）。</summary>
    public static bool AdsOff { get; private set; }

    static bool _started;

    /// <summary>发起后台拉取（幂等）。登录界面出现时调一次；RewardedAdBridge 入口兜底再调。</summary>
    public static void EnsureStarted()
    {
        if (_started) return;
        _started = true;
        AdsOff = PlayerPrefs.GetInt(CacheKey, 0) == 1;
        if (string.IsNullOrEmpty(ConfigUrl))
        {
            Debug.Log($"[AdKillSwitch] ConfigUrl 未配置，按缓存口径运行 adsOff={AdsOff}（急停通道待接入）");
            return;
        }
        Pull();
    }

    static void Pull()
    {
        var req = UnityWebRequest.Get(ConfigUrl);
        req.timeout = TimeoutSeconds;
        var op = req.SendWebRequest();
        op.completed += _ =>
        {
            // SendWebRequest 的 completed 回调在主线程帧更新中执行，无协程宿主依赖
            try
            {
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[AdKillSwitch] 拉取失败（保持现状 adsOff={AdsOff}）：{req.error}");
                    return;
                }
                var cfg = JsonUtility.FromJson<RemoteConfig>(req.downloadHandler.text);
                AdsOff = cfg != null && cfg.ads_off;
                PlayerPrefs.SetInt(CacheKey, AdsOff ? 1 : 0);
                PlayerPrefs.Save();
                Debug.Log($"[AdKillSwitch] 远程配置拉取成功 adsOff={AdsOff}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AdKillSwitch] 配置解析失败（保持现状 adsOff={AdsOff}）：{e.Message}");
            }
            finally
            {
                req.Dispose();
            }
        };
    }

    [Serializable]
    sealed class RemoteConfig
    {
        public bool ads_off;
    }
}
