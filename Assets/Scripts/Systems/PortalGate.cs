using System.Collections;
using UnityEngine;

/// <summary>
/// 传送门 = 主人 2026-10-11 新加的预制体 <c>Resources/VFX/other/world/chuansongmen</c>（挂 BattleUI 下）。
/// <para>主人原话：「先播放传送节点 再 0.3 秒后剩下的两个节点一起出来 这就是传送门，
/// 让玩家走进这个节点中间位置 然后再播放传送节点的特效 就可以黑屏出现关卡界面了；
/// 注意佣兵也要跟着一起进，可以间隔一个身位，佣兵进也要播放传送特效」。</para>
///
/// <para>预制体三个子节点：<c>传送</c>（先出）／<c>door</c>（门皮）／<c>SpinPortalBlue</c>（旋转粒子，后两个一起出）。</para>
///
/// <para>⚠ 门是 <b>UI 节点</b>（RectTransform），但它代表的是世界里的一个位置：
/// 开门那一刻把它的屏幕位置换算成世界 X 并<b>钉死</b>，之后镜头再跟英雄走，门也钉在那儿不动
/// （不钉的话门跟着屏幕漂，英雄永远追不上）。</para>
/// </summary>
public class PortalGate : MonoBehaviour
{
    public const string PrefabPath = "VFX/other/world/chuansongmen";
    const string EnterVfxPath = "VFX/other/world/传送";

    /// <summary>门皮与旋转粒子比「传送」节点晚出来的时间（秒）。【2026-10-11 主人拍板 0.3s】</summary>
    public const float SecondWaveDelay = 0.3f;
    /// <summary>进门的判定余量（世界单位）：走到门中心 ±这个值就算进去了。</summary>
    public const float EnterTolerance = 0.3f;

    RectTransform _rt;
    GameObject _teleportNode;   // 传送
    GameObject _doorNode;       // door
    GameObject _spinNode;       // SpinPortalBlue

    bool _pinned;
    float _worldX;
    float _worldY;

    /// <summary>门是否已经完整出来（传送 + 门皮 + 粒子）。</summary>
    public bool Opened { get; private set; }

    /// <summary>门中心的世界 X —— 单位走到这个 X 就算进门。没钉住时会再试一次（镜头晚一帧就绪的情况）。</summary>
    public float WorldEnterX
    {
        get
        {
            if (!_pinned) PinToCurrentWorldPos();
            return _pinned ? _worldX : float.NaN;
        }
    }

    // ============================================================
    // 开门
    // ============================================================

    public static PortalGate Open(Transform uiParent)
    {
        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[PortalGate] 找不到传送门预制体 Resources/{PrefabPath} —— 通关传送门开不出来");
            return null;
        }
        if (uiParent == null)
        {
            Debug.LogError("[PortalGate] 传送门没有挂点（BattleUI 为空），门无处可挂");
            return null;
        }

        var go = Instantiate(prefab, uiParent, false);
        go.name = "PortalGate";
        var gate = go.GetComponent<PortalGate>() ?? go.AddComponent<PortalGate>();
        gate.Bind();
        gate.PinToCurrentWorldPos();
        gate.StartCoroutine(gate.CoOpen());
        // 挂最后：不被同层 UI 盖住（父层里的嵌套 Canvas 另算，它们走自己的 sortingOrder）
        go.transform.SetAsLastSibling();
        return gate;
    }

    void Bind()
    {
        _rt = transform as RectTransform;
        for (int i = 0; i < transform.childCount; i++)
        {
            var c = transform.GetChild(i);
            if (c == null) continue;
            if (c.name == "传送") _teleportNode = c.gameObject;
            else if (c.name == "door") _doorNode = c.gameObject;
            else if (c.name == "SpinPortalBlue") _spinNode = c.gameObject;
        }
        // 三个节点的显隐**只由本类管**（铁律 15：同一语义只留一个出口），开门前先全关。
        SetNode(_teleportNode, false);
        SetNode(_doorNode, false);
        SetNode(_spinNode, false);
    }

    IEnumerator CoOpen()
    {
        // ① 传送节点先出来
        if (_teleportNode == null)
            Debug.LogError("[PortalGate] 预制体里找不到「传送」节点，门开不出来");
        else
            SetNode(_teleportNode, true);

        // ② 0.3s 后门皮 + 旋转粒子一起出来
        yield return new WaitForSecondsRealtime(SecondWaveDelay);

        if (_doorNode == null)
            Debug.LogError("[PortalGate] 预制体里找不到 door 节点");
        else
            SetNode(_doorNode, true);

        if (_spinNode == null)
            Debug.LogError("[PortalGate] 预制体里找不到 SpinPortalBlue 节点");
        else
            SetNode(_spinNode, true);

        Opened = true;
        Debug.Log($"[PortalGate] 传送门开好：世界X={_worldX:F2} 节点 传送/door/SpinPortalBlue 依次就位");
    }

    static void SetNode(GameObject go, bool on)
    {
        if (go != null) go.SetActive(on);
    }

    // ============================================================
    // 把「屏幕上的一个门」钉到世界坐标
    // ============================================================

    void PinToCurrentWorldPos()
    {
        var cam = Camera.main;
        if (cam == null || _rt == null) return;
        // 刚实例化出来时 RectTransform 的世界位置还没算，先强制刷一次布局再读，否则读到的是 (0,0)
        Canvas.ForceUpdateCanvases();
        Vector2 sp = RectTransformUtility.WorldToScreenPoint(UiCamera(), _rt.position);
        // 单位都在 z=0 平面上；ScreenToWorldPoint 的 z 是「离镜头多远」，取绝对值免得镜头 z 为正时算到镜头背后
        float dist = Mathf.Abs(cam.transform.position.z);
        var w = cam.ScreenToWorldPoint(new Vector3(sp.x, sp.y, dist));
        _worldX = w.x;
        _worldY = w.y;
        _pinned = true;
    }

    void LateUpdate()
    {
        if (!_pinned || _rt == null) return;
        var cam = Camera.main;
        if (cam == null) return;
        var parent = transform.parent as RectTransform;
        if (parent == null) return;

        // 世界 X 不动 → 屏幕位置随镜头走 → 反算回 UI 的 anchoredPosition。
        var sp = cam.WorldToScreenPoint(new Vector3(_worldX, _worldY, 0f));
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, sp, UiCamera(), out var local))
            _rt.anchoredPosition = new Vector2(local.x, _rt.anchoredPosition.y);
    }

    Camera UiCamera()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return null;
        return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
    }

    // ============================================================
    // 进门
    // ============================================================

    /// <summary>单位走进门：在它脚下播一次传送特效（主人：佣兵进也要播）。</summary>
    public void PlayEnterEffect(Vector3 worldPos)
    {
        var prefab = Resources.Load<GameObject>(EnterVfxPath);
        if (prefab == null)
        {
            Debug.LogError($"[PortalGate] 找不到进门特效 Resources/{EnterVfxPath}");
            return;
        }
        var go = Instantiate(prefab, worldPos + new Vector3(0f, 0.6f, 0f), Quaternion.identity);
        go.name = "PortalEnterVfx";
        Destroy(go, 3.5f);
    }

    /// <summary>单位进门后藏起来（渲染关掉，逻辑还在 —— 佣兵还要靠它继续带队形往里走）。</summary>
    public static void SetUnitVisible(UnitBase unit, bool on)
    {
        if (unit == null) return;
        var rends = unit.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rends.Length; i++)
            if (rends[i] != null) rends[i].enabled = on;
    }

    public void Close()
    {
        Destroy(gameObject);
    }
}
