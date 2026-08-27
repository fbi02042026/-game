using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 酒馆底栏入口：踢出冷却时图标变暗 + 秒数倒计时；点击弹出气泡「等酒醒了再来！」。
/// 运行时挂在 NavTavern 上，不改预制体布局。
/// </summary>
[DisallowMultipleComponent]
public class TavernNavBanHud : MonoBehaviour
{
    const string BubbleMsg = "等酒醒了再来！";
    const float BubbleHold = 1.8f;

    Image _dimOverlay;
    Text _countdown;
    RectTransform _bubbleRt;
    Text _bubbleText;
    CanvasGroup _bubbleCg;
    Coroutine _bubbleCo;
    int _lastShownSec = -1;
    Vector2 _bubbleBaseSize = new Vector2(180f, 107f);
    bool _bubbleBaseCaptured;

    public static TavernNavBanHud EnsureOn(Button tavernButton)
    {
        if (tavernButton == null) return null;
        var hud = tavernButton.GetComponent<TavernNavBanHud>();
        if (hud == null) hud = tavernButton.gameObject.AddComponent<TavernNavBanHud>();
        hud.EnsureVisuals();
        return hud;
    }

    void EnsureVisuals()
    {
        var btnRt = transform as RectTransform;
        if (btnRt == null) return;

        // 旧版整钮 CanvasGroup 变暗会把数字一起压暗，清掉
        var legacyCg = GetComponent<CanvasGroup>();
        if (legacyCg != null && legacyCg.alpha < 0.99f)
            legacyCg.alpha = 1f;

        if (_dimOverlay == null)
        {
            Transform existing = transform.Find("LandladyBanDim");
            GameObject go;
            if (existing != null) go = existing.gameObject;
            else
            {
                go = new GameObject("LandladyBanDim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(transform, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            _dimOverlay = go.GetComponent<Image>();
            if (_dimOverlay == null) _dimOverlay = go.AddComponent<Image>();
            _dimOverlay.raycastTarget = false;
            _dimOverlay.color = new Color(0f, 0f, 0f, 0.55f);
            go.SetActive(false);
        }

        if (_countdown == null)
        {
            Transform existing = transform.Find("LandladyBanCountdown");
            GameObject go;
            if (existing != null) go = existing.gameObject;
            else
            {
                go = new GameObject("LandladyBanCountdown", typeof(RectTransform), typeof(CanvasRenderer));
                go.transform.SetParent(transform, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            _countdown = go.GetComponent<Text>();
            if (_countdown == null) _countdown = go.AddComponent<Text>();
            _countdown.font = GameFonts.GetNumber();
            _countdown.fontSize = 28;
            _countdown.alignment = TextAnchor.MiddleCenter;
            _countdown.color = Color.white;
            _countdown.raycastTarget = false;
            _countdown.horizontalOverflow = HorizontalWrapMode.Overflow;
            _countdown.verticalOverflow = VerticalWrapMode.Overflow;
            if (go.GetComponent<Outline>() == null)
            {
                var ol = go.AddComponent<Outline>();
                ol.effectColor = new Color(0f, 0f, 0f, 0.9f);
                ol.effectDistance = new Vector2(1.5f, -1.5f);
            }
            go.transform.SetAsLastSibling();
        }

        if (_bubbleRt == null)
        {
            Transform existing = transform.Find("LandladyBanBubble");
            GameObject go;
            if (existing != null) go = existing.gameObject;
            else
            {
                go = new GameObject("LandladyBanBubble", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
                go.transform.SetParent(transform, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(0f, 8f);
                rt.sizeDelta = new Vector2(180f, 107f);
                var img = go.GetComponent<Image>();
                img.raycastTarget = false;
                img.sprite = StoryProps.Get(StoryProps.SpeechBubble);
                if (img.sprite == null)
                    img.color = new Color(0.95f, 0.92f, 0.88f, 0.96f);
                else
                    img.color = Color.white;
                img.type = Image.Type.Simple;

                var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer));
                textGo.transform.SetParent(go.transform, false);
                var tr = textGo.GetComponent<RectTransform>();
                tr.anchorMin = Vector2.zero;
                tr.anchorMax = Vector2.one;
                tr.offsetMin = new Vector2(14f, 18f);
                tr.offsetMax = new Vector2(-14f, -12f);
                var t = textGo.AddComponent<Text>();
                t.font = GameFonts.GetChinese();
                t.fontSize = 18;
                t.alignment = TextAnchor.MiddleCenter;
                t.color = new Color(0.28f, 0.28f, 0.30f, 1f);
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                t.raycastTarget = false;
                t.text = BubbleMsg;
            }

            _bubbleRt = go.GetComponent<RectTransform>();
            if (_bubbleRt != null && !_bubbleBaseCaptured)
            {
                // 已存在节点也统一回默认比例，避免旧版 168×72 拉扁
                if (_bubbleRt.sizeDelta.x > 8f && Mathf.Abs(_bubbleRt.sizeDelta.x / Mathf.Max(1f, _bubbleRt.sizeDelta.y) - 180f / 107f) > 0.15f)
                    _bubbleRt.sizeDelta = new Vector2(180f, 107f);
                _bubbleBaseSize = _bubbleRt.sizeDelta.x > 8f ? _bubbleRt.sizeDelta : new Vector2(180f, 107f);
                _bubbleBaseCaptured = true;
            }
            _bubbleCg = go.GetComponent<CanvasGroup>();
            if (_bubbleCg == null) _bubbleCg = go.AddComponent<CanvasGroup>();
            _bubbleCg.blocksRaycasts = false;
            _bubbleCg.interactable = false;
            _bubbleText = go.GetComponentInChildren<Text>(true);
            if (_bubbleText != null)
            {
                _bubbleText.font = GameFonts.GetChinese();
                _bubbleText.text = BubbleMsg;
            }
            go.SetActive(false);
            go.transform.SetAsLastSibling();
        }
    }

    /// <summary>每帧由 TownHub 刷新冷却态。</summary>
    public void Refresh(bool banned, float remainingSeconds)
    {
        EnsureVisuals();
        if (_dimOverlay != null)
            _dimOverlay.gameObject.SetActive(banned);

        if (_countdown == null) return;
        if (!banned)
        {
            _countdown.text = "";
            _countdown.enabled = false;
            _lastShownSec = -1;
            return;
        }

        int sec = Mathf.CeilToInt(Mathf.Max(0f, remainingSeconds));
        _countdown.enabled = true;
        if (sec != _lastShownSec)
        {
            _lastShownSec = sec;
            _countdown.text = sec.ToString();
            if (_dimOverlay != null) _dimOverlay.transform.SetAsLastSibling();
            _countdown.transform.SetAsLastSibling();
            if (_bubbleRt != null && _bubbleRt.gameObject.activeSelf)
                _bubbleRt.SetAsLastSibling();
        }
    }

    /// <summary>冷却中再点酒馆：图标上方气泡。</summary>
    public void ShowBannedBubble()
    {
        EnsureVisuals();
        if (_bubbleCo != null)
        {
            StopCoroutine(_bubbleCo);
            GlobalToastUI.PopBubble();
        }
        _bubbleCo = StartCoroutine(CoShowBubble());
    }

    IEnumerator CoShowBubble()
    {
        if (_bubbleRt == null) yield break;
        SpeechBubbleFit.Apply(_bubbleRt, _bubbleText, BubbleMsg, _bubbleBaseSize);
        _bubbleRt.gameObject.SetActive(true);
        _bubbleRt.SetAsLastSibling();
        if (_bubbleCg != null) _bubbleCg.alpha = 1f;
        GlobalToastUI.PushBubble();

        float t = 0f;
        const float pop = 0.12f;
        while (t < pop)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / pop);
            float s = Mathf.Lerp(0.82f, 1f, u);
            _bubbleRt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        _bubbleRt.localScale = Vector3.one;

        yield return new WaitForSecondsRealtime(BubbleHold);

        t = 0f;
        const float fade = 0.2f;
        while (t < fade)
        {
            t += Time.unscaledDeltaTime;
            if (_bubbleCg != null)
                _bubbleCg.alpha = 1f - Mathf.Clamp01(t / fade);
            yield return null;
        }

        _bubbleRt.gameObject.SetActive(false);
        SpeechBubbleFit.ResetSize(_bubbleRt, _bubbleBaseSize);
        if (_bubbleCg != null) _bubbleCg.alpha = 1f;
        GlobalToastUI.PopBubble();
        _bubbleCo = null;
    }

    void OnDisable()
    {
        if (_bubbleCo == null) return;
        StopCoroutine(_bubbleCo);
        _bubbleCo = null;
        GlobalToastUI.PopBubble();
        if (_bubbleRt != null) _bubbleRt.gameObject.SetActive(false);
    }
}
