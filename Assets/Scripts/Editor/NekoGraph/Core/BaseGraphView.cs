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
/// 通用 GraphView 基类 - 封装画布通用逻辑喵~
/// 【彻底拥抱混沌·主语驱动重构版】
/// 【Newtonsoft.Json + TypeNameHandling.Auto 驱动】
/// - 使用 TypeNameHandling.Auto 自动保存类型信息，无需手动定义字段喵~！
/// - 所有节点统一存入 Nodes 列表，类型信息自动记录在 JSON 中
/// - 局部配置，不影响全局 SaveManager 设置喵~
/// 删除所有死板的端口校验，把自由还给开发者喵！
/// - 无 PortType 校验
/// - 无连接规则限制
/// - GetCompatiblePorts 只检查：不是同一节点 + 方向相反
/// - CollectConnections 捕获所有连接，不校验类型
/// 实现 INekoGraphNodeFactory 接口，用于 SearchWindow 解耦喵~
/// 仅编辑器使用喵~
/// </summary>
public abstract class BaseGraphView<TPack> : GraphView, INekoGraphNodeFactory
    where TPack : BasePackData
{
    /// <summary>
    /// 【中央情报局】GUID Dictionary - 基类统一管理画布上所有节点的生死喵~
    /// </summary>
    protected Dictionary<string, BaseNode> NodeMap = new Dictionary<string, BaseNode>();

    /// <summary>
    /// 当前选中的节点列表喵~
    /// </summary>
    protected List<BaseNode> SelectedNodes => selection.OfType<BaseNode>().ToList();

    /// <summary>
    /// Newtonsoft.Json 序列化设置 - 局部配置，不影响全局喵~
    /// TypeNameHandling.Auto 自动保存类型信息，无需手动定义字段喵！
    /// </summary>
    public static readonly JsonSerializerSettings GraphJsonSettings = new JsonSerializerSettings
    {
        Formatting = Formatting.Indented,
        TypeNameHandling = TypeNameHandling.Auto, // 自动保存类型信息喵~！
        NullValueHandling = NullValueHandling.Ignore
    };

    /// <summary>
    /// 构造函数喵~
    /// </summary>
    protected BaseGraphView()
    {
        SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
        this.AddManipulator(new ContentDragger());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());

        // 添加背景网格
        var grid = new GridBackground();
        Insert(0, grid);
        grid.StretchToParentSize();

        // 注册序列化回调
        serializeGraphElements = SerializeCopyElements;
        unserializeAndPaste = UnserializePasteElements;
        graphViewChanged += OnGraphViewChanged;
    }

    /// <summary>
    /// GraphView 变更回调喵~
    /// </summary>
    private GraphViewChange OnGraphViewChanged(GraphViewChange changes)
    {
        if (changes.elementsToRemove != null)
        {
            foreach (var element in changes.elementsToRemove)
            {
                if (element is BaseNode node)
                {
                    // 从 NodeMap 中移除喵~
                    if (!string.IsNullOrEmpty(node.Data?.NodeID) && NodeMap.ContainsKey(node.Data.NodeID))
                    {
                        NodeMap.Remove(node.Data.NodeID);
                    }
                    OnNodeRemovedGeneric(node);
                }
            }
        }
        return changes;
    }

    /// <summary>
    /// 节点移除回调（非泛型版本）喵~
    /// </summary>
    protected virtual void OnNodeRemovedGeneric(BaseNode node)
    {
        // 默认实现，子类重写
    }

    /// <summary>
    /// 核心：端口兼容性过滤喵~
    /// 【彻底拥抱混沌】只检查：1. 不是同一个 Node；2. 进出方向相反
    /// 删除所有 PortType 校验，把自由还给开发者喵！
    /// </summary>
    public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
    {
        var compatiblePorts = new List<Port>();

        foreach (var port in ports.ToList())
        {
            // 跳过自己（不是同一个节点）
            if (port.node == startPort.node)
                continue;

            // 跳过不同方向的端口（必须一进一出）
            if (port.direction == startPort.direction)
                continue;

            // ✅ 通过检查！开发者想怎么连就怎么连喵~
            compatiblePorts.Add(port);
        }

        return compatiblePorts;
    }

    public Vector2 ConvertScreenToLocal(Vector2 screenPosition, EditorWindow window)
    {
        // 1. 将屏幕坐标减去窗口的绝对坐标，得到窗口内相对坐标喵~
        Vector2 windowMousePosition = screenPosition - window.position.position;
        // 2. 利用 GraphView 自带的 contentViewContainer 把窗口坐标转成画布内的缩放/平移坐标喵！
        return contentViewContainer.WorldToLocal(windowMousePosition);
    }

    #region Node Management

    /// <summary>
    /// 同步节点位置到数据喵~
    /// 用于在序列化前确保位置信息是最新的喵！
    /// </summary>
    protected void SyncNodePositionToData(BaseNode node)
    {
        if (node.Data != null)
        {
            node.Data.EditorPosition = node.GetNodePosition();
        }
    }

    /// <summary>
    /// 添加节点到画布喵~
    /// </summary>
    protected void AddNode(BaseNode node)
    {
        AddElement(node);

        // 注册到 NodeMap 喵~
        if (!string.IsNullOrEmpty(node.Data?.NodeID))
        {
            if (NodeMap.ContainsKey(node.Data.NodeID))
            {
                NodeMap[node.Data.NodeID] = node;
            }
            else
            {
                NodeMap.Add(node.Data.NodeID, node);
            }
        }

        OnNodeAddedGeneric(node);
    }

    /// <summary>
    /// 节点添加回调（非泛型版本）喵~
    /// 子类可以重写此方法来分类存储不同类型的节点喵~
    /// </summary>
    protected virtual void OnNodeAddedGeneric(BaseNode node)
    {
        // 默认实现，子类重写
    }

    /// <summary>
    /// 终极节点工厂方法喵~！
    /// 通过反射自动创建任意节点类型，支持从菜单新建节点喵
    /// </summary>
    /// <param name="nodeType">节点类型喵~</param>
    /// <param name="position">节点位置喵~</param>
    /// <param name="data">节点数据（可选，如果为 null 则自动创建默认数据）喵~</param>
    /// <returns>创建的节点喵~</returns>
    public BaseNode CreateNode(Type nodeType, Vector2 position, BaseNodeData data = null)
    {
        // 如果没有传入 data (比如从菜单新建)，就利用反射自动生成默认 Data 喵~
        if (data == null)
        {
            var attr = nodeType.GetCustomAttribute<NodeMenuItemAttribute>();
            if (attr == null)
            {
                Debug.LogError($"节点类型 {nodeType.Name} 缺少 [NodeMenuItem] 标签喵！");
                return null;
            }

            data = Activator.CreateInstance(attr.DataType) as BaseNodeData;
            if (data == null)
            {
                Debug.LogError($"无法创建数据类型 {attr.DataType.Name} 喵！");
                return null;
            }
            data.NodeID = System.Guid.NewGuid().ToString(); // 基类统一包办 UUID 喵！
        }

        // 实例化节点并注入数据喵~
        // 优先尝试使用带参数的构造函数，如果失败则使用无参构造函数

        // 如果带参数的构造函数失败，尝试无参构造函数并手动设置数据
        if (Activator.CreateInstance(nodeType,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            new object[] { data },
            null) is not BaseNode node)
        {
            node = Activator.CreateInstance(nodeType,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                Array.Empty<object>(),
                null) as BaseNode;

            if (node != null && node.Data == null)
            {
                node.Data = data;
                node.SyncGUID(data.NodeID);
            }
        }

        if (node == null)
        {
            Debug.LogError($"无法创建节点 {nodeType.Name}，请检查构造函数是否接受 BaseNodeData 参数喵~");
            return null;
        }

        node.SetNodePosition(position);
        AddNode(node);

        return node;
    }

    /// <summary>
    /// 从数据创建并添加节点喵~
    /// 通过反射自动查找对应的节点类型喵~
    /// </summary>
    protected BaseNode CreateAndAddNodeFromData(BaseNodeData data, Vector2 position)
    {
        // 通过反射查找对应的节点类型喵~
        var nodeType = GetNodeTypeFromData(data);
        if (nodeType != null)
        {
            return CreateNode(nodeType, position, data);
        }

        Debug.LogWarning($"找不到与数据类型 {data.GetType().Name} 匹配的节点类型喵~");
        return null;
    }

    /// <summary>
    /// 根据节点数据类型获取对应的节点类型喵~
    /// 通过扫描所有程序集中带 [NodeMenuItem] 标签的类型来查找喵~
    /// </summary>
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
                    if (attr != null && attr.DataType == dataType)
                    {
                        return type;
                    }
                }
            }
        }
        return null;
    }

    #endregion

    #region Copy & Paste

    /// <summary>
    /// 复制粘贴数据容器喵~
    /// 【Newtonsoft.Json + TypeNameHandling.Auto 驱动】
    /// NodeData 直接使用 BaseNodeData 列表，让 Newtonsoft.Json 处理多态序列化喵~！
    /// </summary>
    [Serializable]
    private class CopyPasteData
    {
        public List<BaseNodeData> NodeDataList = new List<BaseNodeData>();
    }

    /// <summary>
    /// 序列化选中的元素喵~
    /// 【Newtonsoft.Json + TypeNameHandling.Auto 驱动】
    /// </summary>
    private string SerializeCopyElements(IEnumerable<GraphElement> elements)
    {
        var copyData = new CopyPasteData();
        var selectedNodes = elements.OfType<BaseNode>().ToList();

        foreach (var node in selectedNodes)
        {
            node.UpdateData();
            var data = node.CloneData();
            data.EditorPosition = node.GetNodePosition();
            // 直接添加到列表，让 Newtonsoft.Json 处理多态序列化喵~
            copyData.NodeDataList.Add(data);
        }

        return JsonConvert.SerializeObject(copyData, GraphJsonSettings);
    }

    /// <summary>
    /// 反序列化并粘贴元素喵~
    /// 【Newtonsoft.Json + TypeNameHandling.Auto 驱动】
    /// </summary>
    private void UnserializePasteElements(string operationName, string data)
    {
        try
        {
            var copyData = JsonConvert.DeserializeObject<CopyPasteData>(data, GraphJsonSettings);
            if (copyData == null || copyData.NodeDataList.Count == 0) return;

            ClearSelection();
            Vector2 pasteOffset = new Vector2(50, 50);

            foreach (var nodeData in copyData.NodeDataList)
            {
                if (nodeData != null)
                {
                    nodeData.EditorPosition += pasteOffset;
                    var node = CreateAndAddNodeFromData(nodeData, nodeData.EditorPosition);
                    AddToSelection(node);
                    OnNodePasted(node);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"粘贴失败：{e.Message}");
        }
    }

    /// <summary>
    /// 节点粘贴回调喵~
    /// </summary>
    protected virtual void OnNodePasted(BaseNode node)
    {
        // 默认实现，子类重写
    }

    #endregion

    #region Serialize / Deserialize

    /// <summary>
    /// 序列化到数据包喵~
    /// 【Newtonsoft.Json + TypeNameHandling.Auto 驱动】
    /// - 所有节点统一存入 Nodes 列表
    /// - 类型信息自动保存在 JSON 中（$type 字段）
    /// - 无需手动定义字段，新节点自动支持喵~！
    /// </summary>
    public virtual TPack SerializeToPack()
    {
        // 利用反射创建 TPack 实例喵~
        var pack = Activator.CreateInstance<TPack>();
        pack.PackID = System.Guid.NewGuid().ToString();

        // 遍历 NodeMap.Values，对每个节点执行：
        // 1. UpdateData() - 同步 UI 控件的值喵~
        // 2. SyncPositionToData() - 同步节点位置到数据喵~
        // 3. CollectConnections(node) - 从画布读取连线并回写到字段喵~
        foreach (var node in NodeMap.Values)
        {
            node.UpdateData();
            SyncNodePositionToData(node);  // 同步位置信息喵~
            CollectConnections(node);  // 直接更新 node.Data.OutputConnections 和 [OutPort] 字段
        }

        // 【一锅粥方案】所有节点直接丢进 Nodes 列表，类型信息交给 Newtonsoft.Json 处理喵~！
        pack.Nodes = NodeMap.Values.Where(n => n.Data != null).Select(n => n.Data).ToList();

        return pack;
    }

    /// <summary>
    /// 从数据包填充画布喵~
    /// 【Newtonsoft.Json + TypeNameHandling.Auto 驱动】
    /// - 直接从 Nodes 列表读取所有节点
    /// - 类型信息已自动恢复（$type 字段）
    /// - 无需手动遍历字段，一锅粥倒出来喵~！
    /// </summary>
    public virtual void PopulateFromPack(TPack pack)
    {
        // 清空画布和 NodeMap 喵~
        DeleteElements(graphElements);
        NodeMap.Clear();

        // 【一锅粥方案】直接从 Nodes 列表读取所有节点，类型信息已自动恢复喵~！
        foreach (var data in pack.Nodes)
        {
            if (data != null && !string.IsNullOrEmpty(data.NodeID))
            {
                CreateAndAddNodeFromData(data, data.EditorPosition);
            }
        }

        // 恢复连线喵~
        RestoreConnections();
    }

    /// <summary>
    /// 恢复节点连线喵~
    /// 使用 NodeMap 并调用静态工具方法恢复连线，子类无需干预喵~
    /// </summary>
    protected void RestoreConnections()
    {
        // 调用静态工具方法恢复连线（直接使用 NodeMap）
        RestoreConnectionsHelper<BaseGraphView<TPack>, TPack>(this, NodeMap);
    }

    #endregion

    #region Validation

    /// <summary>
    /// 验证数据包是否有效喵~
    /// </summary>
    public virtual bool ValidatePack(TPack pack)
    {
        return pack?.Validate() ?? false;
    }

    #endregion

    #region Connection Restoration

    /// <summary>
    /// 收集连线数据 - 从画布的 Port 读取连线，并回写到 [OutPort] 字段喵~
    /// 【连线自动捕获系统·修复版】
    /// - 遍历所有输出端口
    /// - 正确记录 FromPortIndex 和 ToPortIndex 喵~
    /// - 回写逻辑：通过 [OutPort(index)] 标签，尝试把 ID 同步到 Data 对应的字段里
    /// - 类型对得上就填，对不上也不报错，把自由还给开发者喵！
    /// </summary>
    protected List<ConnectionData> CollectConnections(BaseNode node)
    {
        var connections = new List<ConnectionData>();
        var data = node.Data;

        // 遍历输出容器的每一个 Port 喵~
        int portIndex = 0;
        foreach (var element in node.outputContainer.Children())
        {
            if (element is Port outputPort)
            {
                // 遍历该 Port 连出去的所有 Edge 喵~
                foreach (var edge in outputPort.connections)
                {
                    // 获取目标节点喵~
                    var inputNode = edge.input.node;
                    if (inputNode is BaseNode targetNode && targetNode.Data != null)
                    {
                        var targetNodeId = targetNode.Data.NodeID;
                        if (!string.IsNullOrEmpty(targetNodeId))
                        {
                            // 获取目标输入端口的索引喵~
                            int toPortIndex = GetPortIndexFromContainer(targetNode.inputContainer, edge.input);

                            connections.Add(new ConnectionData(
                                portIndex,
                                targetNodeId,
                                toPortIndex
                            ));
                        }
                    }
                }
                portIndex++;
            }
        }

        // 回写到 [OutPort] 字段喵~
        SyncConnectionsToFields(data, connections);

        // 同时更新 OutputConnections 列表喵~
        data.OutputConnections = connections;

        return connections;
    }

    /// <summary>
    /// 将连线数据回写到 [OutPort] 字段喵~
    /// 支持 string 字段和 List<string> 字段喵~
    /// </summary>
    private void SyncConnectionsToFields(BaseNodeData data, List<ConnectionData> connections)
    {
        var type = data.GetType();

        // 按端口索引分组连线喵~
        var connectionsByPortIndex = connections.GroupBy(c => c.FromPortIndex)
            .ToDictionary(g => g.Key, g => g.Select(c => c.TargetNodeID).ToList());

        // 遍历所有带 [OutPort] 标签的字段喵~
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            var outPortAttr = field.GetCustomAttribute<OutPortAttribute>();
            if (outPortAttr == null) continue;

            var portIndex = outPortAttr.Index;

            // 获取该端口的所有目标 ID 喵~
            if (!connectionsByPortIndex.TryGetValue(portIndex, out var targetIds))
            {
                targetIds = new List<string>();
            }

            // 处理 List<string> 字段喵~
            if (field.FieldType == typeof(List<string>))
            {
                var list = field.GetValue(data) as List<string>;
                if (list == null)
                {
                    list = new List<string>();
                    field.SetValue(data, list);
                }
                else
                {
                    list.Clear();
                }
                list.AddRange(targetIds);
            }
            // 处理 string 字段喵~
            else if (field.FieldType == typeof(string))
            {
                field.SetValue(data, targetIds.FirstOrDefault() ?? "");
            }
        }
    }

    /// <summary>
    /// 恢复连线 - 根据 ConnectionData 恢复所有节点的连线喵~
    /// 同时设置目标节点的 [InPort] 字段值
    /// 这是一个通用静态工具方法，所有子类共用喵~
    /// </summary>
    protected static void RestoreConnectionsHelper<TG, TP>(
        TG graph,
        Dictionary<string, BaseNode> nodeMap)
        where TG : BaseGraphView<TP>
        where TP : BasePackData
    {
        foreach (var kvp in nodeMap)
        {
            var node = kvp.Value;
            var data = node.Data;

            if (data.OutputConnections == null || data.OutputConnections.Count == 0) continue;

            foreach (var conn in data.OutputConnections)
            {
                if (string.IsNullOrEmpty(conn.TargetNodeID)) continue;
                if (!nodeMap.TryGetValue(conn.TargetNodeID, out var targetNode)) continue;

                // 获取输出端口
                var outputPort = GetPortByIndex(node, conn.FromPortIndex, Direction.Output);
                if (outputPort == null) continue;

                // 获取输入端口
                var inputPort = GetPortByIndex(targetNode, conn.ToPortIndex, Direction.Input);
                if (inputPort == null) continue;

                // 连接
                var edge = outputPort.ConnectTo(inputPort);
                graph.AddElement(edge);

                // 设置目标节点的 [InPort(ToPortIndex)] 字段值为源节点的 ID 喵~
                SetInPortFieldValue(targetNode.Data, conn.ToPortIndex, node.Data.NodeID);
            }
        }
    }

    /// <summary>
    /// 设置目标节点的 [InPort] 字段值喵~
    /// </summary>
    private static void SetInPortFieldValue(BaseNodeData data, int portIndex, string sourceNodeID)
    {
        var type = data.GetType();
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            var inPortAttr = field.GetCustomAttribute<InPortAttribute>();
            if (inPortAttr == null || inPortAttr.Index != portIndex) continue;

            // 处理 string 字段喵~
            if (field.FieldType == typeof(string))
            {
                field.SetValue(data, sourceNodeID);
            }
            // 处理 List<string> 字段喵~
            else if (field.FieldType == typeof(List<string>))
            {
                var list = field.GetValue(data) as List<string>;
                if (list == null)
                {
                    list = new List<string>();
                    field.SetValue(data, list);
                }
                if (!list.Contains(sourceNodeID))
                {
                    list.Add(sourceNodeID);
                }
            }
            break;
        }
    }

    /// <summary>
    /// 根据端口索引获取端口喵~
    /// </summary>
    private static Port GetPortByIndex(BaseNode node, int portIndex, Direction direction)
    {
        var container = direction == Direction.Output ? node.outputContainer : node.inputContainer;
        var children = container.Children();

        int index = 0;
        foreach (var element in children)
        {
            if (element is Port port)
            {
                if (index == portIndex) return port;
                index++;
            }
        }

        return null;
    }

    /// <summary>
    /// 从容器中获取端口的索引喵~
    /// </summary>
    private static int GetPortIndexFromContainer(VisualElement container, Port port)
    {
        int index = 0;
        foreach (var element in container.Children())
        {
            if (element == port) return index;
            if (element is Port) index++;
        }
        return 0; // 默认返回 0 喵~
    }

    #endregion
}
#endif
