package com.PixelAdventure.RiftBlade;

import android.app.Activity;
import android.content.Intent;
import android.content.SharedPreferences;
import android.os.Bundle;
import android.view.Gravity;
import android.view.View;
import android.view.ViewTreeObserver;
import android.view.ViewGroup;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;

/**
 * 隐私合规启动 Activity（两段式启动的第一段）。
 *
 * 关键约束：本类【不继承】UnityPlayerActivity，因此 onCreate 里不会发生
 * `new UnityPlayer()`，也就不会触发引擎 native 层读取 AndroidID 的行为。
 * 只有用户点击「已阅读并同意」后，才显式启动 UnityPlayerActivity。
 *
 * 合规要点：
 *  1. LAUNCHER 入口，先于 Unity 展示《隐私政策》全文；
 *  2. 用户未同意前，任何 Unity / 第三方 SDK 均未初始化；
 *  3. 「同意」按钮初始禁用，滚动到底后启用，确保用户已阅读；
 *  4. 不同意则直接退出应用；
 *  5. 同意后写入 SharedPreferences，二次启动不再弹窗（可按渠道要求改为每次弹）。
 */
public class PrivacySplashActivity extends Activity {
    private static final String PREFS_NAME = "privacy_prefs";
    private static final String KEY_ACCEPTED = "privacy_accepted";

