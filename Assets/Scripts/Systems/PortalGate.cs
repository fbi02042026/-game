using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

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
    /// <summary>
    /// 门皮（UI.Image）所在嵌套 Canvas 的 sortingOrder。
    /// 【2026-10-16 主人报「层级不对跑到玩家前面了」根因】旧值 BattleHud+1(951) —— 门皮整块压在
    /// 玩家（单位层 SORT_UNIT=15）前面，玩家走不进「门里」。门是战场里的实体，应压在
    /// 地图（SORT_MAPROOT=10）之上、单位（15）之下 → 抬层 Canvas 只给 12。
    /// 【2026-10-12 旧口径「抬到 BattleHud 同档防 HUD 盖住」作废】门在战场中央，与顶/底 HUD
    /// 屏幕区域基本不重叠；为救层级把门抬到 951 反而盖住玩家，捡芝麻丢西瓜。
    /// </summary>
    const int PortalCanvasOrder = GameConfig.SORT_MAPROOT + 2;   // 12：地图(10) < 门皮(12) < 粒子(13) < 单位(15)
    /// <summary>
    /// 门自带粒子（传送 / SpinPortalBlue / MagicDust / PortalDust / PortalGlow）的排序。
    /// 【2026-10-16 主人报「chuansongmen 没有特效」根因（Editor.log 体检行实锤）】粒子节点位置
    /// 其实是对的（pos=门的世界坐标），但预制体里 ParticleSystemRenderer 的 sortingOrder=1，
    /// 被地图 Canvas（SORT_MAPROOT=10）整个压在背后 → 特效「播了但看不见」。
    /// → 统一抬到 13：门皮(12) 之上、单位(15) 之下 —— 旋涡在门皮前、玩家身后。
    /// </summary>
    const int PortalParticleOrder = GameConfig.SORT_MAPROOT + 3; // 13
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
        // 【2026-10-12 起】门根自带一层嵌套 Canvas（overrideSorting，走摄像机模式）：
        // 【2026-10-14 主人铁律：所有界面 Canvas 走摄像机模式】裸 Overlay 会恒盖最上层、随主相机不重绑 → 糊屏，
        // 改成 ScreenSpaceCamera 绑定 UI 相机。钉点数学在 UiCamera()/LateUpdate 里读的是「父级 Canvas」的
        // worldCamera，与门自身 Canvas 的 renderMode 无关，转换不影响世界钉点定位。
        // order 见 <see cref="PortalCanvasOrder"/>（12：压地图、被玩家盖）。
        // 【2026-10-14 主人铁律：所有界面 Canvas 走摄像机模式】裸 Overlay 会恒盖最上层、随主相机不重绑 → 糊屏，
        // 改成 ScreenSpaceCamera 绑定 UI 相机。钉点数学在 UiCamera()/LateUpdate 里读的是「父级 Canvas」的
        // worldCamera，与门自身 Canvas 的 renderMode 无关，转换不影响世界钉点定位。
        var canvas = go.GetComponent<Canvas>();
        if (canvas == null) canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = UICanvasSetup.ResolveUiCamera();
        canvas.planeDistance = GameConfig.UI_PLANE_DISTANCE;
        canvas.overrideSorting = true;
        canvas.sortingOrder = PortalCanvasOrder;
        if (go.GetComponent<GraphicRaycaster>() == null) go.AddComponent<GraphicRaycaster>();   // 铁律 10
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

        // 【2026-10-16】粒子抬层：预制体里 ParticleSystemRenderer 默认 order=1，被地图(10)压住看不见。
        // 统一抬到 <see cref="PortalParticleOrder"/>(13)，层归 BATTLE_SORTING_LAYER（Default）。
        ApplyNodeSorting(_teleportNode);
        ApplyNodeSorting(_spinNode);

        // 【2026-10-12】门是纯演出，不该吃点击：门皮 Image 的 raycastTarget 一律关掉。
        // 否则加了 GraphicRaycaster（铁律 10）之后，门会挡住它覆盖范围内的按钮（如「继续」）。
        var images = GetComponentsInChildren<UnityEngine.UI.Image>(true);
        for (int i = 0; i < images.Length; i++)
            if (images[i] != null) images[i].raycastTarget = false;
    }

    IEnumerator CoOpen()
    {
        // ① 传送节点先出来
        if (_teleportNode == null)
            Debug.LogError("[PortalGate] 预制体里找不到「传送」节点，门开不出来");
        else
        {
            SetNode(_teleportNode, true);
            // 【2026-10-16 主人原话】chuansongmen 预制体里自带的特效也要播放：
            // 门出现那一刻，把「传送」节点的粒子显式从头播一遍（playOnAwake 只在激活瞬间触发，重播必须显式 Play）。
            PlayNodeParticles(_teleportNode);
        }

        // 【2026-10-12 排查记录（修正）】门预制体里「传送」「SpinPortalBlue」是从世界 prefab 拖进 UI 的
        // 粒子节点 —— 位置实测与门钉住的世界坐标一致（Editor.log 体检行 pos=10.6,0.6），无需搬家；
        // 真正看不见的原因是粒子 sortingOrder=1 被地图(10)压住，已在 Bind() 里统一抬层修复。
        // 旧 workaround「另播一次 PlayPortalOpenVfx」已随误诊删除（会跟门自带特效叠播两遍）。

        // ② 0.3s 后门皮 + 旋转粒子一起出来
        yield return new WaitForSecondsRealtime(SecondWaveDelay);

        if (_doorNode == null)
            Debug.LogError("[PortalGate] 预制体里找不到 door 节点");
        else
        {
            SetNode(_doorNode, true);
            PlayNodeParticles(_doorNode);
        }

        if (_spinNode == null)
            Debug.LogError("[PortalGate] 预制体里找不到 SpinPortalBlue 节点");
        else
        {
            SetNode(_spinNode, true);
            // SpinPortalBlue 自身 + PortalDust + PortalGlow 三套粒子一起从头播（门出现时播一次）。
            PlayNodeParticles(_spinNode);
        }

        Opened = true;
        Debug.Log($"[PortalGate] 传送门开好：世界X={_worldX:F2} 节点 传送/door/SpinPortalBlue 依次就位");
        LogPortalDiagnostics();
    }

    /// <summary>
    /// 【2026-10-12】门到底看得见看不见 —— 一次把量打全，别再靠猜：
    /// 自身激活 / 屏幕坐标 / 父链 Canvas order / 三个子节点各自的**渲染组件类型**与世界坐标。
    /// 主人下次报「没有传送门」时，Editor.log 里这一条就能一次说清是「没建出来」还是「画到了看不见的地方」。
    /// </summary>
    void LogPortalDiagnostics()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("[PortalGate-体检] 激活=").Append(gameObject.activeInHierarchy)
          .Append(" 父=").Append(transform.parent != null ? transform.parent.name : "null");
        if (_rt != null)
            sb.Append(" anchoredPos=").Append(_rt.anchoredPosition.x.ToString("F0")).Append(",")
              .Append(_rt.anchoredPosition.y.ToString("F0"))
              .Append(" 屏幕=").Append(RectTransformUtility.WorldToScreenPoint(UiCamera(), _rt.position).x.ToString("F0"))
              .Append(",").Append(RectTransformUtility.WorldToScreenPoint(UiCamera(), _rt.position).y.ToString("F0"));
        var self = GetComponent<Canvas>();
        sb.Append(" 自带Canvas=").Append(self != null ? self.sortingOrder + (self.overrideSorting ? "(override)" : "") : "无");
        sb.Append(" 世界X=").Append(_worldX.ToString("F2")).Append(" 世界Y=").Append(_worldY.ToString("F2"));

        AppendNodeDiag(sb, "传送", _teleportNode);
        AppendNodeDiag(sb, "door", _doorNode);
        AppendNodeDiag(sb, "SpinPortalBlue", _spinNode);
        Debug.Log(sb.ToString());
    }

    static void AppendNodeDiag(System.Text.StringBuilder sb, string label, GameObject node)
    {
        sb.Append(" | ").Append(label).Append("=");
        if (node == null) { sb.Append("缺失"); return; }
        sb.Append(node.activeInHierarchy ? "on" : "OFF");
        var sr = node.GetComponentInChildren<SpriteRenderer>(true);
        var img = node.GetComponentInChildren<UnityEngine.UI.Image>(true);
        sb.Append(sr != null ? "SpriteRenderer" : (img != null ? "UI.Image" : "无渲染组件"));
        var p = node.transform.position;
        sb.Append(" pos=").Append(p.x.ToString("F1")).Append(",").Append(p.y.ToString("F1")).Append(",").Append(p.z.ToString("F1"));
        sb.Append(" scale=").Append(node.transform.lossyScale.x.ToString("F2"));
    }

    static void SetNode(GameObject go, bool on)
    {
        if (go != null) go.SetActive(on);
    }

    /// <summary>
    /// 把某个门节点（含所有子孙）的粒子系统从头播一遍。
    /// 【2026-10-16 主人原话】「chuansongmen 预制体里的特效也要播放」——
    /// 显式 Play 而不是只靠 playOnAwake，保证节点重新激活 / 已激活状态下都能重播。
    /// </summary>
    void PlayNodeParticles(GameObject node)
    {
        if (node == null) return;
        var systems = node.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
            if (systems[i] != null) systems[i].Play(true);
    }

    /// <summary>
    /// 把门节点（含所有子孙）的粒子/精灵渲染器统一抬到 <see cref="PortalParticleOrder"/>、
    /// 层归 BATTLE_SORTING_LAYER。唯一出口 = Bind()（门开一次抬一次，幂等）。
    /// </summary>
    void ApplyNodeSorting(GameObject node)
    {
        if (node == null) return;
        var prs = node.GetComponentsInChildren<ParticleSystemRenderer>(true);
        for (int i = 0; i < prs.Length; i++)
        {
            if (prs[i] == null) continue;
            prs[i].sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            prs[i].sortingOrder = PortalParticleOrder;
        }
        var srs = node.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
        {
            if (srs[i] == null) continue;
            srs[i].sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            srs[i].sortingOrder = PortalParticleOrder;
        }
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
        // 【2026-10-16 主人原话】有人进到门的中心点时，预制体里「传送」节点自带的特效也要再播一次。
        PlayNodeParticles(_teleportNode);

        var prefab = Resources.Load<GameObject>(EnterVfxPath);
        if (prefab == null)
        {
            Debug.LogError($"[PortalGate] 找不到进门特效 Resources/{EnterVfxPath}");
            return;
        }
        var go = Instantiate(prefab, worldPos + new Vector3(0f, 0.6f, 0f), Quaternion.identity);
        go.name = "PortalEnterVfx";
        // 【2026-10-16】进门特效也要抬层：prefab 里渲染器默认低 order，会被地图压住看不见。
        // 归战斗特效层（SORT_VFX=50），进门瞬间盖在单位之上，与攻击特效同档。
        var prs = go.GetComponentsInChildren<ParticleSystemRenderer>(true);
        for (int i = 0; i < prs.Length; i++)
        {
            if (prs[i] == null) continue;
            prs[i].sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            prs[i].sortingOrder = GameConfig.SORT_VFX;
        }
        var srs = go.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
        {
            if (srs[i] == null) continue;
            srs[i].sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            srs[i].sortingOrder = GameConfig.SORT_VFX;
        }
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
