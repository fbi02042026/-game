using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 佣兵战力<b>绘制</b>入口：战斗界面同款「战斗力」美术（标题 + 0~9 数字子图）。
/// <para>取数<b>不在这里</b> —— 数值唯一真源 = <see cref="PowerCalc.MercPowerOf"/>
/// （面板显示、本局总战力入账、抽卡卡面「+N 战力」三处同调它一个函数）。</para>
///
/// <para><b>2026-11-18 主人拍板</b>：佣兵信息弹窗（huodeyongbing）左上角显示战力，
/// 方便玩家辨识哪个佣兵厉害、厉害多少。口径选「属性公式」——
/// 稀有度成长 / 等级 / 局外星 / 养成资质全进去，普通约 520、稀有约 780、传说 930~1130，
/// 一眼能分高低；<b>并且这个数字原样计入玩家本局总战力</b>，佣兵不再是「看不见的挂件」。</para>
///
/// <para><b>美术来源</b>：<c>Resources/UI/战斗力/战斗力.png</c> 一张图集切 11 张子图
/// （<c>战斗力</c> 标题 + <c>战斗力_0</c>…<c>战斗力_9</c>）。
/// ⚠ 该贴图 meta guid 是团结签发的 56 字符串，拖进预制体不落盘（2026-09-24 教训），
/// 所以<b>一律</b>用 <c>Resources.LoadAll&lt;Sprite&gt;</c> 按路径加载后覆盖 sprite ——
/// 预制体里那组节点即使 sprite 悬空也能正常显示（铁律：代码不依赖 prefab 里的 sprite guid）。</para>
///
/// <para><b>排布</b>：按位取子图、每个位的宽高跟子图原始像素走（「1」窄、「8」宽，与战斗界面一致）；
/// <b>纵向 y 一律沿用预制体里主人摆的值</b>，代码不碰（不抢美术排版）。
/// <b>横向从标题右侧起排</b>（2026-11-22，见 <see cref="DigitStartX"/>）——
/// 战斗界面 <c>PlayerPowerHud</c> 是「整行居中」，本弹窗标题与数字并排同一父节点，居中会压到标题上，故口径不同。</para>
/// </summary>
public static class MercPowerBadge
{
    /// <summary>图集路径（Resources 下，不带后缀）。</summary>
    const string AtlasPath = "UI/战斗力/战斗力";
    /// <summary>标题子图名。</summary>
    const string TitleName = "战斗力";
    /// <summary>数字子图名前缀（拼上 0~9）。</summary>
    const string DigitPrefix = "战斗力_";

    /// <summary>最多显示几位（预制体 Panel/战斗力 下摆了 number~number5 共 6 个位）。</summary>
    public const int MaxDigits = 6;

    /// <summary>数字之间的字距（像素），与战斗界面 <c>PlayerPowerHud.FrameDigitTracking</c> 同值。</summary>
    const float Tracking = 2f;

    /// <summary>
    /// 【2026-11-22 主人拍板】数字行起排 x（容器本地像素，首个数字左边缘）。
    /// 标题 zhandouli 在 x=0、宽 258、pivot 中心 → 字的 alpha 右边缘实测约 129（图集切片 258 宽、右侧无透明留白）。
    /// 起点 137 = 贴标题右侧留约 8 间距。实测数字子图左右无透明留白，故视觉间距 = DigitStartX - 129。
    /// 2026-11-22 二次微调：160（间距 31）主人仍嫌远 → 收到 137（间距 8）。
    /// 旧代码照搬战斗界面的「整行居中」，把数字压回 x≈0 撞上标题，这里改回从标题右侧起排。
    /// 战斗界面 PlayerPowerHud 的居中口径不动。
    /// </summary>
    const float DigitStartX = 137f;

    static Sprite _title;
    static readonly Sprite[] _digits = new Sprite[10];
    static bool _loadAttempted;
    static readonly int[] _buf = new int[MaxDigits];

