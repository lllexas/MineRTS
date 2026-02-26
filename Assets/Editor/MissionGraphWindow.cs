using System;
using UnityEditor;
using UnityEngine;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.UIElements; // 提供 PopupField
using System.Reflection;      // 提供 GetCustomAttribute
using AIBrain;                // 确保能拿到 AIBrainBarHere

// 1. 窗口入口
public class MissionGraphWindow : EditorWindow
{
    private MissionGraphView _graphView;
    private MissionNodeSearchWindow _searchWindow;
    private string _currentFilePath;

    [MenuItem("Tools/猫娘助手/剧本编辑器 (Graph Mode)")]
    public static void Open() => GetWindow<MissionGraphWindow>("Mission Editor");

    private void OnEnable()
    {
        ConstructGraphView();
        GenerateToolbar();
    }

    private void ConstructGraphView()
    {
        // 创建 SearchWindow 提供者
        _searchWindow = ScriptableObject.CreateInstance<MissionNodeSearchWindow>();
        _searchWindow.EditorWindow = this;

        // 创建 GraphView
        _graphView = new MissionGraphView { name = "Mission Graph" };

        // 设置 SearchWindow 的 GraphView 引用
        _searchWindow.GraphView = _graphView;

        // 配置 GraphView 的右键创建节点事件
        _graphView.nodeCreationRequest = context =>
            SearchWindow.Open(new SearchWindowContext(context.screenMousePosition), _searchWindow);

        _graphView.StretchToParentSize();
        rootVisualElement.Add(_graphView);
    }

    private void GenerateToolbar()
    {
        var toolbar = new UnityEditor.UIElements.Toolbar();
        toolbar.Add(new Button(() => _graphView.CreateMissionNode(new MissionData { Title = "新任务", MissionID = System.Guid.NewGuid().ToString(), Goals = new List<MissionGoal>(), IsActive = false }, Vector2.zero)) { text = "新建任务" });

        // --- [NEW] 新建奖励节点按钮 ---
        toolbar.Add(new Button(() => _graphView.CreateRewardNode(new MissionReward { Blueprints = new List<string>() }, new Vector2(50, 50))) { text = "新建奖励" });
        // >>>>>>>>>>> [新增：导演节点按钮] >>>>>>>>>>>
        toolbar.Add(new Button(() => _graphView.CreateDirectorNode(new ScenarioEventData { EventID = System.Guid.NewGuid().ToString() }, new Vector2(100, 100))) { text = "🎬 触发器" });
        toolbar.Add(new Button(() => _graphView.CreateSpawnNode(new SpawnActionData { SpawnID = System.Guid.NewGuid().ToString(), Units = new List<SpawnUnitEntry>() }, new Vector2(350, 100))) { text = "⚔️ 召唤" });
        toolbar.Add(new Button(() => _graphView.CreateAIBrainNode(new AIBrainActionData { BrainNodeID = System.Guid.NewGuid().ToString() }, new Vector2(600, 100))) { text = "🧠 挂载AI" });

        // [新增] 新建地图节点按钮
        toolbar.Add(new Button(() => _graphView.CreateMapNode(new MapNodeData { NodeID = System.Guid.NewGuid().ToString(), MapID = "", SelectedPosition = Vector2Int.zero, PositionName = "新位置" }, new Vector2(800, 100))) { text = "🗺️ 地图位置" });

        // [新增] 新建绑定地图节点按钮
        toolbar.Add(new Button(() => _graphView.CreateBoundMapNode(new MapNodeData { NodeID = System.Guid.NewGuid().ToString(), MapID = "", SelectedPosition = Vector2Int.zero, PositionName = "绑定地图", IsBoundNode = true }, new Vector2(1000, 100))) { text = "🔗 绑定地图" });
        toolbar.Add(new Button(SaveData) { text = "保存 (JSON)" });
        toolbar.Add(new Button(LoadData) { text = "读取 (JSON)" });
        rootVisualElement.Add(toolbar);
    }

    // --- 序列化逻辑 ---
    private void SaveData()
    {
        string path = EditorUtility.SaveFilePanel("保存剧本", "Assets/Resources/Missions", "New_Missions.json", "json");
        if (string.IsNullOrEmpty(path)) return;

        var pack = _graphView.SerializeToPack();
        string json = JsonUtility.ToJson(pack, true);
        System.IO.File.WriteAllText(path, json);
        AssetDatabase.Refresh();
    }

    private void LoadData()
    {
        string path = EditorUtility.OpenFilePanel("读取剧本", "Assets/Resources/Missions", "json");
        if (string.IsNullOrEmpty(path)) return;

        string json = System.IO.File.ReadAllText(path);
        var pack = JsonUtility.FromJson<MissionPackData>(json);
        _graphView.PopulateFromPack(pack);
    }
}

// 2. 画布逻辑
public class MissionGraphView : GraphView
{
    private string _currentMapId = ""; // 当前任务包绑定的地图ID
    public string CurrentMapId => _currentMapId;
    private List<MapNode> _mapNodes = new List<MapNode>(); // 所有地图节点引用
    public List<MapNode> MapNodes => _mapNodes;
    private List<BoundMapNode> _boundMapNodes = new List<BoundMapNode>(); // 所有绑定地图节点引用
    public List<BoundMapNode> BoundMapNodes => _boundMapNodes;

    public MissionGraphView()
    {
        // 允许缩放和拖拽
        SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
        this.AddManipulator(new ContentDragger());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());

        // 背景网格
        var grid = new GridBackground();
        Insert(0, grid);
        grid.StretchToParentSize();

        // 右键创建节点 SearchWindow 将在 MissionGraphWindow 中配置

        // 复制粘贴支持
        serializeGraphElements = SerializeCopyElements;
        unserializeAndPaste = UnserializePasteElements;

