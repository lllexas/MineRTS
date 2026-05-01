using System;
using NekoGraph;

namespace GAL
{
/// <summary>
/// .choice VFS 节点的数据模型
///
/// 声明该类型对应 Csv 格式的载荷，编辑器会自动在扩展名为 .choice 的节点上
/// 将内容类型设置为 Csv，并生成相应的编辑提示。
/// </summary>
[VFSContentKind(VFSContentKind.Csv)]
public class ChoiceData
{
    // 纯声明类，用于在 VFSNodeData 编辑器中提供类型和格式信息
    // 实际的 CSV 解析在 ChoiceVFSHandler 中进行
}

/// <summary>
/// 单个选项数据
/// </summary>
public class ChoiceOption
{
    public int Index;
    public string Text;
}

/// <summary>
/// 整个选项包 - Handler 包装后整体交给 Player
/// </summary>
public class ChoicePackage
{
    public ChoiceOption[] Options;
    public Action<int> RouteSignal;
}
}
