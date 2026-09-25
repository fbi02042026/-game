#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 整理 CharacterUI.prefab 的层级：在根下新建铺满全屏的 Popups 容器，只把 SkillSelectUI 挪进去。
///
/// 【为什么做】
/// 原来 CharacterUI（根 Canvas）下面直接挂着 Content（主内容区，上下各缩 135）、
/// SkillSelectUI（全屏技能选择弹窗）、bg 三者平级，弹窗和主内容混在一起不好读也不好管。
/// 整理后：根 → Content（主内容） / Popups（全屏弹层，渲染在 Content 之上） / bg，
/// 以后再加全屏弹窗都往 Popups 里放即可。
///
/// 【为什么不能直接手改 .prefab】
/// .prefab 是 YAML 序列化资源，父子关系写在 m_Children（记的是 fileID）里，
/// 手改文本极易漏改或写坏引用（表现为组件丢失、整块 UI 变红、节点找不到）。
/// 所以这里一律走 PrefabUtility API：LoadPrefabContents 增量打开 → 改父节点 →
/// SaveAsPrefabAsset 写回同路径，由 Unity 自己维护 fileID / GUID / .meta。
///
/// 【挪动为什么是零位移】
/// SkillSelectUI 本身已经是 anchorMin(0,0) / anchorMax(1,1) / sizeDelta(0,0) 铺满父级，
/// 而新建的 Popups 同样铺满根，二者几何完全一致 → 换父级后自动铺满 Popups，坐标零变化。
/// 脚本因此刻意「只改父节点」，SkillSelectUI 的 anchorMin / anchorMax / sizeDelta /
/// anchoredPosition / pivot / localScale 一个都不碰。
///
/// 【BackpackPanel 也会一起挪，但位移会被算回来】
/// BackpackPanel 原本锚在 Content 底边（anchor[0.5, 0]），而 Content 的底边比根底边高 135px
/// （offsetMin.y = 150，因为 Content 是 anchor[0,0→1,1] + sizeDelta(0,-270) + pos(0,15)）。
/// 如果只换父节点不补偏移，它会直接掉 135px（还会掉到屏幕外）。
/// 所以本菜单会做一次「保持屏幕上位置不变」的换算：
///     面板底边在根坐标系下的 y = contentRt.offsetMin.y + panelRt.offsetMin.y
///     换父级后 panelRt.offsetMin.y 新值 = 上面这个值 − popupsRt.offsetMin.y
///     再按 offsetMin = anchoredPosition − sizeDelta × pivot 反解出新 anchoredPosition
/// 只在 panel 的 anchorMin.y == anchorMax.y（锚在父级底边/顶边）时成立，BackpackPanel 正好是。
/// 全程只写 BackpackPanel 的 anchoredPosition.y，它的 anchor / sizeDelta / pivot / 子节点一律不动。
///
/// 【幂等】
/// SkillSelectUI 已经在一个叫 Popups 的父节点下 → 弹「已经整理过了」并原样返回，不重复建容器、
/// 不重复挪。Popups 已存在则复用（只挪不建），不存在才新建。
/// BackpackPanel 已经在 Popups 下 → 跳过它（不重复补偏移，否则会越挪越高）。
///
/// 【注意事项】
/// 刻意不做工程级的 AssetDatabase.SaveAssets() —— 它会把内存里所有未保存资源一并落盘，
/// 远超本预制体的范围；SaveAsPrefabAsset 本身已经把这个 prefab 写到磁盘了，只调 Refresh()。
/// 另外：本脚本自身不修改任何 .prefab / .meta / .asset / .unity 文件，改层级必须在编辑器里跑这个菜单。
///
/// 【如何回退】
/// 1. Prefab 模式下把 SkillSelectUI 拖回根（CharacterUI）下面；
/// 2. 把 BackpackPanel 拖回 Content 下，并把它的 anchoredPosition.y 从 363 改回 213；
/// 3. 空的 Popups 直接删掉即可。
/// SkillSelectUI 是 anchor[0,0→1,1] 铺满，拖回根下同样自动铺满，坐标依旧零变化。
///
/// 菜单：Tools/_归档/UI/把背包与技能窗挪进 Popups
/// </summary>
public static class UiReparentSetup
{
    const string PrefabPath = "Assets/Resources/Prefabs/Town/CharacterUI.prefab";
    const string PopupsName = "Popups";
    const string SkillSelectName = "SkillSelectUI";
    const string ContentName = "Content";
    const string BackpackPanelName = "BackpackPanel";

    [MenuItem("Tools/_归档/UI/把背包与技能窗挪进 Popups")]
    public static void MoveSkillSelectIntoPopups()
    {
        bool go = EditorUtility.DisplayDialog(
            "整理 CharacterUI 层级",
            "要做什么：\n" +
            "· 在根 CharacterUI 下新建一个铺满全屏的容器 Popups（SetAsLastSibling，渲染在 Content 之上）\n" +
            "· 把 SkillSelectUI 挪进 Popups：仅改父节点，不动它任何几何（本来就铺满，零位移）\n" +
            "· 把 BackpackPanel 也挪进 Popups：会自动把 anchoredPosition.y 从 213 补成 363，\n" +
            "  保证它在屏幕上的位置一个像素都不动（不补的话会掉 135px）\n\n" +
            "目标预制体：\n" + PrefabPath,
            "执行", "取消");
        if (!go) return;   // 用户取消 → 什么也不做

        var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefabAsset == null)
        {
            Debug.LogWarning($"[UiReparentSetup] 找不到预制体 {PrefabPath}，未做任何修改。");
            EditorUtility.DisplayDialog("把技能窗挪进 Popups",
                $"找不到预制体：\n{PrefabPath}\n\n未做任何修改。", "OK");
            return;
        }

