using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 角色页 partial —— 2026-10-31 新增
// 职责：随身仓库「格子快满」时的左右滑动引导箭头。
//
// 【2026-10-31 主人拍板】「我说的翻页的箭头只是引导 并不是真正的按钮」——
// 所以这里**只建一个纯装饰节点**：不挂 Button、CanvasGroup.blocksRaycasts=false，绝不拦点击。
// 触发条件 = 格子快满（只剩 ≤1 格）；表现 = 从背包宽度 1/3 处往右滑到 2/3 处、边滑边闪，
// 5 秒后淡出消失；每次进角色页最多播一次（EnsureBagAlwaysOpen 里重置）。
//
// 箭头不用 Unicode 的 ▶ —— 项目中文像素字体不一定收录，会显示豆腐块
// （QuestHudBar 里已经踩过这个坑）。改成 Image + Resources/UI/Common/箭头01（尖朝下）
// 旋转 +90° 变朝右，取不到贴图就用代码生成的三角兜底。
public partial class CharacterUI
{
    GameObject _bagHintGo;
    RectTransform _bagHintRt;
    CanvasGroup _bagHintCg;
    Coroutine _bagHintCo;
    /// <summary>本次进页是否已经播过（防止每次 RefreshBag 都重播）。</summary>
    bool _bagHintPlayed;

    static Sprite _bagHintArrowSprite;

    /// <summary>引导箭头总时长（秒）。</summary>
    const float BagHintDuration = 5f;
    /// <summary>前段：箭头从 1/3 滑到 2/3 用多久（剩余时间是原地闪 + 淡出）。</summary>
    const float BagHintSlideTime = 2.5f;
    /// <summary>尾段：最后多久淡出。</summary>
    const float BagHintFadeTime = 1f;
    /// <summary>闪烁频率（次/秒）。</summary>
    const float BagHintBlinkHz = 1.2f;
    /// <summary>箭头显示边长（像素）。</summary>
    const float BagHintSize = 64f;
    /// <summary>箭头最低不透明度（闪烁谷值）。</summary>
    const float BagHintAlphaMin = 0.22f;
    /// <summary>
    /// true = 从背包 1/3 处往右滑到 2/3 处（主人给的第二个方案，默认）；
    /// false = 停在背包右侧中间原地闪（第一个方案）。改这一个开关就能切。
    /// </summary>
    const bool BagHintSlide = true;

    /// <summary>箭头颜色：暖金，压在格子图标上也看得见。</summary>
    static readonly Color BagHintColor = new Color(1f, 0.90f, 0.55f, 1f);

    /// <summary>每次进角色页重置（EnsureBagAlwaysOpen 调）。顺手把上一轮没播完的箭头收掉
    /// （中途离开页面时协程会被打断，不清会留一个半透明的箭头挂在面板上）。</summary>
    void ResetBagSwipeHint()
    {
        _bagHintPlayed = false;
        if (_bagHintCo != null) { StopCoroutine(_bagHintCo); _bagHintCo = null; }
        if (_bagHintCg != null) _bagHintCg.alpha = 0f;
        if (_bagHintGo != null && _bagHintGo.activeSelf) _bagHintGo.SetActive(false);
    }

    /// <summary>
    /// RefreshBag 里调：格子快满（已用 ≥ 解锁格数 − 1）就播一次滑动引导。
    /// 只播一次 —— 进页后第一次满足条件时播，之后不再重复（避免整理 / 领取后刷新又弹）。
    ///
    /// <para>⚠ 2026-10-31 现状：<see cref="BagPageCount"/> 恒为 1（32 格一页就把 16 个逻辑格全铺完了），
    /// 也就是说<b>现在没有第 2 页可滑</b>。主人拍板「翻页是必须的」，真翻页做出来后箭头才有实际意义；
    /// 若之后要先只做「多页才播」，在这里补一个 <c>BagPageCount() &lt;= 1 → return</c> 即可。</para>
    /// </summary>
    void TryPlayBagSwipeHint(int used, int unlocked)
    {
        if (_bagHintPlayed) return;
        if (unlocked <= 0) return;
        if (used < unlocked - 1) return;      // 还剩 2 格以上就不打扰
        // 播不出去（页面还在预载、没激活，起不了协程）就先不记「已播」，等真正显示时再来一次
        if (!PlayBagSwipeHint()) return;
        _bagHintPlayed = true;
    }