        // 监听图形元素变化，清理已移除的节点引用
        graphViewChanged += OnGraphViewChanged;
    }

    private GraphViewChange OnGraphViewChanged(GraphViewChange changes)
    {
        if (changes.elementsToRemove != null)
        {
            // 清理被移除的地图节点引用
            foreach (var element in changes.elementsToRemove)
            {
                if (element is MapNode mapNode)
                {
                    _mapNodes.Remove(mapNode);
                }
                else if (element is BoundMapNode boundMapNode)
                {
                    _boundMapNodes.Remove(boundMapNode);
                }
            }
        }
        return changes;
    }

    // 设置当前地图ID，并更新所有地图节点
    public void SetCurrentMapId(string mapId)
    {
        if (_currentMapId == mapId) return;

        _currentMapId = mapId;

        // 更新所有地图节点的数据和UI
        foreach (var mapNode in _mapNodes)
        {
            mapNode.UpdateMapId(mapId);
        }

        // 更新所有绑定地图节点的数据和UI
        foreach (var boundMapNode in _boundMapNodes)
        {
            boundMapNode.UpdateMapId(mapId);
        }
    }

    // =========================================================
    // Copy & Paste Data Structure
    // =========================================================
    [Serializable]
    private class CopyPasteData
    {
        public List<MissionData> Missions = new List<MissionData>();
        public List<RewardSaveData> Rewards = new List<RewardSaveData>();
        public List<ScenarioEventData> Directors = new List<ScenarioEventData>();
        public List<SpawnActionData> Spawns = new List<SpawnActionData>();
        public List<AIBrainActionData> AIBrains = new List<AIBrainActionData>();
        public List<MapNodeData> MapNodes = new List<MapNodeData>();
    }

    // =========================================================
    // Copy & Paste Implementation
    // =========================================================
    private string SerializeCopyElements(IEnumerable<GraphElement> elements)
    {
        var copyData = new CopyPasteData();
        var selectedNodes = elements.OfType<BaseNode>().ToList();

        foreach (var node in selectedNodes)
        {
            var pos = node.GetPosition().position;

            switch (node)
            {
                case MissionNode missionNode:
                    var missionData = missionNode.Data;
                    missionData.EditorPosition = pos;
                    copyData.Missions.Add(missionData);
                    break;

                case RewardNode rewardNode:
                    copyData.Rewards.Add(new RewardSaveData
                    {
                        RewardID = rewardNode.GUID,
                        Position = pos,
                        Data = rewardNode.Reward
                    });
                    break;

                case DirectorNode directorNode:
                    var directorData = directorNode.Data;
                    directorData.EditorPosition = pos;
                    copyData.Directors.Add(directorData);
                    break;

                case SpawnNode spawnNode:
                    var spawnData = spawnNode.Data;
                    spawnData.EditorPosition = pos;
                    copyData.Spawns.Add(spawnData);
                    break;

                case AIBrainNode aiNode:
                    var aiData = aiNode.Data;
                    aiData.EditorPosition = pos;
                    copyData.AIBrains.Add(aiData);
                    break;

                case MapNode mapNode:
                    var mapData = mapNode.Data;
                    mapData.EditorPosition = pos;
                    copyData.MapNodes.Add(mapData);
                    break;
                case BoundMapNode boundMapNode:
                    var boundMapData = boundMapNode.Data;
                    boundMapData.EditorPosition = pos;
                    copyData.MapNodes.Add(boundMapData);
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

            // 清除当前选择
            ClearSelection();

            // 映射表：旧ID -> 新节点实例（用于连接恢复，但这里简化处理）
            var newNodes = new List<BaseNode>();

            // 粘贴偏移量
            Vector2 pasteOffset = new Vector2(50, 50);

            // 粘贴所有节点（注意：先创建所有节点，但不处理连接）
            foreach (var missionData in copyData.Missions)
            {
                var newMissionData = CloneMissionData(missionData);
                newMissionData.EditorPosition += pasteOffset;
                var node = CreateMissionNode(newMissionData, newMissionData.EditorPosition);
                newNodes.Add(node);
                AddToSelection(node);
            }

            foreach (var rewardSave in copyData.Rewards)
            {
                var newReward = CloneRewardData(rewardSave.Data);
                var newPos = rewardSave.Position + pasteOffset;
                var node = CreateRewardNode(newReward, newPos, System.Guid.NewGuid().ToString());
                newNodes.Add(node);
                AddToSelection(node);
            }

            foreach (var directorData in copyData.Directors)
            {
                var newDirectorData = CloneDirectorData(directorData);
                newDirectorData.EditorPosition += pasteOffset;
                var node = CreateDirectorNode(newDirectorData, newDirectorData.EditorPosition);
                newNodes.Add(node);
                AddToSelection(node);
            }

            foreach (var spawnData in copyData.Spawns)
            {
                var newSpawnData = CloneSpawnData(spawnData);
                newSpawnData.EditorPosition += pasteOffset;
                var node = CreateSpawnNode(newSpawnData, newSpawnData.EditorPosition);
                newNodes.Add(node);
                AddToSelection(node);
            }

            foreach (var aiData in copyData.AIBrains)
            {
                var newAiData = CloneAIBrainData(aiData);
                newAiData.EditorPosition += pasteOffset;
                var node = CreateAIBrainNode(newAiData, newAiData.EditorPosition);
                newNodes.Add(node);
                AddToSelection(node);
            }

            // [新增] 粘贴地图节点（区分普通地图节点和绑定地图节点）
            foreach (var mapData in copyData.MapNodes)
            {
                var newMapData = CloneMapNodeData(mapData);
                newMapData.EditorPosition += pasteOffset;
                if (newMapData.IsBoundNode)
                {
                    var node = CreateBoundMapNode(newMapData, newMapData.EditorPosition);
                    newNodes.Add(node);
                    AddToSelection(node);
                }
                else
                {
                    var node = CreateMapNode(newMapData, newMapData.EditorPosition);
                    newNodes.Add(node);
                    AddToSelection(node);
                }
            }

            // 注意：这里没有处理节点间的连线，因为连线ID已经改变
            // 在编辑器中使用时，用户需要手动重新连接复制的节点
        }
        catch (System.Exception e)
        {
            Debug.LogError($"粘贴失败: {e.Message}");
        }
    }

    // 数据克隆辅助方法（生成新GUID，避免冲突）
    private MissionData CloneMissionData(MissionData original)
    {
        var clone = new MissionData
        {
            MissionID = System.Guid.NewGuid().ToString(),
            Title = original.Title + " (副本)",
            Description = original.Description,
            Priority = original.Priority,
            EditorPosition = original.EditorPosition,
            IsActive = original.IsActive,
            IsCompleted = false,
            IsFailed = false,
            Reward = CloneRewardData(original.Reward),
            RewardID = "", // 清空奖励连接，需要重新连接
            NextMissionID = "" // 清空任务链，需要重新连接
        };

        // 克隆目标列表
        clone.Goals = new List<MissionGoal>();
        foreach (var goal in original.Goals)
        {
            clone.Goals.Add(new MissionGoal
            {
                Type = goal.Type,
                TargetKey = goal.TargetKey,
                RequiredAmount = goal.RequiredAmount,
                CurrentAmount = 0
            });
        }

        return clone;
    }

    private MissionReward CloneRewardData(MissionReward original)
    {
        return new MissionReward
        {
            Money = original.Money,
            TechPoints = original.TechPoints,
            Blueprints = new List<string>(original.Blueprints)
        };
    }

    private ScenarioEventData CloneDirectorData(ScenarioEventData original)
    {
        return new ScenarioEventData
        {
            EventID = System.Guid.NewGuid().ToString(),
            EditorPosition = original.EditorPosition,
            Trigger = original.Trigger,
            TriggerParam = original.TriggerParam,
            HasTriggered = false,
            NextSpawnID = "" // 清空连接
        };
    }

    private SpawnActionData CloneSpawnData(SpawnActionData original)
    {
        var clone = new SpawnActionData
        {
            SpawnID = System.Guid.NewGuid().ToString(),
            EditorPosition = original.EditorPosition,
            Team = original.Team,
            SpawnPos = original.SpawnPos,
            AttachAIBrainID = "" // 清空连接
        };

        clone.Units = new List<SpawnUnitEntry>();
        foreach (var unit in original.Units)
        {
            clone.Units.Add(new SpawnUnitEntry
            {
                BlueprintId = unit.BlueprintId,
                Count = unit.Count
            });
        }

        return clone;
    }

    private AIBrainActionData CloneAIBrainData(AIBrainActionData original)
    {
        return new AIBrainActionData
        {
            BrainNodeID = System.Guid.NewGuid().ToString(),
            EditorPosition = original.EditorPosition,
            BrainIdentifier = original.BrainIdentifier,
            TargetPos = original.TargetPos
        };
    }

    private MapNodeData CloneMapNodeData(MapNodeData original)
    {
        return new MapNodeData
        {
            NodeID = System.Guid.NewGuid().ToString(),
            EditorPosition = original.EditorPosition,
            MapID = original.MapID,
            SelectedPosition = original.SelectedPosition,
            PositionName = original.PositionName + " (副本)",
            IsBoundNode = original.IsBoundNode
        };
    }

    // 规定哪些端口能连接（增强类型检查）
    public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
    {
        return ports.ToList().Where(endPort =>
            endPort.direction != startPort.direction &&
            endPort.node != startPort.node &&
            endPort.portType == startPort.portType).ToList();
    }

    public MissionNode CreateMissionNode(MissionData data, Vector2 pos)
    {
        var node = new MissionNode(data);
        node.SetPosition(new Rect(pos, Vector2.zero));
        AddElement(node);
        return node;
    }

    public RewardNode CreateRewardNode(MissionReward reward, Vector2 pos, string id = null)
    {
        var node = new RewardNode(reward, id ?? System.Guid.NewGuid().ToString());
        node.SetPosition(new Rect(pos, Vector2.zero));
        AddElement(node);
        return node;
    }
    public DirectorNode CreateDirectorNode(ScenarioEventData data, Vector2 pos)
    {
        var node = new DirectorNode(data);
        node.SetPosition(new Rect(pos, Vector2.zero));
        AddElement(node);
        return node;
    }
    public SpawnNode CreateSpawnNode(SpawnActionData data, Vector2 pos)
    {
        var node = new SpawnNode(data); node.SetPosition(new Rect(pos, Vector2.zero)); AddElement(node); return node;
    }
    public AIBrainNode CreateAIBrainNode(AIBrainActionData data, Vector2 pos)
    {
        var node = new AIBrainNode(data); node.SetPosition(new Rect(pos, Vector2.zero)); AddElement(node); return node;
    }

    public MapNode CreateMapNode(MapNodeData data, Vector2 pos)
    {
        var node = new MapNode(data, this);
        node.SetPosition(new Rect(pos, Vector2.zero));
        AddElement(node);
        return node;
    }

    public BoundMapNode CreateBoundMapNode(MapNodeData data, Vector2 pos)
    {
        data.IsBoundNode = true; // 确保标记为绑定地图节点
        var node = new BoundMapNode(data, this);
        node.SetPosition(new Rect(pos, Vector2.zero));
        AddElement(node);
        return node;
    }

    public MissionPackData SerializeToPack()
    {
        var pack = new MissionPackData();
        pack.BoundStageID = _currentMapId;
        var allNodes = this.nodes.Cast<BaseNode>().ToList();

        // 1. 保存所有奖励节点（位置和数据）
        var rewardNodes = allNodes.OfType<RewardNode>().ToList();
        foreach (var rNode in rewardNodes)
        {
            pack.Rewards.Add(new RewardSaveData
            {
                RewardID = rNode.GUID,
                Position = rNode.GetPosition().position,
                Data = rNode.Reward
            });
        }

        // 2. 保存所有任务节点
        var missionNodes = allNodes.OfType<MissionNode>().ToList();
        foreach (var mNode in missionNodes)
        {
            var data = mNode.Data;
            data.EditorPosition = mNode.GetPosition().position;

            // 逻辑线 (NextMissionID)
            var logicEdge = this.edges.ToList().FirstOrDefault(e => e.output == mNode.OutputPort);
            if (logicEdge != null && logicEdge.input.node is MissionNode nextM)
                data.NextMissionID = nextM.Data.MissionID;
            else
                data.NextMissionID = "";

            // 🔥 [关键修改] 奖励线：记录 RewardID 供编辑器下次读取连线
            var rewardEdge = this.edges.ToList().FirstOrDefault(e => e.output == mNode.RewardPort);
            if (rewardEdge != null && rewardEdge.input.node is RewardNode rNode)
            {
                data.RewardID = rNode.GUID;
                // 同时为了保证游戏运行时能直接拿到数据，也可以把数据拷贝一份进去
                data.Reward = rNode.Reward;
            }
            else
            {
                data.RewardID = "";
                data.Reward = new MissionReward();
            }

            pack.Missions.Add(data);
        }

        // >>>>>>>>>>> [新增：保存剧本事件] >>>>>>>>>>>
        foreach (var dNode in allNodes.OfType<DirectorNode>())
        {
            var data = dNode.Data; data.EditorPosition = dNode.GetPosition().position;
            var triggerEdge = this.edges.ToList().FirstOrDefault(e => e.input == dNode.InputPort && e.output.node is MissionNode);
            if (triggerEdge != null) { data.Trigger = TriggerType.MissionCompleted; data.TriggerParam = ((MissionNode)triggerEdge.output.node).Data.MissionID; }

            // [新增] 记录 Director -> Spawn 连线
            var spawnEdge = this.edges.ToList().FirstOrDefault(e => e.output == dNode.OutputPort && e.input.node is SpawnNode);
            data.NextSpawnID = spawnEdge != null ? ((SpawnNode)spawnEdge.input.node).Data.SpawnID : "";
            pack.ScenarioEvents.Add(data);
        }

        // [新增] 序列化 Spawn 和 AIBrain
        foreach (var sNode in allNodes.OfType<SpawnNode>())
        {
            var data = sNode.Data; data.EditorPosition = sNode.GetPosition().position;
            var aiEdge = this.edges.ToList().FirstOrDefault(e => e.output == sNode.OutputPort && e.input.node is AIBrainNode);
            data.AttachAIBrainID = aiEdge != null ? ((AIBrainNode)aiEdge.input.node).Data.BrainNodeID : "";
            pack.SpawnActions.Add(data);
        }
        foreach (var aNode in allNodes.OfType<AIBrainNode>())
        {
            var data = aNode.Data; data.EditorPosition = aNode.GetPosition().position;
            pack.AIBrainActions.Add(data);
        }

        // [新增] 序列化地图节点（普通地图节点和绑定地图节点）
        foreach (var mNode in allNodes.OfType<MapNode>())
        {
            var data = mNode.Data; data.EditorPosition = mNode.GetPosition().position;
            data.IsBoundNode = false; // 普通地图节点
            pack.MapNodes.Add(data);
        }
        foreach (var bNode in allNodes.OfType<BoundMapNode>())
        {
            var data = bNode.Data; data.EditorPosition = bNode.GetPosition().position;
            data.IsBoundNode = true; // 绑定地图节点
            pack.MapNodes.Add(data);
        }

        // [新增] 处理地图节点到其他节点的坐标连接
        // 查找所有从地图节点输出端口出发的连接
        foreach (var mNode in allNodes.OfType<MapNode>())
        {
            var mapPos = mNode.Data.SelectedPosition;

            // 清空现有连接列表
            mNode.Data.ConnectedSpawnIds.Clear();
            mNode.Data.ConnectedBrainIds.Clear();

            // 查找连接到SpawnNode坐标输入端口的连接
            var spawnEdges = this.edges.ToList().Where(e => e.output == mNode.OutputPort && e.input.node is SpawnNode).ToList();
            foreach (var edge in spawnEdges)
            {
                var spawnNode = (SpawnNode)edge.input.node;
                // 将地图坐标复制到SpawnNode的Data中
                spawnNode.Data.SpawnPos = mapPos;
                // 保存连接ID
                mNode.Data.ConnectedSpawnIds.Add(spawnNode.Data.SpawnID);
            }

            // 查找连接到AIBrainNode坐标输入端口的连接
            var aiEdges = this.edges.ToList().Where(e => e.output == mNode.OutputPort && e.input.node is AIBrainNode).ToList();
            foreach (var edge in aiEdges)
            {
                var aiNode = (AIBrainNode)edge.input.node;
                // 将地图坐标复制到AIBrainNode的Data中
                aiNode.Data.TargetPos = mapPos;
                // 保存连接ID
                mNode.Data.ConnectedBrainIds.Add(aiNode.Data.BrainNodeID);
            }
        }

        return pack;
    }

    public void PopulateFromPack(MissionPackData pack)
    {
        DeleteElements(graphElements);

        // 清空地图节点列表（因为即将重新创建所有节点）
        _mapNodes.Clear();
        _boundMapNodes.Clear();

        // 设置当前地图ID
        _currentMapId = pack.BoundStageID;

        var missionMap = new Dictionary<string, MissionNode>();
        var rewardMap = new Dictionary<string, RewardNode>();
        var directorMap = new Dictionary<string, DirectorNode>();
        var spawnMap = new Dictionary<string, SpawnNode>();
        var aiMap = new Dictionary<string, AIBrainNode>();
        var mapMap = new Dictionary<string, MapNode>(); // 新增：地图节点映射

        // 1. 还原所有奖励节点
        if (pack.Rewards != null)
        {
            foreach (var rSave in pack.Rewards)
            {
                var rNode = CreateRewardNode(rSave.Data, rSave.Position, rSave.RewardID);
                rewardMap[rSave.RewardID] = rNode;
            }
        }

        // 2. 还原所有任务节点
        foreach (var m in pack.Missions)
        {
            var mNode = CreateMissionNode(m, m.EditorPosition);
            missionMap[m.MissionID] = mNode;
        }
        // >>> [新增：还原导演节点] >>>
        if (pack.ScenarioEvents != null)
        {
            foreach (var d in pack.ScenarioEvents)
            {
                var dNode = CreateDirectorNode(d, d.EditorPosition);
                directorMap[d.EventID] = dNode;
            }
        }

        if (pack.SpawnActions != null) foreach (var s in pack.SpawnActions) spawnMap[s.SpawnID] = CreateSpawnNode(s, s.EditorPosition);
        if (pack.AIBrainActions != null) foreach (var a in pack.AIBrainActions) aiMap[a.BrainNodeID] = CreateAIBrainNode(a, a.EditorPosition);
        // [新增] 还原地图节点（区分普通地图节点和绑定地图节点）
        if (pack.MapNodes != null)
        {
            foreach (var map in pack.MapNodes)
            {
                if (map.IsBoundNode)
                {
                    var bNode = CreateBoundMapNode(map, map.EditorPosition);
                    // 绑定地图节点不需要加入到mapMap中，因为它没有输出端口
                }
                else
                {
                    mapMap[map.NodeID] = CreateMapNode(map, map.EditorPosition);
                }
            }
        }

        // 3. 还原所有连线
        foreach (var m in pack.Missions)
        {
            // 还原逻辑线
            if (!string.IsNullOrEmpty(m.NextMissionID) && missionMap.ContainsKey(m.NextMissionID))
            {
                var edge = missionMap[m.MissionID].OutputPort.ConnectTo(missionMap[m.NextMissionID].InputPort);
                AddElement(edge);
            }

            // 🔥 [还原奖励线]：靠 RewardID 找回刚才生成的奖励节点
            if (!string.IsNullOrEmpty(m.RewardID) && rewardMap.ContainsKey(m.RewardID))
            {
                var edge = missionMap[m.MissionID].RewardPort.ConnectTo(rewardMap[m.RewardID].InputPort);
                AddElement(edge);
            }
        }
        // >>> [新增：还原导演节点的连线] >>>
        if (pack.ScenarioEvents != null)
        {
            foreach (var d in pack.ScenarioEvents)
            {
                if (d.Trigger == TriggerType.MissionCompleted && missionMap.ContainsKey(d.TriggerParam))
                    AddElement(missionMap[d.TriggerParam].OutputPort.ConnectTo(directorMap[d.EventID].InputPort));

                // [新增] 还原 Director -> Spawn 连线
                if (!string.IsNullOrEmpty(d.NextSpawnID) && spawnMap.ContainsKey(d.NextSpawnID))
                    AddElement(directorMap[d.EventID].OutputPort.ConnectTo(spawnMap[d.NextSpawnID].InputPort));
            }
        }
        // [新增] 还原 Spawn -> AIBrain 连线
        if (pack.SpawnActions != null)
        {
            foreach (var s in pack.SpawnActions)
            {
                if (!string.IsNullOrEmpty(s.AttachAIBrainID) && aiMap.ContainsKey(s.AttachAIBrainID))
                    AddElement(spawnMap[s.SpawnID].OutputPort.ConnectTo(aiMap[s.AttachAIBrainID].InputPort));
            }
        }

        // [新增] 还原地图节点到其他节点的连线
        if (pack.MapNodes != null)
        {
            foreach (var mapData in pack.MapNodes)
            {
                if (mapData.IsBoundNode) continue; // 绑定地图节点没有端口，不需要恢复连线

                // 查找对应的MapNode实例
                var mapNode = _mapNodes.FirstOrDefault(n => n.GUID == mapData.NodeID);
                if (mapNode == null) continue;

                // 恢复地图节点到SpawnNode的连接
                foreach (var spawnId in mapData.ConnectedSpawnIds)
                {
                    if (spawnMap.TryGetValue(spawnId, out var spawnNode))
                    {
                        // 查找SpawnNode的CoordInputPort（坐标输入端口）
                        var spawnCoordPort = spawnNode.CoordInputPort;
                        if (spawnCoordPort != null && mapNode.OutputPort != null)
                        {
                            var edge = mapNode.OutputPort.ConnectTo(spawnCoordPort);
                            AddElement(edge);
                        }
                    }
                }

                // 恢复地图节点到AIBrainNode的连接
                foreach (var brainId in mapData.ConnectedBrainIds)
                {
                    if (aiMap.TryGetValue(brainId, out var aiNode))
                    {
                        // 查找AIBrainNode的CoordInputPort（坐标输入端口）
                        var aiCoordPort = aiNode.CoordInputPort;
                        if (aiCoordPort != null && mapNode.OutputPort != null)
                        {
                            var edge = mapNode.OutputPort.ConnectTo(aiCoordPort);
                            AddElement(edge);
                        }
                    }
                }
            }
        }
    }
}

