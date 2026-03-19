using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

// ═══════════════════════════════════════════════════════════════════════════════
//  FontPrewarmManager
//  Dynamic TMP 字体预热管理器
//
//  职责：在游戏启动（或编辑器手动触发）时，把指定字符集加入
//        Dynamic 模式的 TMP_FontAsset，确保首次渲染不卡顿。
//
//  使用方式：
//    1. 将此脚本挂到场景 Bootstrap / GameManager 对象上
//    2. Inspector 里为每种字体添加一条 FontPrewarmEntry
//       - 拖入 TMP 字体资产（必须设为 Dynamic 模式）
//       - 拖入字符文件（.txt 直接写汉字）和/或 Unicode 编号文件
//    3. 运行时在 Awake 自动预热；编辑器下点按钮手动触发
//
//  开闭原则：新增字体 = 在 Inspector 加一条，零代码修改。
// ═══════════════════════════════════════════════════════════════════════════════
public class FontPrewarmManager : MonoBehaviour
{
    [SerializeField]
    private List<FontPrewarmEntry> _entries = new List<FontPrewarmEntry>();

    [SerializeField, Tooltip("运行时在 Awake 自动预热所有字体")]
    private bool _prewarmOnAwake = true;

    [SerializeField, Tooltip("输出每个字体的预热详细日志")]
    private bool _verboseLog = false;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (_prewarmOnAwake)
            PrewarmAll();
    }

    /// <summary>预热所有已配置的字体</summary>
    public void PrewarmAll()
    {
        Debug.Log($"[FontPrewarm] PrewarmAll 触发，共 {_entries.Count} 个条目");
        foreach (var entry in _entries)
        {
            if (entry != null)
                PrewarmEntry(entry);
        }
    }

    /// <summary>预热单个字体条目</summary>
    public void PrewarmEntry(FontPrewarmEntry entry)
    {
        if (entry.fontAsset == null)
        {
            Debug.LogWarning("[FontPrewarm] 存在未配置 fontAsset 的条目，已跳过");
            return;
        }

        if (entry.fontAsset.atlasPopulationMode == AtlasPopulationMode.Static)
        {
            Debug.LogWarning(
                $"[FontPrewarm] ⚠ {entry.DisplayName} 为 Static 模式，TryAddCharacters 无效\n" +
                $"  → 请在 TMP Inspector 中将 Atlas Population Mode 改为 Dynamic");
            return;
        }

        var charSet = CollectCharSet(entry);
        if (charSet.Count == 0)
        {
            if (_verboseLog)
                Debug.Log($"[FontPrewarm] {entry.DisplayName}: 无字符需要预热");
            return;
        }

        var unicodes = new uint[charSet.Count];
        charSet.CopyTo(unicodes);

        bool success = entry.fontAsset.TryAddCharacters(unicodes);

        if (success)
        {
            Debug.Log($"[FontPrewarm] {entry.DisplayName}: ✓ {unicodes.Length} 个字符全部预热完成");
            return;
        }

        // TryAddCharacters 返回 false 有两种原因：
        //   A) 图集容量不足（字符在 TTF 里但放不进当前 atlas）→ Dynamic 会在渲染时按需补充
        //   B) 字符根本不在 TTF 字形里 → 真正的缺字，需要关注
        // 用 FontEngine 直接查字形索引来区分两者
        var sourceFont = entry.fontAsset.sourceFontFile;
        if (sourceFont == null)
        {
            Debug.LogWarning($"[FontPrewarm] {entry.DisplayName}: ⚠ TryAddCharacters 未完全成功，且 Source Font File 未设置，无法进一步诊断");
            return;
        }

        UnityEngine.TextCore.LowLevel.FontEngine.LoadFontFace(sourceFont);

        var notInTTF   = new System.Collections.Generic.List<uint>(); // 真正缺字
        var atlasFull = new System.Collections.Generic.List<uint>(); // 图集满了

        foreach (var u in unicodes)
        {
            if (!entry.fontAsset.characterLookupTable.ContainsKey(u))
            {
                bool hasGlyph = UnityEngine.TextCore.LowLevel.FontEngine.TryGetGlyphIndex(u, out uint glyphIndex);
                if (!hasGlyph || glyphIndex == 0)
                    notInTTF.Add(u);
                else
                    atlasFull.Add(u);
            }
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[FontPrewarm] {entry.DisplayName}: 预热结果 —— {unicodes.Length} 个字符");

        if (atlasFull.Count > 0)
            sb.AppendLine($"  图集容量不足，{atlasFull.Count} 个字符未预热（Dynamic 会在渲染时自动补充，可忽略）");

        if (notInTTF.Count > 0)
        {
            sb.AppendLine($"  ⚠ {notInTTF.Count} 个字符在 TTF 中无字形（真正缺字）：");
            for (int i = 0; i < notInTTF.Count; i += 20)
            {
                var line = new System.Text.StringBuilder("    ");
                for (int j = i; j < Mathf.Min(i + 20, notInTTF.Count); j++)
                {
                    uint u = notInTTF[j];
                    char c = u <= 0xFFFF ? (char)u : '?';
                    line.Append($"{c}(U+{u:X4}) ");
                }
                sb.AppendLine(line.ToString());
            }
        }

        if (notInTTF.Count > 0)
            Debug.LogWarning(sb.ToString());
        else if (_verboseLog)
            Debug.Log(sb.ToString());
    }

    // ─────────────────────────────────────────────────────────────────────────

    private static HashSet<uint> CollectCharSet(FontPrewarmEntry entry)
    {
        var set = new HashSet<uint>();

        // 字符文本文件：直接把每个非空白字符加入集合
        foreach (var asset in entry.charFiles)
        {
            if (asset == null) continue;
            foreach (char c in asset.text)
                if (!char.IsWhiteSpace(c))
                    set.Add((uint)c);
        }

        // Unicode 编号文件：通过解析器解码后加入集合
        foreach (var asset in entry.unicodeFiles)
        {
            if (asset == null) continue;
            foreach (var code in UnicodeInputParser.Parse(asset.text))
                set.Add(code);
        }

        return set;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  FontPrewarmEntry  —  单个字体预热条目（可序列化，Inspector 配置）
// ═══════════════════════════════════════════════════════════════════════════════
[Serializable]
public class FontPrewarmEntry
{
    [Tooltip("条目名称（仅用于 Inspector 识别和日志，不影响功能）")]
    public string label = "";

    [Tooltip("目标 TMP 字体资产（必须为 Dynamic 模式，否则预热无效）")]
    public TMP_FontAsset fontAsset;

    [Tooltip("字符文本文件列表，文件中每个非空白字符都会加入预热集")]
    public List<TextAsset> charFiles = new List<TextAsset>();

    [Tooltip("Unicode 编号文件列表，支持 U+4E2D / U+4E00-U+9FFF / \\u4e2d / 0x4E2D / 20013 / 直接字符，# 和 // 开头为注释")]
    public List<TextAsset> unicodeFiles = new List<TextAsset>();

    /// <summary>用于日志和 Inspector 显示的名称</summary>
    public string DisplayName =>
        !string.IsNullOrWhiteSpace(label) ? label :
        fontAsset != null ? fontAsset.name :
        "(未命名)";
}

// ═══════════════════════════════════════════════════════════════════════════════
//  UnicodeInputParser  —  多格式 Unicode 输入解析器（运行时可用）
//
//  支持格式（同一行可混合）：
//    U+4E2D          ← Unicode hex（大小写均可）
//    U+4E00-U+9FFF   ← 范围（支持 en-dash 和连字符）
//    \u4e2d          ← 转义序列（4 或 6 位）
//    0x4E2D          ← 0x 前缀十六进制
//    20013           ← 十进制 code point（> 31 时判定为数字）
//    你好世界          ← 直接字符
//    # 注释           ← 整行忽略
//    // 注释          ← 整行忽略
// ═══════════════════════════════════════════════════════════════════════════════
public static class UnicodeInputParser
{
    // 范围必须先于单个 U+ 匹配，避免范围被拆成两个单独的码点
    private static readonly Regex s_range =
        new Regex(@"[Uu]\+([0-9A-Fa-f]{1,6})\s*[-–—]\s*[Uu]\+([0-9A-Fa-f]{1,6})",
            RegexOptions.Compiled);

    private static readonly Regex s_uPlus =
        new Regex(@"[Uu]\+([0-9A-Fa-f]{1,6})", RegexOptions.Compiled);

    private static readonly Regex s_backslashU =
        new Regex(@"\\[uU]([0-9A-Fa-f]{4,6})", RegexOptions.Compiled);

    private static readonly Regex s_hex0x =
        new Regex(@"\b0[xX]([0-9A-Fa-f]{1,6})\b", RegexOptions.Compiled);

    private static readonly char[] s_separators =
        { ' ', ',', '\t', ';', '，', '；' };

    /// <summary>
    /// 解析输入字符串，返回去重后的 Unicode code point 列表
    /// </summary>
    public static List<uint> Parse(string input)
    {
        var results = new HashSet<uint>();
        if (string.IsNullOrWhiteSpace(input)) return new List<uint>();

        foreach (var rawLine in input.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("#") || line.StartsWith("//")) continue;
            ParseLine(line, results);
        }

        return new List<uint>(results);
    }

    private static void ParseLine(string line, HashSet<uint> results)
    {
        var processed = line;

        // 1. 范围（U+4E00-U+9FFF）—— 必须先于单个 U+ 处理
        processed = s_range.Replace(processed, m =>
        {
            var from = Convert.ToUInt32(m.Groups[1].Value, 16);
            var to   = Convert.ToUInt32(m.Groups[2].Value, 16);
            for (var c = from; c <= to && c <= 0x10FFFF; c++)
                results.Add(c);
            return " ";
        });

        // 2. U+XXXX
        processed = s_uPlus.Replace(processed, m =>
        {
            results.Add(Convert.ToUInt32(m.Groups[1].Value, 16));
            return " ";
        });

        // 3. \uXXXX
        processed = s_backslashU.Replace(processed, m =>
        {
            results.Add(Convert.ToUInt32(m.Groups[1].Value, 16));
            return " ";
        });

        // 4. 0xXXXX
        processed = s_hex0x.Replace(processed, m =>
        {
            results.Add(Convert.ToUInt32(m.Groups[1].Value, 16));
            return " ";
        });

        // 5. 剩余：十进制数字 或 直接字符
        foreach (var token in processed.Split(s_separators, StringSplitOptions.RemoveEmptyEntries))
        {
            // > 31 才判定为 code point 数字，避免把短汉字的十进制误判
            if (uint.TryParse(token, out var dec) && dec > 31)
            {
                results.Add(dec);
            }
            else
            {
                foreach (char c in token)
                    if (!char.IsWhiteSpace(c))
                        results.Add((uint)c);
            }
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  FontPrewarmManagerEditor  —  编辑器扩展（仅 Editor 模式编译）
// ═══════════════════════════════════════════════════════════════════════════════
#if UNITY_EDITOR
[CustomEditor(typeof(FontPrewarmManager))]
public class FontPrewarmManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("编辑器工具", EditorStyles.boldLabel);

        if (GUILayout.Button("🔥  立即预热所有字体（编辑器）", GUILayout.Height(32)))
        {
            var manager = (FontPrewarmManager)target;
            manager.PrewarmAll();
            EditorUtility.SetDirty(target);
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox(
            "点击按钮可在不进入 Play 模式的情况下预热字体，适合验证字符是否成功加载。\n" +
            "注意：字体资产需已设为 Dynamic 模式（TMP Inspector → Atlas Population Mode）。",
            MessageType.Info);
    }
}
#endif
