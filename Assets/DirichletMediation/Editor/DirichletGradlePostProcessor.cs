#if UNITY_ANDROID
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Android;
using UnityEngine;

namespace Dirichlet.Mediation.Editor
{
    /// <summary>
    /// Post-processes the Gradle files to inject Dirichlet SDK dependencies.
    /// 
    /// This approach allows coexistence with other SDKs (e.g., TapSDK) by injecting
    /// dependencies into Unity-generated Gradle files rather than shipping static templates.
    /// </summary>
    public class DirichletGradlePostProcessor : IPostGenerateGradleAndroidProject
    {
        private const string TAG = "[DirichletMediation]";
        
        // Marker comments to identify our injected content
        private const string DIRICHLET_DEPS_START = "// Dirichlet Mediation Dependencies Start";
        private const string DIRICHLET_DEPS_END = "// Dirichlet Mediation Dependencies End";
        private const string DIRICHLET_REPOS_START = "// Dirichlet Mediation Repositories Start";
        private const string DIRICHLET_REPOS_END = "// Dirichlet Mediation Repositories End";
        
        public int callbackOrder => 100; // Run after EDM4U (which uses lower values)

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var enableCsj = EditorPrefs.GetBool("Dirichlet.Android.EnableCSJ", true);
            var enableGdt = EditorPrefs.GetBool("Dirichlet.Android.EnableGDT", true);
            var enableIqy = EditorPrefs.GetBool("Dirichlet.Android.EnableIQY", true);
            var enableBd = EditorPrefs.GetBool("Dirichlet.Android.EnableBD", true);

            Debug.Log($"{TAG} Processing Gradle project at: {path}");
            Debug.Log($"{TAG} CSJ enabled: {enableCsj}, GDT enabled: {enableGdt}, " +
                      $"IQY enabled: {enableIqy}, BD enabled: {enableBd}");

            ProcessGradleProperties(path);
            ProcessBuildGradle(path, enableCsj, enableGdt, enableIqy, enableBd);
            ProcessSettingsGradle(path, enableCsj, enableGdt, enableIqy, enableBd);
        }

        private void ProcessBuildGradle(string projectPath, bool enableCsj, bool enableGdt,
            bool enableIqy, bool enableBd)
        {
            // Unity 2019.3+: projectPath is unityLibrary folder, build.gradle is directly inside
            // Unity 2019.2 and below: projectPath might be the root, need to search
            var gradlePath = Path.Combine(projectPath, "build.gradle");
            
            if (!File.Exists(gradlePath))
            {
                // Fallback: try to find build.gradle in subdirectories
                var searchPaths = new[]
                {
                    Path.Combine(projectPath, "unityLibrary", "build.gradle"),
                    Path.Combine(projectPath, "src", "main", "build.gradle")
                };
                
                foreach (var path in searchPaths)
                {
                    if (File.Exists(path))
                    {
                        gradlePath = path;
                        break;
                    }
                }
            }
            
            if (!File.Exists(gradlePath))
            {
                Debug.LogWarning($"{TAG} Could not find build.gradle at {projectPath}");
                return;
            }
            
            Debug.Log($"{TAG} Found build.gradle at: {gradlePath}");

            var content = File.ReadAllText(gradlePath);
            Debug.Log($"{TAG} Original build.gradle length: {content.Length}");

            // Remove any previously injected content (for clean re-injection)
            content = RemoveInjectedContent(content, DIRICHLET_DEPS_START, DIRICHLET_DEPS_END);
            content = RemoveInjectedContent(content, DIRICHLET_REPOS_START, DIRICHLET_REPOS_END);

            // Inject repositories
            content = InjectRepositories(content, enableCsj, enableGdt, enableIqy, enableBd);

            // Inject dependencies
            content = InjectDependencies(content, projectPath, enableCsj, enableGdt, enableIqy, enableBd);

            File.WriteAllText(gradlePath, content);
            Debug.Log($"{TAG} Updated build.gradle with Dirichlet Mediation dependencies");
        }