// --- [核心：基础节点类] ---
public abstract class BaseNode : Node
{
    public string GUID;
    public abstract void UpdateData();
}

// --- [任务节点] ---
public class MissionNode : BaseNode
{
    public MissionData Data;
    public Port InputPort;     // 前置逻辑输入
    public Port OutputPort;    // 后续逻辑输出
    public Port RewardPort;    // 奖励连接口

    public MissionNode(MissionData data)
    {
        Data = data;
        GUID = data.MissionID;
        title = "任务: " + data.Title;

        // --- 样式设定 ---
        style.width = 250;
        this.titleContainer.style.backgroundColor = new Color(0.2f, 0.3f, 0.4f);

        // --- 端口设置 ---
        InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
        InputPort.portName = "激活输入";
        inputContainer.Add(InputPort);

        OutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
        OutputPort.portName = "完成输出";
        outputContainer.Add(OutputPort);

        RewardPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(MissionReward));
        RewardPort.portName = "任务奖励";
        outputContainer.Add(RewardPort);

        // --- 内容编辑区 ---
        var foldout = new Foldout() { text = "详细配置", value = true };

        // 标题与描述
        var titleField = new TextField("任务名");
        titleField.value = Data.Title;
        titleField.RegisterValueChangedCallback(evt => { Data.Title = evt.newValue; title = "任务: " + evt.newValue; });
        foldout.Add(titleField);

