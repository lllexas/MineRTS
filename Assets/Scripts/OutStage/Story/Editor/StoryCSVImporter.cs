#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// CSV 导入工具：将 CSV 文件解析成 DialogueSequence 列表喵~
/// 用法：var sequences = StoryCSVImporter.ImportFromCSV("path/to/file.csv");
/// </summary>
public static class StoryCSVImporter
{
    /// <summary>
    /// 从 CSV 文件导入对话序列喵~
    /// </summary>
    /// <param name="csvPath">CSV 文件路径</param>
    /// <returns>DialogueSequence 列表</returns>
    public static List<DialogueSequence> ImportFromCSV(string csvPath)
    {
        if (!File.Exists(csvPath))
        {
            Debug.LogError($"[StoryCSVImporter] 文件不存在：{csvPath}");
            return null;
        }

        var lines = File.ReadAllLines(csvPath);
        if (lines.Length < 2)
        {
            Debug.LogError("[StoryCSVImporter] CSV 文件内容为空或只有标题行");
            return null;
        }

        // 解析标题行
        var headers = ParseCSVLine(lines[0]);
        var columnIndex = new Dictionary<string, int>();
        for (int i = 0; i < headers.Length; i++)
        {
            columnIndex[headers[i].Trim().ToLower()] = i;
        }

        // 验证必需的列
        string[] requiredColumns = { "sequenceid", "title", "lineindex", "speaker", "text" };
        foreach (var col in requiredColumns)
        {
            if (!columnIndex.ContainsKey(col))
            {
                Debug.LogError($"[StoryCSVImporter] 缺少必需的列：{col}");
                return null;
            }
        }

        // 按 SequenceID 分组存储对话
        var sequenceDict = new Dictionary<string, DialogueSequence>();

        // 解析数据行
        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue; // 跳过空行

            var values = ParseCSVLine(line);
            if (values.Length < headers.Length)
            {
                Debug.LogWarning($"[StoryCSVImporter] 第{i + 1}行数据不完整，已跳过");
                continue;
            }

            // 读取字段
            string sequenceID = GetValue(values, columnIndex, "sequenceid");
            string title = GetValue(values, columnIndex, "title");
            int lineIndex = GetIntValue(values, columnIndex, "lineindex");
            string speaker = GetValue(values, columnIndex, "speaker");
            string text = GetValue(values, columnIndex, "text");
            float displayTime = GetFloatValue(values, columnIndex, "displaytime");
            int portraitId = GetIntValue(values, columnIndex, "portraitid");

            // 获取或创建 DialogueSequence
            if (!sequenceDict.ContainsKey(sequenceID))
            {
                sequenceDict[sequenceID] = new DialogueSequence
                {
                    SequenceID = sequenceID,
                    Title = title,
                    Lines = new List<DialogueLine>()
                };
            }

            var sequence = sequenceDict[sequenceID];

            // 添加台词到对应位置
            while (sequence.Lines.Count <= lineIndex)
            {
                sequence.Lines.Add(null); // 占位
            }

            sequence.Lines[lineIndex] = new DialogueLine
            {
                Speaker = speaker,
                Text = text,
                DisplayTime = displayTime > 0 ? displayTime : CalculateDisplayTime(text),
                PortraitSpriteId = portraitId
            };
        }

        // 移除空位并排序
        var result = new List<DialogueSequence>();
        foreach (var seq in sequenceDict.Values)
        {
            seq.Lines.RemoveAll(l => l == null);
            result.Add(seq);
        }

        Debug.Log($"[StoryCSVImporter] 导入成功！共 {result.Count} 个对话序列");
        return result;
    }

    /// <summary>
    /// 将 DialogueSequence 列表导出为 CSV 文件喵~
    /// </summary>
    public static void ExportToCSV(List<DialogueSequence> sequences, string csvPath)
    {
        var sb = new StringBuilder();

        // 写入标题行
        sb.AppendLine("SequenceID|Title|LineIndex|Speaker|Text|DisplayTime|PortraitId");

        // 写入数据
        foreach (var seq in sequences)
        {
            for (int i = 0; i < seq.Lines.Count; i++)
            {
                var line = seq.Lines[i];
                sb.AppendLine($"{EscapeCSV(seq.SequenceID)}|{EscapeCSV(seq.Title)}|{i}|" +
                             $"{EscapeCSV(line.Speaker)}|{EscapeCSV(line.Text)}|{line.DisplayTime}|{line.PortraitSpriteId}");
            }
        }

        File.WriteAllText(csvPath, sb.ToString());
        Debug.Log($"[StoryCSVImporter] 导出成功！路径：{csvPath}");
    }

    /// <summary>
    /// 解析 CSV 行（使用 | 分隔符）喵~
    /// </summary>
    private static string[] ParseCSVLine(string line)
    {
        return line.Split('|');
    }

    /// <summary>
    /// 获取字段值（安全访问）喵~
    /// </summary>
    private static string GetValue(string[] values, Dictionary<string, int> columnIndex, string columnName)
    {
        if (columnIndex.TryGetValue(columnName, out int index) && index < values.Length)
        {
            return values[index]?.Trim() ?? "";
        }
        return "";
    }

    /// <summary>
    /// 获取整数字段值喵~
    /// </summary>
    private static int GetIntValue(string[] values, Dictionary<string, int> columnIndex, string columnName)
    {
        string val = GetValue(values, columnIndex, columnName);
        if (int.TryParse(val, out int result))
        {
            return result;
        }
        return 0;
    }

    /// <summary>
    /// 获取浮点字段值喵~
    /// </summary>
    private static float GetFloatValue(string[] values, Dictionary<string, int> columnIndex, string columnName)
    {
        string val = GetValue(values, columnIndex, columnName);
        if (float.TryParse(val, out float result))
        {
            return result;
        }
        return 0f;
    }

    /// <summary>
    /// 根据文本长度自动计算显示时长喵~
    /// </summary>
    private static float CalculateDisplayTime(string text)
    {
        // 中文约 0.15 秒/字，英文约 0.1 秒/字母
        int chineseCount = text.Count(c => c > 127);
        int englishCount = text.Length - chineseCount;
        return Mathf.Max(1.5f, chineseCount * 0.15f + englishCount * 0.1f);
    }

    /// <summary>
    /// CSV 转义（处理逗号和引号）喵~
    /// </summary>
    private static string EscapeCSV(string value)
    {
        if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        return value;
    }
}
#endif
