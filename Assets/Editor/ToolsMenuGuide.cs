using UnityEditor;

/// <summary>Tools 菜单精简说明（仅软著阶段常用项留在顶层）。</summary>
public static class ToolsMenuGuide
{
    [MenuItem("Tools/菜单说明（软著精简）")]
    public static void Show()
    {
        EditorUtility.DisplayDialog("Tools 精简说明",
            "【常用】\n" +
            "· 软著/导出源程序鉴别材料\n" +
            "· 自检/主路径验收清单\n" +
            "· 自检/存档 JsonUtility 往返\n" +
            "· Build/Android Dev|Release APK\n" +
            "· 流程/设为从 Boot 启动\n" +
            "· 装备/补全 spumName\n" +
            "· UI/检查恢复关弹窗预制体\n" +
            "· 生成角色注册表 / 检查游戏 Icon\n\n" +
            "【其他】已挪到 Tools/_归档（脚本未删，需要时仍可点）。\n" +
            "覆盖预制体类菜单请谨慎，先问再点。",
            "好的");
    }
}