        var descField = new TextField("任务描述") { multiline = true };
        descField.value = Data.Description;
        descField.RegisterValueChangedCallback(evt => Data.Description = evt.newValue);
        foldout.Add(descField);

        // 主支线与初始激活
        var priorityField = new EnumField("优先级", Data.Priority);
        priorityField.RegisterValueChangedCallback(evt => Data.Priority = (MissionPriority)evt.newValue);
        foldout.Add(priorityField);

        var activeToggle = new Toggle("初始激活") { value = Data.IsActive };
        activeToggle.RegisterValueChangedCallback(evt => Data.IsActive = evt.newValue);
        foldout.Add(activeToggle);

        // --- 目标列表 (Goals) ---
        var goalContainer = new VisualElement();
        var addGoalBtn = new Button(() => {
            Data.Goals.Add(new MissionGoal());
            RefreshGoalList(goalContainer);
        })
        { text = "添加目标 +" };

        foldout.Add(new Label("任务目标:"));
        foldout.Add(goalContainer);
        foldout.Add(addGoalBtn);
        RefreshGoalList(goalContainer);

        extensionContainer.Add(foldout);
        RefreshExpandedState();
    }

    private void RefreshGoalList(VisualElement container)
    {
        container.Clear();
        for (int i = 0; i < Data.Goals.Count; i++)
        {
            var goal = Data.Goals[i];

            var box = new VisualElement();
            box.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.6f);

            // --- 修正点：手动设置四个方向的内边距喵 ---
            box.style.paddingTop = 4;
            box.style.paddingBottom = 4;
            box.style.paddingLeft = 4;
            box.style.paddingRight = 4;
            // ----------------------------------------

            box.style.marginBottom = 4;
            box.style.borderBottomWidth = 1;
            box.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f);

            // --- 第一行：类型选择 + 删除按钮 ---
            var line1 = new VisualElement();
            line1.style.flexDirection = FlexDirection.Row;

            var typeField = new EnumField(goal.Type);
            typeField.style.flexGrow = 1;
            typeField.RegisterValueChangedCallback(evt => {
                goal.Type = (GoalType)evt.newValue;
                AutoFixGoalKey(goal);
                RefreshGoalList(container);
            });

            int index = i;
            var delBtn = new Button(() => { Data.Goals.RemoveAt(index); RefreshGoalList(container); }) { text = "×" };
            delBtn.style.color = Color.red;
            delBtn.style.width = 20;

            line1.Add(typeField);
            line1.Add(delBtn);

            // --- 第二行：ID输入 + 数量输入 ---
            var line2 = new VisualElement();
            line2.style.flexDirection = FlexDirection.Row;
            line2.style.marginTop = 2;

            var keyField = new TextField();
            keyField.value = goal.TargetKey;
            keyField.style.flexGrow = 1;
            keyField.style.minWidth = 50;
            keyField.tooltip = GetHintForType(goal.Type);
            keyField.RegisterValueChangedCallback(evt => goal.TargetKey = evt.newValue);

            var amtField = new LongField();
            amtField.value = goal.RequiredAmount;
            amtField.style.width = 65;
            amtField.RegisterValueChangedCallback(evt => goal.RequiredAmount = evt.newValue);

            var lblId = new Label(" ID:");
            lblId.style.fontSize = 10;
            lblId.style.unityTextAlign = TextAnchor.MiddleLeft;

            var lblQty = new Label(" #:");
            lblQty.style.fontSize = 10;
            lblQty.style.unityTextAlign = TextAnchor.MiddleLeft;

            line2.Add(lblId);
            line2.Add(keyField);
            line2.Add(lblQty);
            line2.Add(amtField);

            box.Add(line1);
            box.Add(line2);
            container.Add(box);
        }
    }
    private string GetHintForType(GoalType type)
    {
        switch (type)
        {
            case GoalType.BuildEntity: return "建筑ID:";
            case GoalType.KillEntity: return "单位ID:";
            case GoalType.SellResource: return "资源ID:";
            case GoalType.ReachPosition: return "坐标(x,y):";
            case GoalType.SurviveTime: return "计时器:";
            case GoalType.EarnMoney: return "货币名:";
            default: return "目标Key:";
        }
    }

    // --- 辅助方法 2：自动填好那些不用想的东西喵 ---
    private void AutoFixGoalKey(MissionGoal goal)
    {
        switch (goal.Type)
        {
            case GoalType.EarnMoney:
                goal.TargetKey = "Gold"; // 自动锁定金币
                break;
            case GoalType.SurviveTime:
                goal.TargetKey = "Seconds"; // 自动锁定秒数
                break;
            case GoalType.ReachPosition:
                if (string.IsNullOrEmpty(goal.TargetKey)) goal.TargetKey = "0,0";
                break;
        }
    }
    public override void UpdateData() { }
}

