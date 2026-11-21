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
    const int RT_SIZE = 512;
    // 模型占展示区的比例（越大显示越大）：正交视野=模型半径/该值。
    // 主人 11-21 拍板「立绘缩到 50%」→ 0.5（此前 0.6 仍偏大）。
    const float ART_FILL = 0.5f;
    // 脚底基线：脚底落在相机中心下方「半个正交视野」处（即展示区下部），所有宠物共用这一条线。
    const float FOOT_BASE_RATIO = 0.5f;
    // 立绘在展示框内上移像素（主人 11-21：先 20、「再往上50」→ 70、「再往下20」→ 50）：
    // 只偏移运行时建的 RawImage 显示区，不碰 prefab 里的 PetArt 节点位置。
    const float ART_LIFT_UP = 50f;

    GameObject _artInstance;
    Camera _artCamera;
    RenderTexture _artRt;
    RawImage _artRaw;

    /// <summary>建一次展示舞台：专用相机 + RT + PetArt 下的 RawImage（只加自己的子节点）。</summary>
    void EnsureArtStage()
    {
        if (_artRaw != null || _petArt == null) return;

        _artRt = new RenderTexture(RT_SIZE, RT_SIZE, 16, RenderTextureFormat.ARGB32);
        _artRt.name = "PetArtRT";
        _artRt.Create();

        var camGo = new GameObject("PetArtCamera", typeof(Camera));
        _artCamera = camGo.GetComponent<Camera>();
        _artCamera.orthographic = true;
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
        rawRt.offsetMin = new Vector2(0f, ART_LIFT_UP);
        rawRt.offsetMax = new Vector2(0f, ART_LIFT_UP);
        _artRaw = rawGo.GetComponent<RawImage>();
        _artRaw.texture = _artRt;
        _artRaw.raycastTarget = false;
    }

    void RefreshPetArt(PetDefs.Def def)
    {
        EnsureArtStage();
        if (_petArt == null || def == null) return;

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

        // 用渲染包围盒把模型【脚底】对齐到统一基线（主人 11-21 拍板「改成底对齐」），
        // 解决各动物重心/部件不同导致底边高低不齐；X 仍居中，正交视野算法不变（不写死缩放）。
        var rends = _artInstance.GetComponentsInChildren<Renderer>(true);
        Bounds b = new Bounds();
        bool hasBounds = false;
        foreach (var r in rends)
        {
            if (!hasBounds) { b = r.bounds; hasBounds = true; }
            else b.Encapsulate(r.bounds);
        }

        if (hasBounds)
        {
            float half = Mathf.Max(b.extents.x, b.extents.y);
            float ortho = Mathf.Max(0.5f, half / ART_FILL);   // 模型占展示区 50%
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
