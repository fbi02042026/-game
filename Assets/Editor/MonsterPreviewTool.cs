#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 怪物预览调试工具
/// 菜单：Tools/怪物预览调试
///
/// 功能：
/// 1. 按章节浏览所有怪物精灵（8章 x 12只 = 96只）
/// 2. 实时调整精灵缩放（spriteScale）
/// 3. 预览站立拉伸动画效果
/// 4. 对比不同尺寸版本（default size / min size）
/// 5. 一键创建/更新 MonsterConfig 配置
/// </summary>
public class MonsterPreviewWindow : EditorWindow
{
    private static readonly string[] CHAPTER_NAMES = {
        "1 - Undead (亡灵)", "2 - Jungle (丛林)", "3 - Sea (海洋)",
        "4 - Forest (森林)", "5 - Field (田野)", "6 - Cave (洞穴)",
        "7 - Devil (恶魔)", "8 - Ice (冰霜)"
    };

    private static readonly string[] CHAPTER_FOLDERS = {
        "1 Undead", "2 Jungle", "3 Sea", "4 Forest",
        "5 Field", "6 Cave", "7 Devil", "8 Ice"
    };

    private int _selectedChapter = 0;
    private int _selectedMonsterIndex = 0;
    private Vector2 _scrollPos;
    private float _previewScale = 1f;
    private bool _showAnimation = true;
    private float _animStretchAmount = 0.05f;
    private float _animStretchSpeed = 2f;
    private bool _useMinSize = false;

    // 预览场景对象
    private GameObject _previewRoot;
    private SpriteRenderer _previewSr;
    private double _animStartTime;

    // 缓存的精灵列表
    private List<Sprite> _cachedSprites = new List<Sprite>();
    private string _cachedPath = "";

    [MenuItem("Tools/_归档/怪物预览调试")]
    public static void ShowWindow()
    {
        var window = GetWindow<MonsterPreviewWindow>("怪物预览调试");
        window.minSize = new Vector2(500, 600);
    }

    void OnEnable()
    {
        _animStartTime = EditorApplication.timeSinceStartup;
        EditorApplication.update += OnEditorUpdate;
        LoadSpritesForChapter();
    }

