using UnityEditor;
using UnityEngine;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

// 1. 窗口入口
public class MissionGraphWindow : EditorWindow
{
    private MissionGraphView _graphView;
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
        _graphView = new MissionGraphView { name = "Mission Graph" };
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
    }

    // 规定哪些端口能连接
    public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
    {
        return ports.ToList().Where(endPort =>
            endPort.direction != startPort.direction && endPort.node != startPort.node).ToList();
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

    public MissionPackData SerializeToPack()
    {
        var pack = new MissionPackData();
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

        return pack;
    }

    public void PopulateFromPack(MissionPackData pack)
    {
        DeleteElements(graphElements);

        var missionMap = new Dictionary<string, MissionNode>();
        var rewardMap = new Dictionary<string, RewardNode>();
        var directorMap = new Dictionary<string, DirectorNode>();
        var spawnMap = new Dictionary<string, SpawnNode>();
        var aiMap = new Dictionary<string, AIBrainNode>();

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

    public DirectorNode(ScenarioEventData data)
    {
        Data = data; GUID = data.EventID; title = "🎬 触发器";
        style.width = 200; titleContainer.style.backgroundColor = new Color(0.4f, 0.1f, 0.4f);

        InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
        InputPort.portName = "条件"; inputContainer.Add(InputPort);

        OutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(SpawnActionData));
        OutputPort.portName = "触发行为"; outputContainer.Add(OutputPort);

        var triggerField = new EnumField("类型", Data.Trigger);
        triggerField.RegisterValueChangedCallback(evt => Data.Trigger = (TriggerType)evt.newValue);
        extensionContainer.Add(triggerField);

        var paramField = new TextField("参数") { value = Data.TriggerParam };
        paramField.RegisterValueChangedCallback(evt => Data.TriggerParam = evt.newValue);
        extensionContainer.Add(paramField);

        RefreshExpandedState();
    }
    public override void UpdateData() { }
}

// --- [召唤节点] ---
public class SpawnNode : BaseNode
{
    public SpawnActionData Data;
    public Port InputPort;
    public Port OutputPort;

    public SpawnNode(SpawnActionData data)
    {
        Data = data; GUID = data.SpawnID; title = "⚔️ 召唤单位";
        style.width = 250; titleContainer.style.backgroundColor = new Color(0.6f, 0.2f, 0.2f);

        InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(SpawnActionData));
        InputPort.portName = "执行"; inputContainer.Add(InputPort);

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
        for (int i = 0; i < Data.Units.Count; i++)
        {
            var unit = Data.Units[i];
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 2 } };

            var idField = new TextField { value = unit.BlueprintId, style = { flexGrow = 1 } };
            idField.RegisterValueChangedCallback(evt => unit.BlueprintId = evt.newValue);

            var countField = new IntegerField { value = unit.Count, style = { width = 40 } };
            countField.RegisterValueChangedCallback(evt => unit.Count = evt.newValue);

            int index = i;
            var delBtn = new Button(() => { Data.Units.RemoveAt(index); RefreshList(container); }) { text = "×" };

            row.Add(idField); row.Add(countField); row.Add(delBtn);
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

    public AIBrainNode(AIBrainActionData data)
    {
        Data = data; GUID = data.BrainNodeID; title = "🧠 AI 波次控制";
        style.width = 200; titleContainer.style.backgroundColor = new Color(0.2f, 0.5f, 0.6f);

        InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(AIBrainActionData));
        InputPort.portName = "接管单位"; inputContainer.Add(InputPort);

        var idField = new TextField("大脑标识符") { value = Data.BrainIdentifier };
        idField.RegisterValueChangedCallback(evt => Data.BrainIdentifier = evt.newValue);
        extensionContainer.Add(idField);

        var targetField = new Vector2IntField("战略坐标") { value = Data.TargetPos };
        targetField.RegisterValueChangedCallback(evt => Data.TargetPos = evt.newValue);
        extensionContainer.Add(targetField);

        RefreshExpandedState();
    }
    public override void UpdateData() { }
}