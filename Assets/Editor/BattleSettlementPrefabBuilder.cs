#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 生成战斗结算（撤离/死亡统计）预制体：Resources/Prefabs/Battle/BattleSettlement.prefab
/// </summary>
public static class BattleSettlementPrefabBuilder
{
    const string PrefabAssetPath = "Assets/Resources/Prefabs/Battle/BattleSettlement.prefab";

    [MenuItem("Tools/UI/生成战斗结算界面预制体")]
    public static void Build()
    {
        if (File.Exists(PrefabAssetPath))
        {
            bool ok = EditorUtility.DisplayDialog("战斗结算界面",
                "已存在预制体：\n" + PrefabAssetPath +
                "\n\n覆盖会丢掉你在 Inspector 里换过的图和位置。\n确定重新生成吗？",
                "覆盖重新生成", "取消");
            if (!ok) return;
        }
        BuildBatch();
        var saved = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAssetPath);
        Selection.activeObject = saved;
        EditorGUIUtility.PingObject(saved);
        EditorUtility.DisplayDialog("战斗结算界面",
            "已生成：\n" + PrefabAssetPath +
            "\n\n对齐 GDD §11.3：击杀/伤害/时间 + 金币天赋装备等。\n" +
            "换图：Panel / ConfirmButton；文案：Title / Subtitle / Stats / Rewards",
            "好的");
    }

    public static void BuildBatch()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabAssetPath));
        var host = new GameObject("BattleSettlement", typeof(RectTransform));
        try
        {
            BattleSettlementUI.BuildHierarchy(host);
            PrefabUtility.SaveAsPrefabAsset(host, PrefabAssetPath);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }
}
#endif
