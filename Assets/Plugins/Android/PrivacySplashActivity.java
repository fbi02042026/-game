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

    // TODO(主人): 替换为正式《隐私政策》全文，或改为 WebView 加载远程 H5。
    // 当前为占位文本，需包含：信息收集类型与用途、第三方SDK（含 Unity3d/团结引擎）说明、
    // 用户权利、联系方式、生效时间。正式文本见 Docs/隐私政策_草稿.md。
    private static final String PRIVACY_TEXT =
            "《隐私政策》\n\n" +
            "（本段为占位文本，请替换为正式隐私政策全文。需包含以下要点：）\n\n" +
            "1. 我们收集的信息：设备标识（Android ID）、设备型号、操作系统版本等，用于保障游戏正常运行与设备兼容。\n" +
            "2. 第三方 SDK：本游戏使用 Unity3d（团结）引擎，该引擎在运行时会读取 Android ID 用于设备识别与兼容性适配。\n" +
            "3. 信息使用目的：提供游戏服务、进行统计与反作弊。\n" +
            "4. 您的权利：可随时撤回同意，撤回后我们将停止收集并处理相关个人信息。\n" +
            "5. 联系方式：（请填写公司/个人邮箱与地址）。\n\n" +
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
        // 用 setClassName 而非 .class，避免 Plugins/Android 编译期依赖引擎类
        Intent intent = new Intent();
        intent.setClassName("com.unity3d.player", "com.unity3d.player.UnityPlayerActivity");
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