// --- [奖励节点] ---
public class RewardNode : BaseNode
{
    public MissionReward Reward;
    public Port InputPort;

    public RewardNode(MissionReward reward, string id)
    {
        Reward = reward;
        GUID = id;
        title = "★ 任务奖励 ★";
        style.width = 200;
        this.titleContainer.style.backgroundColor = new Color(0.1f, 0.4f, 0.1f);

        InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(MissionReward));
        InputPort.portName = "连接任务";
        inputContainer.Add(InputPort);

        // 编辑项
        var moneyField = new LongField("金钱奖励");
        moneyField.value = Reward.Money;
        moneyField.RegisterValueChangedCallback(evt => Reward.Money = evt.newValue);
        extensionContainer.Add(moneyField);

        var techField = new IntegerField("科技点数");
        techField.value = Reward.TechPoints;
        techField.RegisterValueChangedCallback(evt => Reward.TechPoints = evt.newValue);
        extensionContainer.Add(techField);

        // 图纸解锁 (简化为逗号隔开)
        var bpField = new TextField("解锁图纸(逗号隔开)");
        bpField.value = string.Join(",", Reward.Blueprints);
        bpField.RegisterValueChangedCallback(evt => {
            Reward.Blueprints = evt.newValue.Split(',').Select(s => s.Trim()).ToList();
        });
        extensionContainer.Add(bpField);

        RefreshExpandedState();
    }

    public override void UpdateData() { }
}

// --- [纯触发器节点] ---
public class DirectorNode : BaseNode
{
    public ScenarioEventData Data;
    public Port InputPort;
    public Port OutputPort; // [新增] 输出动作
    private TextField _paramField;

    public DirectorNode(ScenarioEventData data)
    {
        Data = data; GUID = data.EventID; title = "🎬 触发器";
        style.width = 200; titleContainer.style.backgroundColor = new Color(0.4f, 0.1f, 0.4f);

        InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
        InputPort.portName = "条件"; inputContainer.Add(InputPort);

        OutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(SpawnActionData));
        OutputPort.portName = "触发行为"; outputContainer.Add(OutputPort);

        var triggerField = new EnumField("类型", Data.Trigger);
        triggerField.RegisterValueChangedCallback(evt =>
        {
            Data.Trigger = (TriggerType)evt.newValue;
            UpdateParamFieldHint();
        });
        extensionContainer.Add(triggerField);

        _paramField = new TextField("参数") { value = Data.TriggerParam };
        _paramField.RegisterValueChangedCallback(evt => Data.TriggerParam = evt.newValue);
        extensionContainer.Add(_paramField);

        // 初始化提示
        UpdateParamFieldHint();