        private string InjectRepositories(string content, bool enableCsj, bool enableGdt,
            bool enableIqy, bool enableBd)
        {
            // Check if our repos are already injected
            if (content.Contains(DIRICHLET_REPOS_START))
            {
                return content;
            }
            
            var reposBlock = new StringBuilder();
            reposBlock.AppendLine(DIRICHLET_REPOS_START);
            reposBlock.AppendLine("    google()");
            reposBlock.AppendLine("    mavenCentral()");
            reposBlock.AppendLine("    flatDir {");
            reposBlock.AppendLine("        dirs 'libs', 'DirichletMediation/libs'");
            reposBlock.AppendLine("    }");
            
            if (enableCsj)
            {
                reposBlock.AppendLine("    maven { url 'https://artifact.bytedance.com/repository/pangle' }");
            }
            if (enableGdt)
            {
                reposBlock.AppendLine("    maven { url 'https://mirrors.cloud.tencent.com/nexus/repository/maven-public/' }");
            }
            
            reposBlock.AppendLine($"    {DIRICHLET_REPOS_END}");
            
            // Try to find repositories block and inject after opening brace
            var reposPattern = new Regex(@"(repositories\s*\{)");
            if (reposPattern.IsMatch(content))
            {
                content = reposPattern.Replace(content, m => 
                    m.Groups[1].Value + "\n    " + reposBlock.ToString(), 1);
                Debug.Log($"{TAG} Injected repositories block");
            }
            else
            {
                Debug.LogWarning($"{TAG} Could not find repositories block, adding one");
                var applyPattern = new Regex(@"(apply plugin:\s*'com\.android\.library'[^\n]*\n)");
                if (applyPattern.IsMatch(content))
                {
                    content = applyPattern.Replace(content, m =>
                        m.Groups[1].Value + "\nrepositories {\n    " + reposBlock.ToString() + "}\n", 1);
                }
            }
            
            return content;
        }

