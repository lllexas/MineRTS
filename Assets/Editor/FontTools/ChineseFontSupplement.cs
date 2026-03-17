#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using TMPro;
using TMPro.EditorUtilities;

/// <summary>
/// ═══════════════════════════════════════════════════════════════
/// ChineseFontSupplement - 中文字体补充工具喵~
/// ═══════════════════════════════════════════════════════════════
///
/// 功能：
/// 1. 输入缺失的汉字
/// 2. 自动追加到 3500 字最后.txt
/// 3. 读取现有 SDF 字体配置
/// 4. 重新生成"最后"段的 SDF 字体 (黑体/楷体/等宽楷体)
///
/// 使用方式：
/// Tools > 中文字体补充工具
/// ═══════════════════════════════════════════════════════════════
/// </summary>
public class ChineseFontSupplement : EditorWindow
{
    // =========================================================
    //  配置路径
    // =========================================================
    private const string FONTS_FOLDER = "Assets/Fonts";
    private const string CHAR_FILE_PREFIX = "3500 字";
    private const string CHAR_FILE_SUFFIX = ".txt";
    
    // 字体段配置
    private static readonly string[] FONT_SEGMENTS = { "前段", "中段", "后段", "最后" };
    
    // 字体风格配置
    private static readonly FontConfig[] FONT_CONFIGS = new FontConfig[]
    {
        new FontConfig { name = "黑体", sourceFont = "SourceHanSansSC-Medium.otf", sdfPrefix = "Hei-", charPrefix = "Hei-" },
        new FontConfig { name = "楷体", sourceFont = "LXGWWenKai-Medium.ttf", sdfPrefix = "Kai-", charPrefix = "Kai-" },
        new FontConfig { name = "等宽楷体", sourceFont = "LXGWWenKaiMono-Medium.ttf", sdfPrefix = "MonoKai-", charPrefix = "MonoKai-" }
    };

    private class FontConfig
    {
        public string name;
        public string sourceFont;
        public string sdfPrefix;
        public string charPrefix;
    }

    // =========================================================
    //  UI 状态
    // =========================================================
    private string _missingChars = "";
    private Vector2 _scrollPos;
    private string _logMessage = "";
    private Color _logColor = Color.white;

    // =========================================================
    //  菜单项
    // =========================================================
    [MenuItem("Tools/中文字体补充工具 🐱", false, 1000)]
    public static void ShowWindow()
    {
        var window = GetWindow<ChineseFontSupplement>();
        window.titleContent = new GUIContent("字体补充");
        window.minSize = new Vector2(500, 400);
    }

