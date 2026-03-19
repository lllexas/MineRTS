#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 【NekoGraph 静态分析器】
/// 职责：在编辑器中静态扫描代码，强制执行"邮局契约"喵！
/// </summary>
public class PostContractAnalyzer : AssetPostprocessor
{
    // 扫描所有脚本修改喵~
    private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        bool hasScriptChanges = importedAssets.Any(path => path.EndsWith(".cs"));
        if (hasScriptChanges)
        {
            RunScan();
        }
    }

    [MenuItem("NekoTools/契约检查/执行代码契约扫描喵~")]
    public static void RunScan()
    {
        Debug.Log("[PostContractAnalyzer] 正在进行契约扫描，请稍候喵...");

        string[] triggerEventNames = Enum.GetNames(typeof(TriggerEvent));
        string scriptsPath = Application.dataPath;
        string[] allScripts = Directory.GetFiles(scriptsPath, "*.cs", SearchOption.AllDirectories);

        int violationCount = 0;

        foreach (var scriptPath in allScripts)
        {
            // 跳过自身和 PostOffice/PostSystem/Registry 喵~
            string fileName = Path.GetFileName(scriptPath);
            if (fileName == "PostContractAnalyzer.cs" || fileName == "PostOffice.cs" ||
                fileName == "PostSystem.cs" || fileName == "TriggerRegistry.cs") continue;

            string content = File.ReadAllText(scriptPath);

            foreach (var evtName in triggerEventNames)
            {
                // 正则匹配：PostSystem.Instance.Send("EventName" 喵~
                string pattern = $@"PostSystem\.Instance\.Send\s*\(\s*""{evtName}""";
                if (Regex.IsMatch(content, pattern))
                {
                    violationCount++;
                    Debug.LogError($"<color=red>[契约违例]</color> 在脚本 [{fileName}] 中检测到直接发送契约事件 [{evtName}] 的行为！\n" +
                                   $"请修改为使用 PostOffice.Send(TriggerEvent.{evtName}, payload) 喵！\n路径：{scriptPath}");
                }
            }
        }

        if (violationCount == 0)
        {
            Debug.Log("<color=green>[PostContractAnalyzer] 扫描完成，全项目代码符合契约规范，主人真棒喵！</color>");
        }
        else
        {
            Debug.LogWarning($"[PostContractAnalyzer] 扫描完成，共发现 {violationCount} 处契约违例，请尽快修正喵！");
        }
    }
}
#endif