    /// <summary>返回是否真的播起来了。</summary>
    bool PlayBagSwipeHint()
    {
        if (bagPanelRoot == null) return false;
        if (!gameObject.activeInHierarchy) return false;   // 面板没显示时不能起协程
        EnsureBagHintNode();
        if (_bagHintGo == null) return false;
        if (_bagHintCo != null) StopCoroutine(_bagHintCo);
        _bagHintCo = StartCoroutine(CoBagSwipeHint());
        return true;
    }

    /// <summary>建引导节点（只建一次）。纯装饰：无 Button、不拦射线。</summary>
    void EnsureBagHintNode()
    {
        if (_bagHintGo != null || bagPanelRoot == null) return;

        var go = new GameObject("BagSwipeHint", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        go.transform.SetParent(bagPanelRoot, false);
        _bagHintRt = (RectTransform)go.transform;
        _bagHintRt.anchorMin = new Vector2(0.5f, 0.5f);
        _bagHintRt.anchorMax = new Vector2(0.5f, 0.5f);
        _bagHintRt.pivot = new Vector2(0.5f, 0.5f);
        _bagHintRt.sizeDelta = new Vector2(BagHintSize, BagHintSize);
        _bagHintRt.anchoredPosition = Vector2.zero;
        // 贴图尖朝下，+90° 转成朝右（和 QuestHudBar.CreateArrow 的角度约定一致）
        _bagHintRt.localRotation = Quaternion.Euler(0f, 0f, 90f);

        var img = go.GetComponent<Image>();
        img.sprite = BagHintArrowSprite();
        img.color = BagHintColor;
        img.preserveAspect = true;
        img.raycastTarget = false;             // 铁律：引导箭头不抢点击

        _bagHintCg = go.GetComponent<CanvasGroup>();
        _bagHintCg.blocksRaycasts = false;     // 再兜一层，绝不拦点击
        _bagHintCg.interactable = false;
        _bagHintCg.alpha = 0f;

        _bagHintGo = go;
        go.SetActive(false);
    }

    /// <summary>箭头贴图：优先用现成资源，取不到就代码生成一个尖朝下的白色三角。</summary>
    static Sprite BagHintArrowSprite()
    {
        if (_bagHintArrowSprite == null)
        {
            _bagHintArrowSprite = Resources.Load<Sprite>("UI/Common/箭头01");
            if (_bagHintArrowSprite == null) _bagHintArrowSprite = GenerateBagHintArrowSprite();
        }
        return _bagHintArrowSprite;
    }

    /// <summary>兜底：32×32、尖朝下的白色三角（写法同 QuestHudBar.GenerateArrowSprite）。</summary>
    static Sprite GenerateBagHintArrowSprite()
    {
        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // ny=1 是上边（底边），ny=0 是下边（尖）
                float nx = (x + 0.5f) / size;
                float ny = (y + 0.5f) / size;
                bool inside = Mathf.Abs(nx - 0.5f) <= 0.5f * (1f - ny);
                pixels[y * size + x] = inside ? Color.white : Color.clear;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    IEnumerator CoBagSwipeHint()
    {
        var panelRt = bagPanelRoot as RectTransform;
        // 首帧布局没算完时 rect.width 可能是 0 —— 用 600 兜底，只影响滑动距离，不会歪出面板
        float w = panelRt != null && panelRt.rect.width > 1f ? panelRt.rect.width : 600f;
        float fromX = BagHintSlide ? -w / 6f : (w / 2f - BagHintSize * 0.75f);  // 1/3 处 / 右侧中间
        float toX = w / 6f;                                                    // 2/3 处

        _bagHintGo.SetActive(true);
        _bagHintGo.transform.SetAsLastSibling();   // 压在格子上层，别被图标盖住
        _bagHintRt.anchoredPosition = new Vector2(fromX, 0f);

        float t = 0f;
        while (t < BagHintDuration)
        {
            t += Time.unscaledDeltaTime;

            if (BagHintSlide)
            {
                float k = Mathf.Clamp01(t / BagHintSlideTime);
                float e = 1f - (1f - k) * (1f - k);    // 先快后慢，像"划过去"
                _bagHintRt.anchoredPosition = new Vector2(Mathf.Lerp(fromX, toX, e), 0f);
            }

            float blink = 0.5f + 0.5f * Mathf.Sin(t * BagHintBlinkHz * Mathf.PI * 2f);
            float alpha = Mathf.Lerp(BagHintAlphaMin, 1f, blink);
            float fade = t > BagHintDuration - BagHintFadeTime
                ? Mathf.Clamp01((BagHintDuration - t) / BagHintFadeTime)
                : 1f;
            _bagHintCg.alpha = alpha * fade;

            yield return null;
        }

        _bagHintCg.alpha = 0f;
        _bagHintGo.SetActive(false);
        _bagHintCo = null;
    }
}