        RefreshExpandedState();
    }

    private void UpdateParamFieldHint()
    {
        string hint = GetTriggerParamHint(Data.Trigger);
        string example = GetTriggerParamExample(Data.Trigger);

        // 在tooltip中同时显示提示和示例
        if (!string.IsNullOrEmpty(example))
        {
            _paramField.tooltip = $"{hint}\n\n示例: {example}";
        }
        else
        {
            _paramField.tooltip = hint;
        }

        // 尝试更新TextField的标签（如果支持）
        try
        {
            string labelText = GetTriggerParamLabel(Data.Trigger);
            // 直接尝试设置label属性（如果存在）
            // Unity UI Elements的TextField有label属性
            _paramField.label = labelText;
        }
        catch
        {
            // 如果label属性不存在或不可写，忽略错误
        }
    }

    private string GetTriggerParamHint(TriggerType triggerType)
    {
        switch (triggerType)
        {
            case TriggerType.Time:
                return "触发时间（秒），浮点数。游戏运行达到此时间后触发事件。\n示例: 120.5 表示120.5秒后触发";
            case TriggerType.MissionCompleted:
                return "任务ID。当指定任务完成时触发事件。\n需要输入任务的MissionID，可通过连接任务节点自动获取";
            case TriggerType.AreaReached:
                return "区域标识符或坐标。当单位到达指定区域时触发。\n格式: 区域ID 或 \"x,y\" 坐标字符串";
            default:
                return "触发条件参数";
        }
    }

    private string GetTriggerParamExample(TriggerType triggerType)
    {
        switch (triggerType)
        {
            case TriggerType.Time:
                return "例如: 60.0, 120.5, 300";
            case TriggerType.MissionCompleted:
                return "例如: mission_tutorial_1, wave_1_clear";
            case TriggerType.AreaReached:
                return "例如: enemy_base, 128,64";
            default:
                return "";
        }
    }

    private string GetTriggerParamLabel(TriggerType triggerType)
    {
        switch (triggerType)
        {
            case TriggerType.Time:
                return "时间（秒）";
            case TriggerType.MissionCompleted:
                return "任务ID";
            case TriggerType.AreaReached:
                return "区域参数";
            default:
                return "参数";
        }
    }

    public override void UpdateData() { }
}

// --- [召唤节点] ---
public class SpawnNode : BaseNode
{
    public SpawnActionData Data;
    public Port InputPort;
    public Port CoordInputPort; // 新增：坐标输入端口
    public Port OutputPort;

    public SpawnNode(SpawnActionData data)
    {
        Data = data; GUID = data.SpawnID; title = "⚔️ 召唤单位";
        style.width = 250; titleContainer.style.backgroundColor = new Color(0.6f, 0.2f, 0.2f);

        InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(SpawnActionData));
        InputPort.portName = "执行"; inputContainer.Add(InputPort);

        // 新增：坐标输入端口（Vector2Int类型）
        CoordInputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(Vector2Int));
        CoordInputPort.portName = "坐标输入"; inputContainer.Add(CoordInputPort);

        OutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(AIBrainActionData));
        OutputPort.portName = "挂载AI"; outputContainer.Add(OutputPort);

        var teamField = new IntegerField("阵营") { value = Data.Team };
        teamField.RegisterValueChangedCallback(evt => Data.Team = evt.newValue);
        extensionContainer.Add(teamField);

        var posField = new Vector2IntField("坐标") { value = Data.SpawnPos };
        posField.RegisterValueChangedCallback(evt => Data.SpawnPos = evt.newValue);
        extensionContainer.Add(posField);

        var listContainer = new VisualElement();
        var addBtn = new Button(() => { Data.Units.Add(new SpawnUnitEntry()); RefreshList(listContainer); }) { text = "+ 添加兵种" };
        extensionContainer.Add(addBtn);
        extensionContainer.Add(listContainer);
        RefreshList(listContainer);

        RefreshExpandedState();
    }

    private void RefreshList(VisualElement container)
    {
        container.Clear();

        // 1. 获取所有合法的蓝图 ID
        var bpChoices = BlueprintRegistry.GetAllBlueprintIds();
        if (bpChoices.Count == 0) bpChoices.Add("None");

        for (int i = 0; i < Data.Units.Count; i++)
        {
            var unit = Data.Units[i];
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 2 } };

            // 2. 数据容错：如果当前配置的ID不在列表里（比如改名了/废弃了），强行加进去防止UI报错
            string currentVal = string.IsNullOrEmpty(unit.BlueprintId) ? bpChoices[0] : unit.BlueprintId;
            if (!bpChoices.Contains(currentVal)) bpChoices.Add(currentVal);

            // 3. 【核心修改】使用 PopupField 替代 TextField！
            var idDropdown = new PopupField<string>(bpChoices, currentVal);
            idDropdown.style.flexGrow = 1; // 占满左边空间
            idDropdown.RegisterValueChangedCallback(evt => unit.BlueprintId = evt.newValue);

            var countField = new IntegerField { value = unit.Count, style = { width = 40 } };
            countField.RegisterValueChangedCallback(evt => unit.Count = evt.newValue);

            int index = i;
            var delBtn = new Button(() => { Data.Units.RemoveAt(index); RefreshList(container); }) { text = "×" };

            // 组装 UI
            row.Add(idDropdown);
            row.Add(countField);
            row.Add(delBtn);
            container.Add(row);
        }
    }
    public override void UpdateData() { }
}

// --- [AI 挂载节点] ---
public class AIBrainNode : BaseNode
{
    public AIBrainActionData Data;
    public Port InputPort;
    public Port CoordInputPort; // 新增：坐标输入端口

    public AIBrainNode(AIBrainActionData data)
    {
        Data = data; GUID = data.BrainNodeID; title = "🧠 AI 波次控制";
        style.width = 200; titleContainer.style.backgroundColor = new Color(0.2f, 0.5f, 0.6f);

        InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(AIBrainActionData));
        InputPort.portName = "接管单位"; inputContainer.Add(InputPort);

        // 新增：坐标输入端口（Vector2Int类型）
        CoordInputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(Vector2Int));
        CoordInputPort.portName = "坐标输入"; inputContainer.Add(CoordInputPort);

        // --- 【核心修改：反射抓取所有 AI 标识符】 ---
        var aiChoices = new List<string> { "无" }; // 留个空选项

        // 使用 Unity 超高速的 TypeCache 瞬间抓取所有标记了此特性的类，无需运行时实例化！
        var aiTypes = TypeCache.GetTypesWithAttribute<AIBrainBarHere>();
        foreach (var t in aiTypes)
        {
            var attr = t.GetCustomAttribute<AIBrainBarHere>();
            if (attr != null && !string.IsNullOrEmpty(attr.Identifier))
            {
                aiChoices.Add(attr.Identifier);
            }
        }

        // 数据容错
        string currentAiVal = string.IsNullOrEmpty(Data.BrainIdentifier) ? aiChoices[0] : Data.BrainIdentifier;
        if (!aiChoices.Contains(currentAiVal)) aiChoices.Add(currentAiVal);

        // 使用 PopupField 创建下拉菜单
        var idDropdown = new PopupField<string>("大脑标识符", aiChoices, currentAiVal);
        idDropdown.RegisterValueChangedCallback(evt => Data.BrainIdentifier = (evt.newValue == "无" ? "" : evt.newValue));
        extensionContainer.Add(idDropdown);
        // ------------------------------------------

        var targetField = new Vector2IntField("战略坐标") { value = Data.TargetPos };
        targetField.RegisterValueChangedCallback(evt => Data.TargetPos = evt.newValue);
        extensionContainer.Add(targetField);

        RefreshExpandedState();
    }
    public override void UpdateData() { }
}

