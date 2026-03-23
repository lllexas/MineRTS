using System;
using UnityEngine;

/// <summary>
/// Entity 蓝图组件特性标签喵~ 🏷️
/// 用于标记一个 Component struct 应该被蓝图系统识别
/// 【字符串映射 - 一站式注册】
/// </summary>
[AttributeUsage(AttributeTargets.Struct)]
public class EntityComponentAttribute : Attribute
{
    /// <summary>
    /// 组件类型名称（字符串，用于映射）
    /// 一站式注册：只需在此处声明，系统自动扫描注册喵~
    /// </summary>
    public string TypeName { get; }
    
    /// <summary>
    /// 显示名称（用于编辑器）
    /// </summary>
    public string DisplayName { get; set; }
    
    /// <summary>
    /// 描述信息
    /// </summary>
    public string Description { get; set; }
    
    public EntityComponentAttribute(string typeName)
    {
        TypeName = typeName;
    }
}

/// <summary>
/// 蓝图字段特性标签喵~ 📝
/// 用于标记 Component struct 中的字段应该在蓝图编辑器中显示
/// 【一站式注册】只需添加标签，UI 自动生成喵~
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public class BlueprintFieldAttribute : Attribute
{
    /// <summary>
    /// 显示名称（如果为空则使用字段名）
    /// </summary>
    public string DisplayName { get; set; }
    
    /// <summary>
    /// 提示信息
    /// </summary>
    public string Tooltip { get; set; }
    
    /// <summary>
    /// 最小值（用于数值字段的滑块）
    /// </summary>
    public float Min { get; set; } = float.MinValue;
    
    /// <summary>
    /// 最大值（用于数值字段的滑块）
    /// </summary>
    public float Max { get; set; } = float.MaxValue;
    
    /// <summary>
    /// 步长（用于数值字段）
    /// </summary>
    public float Step { get; set; } = 1f;
    
    /// <summary>
    /// 是否只读
    /// </summary>
    public bool ReadOnly { get; set; }
    
    /// <summary>
    /// 分组名称（用于组织字段）
    /// </summary>
    public string Group { get; set; }
    
    /// <summary>
    /// 是否应该在 Inspector 中隐藏
    /// </summary>
    public bool HideInInspector { get; set; }
}
