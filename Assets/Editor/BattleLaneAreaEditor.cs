using UnityEditor;
using UnityEngine;

/// <summary>战斗可行走区标记：创建/显示，供缩放调上下界。</summary>
public static class BattleLaneAreaEditor
{
    [MenuItem("Tools/战斗/确保 BattleLaneArea")]
    public static void EnsureArea()
    {
        var bm = Object.FindObjectOfType<BattleManager>();
        Transform root = bm != null ? bm.unitRoot : null;
        if (root == null)
        {
            var u = GameObject.Find("unit") ?? GameObject.Find("Unit") ?? GameObject.Find("unitRoot");
            root = u != null ? u.transform : null;
        }
        BattleLaneBounds.EnsureInScene(root, hideVisualInPlay: false);
        BattleLaneBounds.SetVisualVisible(true);
        var area = GameObject.Find(BattleLaneBounds.AreaName);
        if (area != null)
        {
            Selection.activeGameObject = area;
            EditorGUIUtility.PingObject(area);
        }
        EditorUtility.DisplayDialog("BattleLaneArea",
            "已确保场景中有 BattleLaneArea。\n在 Scene 视图缩放高度即可调玩家/怪上下站位范围。\nPlay 时 AutoInit 会默认隐藏半透明显示（逻辑边界仍生效）。",
            "好的");
    }

    [MenuItem("Tools/战斗/显示 BattleLaneArea")]
    public static void ShowArea()
    {
        BattleLaneBounds.SetVisualVisible(true);
    }

    [MenuItem("Tools/战斗/隐藏 BattleLaneArea")]
    public static void HideArea()
    {
        BattleLaneBounds.SetVisualVisible(false);
    }
}