// --- [地图节点] ---
public class MapNode : BaseNode
{
    public MapNodeData Data;
    public Port OutputPort; // Vector2Int输出端口
    private MissionGraphView _graphView; // 引用父GraphView
    private PopupField<string> _mapDropdown; // 地图下拉菜单引用

    public MapNode(MapNodeData data, MissionGraphView graphView)
    {
        Data = data;
        _graphView = graphView;
        GUID = data.NodeID;
        title = "🗺️ 地图位置";
        style.width = 250;
        this.titleContainer.style.backgroundColor = new Color(0.5f, 0.4f, 0.2f); // 土黄色

        // 将当前节点添加到GraphView的列表中
        _graphView.MapNodes.Add(this);

        // 输出端口：Vector2Int类型，容量Multi，支持一对多连接
        OutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(Vector2Int));
        OutputPort.portName = "坐标输出";
        outputContainer.Add(OutputPort);

        // --- 地图选择下拉菜单 ---
        var mapIds = GetAllMapIds();
        if (mapIds.Count == 0) mapIds.Add("无地图");

        // 转换函数：空字符串 <-> "无"
        string ToDropdownValue(string mapId) => string.IsNullOrEmpty(mapId) ? "无" : mapId;
        string ToDataValue(string dropdownValue) => dropdownValue == "无" ? "" : dropdownValue;

        // 确定当前显示值：优先使用Data.MapID，其次使用GraphView的CurrentMapId
        string dataMapId = Data.MapID ?? "";
        string graphMapId = _graphView.CurrentMapId ?? "";

        string dropdownValue;
        if (!string.IsNullOrEmpty(dataMapId))
        {
            dropdownValue = ToDropdownValue(dataMapId);
            // 如果Data.MapID有值且与GraphView不同，更新GraphView
            if (dataMapId != graphMapId)
            {
                _graphView.SetCurrentMapId(dataMapId);
            }
        }
        else if (!string.IsNullOrEmpty(graphMapId))
        {
            dropdownValue = ToDropdownValue(graphMapId);
            Data.MapID = graphMapId; // 同步到数据
        }
        else
        {
            dropdownValue = "无"; // 默认值
            Data.MapID = ""; // 明确设置为空
        }

        // 确保选项列表包含当前值
        if (!mapIds.Contains(dropdownValue)) mapIds.Add(dropdownValue);

        _mapDropdown = new PopupField<string>("选择地图", mapIds, dropdownValue);
        _mapDropdown.RegisterValueChangedCallback(evt => {
            // 当用户选择地图时，更新GraphView的当前地图ID
            string newMapId = ToDataValue(evt.newValue);
            Data.MapID = newMapId; // 更新本地数据
            _graphView.SetCurrentMapId(newMapId);
        });
        extensionContainer.Add(_mapDropdown);

        // --- 坐标编辑 ---
        var posField = new Vector2IntField("坐标") { value = Data.SelectedPosition };
        posField.RegisterValueChangedCallback(evt => Data.SelectedPosition = evt.newValue);
        extensionContainer.Add(posField);

        // --- 位置别名（可选） ---
        var nameField = new TextField("位置别名") { value = Data.PositionName };
        nameField.RegisterValueChangedCallback(evt => Data.PositionName = evt.newValue);
        extensionContainer.Add(nameField);

        // --- 选择坐标按钮 ---
        var selectButton = new Button(() =>
        {
            if (string.IsNullOrEmpty(Data.MapID) || Data.MapID == "")
            {
                EditorUtility.DisplayDialog("错误", "请先选择地图", "确定");
                return;
            }

            MapPreviewWindow.Open(Data.MapID, (selectedCoord) =>
            {
                Data.SelectedPosition = selectedCoord;
                posField.value = selectedCoord;

                // 更新位置别名（可选）
                if (string.IsNullOrEmpty(Data.PositionName))
                {
                    Data.PositionName = $"坐标({selectedCoord.x},{selectedCoord.y})";
                    nameField.value = Data.PositionName;
                }

                Debug.Log($"<color=cyan>[地图节点]</color> 坐标已选择: {selectedCoord}");
            });
        })
        {
            text = "📍 选择坐标",
            style = { height = 24, marginTop = 5 }
        };
        extensionContainer.Add(selectButton);

        RefreshExpandedState();
    }

    // 获取所有地图ID列表（静态方法，可供其他类使用）
    public static List<string> GetAllMapIds()
    {
        var mapIds = new List<string> { "无" }; // 添加"无"选项作为默认值
        var allMaps = Resources.LoadAll<TextAsset>("Levels");
        foreach (var mapAsset in allMaps)
        {
            mapIds.Add(mapAsset.name);
        }
        return mapIds;
    }

    // 更新地图ID显示
    public void UpdateMapId(string newMapId)
    {
        if (_mapDropdown != null)
        {
            // 转换函数：空字符串 <-> "无"
            string ToDropdownValue(string mapId) => string.IsNullOrEmpty(mapId) ? "无" : mapId;
            string dropdownValue = ToDropdownValue(newMapId);

            // 检查新值是否在下拉菜单选项中
            var currentIndex = _mapDropdown.index;
            var choices = _mapDropdown.choices;
            if (choices.Contains(dropdownValue))
            {
                _mapDropdown.value = dropdownValue;
            }
            else
            {
                // 如果不在选项中，添加到选项列表
                choices.Add(dropdownValue);
                _mapDropdown.choices = choices;
                _mapDropdown.value = dropdownValue;
            }

            // 更新数据（保存数据值，不是下拉菜单值）
            Data.MapID = newMapId;
        }
    }

    public override void UpdateData() { }
}

// --- [绑定地图节点] ---
public class BoundMapNode : BaseNode
{
    public MapNodeData Data;
    private MissionGraphView _graphView; // 引用父GraphView
    private PopupField<string> _mapDropdown; // 地图下拉菜单引用

    public BoundMapNode(MapNodeData data, MissionGraphView graphView)
    {
        Data = data;
        _graphView = graphView;
        GUID = data.NodeID;
        title = "🔗 绑定地图";
        style.width = 250;
        this.titleContainer.style.backgroundColor = new Color(0.2f, 0.5f, 0.2f); // 绿色

        // 将当前节点添加到GraphView的列表中
        _graphView.BoundMapNodes.Add(this);

        // 没有输入输出端口

        // --- 地图选择下拉菜单 ---
        var mapIds = MapNode.GetAllMapIds();
        if (mapIds.Count == 0) mapIds.Add("无地图");

        // 转换函数：空字符串 <-> "无"
        string ToDropdownValue(string mapId) => string.IsNullOrEmpty(mapId) ? "无" : mapId;
        string ToDataValue(string dropdownValue) => dropdownValue == "无" ? "" : dropdownValue;

        // 确定当前显示值：优先使用Data.MapID，其次使用GraphView的CurrentMapId
        string dataMapId = Data.MapID ?? "";
        string graphMapId = _graphView.CurrentMapId ?? "";

        string dropdownValue;
        if (!string.IsNullOrEmpty(dataMapId))
        {
            dropdownValue = ToDropdownValue(dataMapId);
            // 如果Data.MapID有值且与GraphView不同，更新GraphView
            if (dataMapId != graphMapId)
            {
                _graphView.SetCurrentMapId(dataMapId);
            }
        }
        else if (!string.IsNullOrEmpty(graphMapId))
        {
            dropdownValue = ToDropdownValue(graphMapId);
            Data.MapID = graphMapId; // 同步到数据
        }
        else
        {
            dropdownValue = "无"; // 默认值
            Data.MapID = ""; // 明确设置为空
        }

        // 确保选项列表包含当前值
        if (!mapIds.Contains(dropdownValue)) mapIds.Add(dropdownValue);

        _mapDropdown = new PopupField<string>("绑定地图", mapIds, dropdownValue);
        _mapDropdown.RegisterValueChangedCallback(evt => {
            // 当用户选择地图时，更新GraphView的当前地图ID
            string newMapId = ToDataValue(evt.newValue);
            Data.MapID = newMapId; // 更新本地数据
            _graphView.SetCurrentMapId(newMapId);
        });
        extensionContainer.Add(_mapDropdown);

        RefreshExpandedState();
    }