        private string InjectDependencies(string content, string projectPath, bool enableCsj,
            bool enableGdt, bool enableIqy, bool enableBd)
        {
            // Check if our deps are already injected
            if (content.Contains(DIRICHLET_DEPS_START))
            {
                return content;
            }
            
            var depsBlock = new StringBuilder();
            depsBlock.AppendLine(DIRICHLET_DEPS_START);
            
            // Core Mediation AAR
            var mediationAarName = FindLocalAarBaseName(projectPath, "DirichletMediation/libs", "DirichletAD_Mediation_*.aar");
            depsBlock.AppendLine($"    implementation(name: '{mediationAarName}', ext: 'aar')");

            // CSJ (穿山甲) Adapter and SDK
            if (enableCsj)
            {
                var csjAdapterName = FindLocalAarBaseName(projectPath, "DirichletMediation/libs", "DirichletAD_CSJ_Adapter_*.aar");
                depsBlock.AppendLine($"    implementation(name: '{csjAdapterName}', ext: 'aar')");
                depsBlock.AppendLine("    implementation('com.pangle.cn:ads-sdk-pro:7.8.1.5') { exclude group: 'com.android.support' }");
            }

            // GDT (广点通) Adapter and SDK
            if (enableGdt)
            {
                var gdtAdapterName = FindLocalAarBaseName(projectPath, "DirichletMediation/libs", "DirichletAD_GDT_Adapter_*.aar");
                depsBlock.AppendLine($"    implementation(name: '{gdtAdapterName}', ext: 'aar')");
                depsBlock.AppendLine("    implementation 'com.qq.e.union:union:4.701.1571'");
            }

            // IQY (爱奇艺) Adapter and SDK
            if (enableIqy)
            {
                var iqyAdapterName = FindLocalAarBaseName(projectPath, "DirichletMediation/libs", "DirichletAD_IQY_Adapter_*.aar");
                var iqySdkName = FindLocalAarBaseName(projectPath, "DirichletMediation/libs", "iadsdk-release-*.aar");
                depsBlock.AppendLine($"    implementation(name: '{iqyAdapterName}', ext: 'aar')");
                depsBlock.AppendLine($"    implementation(name: '{iqySdkName}', ext: 'aar')");
                depsBlock.AppendLine("    implementation 'androidx.constraintlayout:constraintlayout:2.1.4'");
            }

            // BD (百度) Adapter and SDK. MobAds requires an AndroidX host.
            if (enableBd)
            {
                var bdAdapterName = FindLocalAarBaseName(projectPath,
                    "DirichletMediation/libs", "DirichletAD_BD_Adapter_*.aar");
                depsBlock.AppendLine($"    implementation(name: '{bdAdapterName}', ext: 'aar')");
                depsBlock.AppendLine("    implementation 'com.baidu:mobads:9.46.0'");
            }
            
            // Maven dependencies (required for SDK functionality)
            depsBlock.AppendLine("    implementation 'androidx.core:core:1.9.0'");
            depsBlock.AppendLine("    implementation 'androidx.fragment:fragment:1.5.5'");
            depsBlock.AppendLine("    implementation 'androidx.recyclerview:recyclerview:1.2.1'");
            depsBlock.AppendLine("    implementation 'com.squareup.okhttp3:okhttp:3.12.1'");
            
            depsBlock.AppendLine($"    {DIRICHLET_DEPS_END}");
            
            // Find dependencies block and inject after opening brace
            var depsPattern = new Regex(@"(dependencies\s*\{)");
            if (depsPattern.IsMatch(content))
            {
                content = depsPattern.Replace(content, m => 
                    m.Groups[1].Value + "\n    " + depsBlock.ToString(), 1);
                Debug.Log($"{TAG} Injected dependencies block");
            }
            else
            {
                Debug.LogWarning($"{TAG} Could not find dependencies block");
            }
            
            return content;
        }

        private static string FindLocalAarBaseName(string projectPath, string legacyRelativeDir, string pattern)
        {
            // IPostGenerateGradleAndroidProject receives the unityLibrary directory on
            // Unity 2019.3+. Unity flattens imported AAR plug-ins into unityLibrary/libs,
            // regardless of their original Assets subdirectory. Keep the legacy paths
            // as fallbacks for older Unity-generated layouts and exported projects where
            // the callback points at the Gradle root.
            var candidateDirs = new[]
            {
                Path.Combine(projectPath, "libs"),
                Path.Combine(projectPath, legacyRelativeDir),
                Path.Combine(projectPath, "unityLibrary", "libs"),
                Path.Combine(projectPath, "unityLibrary", legacyRelativeDir)
            };

            foreach (var libsDir in candidateDirs)
            {
                if (!Directory.Exists(libsDir))
                {
                    continue;
                }

                var files = Directory.GetFiles(libsDir, pattern, SearchOption.TopDirectoryOnly);
                if (files.Length == 0)
                {
                    continue;
                }

                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                return Path.GetFileNameWithoutExtension(files[files.Length - 1]);
            }

            throw new FileNotFoundException(
                $"{TAG} Missing Unity Android AAR matching {pattern}. Searched: {string.Join(", ", candidateDirs)}");
        }

