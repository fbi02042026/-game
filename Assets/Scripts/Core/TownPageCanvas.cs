using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 城镇功能页 Canvas 统一入口：Screen Space - Camera、720×1280。
/// 嵌在大厅下且 stripCanvasWhenNested 时走父 Canvas（酒馆/冒险/角色）；
/// 日志等需要盖住大厅的页保留自己的 Canvas，但仍绑定同一摄像机。
/// </summary>
public static class TownPageCanvas
{
    public static void Configure(GameObject page, int sortingOrder = GameConfig.UiSort.TownPage, bool stripCanvasWhenNested = true)
    {
        if (page == null) return;

        GuildHallUI hall = page.GetComponentInParent<GuildHallUI>();
        bool nested = hall != null && hall.gameObject != page;

        if (nested && stripCanvasWhenNested)
        {
            var raycaster = page.GetComponent<GraphicRaycaster>();
            if (raycaster != null) Object.Destroy(raycaster);
            var scaler = page.GetComponent<CanvasScaler>();
            if (scaler != null) Object.Destroy(scaler);
            var own = page.GetComponent<Canvas>();
            if (own != null) Object.Destroy(own);
            UICanvasSetup.ApplyOn(hall.gameObject, Camera.main);
            return;
        }

        Canvas canvas = page.GetComponent<Canvas>();
        if (canvas == null)
            canvas = page.AddComponent<Canvas>();
        canvas.enabled = true;
        UICanvasSetup.ApplyPopup(canvas, sortingOrder, Camera.main);
        if (page.GetComponent<GraphicRaycaster>() == null)
            page.AddComponent<GraphicRaycaster>();
    }
}
