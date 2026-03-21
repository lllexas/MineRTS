#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NekoGraph;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Newtonsoft.Json;

/// <summary>
/// 通用 GraphView - 非泛型，无子类，所有 Pack 类型共用一个画布喵~
/// 仅编辑器使用喵~
/// </summary>
public class BaseGraphView : GraphView, INekoGraphNodeFactory
{
    /// <summary>【中央情报局】所有节点 GUID → 视觉节点映射喵~</summary>
    protected Dictionary<string, BaseNode> NodeMap = new Dictionary<string, BaseNode>();

    protected List<BaseNode> SelectedNodes => selection.OfType<BaseNode>().ToList();

    protected string CurrentPackID = string.Empty;

    /// <summary>复制粘贴用的 JSON 设置喵~</summary>
    public static readonly JsonSerializerSettings GraphJsonSettings = new JsonSerializerSettings
    {
        Formatting = Formatting.Indented,
        TypeNameHandling = TypeNameHandling.Objects,
        NullValueHandling = NullValueHandling.Ignore
    };

    public BaseGraphView()
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
                if (element is BaseNode node)
                {
                    if (!string.IsNullOrEmpty(node.Data?.NodeID) && NodeMap.ContainsKey(node.Data.NodeID))
                        NodeMap.Remove(node.Data.NodeID);
                    OnNodeRemovedGeneric(node);
                }
            }
        }
        return changes;
    }

    protected virtual void OnNodeRemovedGeneric(BaseNode node) { }

    public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
    {
        return ports.ToList()
            .Where(p => p.node != startPort.node && p.direction != startPort.direction)
            .ToList();
    }

    public Vector2 ConvertScreenToLocal(Vector2 screenPosition, EditorWindow window)
    {
        Vector2 windowMousePosition = screenPosition - window.position.position;
        return contentViewContainer.WorldToLocal(windowMousePosition);
    }

    #region Node Management

    protected void SyncNodePositionToData(BaseNode node)
    {
        if (node.Data != null)
            node.Data.EditorPosition = node.GetNodePosition();
    }

    protected void AddNode(BaseNode node)
    {
        AddElement(node);
        if (!string.IsNullOrEmpty(node.Data?.NodeID))
        {
            NodeMap[node.Data.NodeID] = node;
        }
        OnNodeAddedGeneric(node);
    }

    protected virtual void OnNodeAddedGeneric(BaseNode node) { }

    public BaseNode CreateNode(Type nodeType, Vector2 position, BaseNodeData data = null)
    {
        if (data == null)
        {
            var attr = nodeType.GetCustomAttribute<NodeMenuItemAttribute>();
            if (attr == null) { Debug.LogError($"节点类型 {nodeType.Name} 缺少 [NodeMenuItem] 标签喵！"); return null; }
            data = Activator.CreateInstance(attr.DataType) as BaseNodeData;
            if (data == null) { Debug.LogError($"无法创建数据类型 {attr.DataType.Name} 喵！"); return null; }
            data.NodeID = Guid.NewGuid().ToString();
        }

        BaseNode node = Activator.CreateInstance(nodeType,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null, new object[] { data }, null) as BaseNode;

        if (node == null)
        {
            node = Activator.CreateInstance(nodeType,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, Array.Empty<object>(), null) as BaseNode;
            if (node != null && node.Data == null) { node.Data = data; node.SyncGUID(data.NodeID); }
        }

        if (node == null) { Debug.LogError($"无法创建节点 {nodeType.Name} 喵~"); return null; }

        node.SetNodePosition(position);
        AddNode(node);
        return node;
    }

    protected BaseNode CreateAndAddNodeFromData(BaseNodeData data, Vector2 position)
    {
        var nodeType = GetNodeTypeFromData(data);
        if (nodeType != null) return CreateNode(nodeType, position, data);
        Debug.LogWarning($"找不到与 {data.GetType().Name} 匹配的节点类型喵~");
        return null;
    }

    private Type GetNodeTypeFromData(BaseNodeData data)
    {
        var dataType = data.GetType();
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var type in assembly.GetTypes())
            {
                if (typeof(BaseNode).IsAssignableFrom(type) && !type.IsAbstract)
                {
                    var attr = type.GetCustomAttribute<NodeMenuItemAttribute>();
                    if (attr != null && attr.DataType == dataType) return type;
                }
            }
        }
        return null;
    }

    #endregion

    #region Copy & Paste

    [Serializable]
    private class CopyPasteData { public List<BaseNodeData> NodeDataList = new List<BaseNodeData>(); }

    private string SerializeCopyElements(IEnumerable<GraphElement> elements)
    {
        var copyData = new CopyPasteData();
        foreach (var node in elements.OfType<BaseNode>())
        {
            node.UpdateData();
            var data = node.CloneData();
            data.EditorPosition = node.GetNodePosition();
            copyData.NodeDataList.Add(data);
        }
        return JsonConvert.SerializeObject(copyData, GraphJsonSettings);
    }

    private void UnserializePasteElements(string operationName, string data)
    {
        try
        {
            var copyData = JsonConvert.DeserializeObject<CopyPasteData>(data, GraphJsonSettings);
            if (copyData == null || copyData.NodeDataList.Count == 0) return;
            ClearSelection();
            foreach (var nodeData in copyData.NodeDataList)
            {
                if (nodeData == null) continue;
                nodeData.NodeID = Guid.NewGuid().ToString();
                nodeData.EditorPosition += new Vector2(50, 50);
                var node = CreateAndAddNodeFromData(nodeData, nodeData.EditorPosition);
                AddToSelection(node);
                OnNodePasted(node);
            }
        }
        catch (Exception e) { Debug.LogError($"粘贴失败：{e.Message}"); }
    }

    /// <summary>粘贴后默认重新生成 GUID 喵~</summary>
    protected virtual void OnNodePasted(BaseNode node)
    {
        if (node?.Data == null) return;
        var newId = Guid.NewGuid().ToString();
        NodeMap.Remove(node.Data.NodeID);
        node.Data.NodeID = newId;
        node.SyncGUID(newId);
        NodeMap[newId] = node;
    }

    #endregion

    #region PackID

    public virtual void SetPackID(string packID) => CurrentPackID = packID ?? string.Empty;
    public virtual string GetPackID() => CurrentPackID;

    #endregion

    #region Flush / Populate

    /// <summary>
    /// 把当前画布节点状态写回到 Pack 的 Nodes 字典喵~
    /// 在 pack.ToJson() 之前调用喵~
    /// </summary>
    public void FlushToPack(BasePackData pack)
    {
        foreach (var node in NodeMap.Values)
        {
            node.UpdateData();
            SyncNodePositionToData(node);
            CollectConnections(node);
        }
        pack.PackID = CurrentPackID;
        pack.Nodes = NodeMap.Values
            .Where(n => n.Data != null)
            .ToDictionary(n => n.Data.NodeID, n => n.Data);
        pack.BuildRootNodeId();
    }

    /// <summary>
    /// 从 Pack 的 Nodes 字典重建画布喵~
    /// </summary>
    public void PopulateFromPack(BasePackData pack)
    {
        DeleteElements(graphElements);
        NodeMap.Clear();
        foreach (var data in pack.Nodes.Values)
        {
            if (data != null && !string.IsNullOrEmpty(data.NodeID))
                CreateAndAddNodeFromData(data, data.EditorPosition);
        }
        RestoreConnections();
    }

    #endregion

    #region Map ID Sync (Mission system)

    /// <summary>
    /// 同步地图 ID 到画布上所有 MapNode / BoundMapNode 喵~
    /// </summary>
    public void SetCurrentMapId(string mapId)
    {
        foreach (var node in NodeMap.Values)
        {
            if (node is MapNode mapNode) mapNode.UpdateMapId(mapId);
            else if (node is BoundMapNode boundMapNode) boundMapNode.UpdateMapId(mapId);
        }
    }

    #endregion

    #region Connection Helpers

    protected List<ConnectionData> CollectConnections(BaseNode node)
    {
        var connections = new List<ConnectionData>();
        int portIndex = 0;
        foreach (var element in node.outputContainer.Children())
        {
            if (element is Port outputPort)
            {
                foreach (var edge in outputPort.connections)
                {
                    if (edge.input.node is BaseNode targetNode && targetNode.Data != null)
                    {
                        var targetNodeId = targetNode.Data.NodeID;
                        if (!string.IsNullOrEmpty(targetNodeId))
                        {
                            int toPortIndex = GetPortIndexFromContainer(targetNode.inputContainer, edge.input);
                            connections.Add(new ConnectionData(portIndex, targetNodeId, toPortIndex));
                        }
                    }
                }
                portIndex++;
            }
        }
        SyncConnectionsToFields(node.Data, connections);
        node.Data.OutputConnections = connections;
        return connections;
    }

    private void SyncConnectionsToFields(BaseNodeData data, List<ConnectionData> connections)
    {
        var type = data.GetType();
        var byPort = connections.GroupBy(c => c.FromPortIndex)
            .ToDictionary(g => g.Key, g => g.Select(c => c.TargetNodeID).ToList());

        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            var attr = field.GetCustomAttribute<OutPortAttribute>();
            if (attr == null) continue;
            byPort.TryGetValue(attr.Index, out var ids);
            ids ??= new List<string>();
            if (field.FieldType == typeof(List<string>))
            {
                var list = field.GetValue(data) as List<string> ?? new List<string>();
                list.Clear(); list.AddRange(ids); field.SetValue(data, list);
            }
            else if (field.FieldType == typeof(string))
                field.SetValue(data, ids.FirstOrDefault() ?? "");
        }
    }

    protected void RestoreConnections()
    {
        foreach (var kvp in NodeMap)
        {
            var node = kvp.Value;
            var data = node.Data;
            if (data.OutputConnections == null || data.OutputConnections.Count == 0) continue;

            foreach (var conn in data.OutputConnections)
            {
                if (string.IsNullOrEmpty(conn.TargetNodeID)) continue;
                if (!NodeMap.TryGetValue(conn.TargetNodeID, out var targetNode)) continue;

                var outputPort = GetPortByIndex(node, conn.FromPortIndex, Direction.Output);
                var inputPort = GetPortByIndex(targetNode, conn.ToPortIndex, Direction.Input);
                if (outputPort == null || inputPort == null) continue;

                AddElement(outputPort.ConnectTo(inputPort));
                SetInPortFieldValue(targetNode.Data, conn.ToPortIndex, node.Data.NodeID);
            }
        }
    }

    private static void SetInPortFieldValue(BaseNodeData data, int portIndex, string sourceNodeID)
    {
        foreach (var field in data.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            var attr = field.GetCustomAttribute<InPortAttribute>();
            if (attr == null || attr.Index != portIndex) continue;
            if (field.FieldType == typeof(string))
                field.SetValue(data, sourceNodeID);
            else if (field.FieldType == typeof(List<string>))
            {
                var list = field.GetValue(data) as List<string> ?? new List<string>();
                if (!list.Contains(sourceNodeID)) list.Add(sourceNodeID);
                field.SetValue(data, list);
            }
            break;
        }
    }

    private static Port GetPortByIndex(BaseNode node, int portIndex, Direction direction)
    {
        var container = direction == Direction.Output ? node.outputContainer : node.inputContainer;
        int idx = 0;
        foreach (var e in container.Children())
        {
            if (e is Port p) { if (idx == portIndex) return p; idx++; }
        }
        return null;
    }

    private static int GetPortIndexFromContainer(VisualElement container, Port port)
    {
        int idx = 0;
        foreach (var e in container.Children())
        {
            if (e == port) return idx;
            if (e is Port) idx++;
        }
        return 0;
    }

    #endregion
}
#endif
