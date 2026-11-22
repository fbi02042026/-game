using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PetArt 展示：在 Stage/PetArt 下用 RawImage + 专用相机 RenderTexture 播放动物 prefab 的 idle 动画。
///
/// 为什么不能直接 Instantiate 到 UI 下：动物 prefab 是 SpriteRenderer/SortingGroup 的 2D 场景物件，
/// 挂进 ScreenSpaceCamera 的 UI 树后不随 RectTransform 排版、会被 UI 层盖住，看不到模型。
/// 这里照项目现成的 OpenIntroOverlay「相机 → RenderTexture → RawImage」套路做，
/// 只新建自己的相机/RawImage/RT，不改 PetArt 节点任何位置/尺寸。
///
/// 资源：Resources/UI/Pets/{Animal/Animal_Dog ...}（主人 11-21 实际放置位置，对应 PetDefs.prefabPath）
/// 缺失时 LogError 明确指出，不静默造替代模型（项目红线：不兜底）。
/// </summary>
public partial class PetUI
{
    const string PET_PREFAB_ROOT = "UI/Pets/";   // 主人实际放置：Assets/Resources/UI/Pets/
    const int ART_LAYER = 31;           // TagManager 中空的层，专供宠物预览相机
    const float ART_STAGE_Y = -10000f;  // 挪出主相机视野，避免宠物出现在城镇/战斗画面
    // 立绘 RT 基准高度：宽度按 RawImage 实际显示区宽高比算，贴回 RawImage 才不会被拉伸。
    // 2026-11-22 修「立绘比例和预制体不一致」：旧写死 512×512 方形；上一版按节点 320×480 建仍不对——
    // RawImage 因 ART_LIFT_UP 上下各内缩 50，真实显示区是 320×380（0.842），RT 必须按显示区比例才等比。
    const int RT_BASE_HEIGHT = 512;
    // 模型占展示区的比例（越大显示越大）：正交视野=模型半径/该值。
    // 主人 11-21「缩到 50%」→ 0.5；11-22「再小 10%」→ 0.45（=0.5×0.9）；又一次「再小 10%」→ 0.405（=0.45×0.9）；
    // 11-22 又一次「再小 10%」→ 0.3645（=0.405×0.9）。
    // 底对齐基线 FOOT_BASE_RATIO 是相对展示区比例、与 ortho 无关，缩大小不影响底对齐。
    const float ART_FILL = 0.3645f;
    // 脚底基线：脚底落在相机中心下方「半个正交视野」处（即展示区下部），所有宠物共用这一条线。
    const float FOOT_BASE_RATIO = 0.5f;
    // 立绘在展示框内上下内缩像素（主人 11-21：先 20、「再往上50」→ 70、「再往下20」→ 50）。
    // 只偏移运行时建的 RawImage 显示区，不碰 prefab 里的 PetArt 节点位置。
    const float ART_LIFT_UP = 50f;
    // 展示区整体上移像素（主人 11-22：先往上 100，「往下 30」→ 70，「再往下 20」→ 50）。
    // 只平移运行时建的 RawImage，显示区尺寸与 RT 比例不变（底对齐/比例全保持），不碰 prefab 节点。
    const float ART_RISE = 50f;
    // 11-22 鸟单独上移：先 20，「鸟再往上 10」→ 30，「鸟再往上 15」→ 45（其余宠物仍走 ART_RISE 基线，位置一个像素没动）。
    // 巨嘴鸟本体与其鹦鹉皮肤都算「鸟」：详情页按 DisplayDef 跟随上阵皮肤展示，两者须同一偏移。
    const float BIRD_ART_RISE_EXTRA = 45f;

    GameObject _artInstance;
    Camera _artCamera;
    RenderTexture _artRt;
    RawImage _artRaw;