    void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        CleanupPreview();
    }

    void OnEditorUpdate()
    {
        if (_showAnimation && _previewSr != null)
        {
            // 模拟 MonsterAnimation 的拉伸效果
            float t = Mathf.Sin((float)(EditorApplication.timeSinceStartup - _animStartTime) * _animStretchSpeed) * 0.5f + 0.5f;
            float scaleY = Mathf.Lerp(1f - _animStretchAmount, 1f + _animStretchAmount, t);
            float scaleX = Mathf.Lerp(1f + _animStretchAmount * 0.5f, 1f - _animStretchAmount * 0.5f, t);

            _previewSr.transform.localScale = new Vector3(_previewScale * scaleX, _previewScale * scaleY, 1f);

            // 底部固定
            Vector3 pos = _previewSr.transform.localPosition;
            float heightDiff = (_previewScale * scaleY - _previewScale) * 0.5f;
            _previewSr.transform.localPosition = new Vector3(pos.x, heightDiff, pos.z);

            Repaint();
        }
    }

    void OnGUI()
    {
        // === 顶部：章节选择 ===
        EditorGUILayout.Space(5);
        GUILayout.Label("怪物预览调试", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "选择章节和怪物编号，预览精灵大小和动画效果。\n" +
            "调整缩放参数后可直接生成 MonsterConfig 配置。", MessageType.Info);

        EditorGUILayout.Space(5);
        int newChapter = EditorGUILayout.Popup("章节", _selectedChapter, CHAPTER_NAMES);
        if (newChapter != _selectedChapter)
        {
            _selectedChapter = newChapter;
            _selectedMonsterIndex = 0;
            LoadSpritesForChapter();
        }

        // === 尺寸版本切换 ===
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("尺寸版本:", GUILayout.Width(60));
        bool newMinSize = GUILayout.Toggle(!_useMinSize, "Default Size");
        bool newMinSize2 = GUILayout.Toggle(_useMinSize, "Min Size");
        bool targetMinSize = !newMinSize && newMinSize2;
        if (targetMinSize != _useMinSize)
        {
            _useMinSize = targetMinSize;
            LoadSpritesForChapter();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // === 怪物选择 ===
        if (_cachedSprites.Count == 0)
        {
            EditorGUILayout.HelpBox("未找到怪物精灵！请检查资源路径:\n" +
                "Assets/Art/2D Pixel RPG Monster Pack/Icons/[size]/no shadow/[chapter]/", MessageType.Warning);
            return;
        }

        // 怪物索引滑动条
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("怪物编号:", GUILayout.Width(60));
        _selectedMonsterIndex = Mathf.Clamp(
            EditorGUILayout.IntSlider(_selectedMonsterIndex, 0, _cachedSprites.Count - 1),
            0, _cachedSprites.Count - 1);
        EditorGUILayout.EndHorizontal();

        // 怪物名称
        string monsterName = GetMonsterName(_selectedChapter, _selectedMonsterIndex);
        GUILayout.Label($"当前: {monsterName}", EditorStyles.boldLabel);

        EditorGUILayout.Space(5);

        // === 缩放控制 ===
        GUILayout.Label("缩放设置", EditorStyles.boldLabel);
        _previewScale = EditorGUILayout.Slider("精灵缩放 (spriteScale)", _previewScale, 0.1f, 5f);

        EditorGUILayout.Space(5);

        // === 动画控制 ===
        GUILayout.Label("站立动画参数", EditorStyles.boldLabel);
        _showAnimation = EditorGUILayout.Toggle("启用动画预览", _showAnimation);
        EditorGUI.BeginDisabledGroup(!_showAnimation);
        _animStretchAmount = EditorGUILayout.Slider("拉伸幅度", _animStretchAmount, 0f, 0.3f);
        _animStretchSpeed = EditorGUILayout.Slider("拉伸速度", _animStretchSpeed, 0.5f, 8f);
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(10);

        // === 预览区域 ===
        GUILayout.Label("预览 (白色线 = 地面参考线)", EditorStyles.boldLabel);
        Rect previewRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
            GUILayout.Height(250), GUILayout.ExpandWidth(true));

        if (Event.current.type == EventType.Repaint)
        {
            DrawPreview(previewRect);
        }

        EditorGUILayout.Space(10);

        // === 操作按钮 ===
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("上一个", GUILayout.Height(30)))
        {
            _selectedMonsterIndex = Mathf.Max(0, _selectedMonsterIndex - 1);
        }
        if (GUILayout.Button("下一个", GUILayout.Height(30)))
        {
            _selectedMonsterIndex = Mathf.Min(_cachedSprites.Count - 1, _selectedMonsterIndex + 1);
        }
        if (GUILayout.Button("保存缩放到Config", GUILayout.Height(30)))
        {
            SaveScaleToConfig();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // === 配置信息 ===
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("创建/更新 MonsterConfig", GUILayout.Height(30)))
        {
            CreateOrUpdateMonsterConfig();
        }
        if (GUILayout.Button("打开怪物配置目录", GUILayout.Height(30)))
        {
            OpenMonsterConfigFolder();
        }
        EditorGUILayout.EndHorizontal();

        // === 当前精灵信息 ===
        EditorGUILayout.Space(10);
        if (_selectedMonsterIndex < _cachedSprites.Count && _cachedSprites[_selectedMonsterIndex] != null)
        {
            Sprite s = _cachedSprites[_selectedMonsterIndex];
            GUILayout.Label("精灵信息:", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            GUILayout.Label($"名称: {s.name}");
            GUILayout.Label($"尺寸: {s.texture.width} x {s.texture.height} px");
            GUILayout.Label($"Pivot: {s.pivot}");
            GUILayout.Label($"Pixels Per Unit: {s.pixelsPerUnit}");
            GUILayout.Label($"Rect: {s.rect}");
            EditorGUI.indentLevel--;
        }
    }

    /// <summary>
    /// 绘制预览区域
    /// </summary>
    void DrawPreview(Rect rect)
    {
        // 背景
        EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));

        if (_selectedMonsterIndex >= _cachedSprites.Count || _cachedSprites[_selectedMonsterIndex] == null)
            return;

        Sprite sprite = _cachedSprites[_selectedMonsterIndex];
        Texture2D tex = sprite.texture;

        // 计算动画缩放
        float displayScale = _previewScale;
        if (_showAnimation)
        {
            float t = Mathf.Sin((float)(EditorApplication.timeSinceStartup - _animStartTime) * _animStretchSpeed) * 0.5f + 0.5f;
            float scaleY = Mathf.Lerp(1f - _animStretchAmount, 1f + _animStretchAmount, t);
            float scaleX = Mathf.Lerp(1f + _animStretchAmount * 0.5f, 1f - _animStretchAmount * 0.5f, t);
            displayScale = _previewScale; // 实际缩放在尺寸计算中应用
        }

        // 计算绘制尺寸（保持宽高比，限制最大尺寸）
        float maxDisplayHeight = rect.height * 0.8f;
        float maxDisplayWidth = rect.width * 0.5f;
        float aspectRatio = (float)tex.width / tex.height;
        float displayHeight = maxDisplayHeight * _previewScale;
        float displayWidth = displayHeight * aspectRatio;

        if (displayWidth > maxDisplayWidth)
        {
            displayWidth = maxDisplayWidth;
            displayHeight = displayWidth / aspectRatio;
        }

        // 动画缩放
        float animScaleY = 1f, animScaleX = 1f;
        if (_showAnimation)
        {
            float t = Mathf.Sin((float)(EditorApplication.timeSinceStartup - _animStartTime) * _animStretchSpeed) * 0.5f + 0.5f;
            animScaleY = Mathf.Lerp(1f - _animStretchAmount, 1f + _animStretchAmount, t);
            animScaleX = Mathf.Lerp(1f + _animStretchAmount * 0.5f, 1f - _animStretchAmount * 0.5f, t);
        }

        float finalWidth = displayWidth * animScaleX;
        float finalHeight = displayHeight * animScaleY;

        // 底部固定：Y坐标从底部向上
        float groundY = rect.yMax - 20f;
        float drawY = groundY - finalHeight;

        // 绘制地面线
        EditorGUI.DrawRect(new Rect(rect.x, groundY, rect.width, 2f), Color.white);

        // 绘制精灵（水平居中）
        float drawX = rect.center.x - finalWidth / 2f;
        Rect drawRect = new Rect(drawX, drawY, finalWidth, finalHeight);

        // 翻转X（怪物面向左）
        DrawSpriteFlipped(sprite, drawRect);

        // 绘制尺寸标注
        GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel);
        labelStyle.normal.textColor = Color.yellow;
        GUI.Label(new Rect(rect.x + 5, rect.y + 5, 200, 20),
            $"缩放: {_previewScale:F2}x  动画: {(_showAnimation ? "ON" : "OFF")}", labelStyle);
        GUI.Label(new Rect(rect.x + 5, rect.y + 22, 200, 20),
            $"显示尺寸: {finalWidth:F0} x {finalHeight:F0}", labelStyle);
    }

    void DrawSpriteFlipped(Sprite sprite, Rect rect)
    {
        // 简单绘制（水平翻转通过反向UV实现）
        Vector2 pivot = sprite.pivot;
        Rect spriteRect = sprite.rect;

        // 使用 Graphics.DrawTexture 绘制
        Material mat = new Material(Shader.Find("UI/Default"));
        Graphics.DrawTexture(rect, sprite.texture, mat);
        DestroyImmediate(mat);
    }

    /// <summary>
    /// 加载章节精灵
    /// </summary>
    void LoadSpritesForChapter()
    {
        _cachedSprites.Clear();

        string sizeFolder = _useMinSize ? "min size" : "default size";
        string chapterFolder = CHAPTER_FOLDERS[_selectedChapter];
        string basePath = $"Assets/Art/2D Pixel RPG Monster Pack/Icons/{sizeFolder}/no shadow/{chapterFolder}";

        if (!AssetDatabase.IsValidFolder(basePath))
        {
            Debug.LogWarning("[MonsterPreview] 文件夹不存在: " + basePath);
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { basePath });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
                _cachedSprites.Add(sprite);
        }

        // 按名称排序
        _cachedSprites = _cachedSprites.OrderBy(s => s.name).ToList();
        _cachedPath = basePath;

        Debug.Log($"[MonsterPreview] 加载 {_cachedSprites.Count} 个精灵 from {basePath}");
    }

    /// <summary>
    /// 获取怪物名称
    /// </summary>
    string GetMonsterName(int chapter, int index)
    {
        if (_cachedSprites.Count == 0 || index >= _cachedSprites.Count) return "N/A";
        return _cachedSprites[index].name;
    }

    /// <summary>
    /// 保存缩放到现有 MonsterConfig
    /// </summary>
    void SaveScaleToConfig()
    {
        string monsterId = GetMonsterName(_selectedChapter, _selectedMonsterIndex);
        string configPath = $"Assets/Resources/Config/Monsters/{monsterId}.asset";

        MonsterConfig config = AssetDatabase.LoadAssetAtPath<MonsterConfig>(configPath);
        if (config != null)
        {
            Undo.RecordObject(config, "Update spriteScale");
            config.spriteScale = _previewScale;
            config.spriteIndex = _selectedMonsterIndex + 1;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log($"[MonsterPreview] 已更新 {monsterId} 的 spriteScale = {_previewScale}");
        }
        else
        {
            if (EditorUtility.DisplayDialog("配置不存在",
                $"怪物配置 {configPath} 不存在。\n是否创建新配置？", "创建", "取消"))
            {
                CreateOrUpdateMonsterConfig();
            }
        }
    }

    /// <summary>
    /// 创建或更新 MonsterConfig
    /// </summary>
    void CreateOrUpdateMonsterConfig()
    {
        string monsterId = GetMonsterName(_selectedChapter, _selectedMonsterIndex);
        string configPath = $"Assets/Resources/Config/Monsters/{monsterId}.asset";

        MonsterConfig config = AssetDatabase.LoadAssetAtPath<MonsterConfig>(configPath);
        bool isNew = false;

        if (config == null)
        {
            config = ScriptableObject.CreateInstance<MonsterConfig>();
            isNew = true;
        }

        Undo.RecordObject(config, isNew ? "Create MonsterConfig" : "Update MonsterConfig");

        config.id = monsterId;
        config.monsterName = monsterId;
        config.minWave = 1;
        config.isBoss = (_selectedMonsterIndex >= 9); // 后几只作为Boss
        config.spriteIndex = _selectedMonsterIndex + 1;
        config.spriteScale = _previewScale;

        // 默认属性
        if (isNew)
        {
            config.baseHp = config.isBoss ? 500 : 50;
            config.baseAttack = config.isBoss ? 20 : 5;
            config.baseAttackSpeed = 1;
            config.attackRange = 1.5f;
            config.baseMoveSpeed = 0;
            config.baseGoldDrop = config.isBoss ? 100 : 10;
            config.expDrop = config.isBoss ? 50 : 5;
        }

        if (isNew)
        {
            AssetDatabase.CreateAsset(config, configPath);
            Debug.Log($"[MonsterPreview] 创建新配置: {configPath}");
        }

        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 选中新创建的资产
        EditorGUIUtility.PingObject(config);

        Debug.Log($"[MonsterPreview] 配置已保存: {monsterId} (scale={_previewScale}, boss={config.isBoss})");
    }

    void OpenMonsterConfigFolder()
    {
        string folder = "Assets/Resources/Config/Monsters";
        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder("Assets/Resources/Config", "Monsters");
        }
        var obj = AssetDatabase.LoadAssetAtPath<Object>(folder);
        EditorGUIUtility.PingObject(obj);
    }

    void CleanupPreview()
    {
        if (_previewRoot != null)
        {
            DestroyImmediate(_previewRoot);
            _previewRoot = null;
            _previewSr = null;
        }
    }
}
#endif
