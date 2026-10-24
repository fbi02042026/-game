using UnityEngine;

// 角色页 partial —— 2026-10-24 主人拍板（二次）：不再页内克隆缩放条，改用全局唯一资源条。
// 页顶 金币/钻石/天赋石 = ResourceStrip（提取自主界面顶条的原生列，见 ResourceStrip.cs），
// ShowPage 显示、HidePage 归还主界面；数值由 ResourceStrip 内部轮询统一刷，本页不再自管。
public partial class CharacterUI
{
    void EnsureResBar()
    {
        // 旧方案（页内克隆 0.78 缩放条）的一次性清理：老会话残留节点直接销毁
        var legacy = transform.Find("CharResBar");
        if (legacy != null) Destroy(legacy.gameObject);

        ResourceStrip.Show(transform, ResourceStrip.Mode.GoldDiaTalent);
    }
}