    /// <summary>
    /// 加载图集并缓存（只试一次）。返回 false = 图不全，调用方直接不画（不兜底、不建树）。
    /// <para>按<b>子图名末位数字</b>认位，不按数组下标 —— 团结 Sprite Editor 重切遇到同名残留会
    /// 自动跳号（历史上出现过 _0~_7、_9、_10 缺 _8），按名字认才不会被排序坑到
    /// （与 PlayerPowerHud 注释里同一个坑）。</para>
    /// </summary>
    static bool EnsureArt()
    {
        if (_loadAttempted) return _digits[0] != null;
        _loadAttempted = true;

        var all = Resources.LoadAll<Sprite>(AtlasPath);
        if (all == null || all.Length == 0)
        {
            Debug.LogError("[MercPowerBadge] 取不到战力美术图集 " + AtlasPath + "（佣兵战力不显示）");
            return false;
        }

        for (int i = 0; i < all.Length; i++)
        {
            var sp = all[i];
            if (sp == null || string.IsNullOrEmpty(sp.name)) continue;
            string n = sp.name;
            if (n == TitleName) { _title = sp; continue; }
            if (n.Length == DigitPrefix.Length + 1 && n.StartsWith(DigitPrefix))
            {
                char c = n[n.Length - 1];
                if (c >= '0' && c <= '9') _digits[c - '0'] = sp;
            }
        }

        for (int i = 0; i < 10; i++)
        {
            if (_digits[i] != null) continue;
            Debug.LogError("[MercPowerBadge] 战力数字图缺「" + DigitPrefix + i + "」（图集重切后子图名变了？战力不显示）");
            return false;
        }
        if (_title == null)
            Debug.LogWarning("[MercPowerBadge] 图集里没有标题子图「" + TitleName + "」，标题位保持预制体原样");
        return true;
    }

    /// <summary>
    /// 把战力画到节点组上：标题换图 + 数字按位换图并整行居中。
    /// 幂等 —— 弹窗是 DDOL 单例复用，每次 Fill 都全量重刷，不会残留上一个佣兵的位数。
    /// </summary>
    /// <param name="title">Panel/战斗力/zhandouli 的 Image（可为 null，只画数字）</param>
    /// <param name="slots">Panel/战斗力/number…number5 的 Image 数组（最多 MaxDigits 个）</param>
    public static void Paint(Image title, Image[] slots, int power)
    {
        if (!EnsureArt()) return;
        if (title != null && _title != null) title.sprite = _title;
        if (slots == null || slots.Length == 0) return;

        int n = Mathf.Max(0, power);
        int count = 0;
        do
        {
            _buf[count++] = n % 10;
            n /= 10;
        } while (n > 0 && count < MaxDigits);

        if (n > 0)
            Debug.LogError("[MercPowerBadge] 战力 " + power + " 超过 " + MaxDigits + " 位，只显示低位（预制体数字位不够）");

        int shown = Mathf.Min(count, slots.Length);

        // 【2026-11-22 主人拍板】数字从标题右侧起排，不再整行居中（居中会压到 x=0 的标题上）。
        // 起点见 DigitStartX（标题字右边缘实测约 129，留约 8 间距，贴字右边不粘连）。
        float x = DigitStartX;
        for (int slot = 0; slot < shown; slot++)
        {
            var sp = _digits[_buf[shown - 1 - slot]];   // 高位在左：缓存是倒序的
            var img = slots[slot];
            if (img == null)
            {
                Debug.LogError("[MercPowerBadge] 第 " + slot + " 个数字位为空（战力数字缺位）");
                continue;
            }
            float w = sp.rect.width;
            img.sprite = sp;
            var rt = img.rectTransform;
            // 宽高跟子图原始像素走（与 PlayerPowerHud.PaintNumber 同一做法）；y 保持预制体值不动
            rt.sizeDelta = new Vector2(w, sp.rect.height);
            rt.anchoredPosition = new Vector2(x + w * 0.5f, rt.anchoredPosition.y);
            img.enabled = true;
            x += w + Tracking;
        }
        for (int slot = shown; slot < slots.Length; slot++)
            if (slots[slot] != null) slots[slot].enabled = false;
    }
}