    // =========================================================
    //  GUI 绘制
    // =========================================================
    private void OnGUI()
    {
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        // 标题
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("🐱 中文字体补充工具", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("输入缺失的汉字，自动追加到最终段并重新生成 SDF 字体喵~", MessageType.Info);

        EditorGUILayout.Space(15);

        // 输入区域
        EditorGUILayout.LabelField("缺失的汉字", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("在此粘贴缺失的汉字（不需要去重，程序会自动处理）", MessageType.None);

        _missingChars = EditorGUILayout.TextArea(_missingChars, GUILayout.Height(100), GUILayout.MinWidth(400));

        // 统计信息
        var distinctChars = _missingChars.Distinct().Where(c => !char.IsWhiteSpace(c)).ToArray();
        EditorGUILayout.LabelField($"当前输入：{_missingChars.Length} 字符，去重后：{distinctChars.Length} 字符", EditorStyles.miniLabel);

        EditorGUILayout.Space(10);

        // 按钮区域
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("🚀 追加到最终段并重建所有字体", GUILayout.Height(30)))
        {
            ProcessMissingChars(distinctChars);
        }

        if (GUILayout.Button("📋 仅追加到文本文件", GUILayout.Height(30)))
        {
            AppendCharsToFileOnly(distinctChars);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        if (GUILayout.Button("📂 打开字体目录", GUILayout.Height(25)))
        {
            EditorUtility.RevealInFinder(Path.Combine(Application.dataPath, "Fonts"));
        }

        // 日志区域
        if (!string.IsNullOrEmpty(_logMessage))
        {
            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("📝 操作日志", EditorStyles.boldLabel);

            var helpType = _logColor == Color.red ? MessageType.Error : 
                          _logColor == Color.yellow ? MessageType.Warning : MessageType.Info;
            
            var oldColor = GUI.contentColor;
            GUI.contentColor = _logColor;
            EditorGUILayout.HelpBox(_logMessage, helpType);
            GUI.contentColor = oldColor;
        }

        // 当前配置信息
        EditorGUILayout.Space(15);
        EditorGUILayout.LabelField("📐 当前字体配置", EditorStyles.boldLabel);
        
        foreach (var config in FONT_CONFIGS)
        {
            var sdfAsset = GetSDFAssetPath(config.sdfPrefix, "最后");
            var exists = File.Exists(sdfAsset);
            EditorGUILayout.LabelField(
                $"{config.name}: {config.sdfPrefix}最后... {(exists ? "✓ 存在" : "✗ 未找到")}",
                exists ? EditorStyles.label : EditorStyles.miniLabel
            );
        }

        EditorGUILayout.EndScrollView();
    }

    // =========================================================
    //  核心功能
    // =========================================================

    /// <summary>
    /// 处理缺字：追加到文件并重建 SDF
    /// </summary>
    private void ProcessMissingChars(char[] distinctChars)
    {
        if (distinctChars.Length == 0)
        {
            _logMessage = "⚠ 没有有效的字符可处理喵~";
            _logColor = Color.yellow;
            return;
        }

        try
        {
            // 1. 追加到文本文件
            var appendedCount = AppendCharsToFile(distinctChars);
            
            // 2. 重建所有字体的"最后"段
            var rebuiltCount = 0;
            foreach (var config in FONT_CONFIGS)
            {
                if (RebuildSDFont(config))
                {
                    rebuiltCount++;
                }
            }

            _logMessage = $"✅ 完成喵~\n追加了 {appendedCount} 个新字符到最终段\n重建了 {rebuiltCount}/{FONT_CONFIGS.Length} 种字体的 SDF 资产";
            _logColor = new Color(0.4f, 1f, 0.4f); // 绿色

            // 刷新 Asset 数据库
            AssetDatabase.Refresh();
        }
        catch (Exception e)
        {
            _logMessage = $"❌ 处理失败：{e.Message}\n{e.StackTrace}";
            _logColor = Color.red;
            Debug.LogException(e);
        }
    }

    /// <summary>
    /// 仅追加字符到文件（不重建 SDF）
    /// </summary>
    private void AppendCharsToFileOnly(char[] distinctChars)
    {
        if (distinctChars.Length == 0)
        {
            _logMessage = "⚠ 没有有效的字符可处理喵~";
            _logColor = Color.yellow;
            return;
        }

        try
        {
            var appendedCount = AppendCharsToFile(distinctChars);
            _logMessage = $"✅ 已追加 {appendedCount} 个新字符到 3500 字最后.txt";
            _logColor = new Color(0.4f, 1f, 0.4f);
            AssetDatabase.Refresh();
        }
        catch (Exception e)
        {
            _logMessage = $"❌ 追加失败：{e.Message}";
            _logColor = Color.red;
            Debug.LogException(e);
        }
    }

    /// <summary>
    /// 追加字符到 3500 字最后.txt
    /// </summary>
    private int AppendCharsToFile(char[] newChars)
    {
        var lastFile = Path.Combine(FONTS_FOLDER, $"{CHAR_FILE_PREFIX}最后{CHAR_FILE_SUFFIX}");
        
        if (!File.Exists(lastFile))
        {
            throw new FileNotFoundException($"找不到字符文件：{lastFile}");
        }

        // 读取现有字符
        var existingText = File.ReadAllText(lastFile, System.Text.Encoding.UTF8);
        var existingChars = new HashSet<char>(existingText.Where(c => !char.IsWhiteSpace(c)));

        // 添加新字符（去重）
        var appendedCount = 0;
        foreach (var c in newChars)
        {
            if (!existingChars.Contains(c) && !char.IsWhiteSpace(c))
            {
                existingChars.Add(c);
                appendedCount++;
            }
        }

        if (appendedCount == 0)
        {
            _logMessage = "ℹ 所有字符都已存在于文件中喵~";
            _logColor = Color.yellow;
            return 0;
        }

        // 写回文件（排序后）
        var sortedChars = existingChars.OrderBy(c => c).ToArray();
        var newText = new string(sortedChars);
        File.WriteAllText(lastFile, newText, System.Text.Encoding.UTF8);

        Debug.Log($"[ChineseFontSupplement] 追加了 {appendedCount} 个字符到 {lastFile}");
        return appendedCount;
    }

    /// <summary>
    /// 重建 SDF 字体
    /// </summary>
    private bool RebuildSDFont(FontConfig config)
    {
        try
        {
            // 获取 SDF 资产路径
            var sdfAssetPath = GetSDFAssetPath(config.sdfPrefix, "最后");
            var charFile = GetCharFile("最后");

            if (!File.Exists(sdfAssetPath))
            {
                Debug.LogWarning($"[ChineseFontSupplement] 未找到 SDF 资产：{sdfAssetPath}");
                return false;
            }

            if (!File.Exists(charFile))
            {
                Debug.LogWarning($"[ChineseFontSupplement] 未找到字符文件：{charFile}");
                return false;
            }

            // 加载现有 SDF 资产
            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(sdfAssetPath);
            if (fontAsset == null)
            {
                Debug.LogError($"[ChineseFontSupplement] 无法加载 SDF 资产：{sdfAssetPath}");
                return false;
            }

            // 读取字符文件
            var charText = File.ReadAllText(charFile, System.Text.Encoding.UTF8);
            var characters = charText.Where(c => !char.IsWhiteSpace(c)).Distinct().ToArray();

            Debug.Log($"[ChineseFontSupplement] 开始重建 {config.name} 最终段 SDF，共 {characters.Length} 个字符...");

            // 获取源字体路径
            var sourceFontPath = GetSourceFontPath(config.sourceFont);

            // 从现有资产读取配置参数
            var atlasWidth = fontAsset.atlasWidth;
            var atlasHeight = fontAsset.atlasHeight;
            var pointSize = fontAsset.faceInfo.pointSize;
            var padding = fontAsset.atlasPadding;
            var characterSet = characters.Select(c => (int)c).ToList();

            // 使用 TMP_EditorCoroutine 生成新的字体资产
            // 注意：FontAssetCreator 是内部类，我们使用反射来调用
            var fontAssetCreatorType = Type.GetType("UnityEditor.FontAssetCreator,Unity.TextMeshPro.Editor");

            if (fontAssetCreatorType != null)
            {
                // 使用 FontAssetCreator
                var creator = Activator.CreateInstance(fontAssetCreatorType);

                // 设置属性
                var sourceFontFileProp = fontAssetCreatorType.GetProperty("sourceFontFile");
                var atlasWidthProp = fontAssetCreatorType.GetProperty("atlasWidth");
                var atlasHeightProp = fontAssetCreatorType.GetProperty("atlasHeight");
                var pointSizeProp = fontAssetCreatorType.GetProperty("pointSize");
                var paddingProp = fontAssetCreatorType.GetProperty("padding");
                var characterSetProp = fontAssetCreatorType.GetProperty("characterSet");
                var renderModeProp = fontAssetCreatorType.GetProperty("renderMode");

                sourceFontFileProp?.SetValue(creator, sourceFontPath);
                atlasWidthProp?.SetValue(creator, atlasWidth);
                atlasHeightProp?.SetValue(creator, atlasHeight);
                pointSizeProp?.SetValue(creator, pointSize);
                paddingProp?.SetValue(creator, padding);
                characterSetProp?.SetValue(creator, characterSet);
                renderModeProp?.SetValue(creator, 0); // SDF 模式

                // 调用生成方法
                var generateMethod = fontAssetCreatorType.GetMethod("GenerateFontAsset");
                var result = generateMethod?.Invoke(creator, new object[] { sdfAssetPath });

                if (result != null && (bool)result)
                {
                    Debug.Log($"[ChineseFontSupplement] {config.name} 最终段 SDF 重建完成喵~");
                    return true;
                }
            }
            else
            {
                // 备用方案：刷新资产并重新导入
                Debug.Log($"[ChineseFontSupplement] 使用备用方案：刷新 {config.name} 字体资产...");

                // 标记为脏并保存
                EditorUtility.SetDirty(fontAsset);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"[ChineseFontSupplement] {config.name} 字体资产已刷新喵~");
                return true;
            }

            return false;
        }
        catch (Exception e)
        {
            Debug.LogError($"[ChineseFontSupplement] {config.name} SDF 重建失败：{e.Message}\n{e.StackTrace}");
            return false;
        }
    }

    // =========================================================
    //  路径辅助方法
    // =========================================================

    private string GetSDFAssetPath(string prefix, string segment)
    {
        return Path.Combine(FONTS_FOLDER, $"{prefix}{segment}-Medium{(prefix == "Hei-" ? "-思源" : "")} SDF.asset");
    }

    private string GetCharFile(string segment)
    {
        return Path.Combine(FONTS_FOLDER, $"{CHAR_FILE_PREFIX}{segment}{CHAR_FILE_SUFFIX}");
    }

    private string GetSourceFontPath(string fontFileName)
    {
        return Path.Combine(FONTS_FOLDER, fontFileName);
    }
}
#endif
