#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// GraphView 画布逻辑喵~
/// </summary>
public class StoryGraphView : GraphView
{
    // CSV 导入后暂存的 Sequences 数据
    public List<DialogueSequence> Sequences = new List<DialogueSequence>();

    // 节点引用
    private StoryRootNode _rootNode;
    private List<SpineIDNode> _spineNodes = new List<SpineIDNode>();
    private List<LeafIDNode> _leafNodes = new List<LeafIDNode>();
    private List<TriggerNode> _triggerNodes = new List<TriggerNode>();
    private List<CommandNode> _commandNodes = new List<CommandNode>();

    public StoryRootNode RootNode => _rootNode;
    public List<SpineIDNode> SpineNodes => _spineNodes;
    public List<LeafIDNode> LeafNodes => _leafNodes;
    public List<TriggerNode> TriggerNodes => _triggerNodes;
    public List<CommandNode> CommandNodes => _commandNodes;

    public StoryGraphView()
    {
        SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
        this.AddManipulator(new ContentDragger());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());

        var grid = new GridBackground();
        Insert(0, grid);
        grid.StretchToParentSize();

        serializeGraphElements = SerializeCopyElements;
        unserializeAndPaste = UnserializePasteElements;
        graphViewChanged += OnGraphViewChanged;
    }

    private GraphViewChange OnGraphViewChanged(GraphViewChange changes)
    {
        if (changes.elementsToRemove != null)
        {
            foreach (var element in changes.elementsToRemove)
            {
                if (element is SpineIDNode spineNode)
                    _spineNodes.Remove(spineNode);
                else if (element is LeafIDNode leafNode)
                    _leafNodes.Remove(leafNode);
                else if (element is TriggerNode triggerNode)
                    _triggerNodes.Remove(triggerNode);
                else if (element is CommandNode commandNode)
                    _commandNodes.Remove(commandNode);
            }
        }
        return changes;
    }

    /// <summary>
    /// 核心：端口兼容性过滤（白名单机制）喵~
    /// </summary>
    public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
    {
        var compatiblePorts = new List<Port>();

        foreach (var port in ports.ToList())
        {
            // 跳过自己
            if (port.node == startPort.node)
                continue;

            // 跳过不同方向的端口
            if (port.direction == startPort.direction)
                continue;

            // 白名单检查
            if (IsValidConnection(startPort, port))
            {
                compatiblePorts.Add(port);
            }
        }

        return compatiblePorts;
    }

    /// <summary>
    /// 检查连接是否合法喵~
    /// </summary>
    private bool IsValidConnection(Port fromPort, Port toPort)
    {
        // 确定方向（可能是从输入到输出，也可能是从输出到输入）
        var outputPort = fromPort.direction == Direction.Output ? fromPort : toPort;
        var inputPort = fromPort.direction == Direction.Input ? fromPort : toPort;

        var outputNode = outputPort.node;
        var inputNode = inputPort.node;

        // 白名单规则：
        // ✅ Root_Output → Spine_Input
        // ✅ Spine_Output → Spine_Input
        // ✅ Trigger_Output → Trigger_Input
        // ✅ Trigger_Output → Leaf_Input
        // ✅ Leaf_Output → Command_Input
        // ✅ Command_Output → Command_Input

        // Root → Spine
        if (outputNode is StoryRootNode && inputNode is SpineIDNode)
            return true;

        // Spine → Spine
        if (outputNode is SpineIDNode && inputNode is SpineIDNode)
            return true;

        // Trigger → Trigger
        if (outputNode is TriggerNode && inputNode is TriggerNode)
            return true;

        // Trigger → Leaf
        if (outputNode is TriggerNode && inputNode is LeafIDNode)
            return true;

        // Leaf → Command
        if (outputNode is LeafIDNode && inputNode is CommandNode)
            return true;

        // Command → Command
        if (outputNode is CommandNode && inputNode is CommandNode)
            return true;

        // ILLEGAL!
        return false;
    }

    #region Node Creation

    public StoryRootNode CreateRootNode(Vector2 pos)
    {
        var node = new StoryRootNode();
        node.SetPosition(new Rect(pos, Vector2.zero));
        AddElement(node);
        _rootNode = node;
        return node;
    }

    public SpineIDNode CreateSpineNode(SpineIDNodeData data, Vector2 pos)
    {
        var node = new SpineIDNode(data);
        node.SetPosition(new Rect(pos, Vector2.zero));
        AddElement(node);
        _spineNodes.Add(node);
        return node;
    }

    public LeafIDNode CreateLeafNode(LeafIDNodeData data, Vector2 pos)
    {
        var node = new LeafIDNode(data);
        node.SetPosition(new Rect(pos, Vector2.zero));
        AddElement(node);
        _leafNodes.Add(node);
        return node;
    }

    public TriggerNode CreateTriggerNode(TriggerNodeData data, Vector2 pos)
    {
        var node = new TriggerNode(data);
        node.SetPosition(new Rect(pos, Vector2.zero));
        AddElement(node);
        _triggerNodes.Add(node);
        return node;
    }

    public CommandNode CreateCommandNode(CommandNodeData data, Vector2 pos)
    {
        var node = new CommandNode(data);
        node.SetPosition(new Rect(pos, Vector2.zero));
        AddElement(node);
        _commandNodes.Add(node);
        return node;
    }

    #endregion

    #region Copy & Paste

    [Serializable]
    private class CopyPasteData
    {
        public List<SpineIDNodeData> SpineNodes = new List<SpineIDNodeData>();
        public List<LeafIDNodeData> LeafNodes = new List<LeafIDNodeData>();
        public List<TriggerNodeData> Triggers = new List<TriggerNodeData>();
        public List<CommandNodeData> Commands = new List<CommandNodeData>();
    }

    private string SerializeCopyElements(IEnumerable<GraphElement> elements)
    {
        var copyData = new CopyPasteData();
        var selectedNodes = elements.OfType<BaseNode>().ToList();

        foreach (var node in selectedNodes)
        {
            var pos = node.GetPosition().position;

            switch (node)
            {
                case SpineIDNode spineNode:
                    var spineData = spineNode.Data;
                    spineData.EditorPosition = pos;
                    copyData.SpineNodes.Add(spineData);
                    break;
                case LeafIDNode leafNode:
                    var leafData = leafNode.Data;
                    leafData.EditorPosition = pos;
                    copyData.LeafNodes.Add(leafData);
                    break;
                case TriggerNode triggerNode:
                    var triggerData = triggerNode.Data;
                    triggerData.EditorPosition = pos;
                    copyData.Triggers.Add(triggerData);
                    break;
                case CommandNode commandNode:
                    var commandData = commandNode.Data;
                    commandData.EditorPosition = pos;
                    copyData.Commands.Add(commandData);
                    break;
            }
        }

        return JsonUtility.ToJson(copyData, false);
    }

    private void UnserializePasteElements(string operationName, string data)
    {
        try
        {
            var copyData = JsonUtility.FromJson<CopyPasteData>(data);
            if (copyData == null) return;

            ClearSelection();
            Vector2 pasteOffset = new Vector2(50, 50);

            foreach (var spineData in copyData.SpineNodes)
            {
                var newData = CloneSpineData(spineData);
                newData.EditorPosition += pasteOffset;
                var node = CreateSpineNode(newData, newData.EditorPosition);
                AddToSelection(node);
            }

            foreach (var leafData in copyData.LeafNodes)
            {
                var newData = CloneLeafData(leafData);
                newData.EditorPosition += pasteOffset;
                var node = CreateLeafNode(newData, newData.EditorPosition);
                AddToSelection(node);
            }

            foreach (var triggerData in copyData.Triggers)
            {
                var newData = CloneTriggerData(triggerData);
                newData.EditorPosition += pasteOffset;
                var node = CreateTriggerNode(newData, newData.EditorPosition);
                AddToSelection(node);
            }

            foreach (var commandData in copyData.Commands)
            {
                var newData = CloneCommandData(commandData);
                newData.EditorPosition += pasteOffset;
                var node = CreateCommandNode(newData, newData.EditorPosition);
                AddToSelection(node);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"粘贴失败：{e.Message}");
        }
    }

    private SpineIDNodeData CloneSpineData(SpineIDNodeData original)
    {
        return new SpineIDNodeData
        {
            NodeID = System.Guid.NewGuid().ToString(),
            EditorPosition = original.EditorPosition,
            StoryProcessID = original.StoryProcessID
        };
    }

    private LeafIDNodeData CloneLeafData(LeafIDNodeData original)
    {
        return new LeafIDNodeData
        {
            NodeID = System.Guid.NewGuid().ToString(),
            EditorPosition = original.EditorPosition,
            StoryProcessID = original.StoryProcessID,
            SequenceID = original.SequenceID
        };
    }

    private TriggerNodeData CloneTriggerData(TriggerNodeData original)
    {
        return new TriggerNodeData
        {
            NodeID = System.Guid.NewGuid().ToString(),
            EditorPosition = original.EditorPosition,
            Trigger = new TriggerData
            {
                UseEnumTrigger = original.Trigger.UseEnumTrigger,
                TriggerType = original.Trigger.TriggerType,
                CustomEventName = original.Trigger.CustomEventName,
                TriggerParam = original.Trigger.TriggerParam
            }
        };
    }

    private CommandNodeData CloneCommandData(CommandNodeData original)
    {
        return new CommandNodeData
        {
            NodeID = System.Guid.NewGuid().ToString(),
            EditorPosition = original.EditorPosition,
            Command = new CommandData
            {
                CommandType = original.Command.CommandType,
                CommandParam = original.Command.CommandParam
            }
        };
    }

    #endregion

    #region Serialize / Deserialize

    public StoryPackData SerializeToPack()
    {
        var pack = new StoryPackData();

        // 1. 保存根节点
        if (_rootNode != null)
        {
            pack.Root = new StoryRootNodeData
            {
                NodeID = _rootNode.GUID,
                EditorPosition = _rootNode.GetPosition().position
            };
        }
        else
        {
            Debug.LogError("[StoryGraph] 错误：缺少根节点！");
            return null;
        }

        // 2. 保存所有节点并记录连线
        foreach (var node in _spineNodes)
        {
            node.UpdateData();
            var data = node.Data;
            data.EditorPosition = node.GetPosition().position;
            
            // 保存 Spine → Spine 一对多连线
            var spineEdges = this.edges.ToList().Where(e => 
                e.output.node == node && e.output == node.OutputPort && e.input.node is SpineIDNode);
            foreach (var edge in spineEdges)
            {
                data.NextSpineNodeIDs.Add(((SpineIDNode)edge.input.node).Data.NodeID);
            }
            
            // 检查是否连接到根节点
            var rootEdge = this.edges.ToList().FirstOrDefault(e => 
                e.output.node == _rootNode && e.output == _rootNode.OutputPort && e.input.node == node);
            data.IsConnectedToRoot = (rootEdge != null);
            
            pack.SpineNodes.Add(data);
        }

        foreach (var node in _leafNodes)
        {
            node.UpdateData();
            var data = node.Data;
            data.EditorPosition = node.GetPosition().position;
            
            // 保存 Leaf → Command 一对多连线
            var commandEdges = this.edges.ToList().Where(e => 
                e.output.node == node && e.output == node.OutputPort && e.input.node is CommandNode);
            foreach (var edge in commandEdges)
            {
                data.ConnectedCommandNodeIDs.Add(((CommandNode)edge.input.node).Data.NodeID);
            }
            
            pack.LeafNodes.Add(data);
        }

        foreach (var node in _triggerNodes)
        {
            node.UpdateData();
            var data = node.Data;
            data.EditorPosition = node.GetPosition().position;
            
            // 保存 Trigger → Trigger/Leaf 一对多连线
            var triggerEdges = this.edges.ToList().Where(e => 
                e.output.node == node && e.output == node.OutputPort);
            foreach (var edge in triggerEdges)
            {
                if (edge.input.node is TriggerNode nextTrigger)
                    data.NextNodeIDs.Add(nextTrigger.Data.NodeID);
                else if (edge.input.node is LeafIDNode leafNode)
                    data.NextNodeIDs.Add(leafNode.Data.NodeID);
            }
            
            pack.Triggers.Add(data);
        }

        foreach (var node in _commandNodes)
        {
            node.UpdateData();
            var data = node.Data;
            data.EditorPosition = node.GetPosition().position;
            
            // 保存 Command → Command 一对多连线
            var commandEdges = this.edges.ToList().Where(e => 
                e.output.node == node && e.output == node.OutputPort && e.input.node is CommandNode);
            foreach (var edge in commandEdges)
            {
                data.NextCommandNodeIDs.Add(((CommandNode)edge.input.node).Data.NodeID);
            }
            
            pack.Commands.Add(data);
        }

        // 3. 保存 Sequences 数据
        pack.Sequences = Sequences;

        // 4. Twin-ID 规则校验
        if (!ValidateTwinIDRule(pack))
        {
            return null; // 校验失败，不保存
        }

        return pack;
    }

    /// <summary>
    /// Twin-ID 规则校验喵~
    /// </summary>
    private bool ValidateTwinIDRule(StoryPackData pack)
    {
        var spineIDs = pack.SpineNodes.Select(s => s.StoryProcessID).Where(id => !string.IsNullOrEmpty(id)).ToHashSet();
        var leafIDs = pack.LeafNodes.Select(l => l.StoryProcessID).Where(id => !string.IsNullOrEmpty(id)).ToHashSet();

        // 找出只有 Spine 没有 Leaf 的 ID
        var spineOnly = spineIDs.Except(leafIDs).ToList();
        // 找出只有 Leaf 没有 Spine 的 ID
        var leafOnly = leafIDs.Except(spineIDs).ToList();

        if (spineOnly.Count > 0)
        {
            string errorMsg = $"Twin-ID 规则违反！\n以下故事进程 ID 只有 Spine 节点，没有 Leaf 节点：\n{string.Join(", ", spineOnly)}";
            EditorUtility.DisplayDialog("错误", errorMsg, "确定");
            Debug.LogError($"[StoryGraph] {errorMsg}");
            return false;
        }

        if (leafOnly.Count > 0)
        {
            string errorMsg = $"Twin-ID 规则违反！\n以下故事进程 ID 只有 Leaf 节点，没有 Spine 节点：\n{string.Join(", ", leafOnly)}";
            EditorUtility.DisplayDialog("错误", errorMsg, "确定");
            Debug.LogError($"[StoryGraph] {errorMsg}");
            return false;
        }

        return true;
    }

    public void PopulateFromPack(StoryPackData pack)
    {
        DeleteElements(graphElements);
        _spineNodes.Clear();
        _leafNodes.Clear();
        _triggerNodes.Clear();
        _commandNodes.Clear();

        // 1. 恢复根节点
        if (pack.Root != null)
        {
            CreateRootNode(pack.Root.EditorPosition);
        }

        // 2. 恢复所有节点
        if (pack.SpineNodes != null)
        {
            foreach (var data in pack.SpineNodes)
            {
                CreateSpineNode(data, data.EditorPosition);
            }
        }

        if (pack.LeafNodes != null)
        {
            foreach (var data in pack.LeafNodes)
            {
                CreateLeafNode(data, data.EditorPosition);
            }
        }

        if (pack.Triggers != null)
        {
            foreach (var data in pack.Triggers)
            {
                CreateTriggerNode(data, data.EditorPosition);
            }
        }

        if (pack.Commands != null)
        {
            foreach (var data in pack.Commands)
            {
                CreateCommandNode(data, data.EditorPosition);
            }
        }

        // 3. 恢复 Sequences 数据
        if (pack.Sequences != null)
        {
            Sequences = pack.Sequences;
        }

        // 4. 恢复连线喵~
        RestoreConnections();
    }

    /// <summary>
    /// 恢复节点间的连线喵~
    /// </summary>
    private void RestoreConnections()
    {
        // 创建节点 ID 到节点的映射
        var nodeMap = new Dictionary<string, BaseNode>();
        
        foreach (var node in _spineNodes)
            nodeMap[node.Data.NodeID] = node;
        foreach (var node in _leafNodes)
            nodeMap[node.Data.NodeID] = node;
        foreach (var node in _triggerNodes)
            nodeMap[node.Data.NodeID] = node;
        foreach (var node in _commandNodes)
            nodeMap[node.Data.NodeID] = node;

        // 恢复 Root → Spine 连线
        if (_rootNode != null)
        {
            foreach (var spineNode in _spineNodes)
            {
                if (spineNode.Data.IsConnectedToRoot)
                {
                    var edge = _rootNode.OutputPort.ConnectTo(spineNode.InputPort);
                    AddElement(edge);
                }
            }
        }

        // 恢复 Spine → Spine 一对多连线
        foreach (var spineNode in _spineNodes)
        {
            foreach (var nextId in spineNode.Data.NextSpineNodeIDs)
            {
                if (nodeMap.TryGetValue(nextId, out var targetSpine))
                {
                    var edge = spineNode.OutputPort.ConnectTo(((SpineIDNode)targetSpine).InputPort);
                    AddElement(edge);
                }
            }
        }

        // 恢复 Trigger → Trigger/Leaf 一对多连线
        foreach (var triggerNode in _triggerNodes)
        {
            foreach (var nextId in triggerNode.Data.NextNodeIDs)
            {
                if (nodeMap.TryGetValue(nextId, out var targetNode))
                {
                    var edge = triggerNode.OutputPort.ConnectTo(
                        targetNode is TriggerNode t ? t.InputPort : ((LeafIDNode)targetNode).InputPort);
                    AddElement(edge);
                }
            }
        }

        // 恢复 Leaf → Command 一对多连线
        foreach (var leafNode in _leafNodes)
        {
            foreach (var commandId in leafNode.Data.ConnectedCommandNodeIDs)
            {
                if (nodeMap.TryGetValue(commandId, out var targetCommand))
                {
                    var edge = leafNode.OutputPort.ConnectTo(((CommandNode)targetCommand).InputPort);
                    AddElement(edge);
                }
            }
        }

        // 恢复 Command → Command 一对多连线
        foreach (var commandNode in _commandNodes)
        {
            foreach (var nextId in commandNode.Data.NextCommandNodeIDs)
            {
                if (nodeMap.TryGetValue(nextId, out var nextCommand))
                {
                    var edge = commandNode.OutputPort.ConnectTo(((CommandNode)nextCommand).InputPort);
                    AddElement(edge);
                }
            }
        }

        Debug.Log($"[StoryGraph] 连线恢复完成！共恢复了 {_spineNodes.Count + _leafNodes.Count + _triggerNodes.Count + _commandNodes.Count} 个节点的连线");
    }

    #endregion
}
#endif