        private void ProcessSettingsGradle(string projectPath, bool enableCsj, bool enableGdt,
            bool enableIqy, bool enableBd)
        {
            var parentDir = Directory.GetParent(projectPath)?.FullName;
            if (string.IsNullOrEmpty(parentDir))
            {
                Debug.LogWarning($"{TAG} Could not get parent directory");
                return;
            }
            
            var settingsPath = Path.Combine(parentDir, "settings.gradle");
            if (!File.Exists(settingsPath))
            {
                Debug.LogWarning($"{TAG} Could not find settings.gradle at {settingsPath}");
                return;
            }

            var content = File.ReadAllText(settingsPath);
            
            // Check if already injected
            if (content.Contains(DIRICHLET_REPOS_START))
            {
                Debug.Log($"{TAG} settings.gradle already has Dirichlet repos");
                return;
            }
            
            // Remove any previously injected content
            content = RemoveInjectedContent(content, DIRICHLET_REPOS_START, DIRICHLET_REPOS_END);
            
            var reposBlock = new StringBuilder();
            reposBlock.AppendLine(DIRICHLET_REPOS_START);
            reposBlock.AppendLine("        google()");
            reposBlock.AppendLine("        mavenCentral()");
            reposBlock.AppendLine("        flatDir {");
            reposBlock.AppendLine("            dirs \"${project(':unityLibrary').projectDir}/libs\", \"${project(':unityLibrary').projectDir}/DirichletMediation/libs\"");
            reposBlock.AppendLine("        }");
            
            if (enableCsj)
            {
                reposBlock.AppendLine("        maven { url 'https://artifact.bytedance.com/repository/pangle' }");
            }
            if (enableGdt)
            {
                reposBlock.AppendLine("        maven { url 'https://mirrors.cloud.tencent.com/nexus/repository/maven-public/' }");
            }
            
            reposBlock.AppendLine($"        {DIRICHLET_REPOS_END}");
            
            // Find dependencyResolutionManagement repositories block
            var reposPattern = new Regex(@"(dependencyResolutionManagement\s*\{[\s\S]*?repositories\s*\{)");
            if (reposPattern.IsMatch(content))
            {
                content = reposPattern.Replace(content, m => 
                    m.Groups[1].Value + "\n        " + reposBlock.ToString(), 1);
                File.WriteAllText(settingsPath, content);
                Debug.Log($"{TAG} Updated settings.gradle with Dirichlet Mediation repositories");
            }
            else
            {
                Debug.LogWarning($"{TAG} Could not find dependencyResolutionManagement repositories block in settings.gradle");
            }
        }

        private void ProcessGradleProperties(string projectPath)
        {
            var projectDirectory = new DirectoryInfo(projectPath);
            var parentDirectory = projectDirectory.Parent;
            var exportRoot = string.Equals(projectDirectory.Name, "unityLibrary",
                    StringComparison.OrdinalIgnoreCase) && parentDirectory != null
                ? parentDirectory.FullName
                : projectDirectory.FullName;
            var candidates = new[]
            {
                Path.Combine(exportRoot, "gradle.properties"),
                Path.Combine(projectDirectory.FullName, "gradle.properties")
            };
            var propertiesPath = Array.Find(candidates, File.Exists) ?? candidates[0];
            var content = File.Exists(propertiesPath)
                ? File.ReadAllText(propertiesPath)
                : string.Empty;

            content = SetGradleProperty(content, "android.useAndroidX", "true");
            content = SetGradleProperty(content, "android.enableJetifier", "true");
            File.WriteAllText(propertiesPath, content);
            Debug.Log($"{TAG} AndroidX and Jetifier enabled in: {propertiesPath}");
        }

        private static string SetGradleProperty(string content, string key, string value)
        {
            var line = $"{key}={value}";
            var pattern = new Regex(
                $@"(?m)^[ \t]*{Regex.Escape(key)}[ \t]*=.*$");
            if (pattern.IsMatch(content))
            {
                return pattern.Replace(content, line);
            }

            if (!string.IsNullOrEmpty(content) &&
                !content.EndsWith("\n", StringComparison.Ordinal))
            {
                content += Environment.NewLine;
            }
            return content + line + Environment.NewLine;
        }

        private string RemoveInjectedContent(string content, string startMarker, string endMarker)
        {
            var pattern = new Regex($@"\s*{Regex.Escape(startMarker)}[\s\S]*?{Regex.Escape(endMarker)}\s*");
            return pattern.Replace(content, "\n");
        }
    }
}
#endif
