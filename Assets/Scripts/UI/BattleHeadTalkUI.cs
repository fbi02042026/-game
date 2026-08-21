using System.Collections;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 战斗内角色头顶对话气泡（引导专用）。
/// </summary>
public class BattleHeadTalkUI : MonoBehaviour
{
    public static BattleHeadTalkUI Instance { get; private set; }

    const string BubbleAssetPath = "Assets/Art/UI/剧情/对话框.png";

    Canvas _canvas;
    CanvasGroup _group;
    RectTransform _root;
    Image _bg;
    Text _text;
    Image _tail;
    UnitBase _follow;
    Coroutine _co;
    Coroutine _animCo;
    const float TypeCharsPerSecond = 28f;

    public static BattleHeadTalkUI Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("BattleHeadTalkUI", typeof(RectTransform));
        DontDestroyOnLoad(go);
        return go.AddComponent<BattleHeadTalkUI>();
    }

    void Awake()
    {
        Instance = this;
        Build();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Build()
    {
        _canvas = gameObject.GetComponent<Canvas>();
        if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 560;

        var scaler = gameObject.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(720f, 1280f);
        scaler.matchWidthOrHeight = 1f;

        if (gameObject.GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
        _group = gameObject.GetComponent<CanvasGroup>();
        if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 1f;

        var rootGo = new GameObject("Bubble", typeof(RectTransform), typeof(Image));
        rootGo.transform.SetParent(transform, false);
        _root = rootGo.GetComponent<RectTransform>();
        _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
        _root.pivot = new Vector2(0.5f, 0f);
        _root.sizeDelta = new Vector2(360f, 120f);
        _bg = rootGo.GetComponent<Image>();
        _bg.raycastTarget = false;
        _bg.color = Color.white;
        _bg.sprite = LoadBubbleSprite();
        if (_bg.sprite == null)
            _bg.color = new Color(0.93f, 0.88f, 0.75f, 0.96f);
        ApplyBubbleStretchMode();
        // 禁止非等比缩放，气泡只按文字改宽高
        _root.localScale = Vector3.one;

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(rootGo.transform, false);
        _text = textGo.AddComponent<Text>();
        _text.alignment = TextAnchor.MiddleCenter;
        _text.fontSize = 24;
        _text.color = new Color(0.22f, 0.14f, 0.1f, 1f);
        _text.horizontalOverflow = HorizontalWrapMode.Wrap;
        _text.verticalOverflow = VerticalWrapMode.Overflow;
        _text.raycastTarget = false;
        _text.font = GameFonts.GetChinese();
        var tr = _text.rectTransform;
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(26f, 22f);
        tr.offsetMax = new Vector2(-26f, -18f);

        var tailGo = new GameObject("Tail", typeof(RectTransform), typeof(Image));
        tailGo.transform.SetParent(rootGo.transform, false);
        _tail = tailGo.GetComponent<Image>();
        _tail.raycastTarget = false;
        _tail.color = new Color(0.94f, 0.9f, 0.84f, 1f);
        var tailRt = _tail.rectTransform;
        tailRt.anchorMin = tailRt.anchorMax = new Vector2(0.5f, 0f);
        tailRt.pivot = new Vector2(0.5f, 1f);
        tailRt.anchoredPosition = new Vector2(0f, -2f);
        tailRt.sizeDelta = new Vector2(20f, 16f);

        rootGo.SetActive(false);
    }

    /// <summary>
    /// 气泡底图有九宫格 border 才能随意拉伸；没设 border 的图用 Sliced 等于 Simple，
    /// 会被拉成扁条（看起来「被压缩了」）。没 border 就改成等比 Simple，靠尺寸保住比例。
    /// </summary>
    void ApplyBubbleStretchMode()
    {
        if (_bg == null) return;
        if (_bg.sprite == null)
        {
            _bg.type = Image.Type.Sliced;
            _bg.preserveAspect = false;
            _bubbleSliced = true;
            _bubbleAspect = 0f;
            return;
        }
        Vector4 b = _bg.sprite.border;
        _bubbleSliced = (b.x + b.y + b.z + b.w) > 0.01f;
        _bg.type = _bubbleSliced ? Image.Type.Sliced : Image.Type.Simple;
        _bg.preserveAspect = !_bubbleSliced;
        Rect r = _bg.sprite.rect;
        _bubbleAspect = r.height > 1f ? r.width / r.height : 0f;
    }

    bool _bubbleSliced = true;
    float _bubbleAspect;

    Sprite LoadBubbleSprite()
    {
        var sp = Resources.Load<Sprite>("UI/StoryProps/dialog_bubble");
        if (sp != null) return sp;
#if UNITY_EDITOR
        var ed = AssetDatabase.LoadAssetAtPath<Sprite>(BubbleAssetPath);
        if (ed != null) return ed;
#endif
        return null;
    }

    public Coroutine PlayLine(UnitBase speaker, string content, float hold = 1.8f)
    {
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(PlayLineRoutine(speaker, content, hold));
        return _co;
    }

    IEnumerator PlayLineRoutine(UnitBase speaker, string content, float hold)
    {
        _follow = speaker;
        string full = content ?? "";
        if (_text != null) _text.text = "";
        if (_root != null)
        {
            _root.localScale = Vector3.one;
            _root.gameObject.SetActive(true);
            FitBubbleSize(full);
        }
        if (_group != null) _group.alpha = 1f;
        PlayPopIn();
        RefreshFollowPosition();

        if (_text != null && full.Length > 0)
        {
            float delay = 1f / Mathf.Max(8f, TypeCharsPerSecond);
            for (int i = 1; i <= full.Length; i++)
            {
                _text.text = full.Substring(0, i);
                FitBubbleSize(full.Substring(0, i));
                float tType = 0f;
                while (tType < delay)
                {
                    tType += Time.unscaledDeltaTime;
                    RefreshFollowPosition();
                    yield return null;
                }
            }
            FitBubbleSize(full);
        }

        float t = 0f;
        while (t < hold)
        {
            t += Time.unscaledDeltaTime;
            RefreshFollowPosition();
            yield return null;
        }
        yield return FadeOutAndHide(0.16f);
    }

    void LateUpdate()
    {
        RefreshFollowPosition();
    }

    void FitBubbleSize(string content)
    {
        if (_root == null || _text == null) return;
        float maxW = 420f;
        float minW = 180f;
        _text.rectTransform.sizeDelta = new Vector2(maxW - 52f, 0f);
        float prefH = Mathf.Max(28f, _text.preferredHeight);
        float prefW = Mathf.Clamp(_text.preferredWidth + 52f, minW, maxW);
        // 短句别拉成扁长条；高度随字数涨，宽高比别离谱
        float h = Mathf.Clamp(prefH + 40f, 72f, 220f);
        float w = prefW;

        if (!_bubbleSliced && _bubbleAspect > 0.01f)
        {
            // 没有九宫格：必须严格按原图宽高比取尺寸，先按文字需要的高度反推宽度，
            // 宽度不够放字就反过来加宽再抬高，始终保持等比。
            w = Mathf.Max(w, h * _bubbleAspect);
            h = w / _bubbleAspect;
            if (w > maxW)
            {
                w = maxW;
                h = w / _bubbleAspect;
            }
            // 等比后高度仍装不下文字就整体放大
            float needH = prefH + 40f;
            if (h < needH)
            {
                h = needH;
                w = h * _bubbleAspect;
            }
        }

        _root.sizeDelta = new Vector2(w, h);
        _root.localScale = Vector3.one;
    }

    void RefreshFollowPosition()
    {
        if (_root == null || !_root.gameObject.activeSelf || _follow == null) return;
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 world = _follow.transform.position + new Vector3(0f, 1.65f, 0f);
        Vector3 screen = cam.WorldToScreenPoint(world);
        if (screen.z <= 0f)
        {
            // 镜头背后时仍贴屏幕上方，避免气泡直接消失
            screen.z = 1f;
            screen.x = Mathf.Clamp(screen.x, Screen.width * 0.2f, Screen.width * 0.8f);
            screen.y = Screen.height * 0.55f;
        }
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            transform as RectTransform, screen, null, out var local);
        var hostRt = transform as RectTransform;
        float halfW = _root.rect.width * 0.5f;
        float minX = hostRt.rect.xMin + halfW + 8f;
        float maxX = hostRt.rect.xMax - halfW - 8f;
        float clampedX = Mathf.Clamp(local.x, minX, maxX);
        _root.anchoredPosition = new Vector2(clampedX, local.y);
        RefreshTail(local.x - clampedX);
    }

    void RefreshTail(float speakerXOffset)
    {
        if (_tail == null || _root == null) return;
        float half = _root.rect.width * 0.5f - 18f;
        float x = Mathf.Clamp(speakerXOffset, -half, half);
        var rt = _tail.rectTransform;
        rt.anchoredPosition = new Vector2(x, -2f);
        float angle = Mathf.Clamp(-x * 0.08f, -24f, 24f);
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    void PlayPopIn()
    {
        if (_root == null) return;
        _root.localScale = new Vector3(0.9f, 0.9f, 1f);
        if (_animCo != null) StopCoroutine(_animCo);
        _animCo = StartCoroutine(PopInRoutine());
    }

    IEnumerator PopInRoutine()
    {
        if (_root == null) yield break;
        float t = 0f;
        const float dur = 0.12f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / dur);
            float s = Mathf.Lerp(0.9f, 1f, 1f - Mathf.Pow(1f - p, 3f));
            _root.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        _root.localScale = Vector3.one;
        _animCo = null;
    }

    IEnumerator FadeOutAndHide(float duration)
    {
        if (_group == null)
        {
            HideNow();
            yield break;
        }
        float t = 0f;
        float startA = _group.alpha;
        duration = Mathf.Max(0.05f, duration);
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            _group.alpha = Mathf.Lerp(startA, 0f, Mathf.Clamp01(t / duration));
            yield return null;
        }
        _group.alpha = 1f;
        HideNow();
    }

    public void HideNow()
    {
        if (_animCo != null)
        {
            StopCoroutine(_animCo);
            _animCo = null;
        }
        _follow = null;
        if (_root != null) _root.gameObject.SetActive(false);
    }
}
