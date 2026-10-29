package com.PixelAdventure.RiftBlade;

import android.content.ContentProvider;
import android.content.ContentValues;
import android.content.Context;
import android.content.Intent;
import android.database.Cursor;
import android.net.Uri;
import android.os.ParcelFileDescriptor;
import android.text.TextUtils;

import java.io.File;
import java.io.FileNotFoundException;

/**
 * 带图分享的极简 ContentProvider + Java 侧 Intent 组装（2026-10-29 主人拍板：分享统一带竖图）。
 *
 * 为什么不用 androidx FileProvider：本工程未引 androidx 依赖，为不碰 gradle 模板，
 * 用 android framework 自带 ContentProvider 手写一个极简版（openFile 返回只读 fd 即可）。
 *
 * 为什么把 Intent 组装放在 Java 静态方法（{@link #launchImageShare}）：
 * C# 侧 AndroidJavaObject 调 putExtra(String, STREAM) 走 Parcelable 重载，JNI 按参数类名
 * 精确解析签名，android.net.Uri → Parcelable（接口）的 fallback 不保证各引擎版本一致 ——
 * 不确定 API 不猜，Java 编译期确定。C# 只跨一个 (String,String)->boolean 的 CallStatic。
 *
 * 安全口径：
 *  1. exported=false + grantUriPermissions=true —— 只有本应用 grant 的接收方能读；
 *  2. 只暴露固定文件名 {@link #SHARE_FILE_NAME}（Unity 写在 app 私有 filesDir 下），
 *     uri 其它 lastPathSegment 一律 FileNotFoundException，无路径穿越面；
 *  3. authorities 为固定串（与 AndroidManifest.xml 的 provider 声明、
 *     ShareService.cs 的 ShareAuthority 三处同值，改要一起改），不依赖
 *     ${applicationId} 占位符，规避团结/AGP 两层 manifest 替换的不确定性。
 *
 * 微信注意：ACTION_SEND 图文同发时微信只收图丢字 —— 海报图上自带宣传语，可接受。
 */
public class ShareFileProvider extends ContentProvider {

    /** content:// authority（与 AndroidManifest.xml、ShareService.cs 三处同值）。 */
    public static final String AUTHORITY = "com.pixeladventure.riftblade.sharefile";

    /** 分享图文件名（与 ShareService.cs 的 ShareImageFileName 同值，改要一起改）。 */
    public static final String SHARE_FILE_NAME = "share_poster.jpg";

    /** Unity 调：拼出分享图 content:// Uri（极简，无 query 参数）。 */
    public static Uri buildShareUri(Context ctx) {
        return Uri.parse("content://" + AUTHORITY + "/" + SHARE_FILE_NAME);
    }

    /**
     * Unity 调（唯一 JNI 出入口）：组装「带竖图 + 文案」的 ACTION_SEND 并唤起系统分享面板。
     * Unity 侧须已把分享图写到 filesDir/{@link #SHARE_FILE_NAME}（TryPrepareShareImage）；
     * 文件不存在 / 组装 / 唤起任何一步失败都返回 false，由 C# 侧降级纯文本（LogError 可见）。
     */
    public static boolean launchImageShare(String text, String chooserTitle) {
        try {
            Context ctx = com.unity3d.player.UnityPlayer.currentActivity;
            if (ctx == null) return false;
            File f = new File(ctx.getFilesDir(), SHARE_FILE_NAME);
            if (!f.exists()) return false;   // 图未就绪（C# 写盘失败？），让 C# 走纯文本降级

            Intent intent = new Intent(Intent.ACTION_SEND);
            intent.setType("image/jpeg");
            intent.putExtra(Intent.EXTRA_STREAM, buildShareUri(ctx));
            intent.putExtra(Intent.EXTRA_TEXT, text);            // QQ 等收图文；微信只收图丢字
            intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
            ctx.startActivity(Intent.createChooser(intent, chooserTitle));
            return true;
        } catch (Throwable t) {
            return false;   // 不吞细节：C# 侧返回 false 会 LogError 降级纯文本，真机排查看 logcat
        }
    }

    @Override
    public boolean onCreate() {
        return true;
    }

    @Override
    public ParcelFileDescriptor openFile(Uri uri, String mode) throws FileNotFoundException {
        if (uri == null || !TextUtils.equals(SHARE_FILE_NAME, uri.getLastPathSegment())) {
            throw new FileNotFoundException("ShareFileProvider 只允许读 " + SHARE_FILE_NAME);
        }
        Context ctx = getContext();
        File f = ctx != null ? new File(ctx.getFilesDir(), SHARE_FILE_NAME) : null;
        if (f == null || !f.exists()) {
            throw new FileNotFoundException("分享图尚未生成: " + (f == null ? "null" : f.getAbsolutePath()));
        }
        return ParcelFileDescriptor.open(f, ParcelFileDescriptor.MODE_READ_ONLY);
    }

    @Override
    public Cursor query(Uri uri, String[] projection, String selection,
                        String[] selectionArgs, String sortOrder) {
        return null;   // 分享方不查询元数据也能读（各接收方 query 失败会直接 openFile）
    }

    @Override
    public String getType(Uri uri) {
        return "image/jpeg";
    }

    @Override
    public Uri insert(Uri uri, ContentValues values) {
        return null;   // 只读
    }

    @Override
    public int delete(Uri uri, String selection, String[] selectionArgs) {
        return 0;      // 只读
    }

    @Override
    public int update(Uri uri, ContentValues values, String selection, String[] selectionArgs) {
        return 0;      // 只读
    }
}

