using System;

/// <summary>
/// 标记节点字段在序列化时应提取到 Pack.SidePara 喵~
/// 用法：[SideParaKey("BoundMapID")] public string MapID;
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public class SideParaKeyAttribute : Attribute
{
    public readonly string Key;
    public SideParaKeyAttribute(string key) => Key = key;
}