    /// <summary>建一次展示舞台：专用相机 + RT + PetArt 下的 RawImage（只加自己的子节点）。</summary>
    void EnsureArtStage()
    {
        if (_artRaw != null || _petArt == null) return;

        // RT 宽高比必须等于 RawImage【实际显示区】宽高比，否则纹理贴回去会被单轴拉伸。
        // RawImage anchor=0~1、offsetMin/Max.y=ART_LIFT_UP ⇒ 显示区高 = 节点高 - 2*ART_LIFT_UP（宽不变）。
        // 只从 RectTransform 现读比例，不改节点任何尺寸/锚点（铁律 20）。
        float viewW = 1f, viewH = 1f;
        var artRt = _petArt as RectTransform;
        if (artRt != null)
        {
            viewW = Mathf.Max(1f, artRt.rect.width);
            // 与下方 rawRt.offsetMin/Max=(0, ART_LIFT_UP) 严格对应：上下各内缩 ART_LIFT_UP。
            viewH = Mathf.Max(1f, artRt.rect.height - 2f * ART_LIFT_UP);
        }
        int rtH = RT_BASE_HEIGHT;
        int rtW = Mathf.Max(1, Mathf.RoundToInt(rtH * viewW / viewH));
        _artRt = new RenderTexture(rtW, rtH, 16, RenderTextureFormat.ARGB32);
        _artRt.name = "PetArtRT";
        _artRt.Create();

        var camGo = new GameObject("PetArtCamera", typeof(Camera));
        _artCamera = camGo.GetComponent<Camera>();
        _artCamera.orthographic = true;
        // 正交相机 aspect 必须跟 RT 一致（否则渲染出的画面再次被压扁）；orthographicSize 是半高。
        _artCamera.aspect = (float)rtW / rtH;
        _artCamera.orthographicSize = 2.5f;
        _artCamera.clearFlags = CameraClearFlags.SolidColor;
        _artCamera.backgroundColor = Color.clear;
        _artCamera.cullingMask = 1 << ART_LAYER;
        _artCamera.targetTexture = _artRt;
        _artCamera.transform.position = new Vector3(0f, ART_STAGE_Y, -10f);
        _artCamera.transform.rotation = Quaternion.identity;

        var rawGo = new GameObject("PetArtRaw", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        rawGo.transform.SetParent(_petArt, false);
        var rawRt = (RectTransform)rawGo.transform;
        rawRt.anchorMin = Vector2.zero;
        rawRt.anchorMax = Vector2.one;
        // 纵向 offset 由 ApplyArtRise 按当前展示宠物统一设置（鸟额外上移），此处不写死。
        _artRaw = rawGo.GetComponent<RawImage>();
        _artRaw.texture = _artRt;
        _artRaw.raycastTarget = false;
    }

    void RefreshPetArt(PetDefs.Def def)
    {
        EnsureArtStage();
        if (_petArt == null || def == null) return;
        // 每次切宠物都按 id 重算纵向偏移：鸟 +20，其余回基线（切走鸟立刻恢复）。
        ApplyArtRise(def);

        if (_artInstance != null)
        {
            Destroy(_artInstance);
            _artInstance = null;
        }

        var prefab = Resources.Load<GameObject>(PET_PREFAB_ROOT + def.prefabPath);
        if (prefab == null)
        {
            Debug.LogError(TAG + "缺宠物展示 prefab：Resources/" + PET_PREFAB_ROOT + def.prefabPath
                + "（应位于 Assets/Resources/UI/Pets/ 下，否则 PetArt 不显示）");
            return;
        }

        _artInstance = Instantiate(prefab);
        _artInstance.name = "PetArtModel";
        SetLayerRecursive(_artInstance, ART_LAYER);

        // 11-22 底对齐修复①：Animator 在 Instantiate 后要到首帧才把骨骼从 prefab 静态姿态搬到 Idle
        // 第 0 帧姿态（实测 Wolf 的 Leg_bone y：-0.293 → -0.106）。若量 bounds 早于这次采样，
        // 位移按错姿态算，动画一播脚底就偏移、各宠物高低不齐。这里先强制采样第 0 帧再量。
        var artAnim = _artInstance.GetComponent<Animator>();
        if (artAnim != null && artAnim.isActiveAndEnabled)
        {
            artAnim.Rebind();
            artAnim.Update(0f);
        }

        // 用渲染包围盒把模型【脚底】对齐到统一基线（主人 11-21 拍板「改成底对齐」），
        // 解决各动物重心/部件不同导致底边高低不齐；X 仍居中，正交视野算法不变（不写死缩放）。
        // 11-22 修复②：只统计身体部件——影子(Shadow)与攻击/受击/死亡特效 sprite 尺寸/位置各异，
        // 混进并集会把"底"定到非脚底处（与 Pet.cs 血条排除口径一致）。
        var srs = _artInstance.GetComponentsInChildren<SpriteRenderer>(true);
        Bounds b = new Bounds();
        bool hasBounds = false;
        for (int i = 0; i < srs.Length; i++)
        {
            var sr = srs[i];
            if (sr == null || sr.sprite == null) continue;
            string nm = sr.name;
            if (nm == "Shadow" || nm.IndexOf("shadow", System.StringComparison.OrdinalIgnoreCase) >= 0
                || nm == "Attack" || nm == "Hit" || nm == "Hit_2" || nm == "Dead"
                || nm.IndexOf("bone", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
            if (!hasBounds) { b = sr.bounds; hasBounds = true; }
            else b.Encapsulate(sr.bounds);
        }

        if (hasBounds)
        {
            float half = Mathf.Max(b.extents.x, b.extents.y);
            // 2026-11-22：RT 改竖版后，正交半高要按 aspect 反推，才能保持「水平方向占展示区 50%」
            // 与旧方形 RT 同一视觉大小；否则竖框下水平占比会撑到 75%（等于把宠物放大 1.5 倍）。
            float aspect = _artCamera != null && _artCamera.aspect > 0.01f ? _artCamera.aspect : 1f;
            float ortho = Mathf.Max(0.5f, half / (ART_FILL * aspect));
            // 脚底落在相机中心下方半个视野处：所有宠物共用同一条基线，底边齐平。
            float footY = ART_STAGE_Y - ortho * FOOT_BASE_RATIO;
            _artInstance.transform.position += new Vector3(-b.center.x, footY - b.min.y, 0f);
            if (_artCamera != null)
            {
                _artCamera.transform.position = new Vector3(0f, ART_STAGE_Y, -10f);
                _artCamera.orthographicSize = ortho;
            }
        }
        else
        {
            _artInstance.transform.position = new Vector3(0f, ART_STAGE_Y, 0f);
            if (_artCamera != null)
            {
                _artCamera.transform.position = new Vector3(0f, ART_STAGE_Y, -10f);
                _artCamera.orthographicSize = 2.5f;
            }
        }

        // prefab 自带 Animator + controller，Idle 是默认状态，实例化即播，无需手写参数
        if (_artInstance.GetComponent<Animator>() == null)
            Debug.LogWarning(TAG + def.prefabPath + " 无 Animator，idle 不会播放（prefab 应自带 Animal.controller）");
    }

    /// <summary>按当前展示宠物设置立绘显示区纵向偏移：只巨嘴鸟/鹦鹉额外上移，其余一律基线。
    /// 同语义单一出口——建 RawImage 时和每次 RefreshPetArt 都走这里；只改运行时 RawImage 的
    /// offsetMin/Max.y（纵向 y 排版），尺寸/anchor/RT 比例/底对齐/prefab 节点一律不碰。</summary>
    void ApplyArtRise(PetDefs.Def def)
    {
        if (_artRaw == null) return;
        float rise = ART_RISE + (IsBird(def) ? BIRD_ART_RISE_EXTRA : 0f);
        var rawRt = (RectTransform)_artRaw.transform;
        rawRt.offsetMin = new Vector2(0f, ART_LIFT_UP + rise);
        rawRt.offsetMax = new Vector2(0f, ART_LIFT_UP + rise);
    }

    /// <summary>「鸟」= 巨嘴鸟本体 + 其鹦鹉皮肤（同一物种两条目，共用 Bird/ 展示 prefab）。</summary>
    static bool IsBird(PetDefs.Def def)
    {
        if (def == null) return false;
        return def.id == "toucan" || def.id == "toucan_parrot";
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        if (go == null) return;
        go.layer = layer;
        for (int i = 0; i < go.transform.childCount; i++)
            SetLayerRecursive(go.transform.GetChild(i).gameObject, layer);
    }

    /// <summary>由 PetUI.OnDestroy 调用，回收 RT 与专用相机。</summary>
    void DisposeArtStage()
    {
        if (_artInstance != null) { Destroy(_artInstance); _artInstance = null; }
        if (_artCamera != null) { Destroy(_artCamera.gameObject); _artCamera = null; }
        if (_artRt != null) { _artRt.Release(); Destroy(_artRt); _artRt = null; }
        _artRaw = null;
    }
}
