#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>扫描剧情立绘 Resources 是否齐全。</summary>
public static class StoryPortraitCheckTool
{
    [MenuItem("Tools/UI/检查剧情立绘缺失")]
    public static void CheckMissingPortraits()
    {
        var ids = StoryPortraitLayout.TutorialCastIds;
        int missing = 0;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("剧情立绘检查 (佣兵立绘 / MercStand):");
        for (int i = 0; i < ids.Length; i++)
        {
            string id = ids[i];
            var sp = StoryPortraits.Get(id);
            if (sp == null)
            {
                missing++;
                sb.AppendLine("  MISSING: " + id);
            }
            else
                sb.AppendLine("  OK: " + id + " (" + sp.name + ")");
        }
        Debug.Log(sb.ToString());
        EditorUtility.DisplayDialog("剧情立绘",
            missing > 0 ? "缺失 " + missing + " 张，见 Console。" : "TutorialCast 全部就绪。",
            "OK");
    }
}
#endif
