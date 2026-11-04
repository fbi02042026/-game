using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>
/// 【一键测试入口】Tools → PixelAdventureTown → 一键测试：跑全部 EditMode 用例，
/// 跑完把 通过/失败数 + 失败用例名与断言信息 打到 Console，并把结果 XML 写到
/// Temp/oneclick_test_results.xml（与根目录「一键测试.bat」同一个固定路径）。
/// </summary>
public static class OneClickTest
{
    const string ResultsPath = "Temp/oneclick_test_results.xml";

    [MenuItem("Tools/PixelAdventureTown/一键测试")]
    public static void RunAllEditModeTests()
    {
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new SummaryCallbacks());
        api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
        Debug.Log("[一键测试] 已启动 EditMode 全量测试，跑完在此输出汇总……（结果 XML：" + Path.GetFullPath(ResultsPath) + "）");
    }

    class SummaryCallbacks : ICallbacks
    {
        readonly List<string> _fails = new List<string>();

        public void RunStarted(ITestAdaptor testsToRun) => _fails.Clear();

        public void RunFinished(ITestResultAdaptor result)
        {
            var sb = new StringBuilder();
            sb.AppendLine("========== 一键测试结果（EditMode）==========");
            sb.AppendLine($"通过 {result.PassCount} / 失败 {result.FailCount} / 跳过 {result.SkipCount}");
            if (_fails.Count > 0)
            {
                sb.AppendLine("失败用例：");
                foreach (var f in _fails)
                    sb.AppendLine("  ✗ " + f);
            }
            else
            {
                sb.AppendLine("全部通过");
            }
            sb.AppendLine("结果 XML：" + Path.GetFullPath(ResultsPath));
            Debug.Log(sb.ToString());

            try
            {
                File.WriteAllText(ResultsPath, result.ToXml().OuterXml);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[一键测试] 写结果 XML 失败：" + e.Message);
            }
        }

        public void TestStarted(ITestAdaptor test) { }

        public void TestFinished(ITestResultAdaptor result)
        {
            // 注：ITestAdaptor 无 IsTest（用 !IsSuite 判叶子）；工程内有两套 NUnit 程序集，
            // TestStatus 枚举跨程序集不能 == 比较，故用 FailCount 判失败（均按包源码核实）。
            if (result.Test != null && !result.Test.IsSuite && result.FailCount > 0)
                _fails.Add(result.Test.FullName + " ｜ " + result.Message);
        }
    }
}