    // 更新地图ID显示
    public void UpdateMapId(string newMapId)
    {
        if (_mapDropdown != null)
        {
            // 转换函数：空字符串 <-> "无"
            string ToDropdownValue(string mapId) => string.IsNullOrEmpty(mapId) ? "无" : mapId;
            string dropdownValue = ToDropdownValue(newMapId);

            // 检查新值是否在下拉菜单选项中
            var choices = _mapDropdown.choices;
            if (choices.Contains(dropdownValue))
            {
                _mapDropdown.value = dropdownValue;
            }
            else
            {
                // 如果不在选项中，添加到选项列表
                choices.Add(dropdownValue);
                _mapDropdown.choices = choices;
                _mapDropdown.value = dropdownValue;
            }

            // 更新数据（保存数据值，不是下拉菜单值）
            Data.MapID = newMapId;
        }
    }

    public override void UpdateData() { }
}

// =========================================================
// Search Window Provider for Node Creation
// =========================================================
public class MissionNodeSearchWindow : ScriptableObject, ISearchWindowProvider
{
    public MissionGraphView GraphView;
    public EditorWindow EditorWindow;

    public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
    {
        var tree = new List<SearchTreeEntry>();

        // 根节点
        tree.Add(new SearchTreeGroupEntry(new GUIContent("创建节点"), 0));

        // 任务节点
        tree.Add(new SearchTreeGroupEntry(new GUIContent("任务节点"), 1));
        tree.Add(new SearchTreeEntry(new GUIContent("新建任务节点"))
        {
            level = 2,
            userData = new CreateNodeData { NodeType = NodeType.Mission }
        });

        // 奖励节点
        tree.Add(new SearchTreeGroupEntry(new GUIContent("奖励节点"), 1));
        tree.Add(new SearchTreeEntry(new GUIContent("新建奖励节点"))
        {
            level = 2,
            userData = new CreateNodeData { NodeType = NodeType.Reward }
        });

        // 导演节点
        tree.Add(new SearchTreeGroupEntry(new GUIContent("导演节点"), 1));
        tree.Add(new SearchTreeEntry(new GUIContent("新建触发器节点"))
        {
            level = 2,
            userData = new CreateNodeData { NodeType = NodeType.Director }
        });

        // 召唤节点
        tree.Add(new SearchTreeGroupEntry(new GUIContent("召唤节点"), 1));
        tree.Add(new SearchTreeEntry(new GUIContent("新建召唤单位节点"))
        {
            level = 2,
            userData = new CreateNodeData { NodeType = NodeType.Spawn }
        });

        // AI挂载节点
        tree.Add(new SearchTreeGroupEntry(new GUIContent("AI节点"), 1));
        tree.Add(new SearchTreeEntry(new GUIContent("新建AI挂载节点"))
        {
            level = 2,
            userData = new CreateNodeData { NodeType = NodeType.AIBrain }
        });

        // 地图节点
        tree.Add(new SearchTreeGroupEntry(new GUIContent("地图节点"), 1));
        tree.Add(new SearchTreeEntry(new GUIContent("新建地图节点"))
        {
            level = 2,
            userData = new CreateNodeData { NodeType = NodeType.Map }
        });

        return tree;
    }

    public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
    {
        if (entry.userData is not CreateNodeData createData)
            return false;

        // 安全检查
        if (GraphView == null)
        {
            Debug.LogError("MissionNodeSearchWindow: GraphView is null!");
            return false;
        }

        // 获取 EditorWindow 引用（优先使用字段，否则动态查找）
        var editorWindow = EditorWindow ?? GraphView.GetFirstAncestorOfType<EditorWindow>();
        if (editorWindow == null)
        {
            Debug.LogError("MissionNodeSearchWindow: Cannot find parent EditorWindow!");
            return false;
        }

        // 坐标系转换：屏幕坐标 -> 窗口本地坐标 -> 画布坐标
        var screenMousePos = context.screenMousePosition;

        // 安全获取根视觉元素
        if (editorWindow.rootVisualElement == null)
        {
            Debug.LogError("MissionNodeSearchWindow: EditorWindow.rootVisualElement is null!");
            return false;
        }

        // 计算窗口本地坐标
        var relativeMousePos = screenMousePos - editorWindow.position.position;
        VisualElement targetCoordElement = editorWindow.rootVisualElement.parent ?? editorWindow.rootVisualElement;
        var windowMousePos = editorWindow.rootVisualElement.ChangeCoordinatesTo(targetCoordElement, relativeMousePos);

        // 安全获取 GraphView 的内容容器
        if (GraphView.contentViewContainer == null)
        {
            Debug.LogError("MissionNodeSearchWindow: GraphView.contentViewContainer is null!");
            return false;
        }

        var graphMousePos = GraphView.contentViewContainer.WorldToLocal(windowMousePos);

        // 根据节点类型创建对应节点
        switch (createData.NodeType)
        {
            case NodeType.Mission:
                GraphView.CreateMissionNode(new MissionData
                {
                    Title = "新任务",
                    MissionID = System.Guid.NewGuid().ToString(),
                    Goals = new List<MissionGoal>(),
                    IsActive = false,
                    Reward = new MissionReward()
                }, graphMousePos);
                break;

            case NodeType.Reward:
                GraphView.CreateRewardNode(new MissionReward
                {
                    Money = 0,
                    TechPoints = 0,
                    Blueprints = new List<string>()
                }, graphMousePos);
                break;

            case NodeType.Director:
                GraphView.CreateDirectorNode(new ScenarioEventData
                {
                    EventID = System.Guid.NewGuid().ToString(),
                    Trigger = TriggerType.Time,
                    TriggerParam = "0"
                }, graphMousePos);
                break;

            case NodeType.Spawn:
                GraphView.CreateSpawnNode(new SpawnActionData
                {
                    SpawnID = System.Guid.NewGuid().ToString(),
                    Team = 1,
                    SpawnPos = Vector2Int.zero,
                    Units = new List<SpawnUnitEntry>()
                }, graphMousePos);
                break;

            case NodeType.AIBrain:
                GraphView.CreateAIBrainNode(new AIBrainActionData
                {
                    BrainNodeID = System.Guid.NewGuid().ToString(),
                    BrainIdentifier = "",
                    TargetPos = Vector2Int.zero
                }, graphMousePos);
                break;

            case NodeType.Map:
                GraphView.CreateMapNode(new MapNodeData
                {
                    NodeID = System.Guid.NewGuid().ToString(),
                    MapID = "", // 默认空地图
                    SelectedPosition = Vector2Int.zero,
                    PositionName = "新位置",
                    IsBoundNode = false
                }, graphMousePos);
                break;
        }

        return true;
    }

    private enum NodeType { Mission, Reward, Director, Spawn, AIBrain, Map }

    private class CreateNodeData
    {
        public NodeType NodeType;
    }
}