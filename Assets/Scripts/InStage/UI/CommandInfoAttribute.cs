using System;
using UnityEngine;

/// <summary>
/// 命令信息特性 - 用于标记命令方法，包含元数据和执行逻辑喵~
///
/// 使用示例：
/// [CommandInfo("spawn", "🏗️ 召唤单位", "Entity", new[] { "BlueprintID", "Position", "Team" })]
/// public static CommandResult Spawn(string[] args) { ... }
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class CommandInfoAttribute : Attribute
{
    /// <summary>
    /// 命令内部名（如 "spawn"）喵~
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 显示名称（如 "🏗️ 召唤单位"）喵~
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// 分类（如 "Entity", "Debug"）喵~
    /// </summary>
    public string Category { get; set; }

    /// <summary>
    /// 参数名称列表喵~
    /// </summary>
    public string[] Parameters { get; set; }

    /// <summary>
    /// 提示信息喵~
    /// </summary>
    public string Tooltip { get; set; }

    /// <summary>
    /// 编辑器颜色（格式："R,G,B" 或 "R,G,B,A"）喵~
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// 解析后的颜色值喵~
    /// </summary>
    public Color ParsedColor
    {
        get
        {
            if (string.IsNullOrEmpty(Color))
                return UnityEngine.Color.white;

            string[] parts = Color.Split(',');
            if (parts.Length >= 3 &&
                float.TryParse(parts[0], out float r) &&
                float.TryParse(parts[1], out float g) &&
                float.TryParse(parts[2], out float b))
            {
                float a = parts.Length >= 4 && float.TryParse(parts[3], out float alpha) ? alpha : 1f;
                return new Color(r, g, b, a);
            }
            return UnityEngine.Color.white;
        }
    }

    /// <summary>
    /// 构造函数喵~
    /// </summary>
    public CommandInfoAttribute(string name, string displayName, string category, string[] parameters = null)
    {
        Name = name;
        DisplayName = displayName;
        Category = category;
        Parameters = parameters ?? Array.Empty<string>();
        Tooltip = "";
        Color = "0.5,0.5,0.5";
    }
}
