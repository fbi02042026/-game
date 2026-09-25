#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 生成战斗结算预制体：Resources/Prefabs/Battle/BattleSettlement.prefab
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
            "\n\n节点：PortraitHost / StatRow_* / RewardsGrid / ConfirmButton\n" +
            "美术：Assets/Art/UI/战斗结算（Texture Type=Sprite 后重跑可自动挂图标）\n" +
            "通关/撤离/阵亡都会弹出；无战斗时长、无经验格。",
            "好的");
    }

    /// <summary>批处理：-executeMethod BattleSettlementPrefabBuilder.BuildBatch</summary>
    public static void BuildBatch()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabAssetPath));
        var host = new GameObject("BattleSettlement", typeof(RectTransform));
        try
        {
            BattleSettlementUI.BuildHierarchy(host);
            PrefabUtility.SaveAsPrefabAsset(host, PrefabAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[BattleSettlement] 已生成：" + PrefabAssetPath);
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }
}
#endif