    // 《像素冒险：裂隙之刃》隐私政策 —— 原生首启弹窗展示文本（摘要）。
    //
    // 【唯一权威全文】TapTap 隐私政策页（渠道审核以此为准）：
    //   https://www.taptap.cn/app/959893/common-privacy
    // 本段只做首启弹窗的摘要展示：渠道机器检测要求「同意前可见政策内容」，
    // 放链接不被识别，故必须内嵌文字。正文改动只需改 TapTap 那一份。
    // TODO(主人)：若 TapTap 正文条款有增删，同步更新下方摘要要点。
    private static final String PRIVACY_TEXT =
            "《像素冒险：裂隙之刃》隐私政策（摘要）\n\n" +
            "运营者：谷成亮（苹果核工坊）\n" +
            "联系邮箱：635016409@qq.com\n" +
            "本政策自游戏首次发布之日起生效。\n\n" +
            "我们非常重视您的个人信息与隐私保护。本页面为摘要，完整政策见文末地址。\n\n" +
            "一、我们收集的信息\n" +
            "1. 设备标识与设备信息：设备ID（Android ID）、设备名称、设备型号、操作系统及版本、屏幕分辨率、CPU/GPU 信息、语言、时区、网络类型等。\n" +
            "2. 使用目的：保障游戏正常运行、维护基础功能、兼容性与性能优化、设备识别与反作弊。\n" +
            "3. 我们不收集您的姓名、身份证号、手机号、精确位置、通讯录与相册。本游戏无账号注册与实名认证，游戏进度等数据仅保存在您的设备本地。\n\n" +
            "二、我们申请的设备权限\n" +
            "网络访问权限：仅用于游戏内容与资源的正常加载。您可随时在系统设置中关闭，关闭后部分功能可能无法使用。\n\n" +
            "三、第三方 SDK\n" +
            "1. Unity3D SDK（Unity Technologies / 团结引擎）：提供游戏引擎运行时服务，引擎启动时读取设备标识（Android ID）、设备型号、操作系统版本等信息。其隐私政策：https://unity.cn/legal/china-privacy-policy\n" +
            "2. TapSDK（易玩（上海）网络科技有限公司）：提供 TapTap 账号登录与游戏数据分析服务，读取系统版本、设备型号、设备 CPU 信息、网络类型、Android ID、设备内存信息。其隐私政策：https://developer.taptap.cn/docs/sdk/start/agreement/\n" +
            "我们会对第三方 SDK 的数据处理进行监督，其处理行为受其自身隐私政策约束。上述 SDK 均在您同意本政策之后才初始化。\n\n" +
            "四、我们如何保存与保护\n" +
            "您的数据保存在您的设备本地。我们仅在本政策目的所需及法律法规要求的最短期限内保存个人信息，期限届满后删除或匿名化。我们已采取网络隔离、数据加密、访问控制等合理措施保护您的信息安全。\n\n" +
            "五、共享、转让与公开披露\n" +
            "我们不会与任何业务合作伙伴共享您的个人信息，也不会擅自转让或公开披露您的个人信息。仅在法律法规明确要求，或经您明确同意的情况下才会提供。\n\n" +
            "六、您的权利\n" +
            "您可通过系统设置关闭设备权限，或通过上方邮箱联系我们，申请访问与更正、删除个人信息、撤回同意、注销账号。我们原则上不收取费用，将在十五个工作日内响应。\n\n" +
            "七、未成年人保护\n" +
            "不满十四周岁的未成年人使用本游戏须经其监护人同意；我们不会主动收集未成年人的个人信息。\n\n" +
            "八、本政策的更新\n" +
            "本政策可能适时变更，更新后将在下方地址公布；如变更涉及您的实质权利，我们将以显著方式提示。\n\n" +
            "九、完整政策\n" +
            "https://www.taptap.cn/app/959893/common-privacy\n\n" +
            "请完整阅读并理解上述条款后，点击“已阅读并同意”进入游戏。";

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        SharedPreferences sp = getSharedPreferences(PREFS_NAME, MODE_PRIVATE);
        if (sp.getBoolean(KEY_ACCEPTED, false)) {
            enterGame();
            return;
        }
        showPrivacyUi();
    }

    private void showPrivacyUi() {
        int pad = dp(16);

        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setBackgroundColor(0xFFFFFFFF);
        root.setPadding(pad, pad, pad, pad);

        TextView title = new TextView(this);
        title.setText("隐私政策");
        title.setTextSize(20f);
        title.setTextColor(0xFF000000);
        title.setGravity(Gravity.CENTER);
        title.setPadding(0, dp(8), 0, dp(8));

        ScrollView scroll = new ScrollView(this);
        scroll.setFillViewport(true);

        TextView body = new TextView(this);
        body.setText(PRIVACY_TEXT);
        body.setTextSize(14f);
        body.setTextColor(0xFF222222);
        body.setPadding(dp(8), dp(8), dp(8), dp(8));

        scroll.addView(body);

        LinearLayout btnRow = new LinearLayout(this);
        btnRow.setOrientation(LinearLayout.HORIZONTAL);
        btnRow.setPadding(0, dp(8), 0, 0);

        Button decline = new Button(this);
        decline.setText("不同意并退出");
        decline.setOnClickListener(v -> exitApp());

        Button agree = new Button(this);
        agree.setText("已阅读并同意");
        agree.setEnabled(false); // 滚动到底前禁用，确保已阅读
        agree.setOnClickListener(v -> {
            getSharedPreferences(PREFS_NAME, MODE_PRIVATE)
                    .edit().putBoolean(KEY_ACCEPTED, true).apply();
            enterGame();
        });

        btnRow.addView(decline, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f));
        btnRow.addView(agree, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f));

        root.addView(title);
        root.addView(scroll, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, 0, 1f));
        root.addView(btnRow);

        setContentView(root);

        // 滚动到底部才允许点击「同意」
        scroll.getViewTreeObserver().addOnScrollChangedListener(new ViewTreeObserver.OnScrollChangedListener() {
            @Override
            public void onScrollChanged() {
                if (!agree.isEnabled()) {
                    int extent = scroll.getChildAt(0).getBottom() - (scroll.getHeight() + scroll.getScrollY());
                    if (extent <= dp(8)) {
                        agree.setEnabled(true);
                    }
                }
            }
        });
    }

    private void enterGame() {
        // 用 setClassName 而非 .class，避免 Plugins/Android 编译期依赖引擎类。
        // ⚠️ 第一个参数必须是【应用包名（applicationId）】，即 getPackageName()
        //    （= com.PixelAdventure.RiftBlade）；之前硬编码 "com.unity3d.player" 只是
        //    UnityPlayerActivity 的 Java 包名，设备上不存在该应用 → startActivity
        //    抛 ActivityNotFoundException 闪退（2026-10-14 真机「点已同意也退出」的根因）。
        Intent intent = new Intent();
        intent.setClassName(getPackageName(), "com.unity3d.player.UnityPlayerActivity");
        startActivity(intent);
        finish();
    }

    private void exitApp() {
        finishAffinity();
        // 部分机型 finishAffinity 不足以保证退出，补一刀
        android.os.Process.killProcess(android.os.Process.myPid());
    }

    private int dp(int v) {
        return Math.round(getResources().getDisplayMetrics().density * v);
    }
}
