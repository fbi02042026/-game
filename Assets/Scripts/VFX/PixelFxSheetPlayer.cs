using UnityEngine;

/// <summary>
/// pixelfx 逐帧特效播放器（2026-11-05 主人拍板接入，
/// 改造点见 Docs/《战斗趣味升级_pixelfx特效与打击反馈》_待办任务清单.md）。
/// 读 Resources 下的规则网格 sheet（PNG + 同名 JSON，pixelfx render.mjs --sheet 产物），
/// 运行时 Sprite.Create 逐帧播放。prefab 只挂本组件一个脚本，参数在 prefab YAML 上配。
/// sheet 约定：meta.pixelfx = { fps, loop, frames, frameSize:[w,h], pivot:[x,y](帧内左上原点像素), columns }。
/// 纹理按 Default 类型导入即可（Sprite.Create 不要求 Read/Write），过滤模式在 .meta 里写死 Point。
/// </summary>
public class PixelFxSheetPlayer : MonoBehaviour
{
    [Header("Resources 路径（不带扩展名，PNG+JSON 同名同目录）")]
    public string sheetPath;

    [Header("覆盖项（0/默认 = 用 JSON 里的值）")]
    public int fpsOverride;
    public bool loopOverride = false;

    [Header("播完自毁（once 型特效勾上；loop 型靠宿主回收）")]
    public bool destroyOnFinish;

    [Header("保险丝：>0 秒后强制自毁（0 = 不启用）")]
    public float autoDestroyAfter;

    [Header("整体缩放 / 排序")]
    public float scale = 1f;
    public int sortingOrder = 20;

    SpriteRenderer _sr;
    Sprite[] _frames;
    float _frameDur = 1f / 30f;
    bool _loop = true;
    int _index;
    float _timer;
    bool _finished;
    float _bornAt;

    // —— sheet JSON 反序列化结构（只取需要的字段；JsonUtility 不吃字典，pixelfx 是固定形状） ——
    [System.Serializable] class SheetJson { public FrameEntry[] frames; public SheetMeta meta; }
    [System.Serializable] class FrameEntry { public int duration; }
    [System.Serializable] class SheetMeta { public PixelfxMeta pixelfx; }
    [System.Serializable] class PixelfxMeta { public int fps; public bool loop; public int frames; public int[] frameSize; public int[] pivot; public int columns; }

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();
        _sr.sortingOrder = sortingOrder;
        LoadSheet();
    }

    void OnEnable()
    {
        _bornAt = Time.time;
        _index = 0;
        _timer = 0f;
        _finished = _frames == null || _frames.Length == 0;
        ApplyFrame();
    }

    void LoadSheet()
    {
        if (_frames != null || string.IsNullOrEmpty(sheetPath)) return;

        var tex = Resources.Load<Texture2D>(sheetPath);
        var metaAsset = Resources.Load<TextAsset>(sheetPath);
        if (tex == null || metaAsset == null)
        {
            Debug.LogError($"[PixelFx] sheet 加载失败 path=Resources/{sheetPath} tex={(tex != null)} json={(metaAsset != null)}（PNG/JSON 必须同名同目录放 Resources 下，路径不带扩展名）");
            _finished = true;
            return;
        }

        SheetJson json;
        try { json = JsonUtility.FromJson<SheetJson>(metaAsset.text); }
        catch (System.Exception e)
        {
            Debug.LogError($"[PixelFx] sheet JSON 解析失败 path=Resources/{sheetPath}: {e.Message}");
            _finished = true;
            return;
        }
        if (json == null || json.meta == null || json.meta.pixelfx == null
            || json.meta.pixelfx.frameSize == null || json.meta.pixelfx.frameSize.Length < 2)
        {
            Debug.LogError($"[PixelFx] sheet JSON 缺 meta.pixelfx（fps/frames/frameSize/columns）path=Resources/{sheetPath}");
            _finished = true;
            return;
        }

        var px = json.meta.pixelfx;
        int fw = px.frameSize[0], fh = px.frameSize[1];
        int cols = px.columns > 0 ? px.columns : Mathf.Max(1, Mathf.CeilToInt((float)tex.width / fw));
        int count = px.frames > 0 ? px.frames : (json.frames != null ? json.frames.Length : 0);
        if (count <= 0 || fw <= 0 || fh <= 0)
        {
            Debug.LogError($"[PixelFx] sheet 帧数/帧尺寸非法 path=Resources/{sheetPath} frames={px.frames} frameSize={fw}x{fh}");
            _finished = true;
            return;
        }

        // pivot：pixelfx 是帧内左上原点像素 → Unity Sprite 归一化左下原点
        Vector2 pivot = new Vector2(0.5f, 0.5f);
        if (px.pivot != null && px.pivot.Length >= 2 && fw > 0 && fh > 0)
            pivot = new Vector2(px.pivot[0] / (float)fw, 1f - px.pivot[1] / (float)fh);

        _frames = new Sprite[count];
        for (int i = 0; i < count; i++)
        {
            int col = i % cols;
            int row = i / cols;
            var rect = new Rect(col * fw, row * fh, fw, fh);
            _frames[i] = Sprite.Create(tex, rect, pivot, 100f, 0u, SpriteMeshType.FullRect);
        }

        _frameDur = 1f / (fpsOverride > 0 ? fpsOverride : (px.fps > 0 ? px.fps : 30));
        // fpsOverride>0 时 loop 也以 prefab 上的覆盖为准；否则信 JSON
        _loop = fpsOverride > 0 ? loopOverride : px.loop;
    }

    void Update()
    {
        if (_finished || _frames == null || _frames.Length == 0) return;

        if (autoDestroyAfter > 0f && Time.time - _bornAt >= autoDestroyAfter)
        {
            Destroy(gameObject);
            return;
        }

        _timer += Time.deltaTime;
        while (_timer >= _frameDur)
        {
            _timer -= _frameDur;
            _index++;
            if (_index >= _frames.Length)
            {
                if (_loop)
                {
                    _index = 0;
                }
                else
                {
                    _index = _frames.Length - 1;
                    _finished = true;
                    if (destroyOnFinish)
                    {
                        Destroy(gameObject);
                        return;
                    }
                    break;
                }
            }
        }
        ApplyFrame();
    }

    /// <summary>
    /// 运行时把 once 型 sheet 改成循环播放（2026-11-08 主人「星落同理：触发完了也可以再转一会消失」）。
    /// 只改内存实例、不动 prefab；关掉播完自毁，存活交给宿主的 ScheduleRelease / Destroy 单一出口回收。
    /// 仅由 SkillRegistry.PlaySkillVfx 对 starfall 调用，其它特效默认不走这里。
    /// </summary>
    public void SetLooping()
    {
        _loop = true;
        _finished = false;
        destroyOnFinish = false;
    }

    void ApplyFrame()
    {
        if (_sr == null || _frames == null || _frames.Length == 0) return;
        _sr.sprite = _frames[Mathf.Clamp(_index, 0, _frames.Length - 1)];
        if (!Mathf.Approximately(scale, 1f))
            transform.localScale = Vector3.one * Mathf.Max(0.01f, scale);
    }
}
