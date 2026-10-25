using UnityEditor;
using UnityEngine;

namespace Dirichlet.Mediation.Editor
{
    /// <summary>
    /// Editor window for configuring optional adapters (CSJ, GDT, IQY, BD).
    /// DirichletAdSDK is always included as the core SDK.
    /// Supports both Android and iOS platforms.
    /// </summary>
    public class DirichletAdapterSettingsWindow : EditorWindow
    {
        // 默认值迁移标记：确保“首次安装/升级后”iOS 适配器默认勾选
        private const string PrefKeyDefaultsInitialized = "Dirichlet.Mediation.AdapterSettings.DefaultsInitialized.v1";

        // Android Adapter Preferences
        private const string PrefKeyAndroidEnableCsj = "Dirichlet.Android.EnableCSJ";
        private const string PrefKeyAndroidEnableGdt = "Dirichlet.Android.EnableGDT";
        private const string PrefKeyAndroidEnableIqy = "Dirichlet.Android.EnableIQY";
        private const string PrefKeyAndroidEnableBd = "Dirichlet.Android.EnableBD";

        // iOS Adapter Preferences (DirichletAdSDK is always enabled)
        private const string PrefKeyIOSEnableCsj = "Dirichlet.iOS.EnableCSJ";
        private const string PrefKeyIOSEnableGdt = "Dirichlet.iOS.EnableGDT";
        
        private bool androidFoldout = true;
        private bool iosFoldout = true;

        [MenuItem("Dirichlet/Adapter Settings", priority = 2)]
        public static void ShowWindow()
        {
            var window = GetWindow<DirichletAdapterSettingsWindow>("Dirichlet Adapter Settings");
            window.minSize = new Vector2(450, 400);
            window.Show();
        }

        private void OnEnable()
        {
            EnsureDefaultAdapterPrefs();
        }

        private static void EnsureDefaultAdapterPrefs()
        {
            if (EditorPrefs.GetBool(PrefKeyDefaultsInitialized, false))
            {
                return;
            }

            // 默认“勾上”：Android 全部 adapter；iOS CSJ/GDT。
            // 说明：历史版本可能将 iOS 默认写成未勾选（EditorPrefs=false），升级后需要一次性迁移。
            EditorPrefs.SetBool(PrefKeyAndroidEnableCsj, true);
            EditorPrefs.SetBool(PrefKeyAndroidEnableGdt, true);
            EditorPrefs.SetBool(PrefKeyAndroidEnableIqy, true);
            EditorPrefs.SetBool(PrefKeyAndroidEnableBd, true);
            EditorPrefs.SetBool(PrefKeyIOSEnableCsj, true);
            EditorPrefs.SetBool(PrefKeyIOSEnableGdt, true);

            EditorPrefs.SetBool(PrefKeyDefaultsInitialized, true);
        }

        private void OnGUI()
        {
            // 兜底：避免 domain reload / 脚本重载导致 OnEnable 未触发
            EnsureDefaultAdapterPrefs();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Dirichlet Mediation Adapter Configuration", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Configure which adapters to include in Android and iOS builds. " +
                "These settings affect Gradle dependencies (Android) and CocoaPods dependencies (iOS) during build.",
                MessageType.Info);

            EditorGUILayout.Space(10);