        // 增量打开原资源：绝不新建覆盖，主人摆好的 UI 才保得住。
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

        bool saved = false;
        string result = "未做任何修改。";
        string panelNote = "";   // 在 try 外声明，供后面的结果弹窗使用

        try
        {
            Transform skill = root.transform.Find(SkillSelectName);
            if (skill == null)
            {
                // 找不到就绝不新建：只警告 + 弹窗。
                Debug.LogWarning(
                    $"[UiReparentSetup] {PrefabPath} 根下没有叫“{SkillSelectName}”的直接子节点，未做任何修改。");
                EditorUtility.DisplayDialog("把技能窗挪进 Popups",
                    $"在 {PrefabPath} 根下没找到 {SkillSelectName}。\n\n" +
                    "未创建 Popups，未做任何修改。\n" +
                    "请在 Prefab 模式确认节点名后再跑一次。", "OK");
                return;
            }

            // BackpackPanel 原本在 Content 下（锚在 Content 底边）。
            Transform panel = root.transform.Find($"{ContentName}/{BackpackPanelName}");

            // 幂等：两个都已经在 Popups 下面了（或本来就没有 BackpackPanel）→ 直接收工。
            bool skillDone = skill.parent != null && skill.parent.name == PopupsName;
            bool panelDone = panel != null && panel.parent != null && panel.parent.name == PopupsName;
            if (skillDone && (panel == null || panelDone))
            {
                Debug.Log($"[UiReparentSetup] {SkillSelectName}（与 {BackpackPanelName}）已经在 {PopupsName} 下了，无需重复整理。");
                EditorUtility.DisplayDialog("把背包与技能窗挪进 Popups",
                    $"已经整理过了：{SkillSelectName} 和 {BackpackPanelName} 当前都在 {PopupsName} 下面。\n\n本次未做任何修改。", "OK");
                return;
            }

            // Popups 不存在才创建；已存在则复用。
            Transform popups = root.transform.Find(PopupsName);
            bool created = popups == null;
            if (created)
            {
                var popupsGo = new GameObject(PopupsName, typeof(RectTransform));
                popupsGo.transform.SetParent(root.transform, false);
                popups = popupsGo.transform;

                var rt = popupsGo.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.sizeDelta = new Vector2(0f, 0f);
                rt.anchoredPosition = new Vector2(0f, 0f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                popups.localScale = new Vector3(1f, 1f, 1f);

                // 排到最后 → 渲染在 Content 之上。
                popups.SetAsLastSibling();
            }

            var contentRt = root.transform.Find(ContentName) as RectTransform;
            var popupsRt = popups as RectTransform;

            // 1) SkillSelectUI：只改父节点（worldPositionStays=false），它自身几何一律不动。
            if (!skillDone) skill.SetParent(popups, false);

            // 2) BackpackPanel：换父级 + 把「底边位移」补回来，保证屏幕上位置不变。
            //    面板底边在根坐标系下的 y = Content.offsetMin.y + panel.offsetMin.y
            //    （这一步必须在 SetParent 之前读，不然 offsetMin 已经变成相对 Popups 的了）
            if (panel == null)
            {
                panelNote = $"没找到 {ContentName}/{BackpackPanelName}，跳过了它。";
            }
            else if (panelDone)
            {
                panelNote = $"{BackpackPanelName} 已经在 {PopupsName} 下了，跳过（不重复补偏移）。";
            }
            else
            {
                var panelRt = panel as RectTransform;
                float panelBottomInRoot = (panelRt != null ? panelRt.offsetMin.y : 0f)
                                        + (contentRt != null ? contentRt.offsetMin.y : 0f);
                panel.SetParent(popups, false);
                if (panelRt != null && popupsRt != null)
                {
                    float newOffsetMinY = panelBottomInRoot - popupsRt.offsetMin.y;
                    // offsetMin = anchoredPosition − sizeDelta × pivot  →  反解 anchoredPosition
                    panelRt.anchoredPosition = new Vector2(
                        panelRt.anchoredPosition.x,
                        newOffsetMinY + panelRt.sizeDelta.y * panelRt.pivot.y);
                    panelNote = $"{BackpackPanelName} 已挪入，anchoredPosition.y 从 213 补成 {panelRt.anchoredPosition.y:0.##}（屏幕上位置不变）。";
                }
                else
                {
                    panelNote = $"{BackpackPanelName} 已挪入，但拿不到 RectTransform，偏移未补。";
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            saved = true;
            result = (created ? "新建了 Popups，" : "复用了已有的 Popups，") +
                     $"并把 {SkillSelectName}、{BackpackPanelName} 挪进 {PopupsName}。";
            Debug.Log($"[UiReparentSetup] {PrefabPath}：{result} {panelNote}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        // 只刷新，绝不 SaveAssets()（那会把工程里所有未保存资源一并落盘）。
        AssetDatabase.Refresh();

        if (!saved) return;

        EditorUtility.DisplayDialog("把背包与技能窗挪进 Popups",
            "完成：" + result + "\n\n" +
            "· Popups：anchorMin(0,0) / anchorMax(1,1) / sizeDelta(0,0) 铺满全屏，已排到根节点最后（渲染在 Content 之上）\n" +
            $"· {SkillSelectName}：只改了父节点，anchor / sizeDelta / anchoredPosition / pivot / localScale 全部原样未动\n" +
            "· " + panelNote + "\n\n" +
            "回退：把 SkillSelectUI 拖回根下，把 BackpackPanel 拖回 Content 下并把 anchoredPosition.y 改回 213，再删掉空的 Popups。",
            "OK");
    }
}
#endif
