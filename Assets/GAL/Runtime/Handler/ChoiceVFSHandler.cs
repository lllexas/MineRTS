using NekoGraph;
using UnityEngine;
using System.Collections.Generic;

namespace GAL
{
/// <summary>
/// Choice VFS 处理器 - 处理 .choice 后缀的 VFS 节点
///
/// 执行流程：
/// 1. 解析 CSV 文本（回车分割，无表头）
/// 2. 从 pack.Nodes 反查当前节点的 ChildNodeIDs
/// 3. 构建路由闭包和选项包
/// 4. 整包交给 ChoicePlayer，由其拆包分发
/// 5. 返回 HandleResult.Wait
/// </summary>
public static class ChoiceVFSHandler
{
    [EXEHandler(".choice", typeof(ChoiceData))]
    public static HandleResult Handle(
        VFSResolvedContent content,
        SignalContext context,
        BasePackData pack,
        GraphRunner runner,
        string packInstanceID,
        System.Action continueAction)
    {
        // 1. 解析 CSV 文本（回车分割）
        if (!content.HasText)
        {
            Debug.LogError("[ChoiceVFSHandler] 节点内容为空，无法解析选项");
            return HandleResult.Error;
        }

        string[] lines = content.RawText.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (lines == null || lines.Length == 0)
        {
            Debug.LogError("[ChoiceVFSHandler] 选项列表为空");
            return HandleResult.Error;
        }

        // 2. 反查当前节点获取 ChildNodeIDs
        if (!pack.Nodes.TryGetValue(context.CurrentNodeId, out var nodeData)
            || nodeData is not VFSNodeData vfsNode)
        {
            Debug.LogError($"[ChoiceVFSHandler] 无法找到 VFSNodeData，NodeId: {context.CurrentNodeId}");
            return HandleResult.Error;
        }

        var childIds = new List<string>(vfsNode.ChildNodeIDs);

        if (childIds.Count != lines.Length)
        {
            Debug.LogWarning($"[ChoiceVFSHandler] 选项数量({lines.Length})与子节点数量({childIds.Count})不一致");
        }

        // 3. 构建路由闭包（NekoGraph 引用全留在这里，ChoicePlayer 不感知）
        string sourceNodeId = context.CurrentNodeId;
        System.Action<int> routeSignal = (index) =>
        {
            if (index < 0 || index >= childIds.Count)
            {
                Debug.LogError($"[ChoiceVFSHandler] 选项索引({index})越界，子节点共 {childIds.Count} 个");
                return;
            }

            // 标记当前节点已处理
            if (pack.Nodes.TryGetValue(sourceNodeId, out var cur))
            {
                cur.IsChecked = true;
            }

            string targetId = childIds[index];
            var newSignal = context.Clone(copyPath: true);
            newSignal.RecordConnection(new ConnectionData(sourceNodeId, -1, targetId, -1));
            newSignal.CurrentNodeId = targetId;
            pack.ActiveSignals.Enqueue(newSignal);

            Debug.Log($"[ChoiceVFSHandler] 选择选项[{index}] = '{lines[index]}'，路由到节点: {targetId}");
        };

        // 4. 封装选项包
        var package = new ChoicePackage
        {
            Options = new ChoiceOption[lines.Length],
            RouteSignal = routeSignal
        };

        for (int i = 0; i < lines.Length; i++)
        {
            package.Options[i] = new ChoiceOption { Index = i, Text = lines[i].Trim() };
        }

        // 5. 交给 ChoicePlayer 处理
        bool handled = ChoicePlayer.Instance.TryPresent(package);

        if (!handled)
        {
            Debug.LogWarning("[ChoiceVFSHandler] ChoicePlayer 未能接管，透传图流程");
            return HandleResult.Push;
        }

        return HandleResult.Wait;
    }
}
}