            // Android Section
            androidFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(androidFoldout, "Android Adapters");
            if (androidFoldout)
            {
                EditorGUI.indentLevel++;
                
                var androidEnableCsj = EditorPrefs.GetBool(PrefKeyAndroidEnableCsj, true);
                var androidEnableGdt = EditorPrefs.GetBool(PrefKeyAndroidEnableGdt, true);
                var androidEnableIqy = EditorPrefs.GetBool(PrefKeyAndroidEnableIqy, true);
                var androidEnableBd = EditorPrefs.GetBool(PrefKeyAndroidEnableBd, true);

                EditorGUI.BeginChangeCheck();

                androidEnableCsj = EditorGUILayout.Toggle(
                    new GUIContent("Enable CSJ (穿山甲)", "Include CSJ adapter and SDK in Android build"),
                    androidEnableCsj);

                EditorGUILayout.Space(3);

                androidEnableGdt = EditorGUILayout.Toggle(
                    new GUIContent("Enable GDT (广点通)", "Include GDT adapter and SDK in Android build"),
                    androidEnableGdt);

                EditorGUILayout.Space(3);

                androidEnableIqy = EditorGUILayout.Toggle(
                    new GUIContent("Enable IQY (爱奇艺)", "Include IQY adapter and SDK in Android build"),
                    androidEnableIqy);

                EditorGUILayout.Space(3);

                androidEnableBd = EditorGUILayout.Toggle(
                    new GUIContent("Enable BD (百度)", "Include Baidu adapter and MobAds SDK in Android build"),
                    androidEnableBd);

                if (EditorGUI.EndChangeCheck())
                {
                    EditorPrefs.SetBool(PrefKeyAndroidEnableCsj, androidEnableCsj);
                    EditorPrefs.SetBool(PrefKeyAndroidEnableGdt, androidEnableGdt);
                    EditorPrefs.SetBool(PrefKeyAndroidEnableIqy, androidEnableIqy);
                    EditorPrefs.SetBool(PrefKeyAndroidEnableBd, androidEnableBd);
                }

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Current Android Settings:", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"  CSJ: {(androidEnableCsj ? "✓ Enabled" : "✗ Disabled")}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"  GDT: {(androidEnableGdt ? "✓ Enabled" : "✗ Disabled")}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"  IQY: {(androidEnableIqy ? "✓ Enabled" : "✗ Disabled")}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"  BD: {(androidEnableBd ? "✓ Enabled" : "✗ Disabled")}", EditorStyles.miniLabel);

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(10);

            // iOS Section
            iosFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(iosFoldout, "iOS Adapters");
            if (iosFoldout)
            {
                EditorGUI.indentLevel++;
                
                var iosEnableCsj = EditorPrefs.GetBool(PrefKeyIOSEnableCsj, true);
                var iosEnableGdt = EditorPrefs.GetBool(PrefKeyIOSEnableGdt, true);

                EditorGUI.BeginChangeCheck();

                iosEnableCsj = EditorGUILayout.Toggle(
                    new GUIContent("Enable CSJ (穿山甲)", "Include CSJ adapter in iOS build via CocoaPods"),
                    iosEnableCsj);

                EditorGUILayout.Space(3);

                iosEnableGdt = EditorGUILayout.Toggle(
                    new GUIContent("Enable GDT (广点通)", "Include GDT adapter in iOS build via CocoaPods"),
                    iosEnableGdt);

                if (EditorGUI.EndChangeCheck())
                {
                    EditorPrefs.SetBool(PrefKeyIOSEnableCsj, iosEnableCsj);
                    EditorPrefs.SetBool(PrefKeyIOSEnableGdt, iosEnableGdt);
                }

                EditorGUILayout.Space(3);

                // DirichletAdSDK is always enabled (required core SDK)
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.Toggle(
                    new GUIContent("Enable DirichletAdSDK", "Core SDK, always included in iOS build via CocoaPods"),
                    true);
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Current iOS Settings:", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"  CSJ: {(iosEnableCsj ? "✓ Enabled" : "✗ Disabled")}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"  GDT: {(iosEnableGdt ? "✓ Enabled" : "✗ Disabled")}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("  DirichletAdSDK: ✓ Enabled (Required)", EditorStyles.miniLabel);

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "Note: Changes take effect on the next platform build.\n\n" +
                "• Android: Gradle dependencies will be modified during export.\n" +
                "• iOS: Podfile will be generated dynamically and pod install will run automatically.",
                MessageType.Warning);
        }
    }
}
