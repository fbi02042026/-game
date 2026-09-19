using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 佣兵本命碎片（分层模板）动态绑图。
/// 配套预制体 Assets/Resources/Prefabs/other/yongbingsuipian.prefab（结构不得改动）：
///   yongbingsuipian (root)
///   ├── suipianicon (Image = 碎片稀有度底图)
///   └── mask (Image = 碎片图 + Mask 组件；用碎片图 alpha 裁 HeadIcon)
///       └── HeadIcon (Image = 佣兵头像)
/// 换稀有度时必须同时换 suipianicon 与 mask 两处的 sprite，否则遮罩形状仍是旧档位。
/// </summary>
public class MercBirthFragmentView : MonoBehaviour
{
    /// <summary>佣兵 id（H001~H022；C001~C003 也有头像）。</summary>
    public string MercId { get; private set; }
    /// <summary>稀有度档位：0=普通 / 1=稀有 / 2=传奇。</summary>
    public int Tier { get; private set; }

    /// <summary>
    /// 绑定碎片底图（按稀有度）与佣兵头像（按 mercId）。
    /// 加载失败保留默认图并打 LogWarning，绝不置 null 变白块。
    /// </summary>
    public void Bind(string mercId, int tier)
    {
        MercId = mercId;
        Tier = Mathf.Clamp(tier, 0, 2);

        string tierName = Tier == 0 ? "普通" : (Tier == 1 ? "稀有" : "传奇");

        Transform fragTf = transform.Find("suipianicon");
        Transform maskTf = transform.Find("mask");
        Transform headTf = maskTf != null ? maskTf.Find("HeadIcon") : null;
        if (headTf == null) headTf = transform.Find("HeadIcon"); // 兜底：HeadIcon 也可能直接在 root 下

        Image fragImg = fragTf != null ? fragTf.GetComponent<Image>() : null;
        Image maskImg = maskTf != null ? maskTf.GetComponent<Image>() : null;
        Image headImg = headTf != null ? headTf.GetComponent<Image>() : null;

        if (fragImg == null || maskImg == null || headImg == null)
        {
            Debug.LogWarning("[MercBirthFragment] 预制体结构异常：缺 suipianicon / mask / HeadIcon 节点，跳过绑定");
            return;
        }

        // 碎片稀有度底图：只换 suipianicon；mask 的图不动（主人 2026-09-19 拍板）
        Sprite fragSprite = Resources.Load<Sprite>(ContentPaths.Icons.MercFragmentBase + "/" + tierName + "碎片");
        if (fragSprite != null)
        {
            fragImg.sprite = fragSprite;
        }
        else
        {
            Debug.LogWarning("[MercBirthFragment] 找不到碎片底图：" + tierName + "碎片（保留默认图）");
        }

        // 佣兵头像
        if (!string.IsNullOrEmpty(mercId))
        {
            Sprite headSprite = Resources.Load<Sprite>(ContentPaths.Icons.MercHead + "/" + mercId);
            if (headSprite != null)
            {
                headImg.sprite = headSprite;
            }
            else
            {
                Debug.LogWarning("[MercBirthFragment] 找不到佣兵头像：" + mercId + "（保留默认图）");
            }
        }
    }
}

/// <summary>
/// 工厂：实例化 yongbingsuipian 预制体并绑定（佣兵 × 稀有度）。
/// </summary>
public static class MercBirthFragmentFactory
{
    const string PrefabPath = "Prefabs/other/yongbingsuipian";

    /// <summary>
    /// 实例化碎片预制体到 parent 下并绑定。失败返回 null（不抛）。
    /// </summary>
    public static GameObject Create(string mercId, int tier, Transform parent)
    {
        GameObject prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("[MercBirthFragment] 找不到预制体：" + PrefabPath);
            return null;
        }

        GameObject go = Object.Instantiate(prefab, parent);
        if (go == null) return null;

        var view = go.GetComponent<MercBirthFragmentView>();
        if (view == null) view = go.AddComponent<MercBirthFragmentView>();
        view.Bind(mercId, tier);
        return go;
    }
}
