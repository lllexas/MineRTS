using System;
using System.Collections.Generic;
using UnityEngine;

// =========================================================
// 独立于 ECS 的、描述网络的数据结构
// =========================================================

[Serializable]
public struct TransportItem
{
    public int ItemId;
    public int ItemType;
    public float Distance;
}

public enum MergeAlignment
{
    Straight,   // 正接（流向相同）：Distance 应该是 1.0 -> 0.0 的物理延续
    Orthogonal  // 侧接（流向垂直）：Distance 应该直接映射到目标格子的中心 (0.5)
}

[Serializable]
public struct TransportConnection
{
    public int TargetLineID;
    public int TargetSegmentIndex;
    public MergeAlignment Alignment; // 核心：不再看位置，看角度
    public Vector3 WorldPos;
}
public class TransportLine
{
    public int ID;
    public float Speed;
    public float Length;
    public List<int> ConveyorCreationIndices;

    // --- 核心修改：定长数组 ---
    public TransportItem[] Items;
    public int ItemCount; // 当前实际物品数量

    public TransportConnection OutputConnection;

    public List<Vector2Int> Directions; // 新增：记录每一节的方向

    // 谁连入了我？(用于拓扑分析，可选)
    public List<int> IncomingLineIDs = new List<int>();

    public TransportLine(int id, float speed)
    {
        ID = id;
        Speed = speed;
        Length = 0;
        ConveyorCreationIndices = new List<int>();
        // 初始化时先设为空，等 WalkAndBuildLine 确定长度后再分配
        Items = null;
        ItemCount = 0;
        OutputConnection = new();
        IncomingLineIDs = new List<int>();
        Directions = new List<Vector2Int>();
    }

    // 根据线路长度初始化数组
    public void InitializeItemsArray()
    {
        // 按照每格最多容纳 2 个物品预留空间（安全系数 2.5）
        int capacity = Mathf.CeilToInt(Length * 2.5f) + 2;
        Items = new TransportItem[capacity];
        ItemCount = 0;
    }
}

// =========================================================
// 核心系统：TransportSystem
// =========================================================
public class TransportSystem : SingletonMono<TransportSystem>
{
    [Header("调试")]
    public bool enableDebugLog = true;
    public const float ITEM_SPACING = 0.8f; 
    public const int MAX_LINE_LENGTH = 32;
    private static int _nextItemId = 1; // 全局计数器
    private bool _isNetworkDirty = false;

    private Dictionary<int, TransportLine> _lines = new Dictionary<int, TransportLine>();
    private Dictionary<Vector2Int, int> _gridToLineID = new Dictionary<Vector2Int, int>();
    private Dictionary<int, int> _creationIndexToEntityIndex = new Dictionary<int, int>();
    private Dictionary<Vector2Int, int> _incomingCounts = new Dictionary<Vector2Int, int>();

    private struct ConveyorFlow
    {
        public Vector2Int WorldPos;
        public Vector2Int InPos;  // 逻辑上的入口格子坐标
        public Vector2Int OutPos; // 逻辑上的出口格子坐标
        public Vector2Int Direction; // 流向
    }
    // 临时的存护结构
    private struct PersistItem
    {
        public int ItemType;
        public int ItemId;
        public int ConveyorCreationIndex; // 靠身份证找
        public float Fraction;            // 靠格子内进度找
        public Vector3 WorldPos;          // 靠物理位置兜底
    }
    /// <summary>
    /// 获取所有当前激活的传输线路（给渲染系统用喵！）
    /// </summary>
    public IEnumerable<TransportLine> GetLines()
    {
        return _lines.Values;
    }

    /// <summary>
    /// 尝试通过创建序号获取当前 ECS 数组的索引
    /// </summary>
    public bool TryGetEntityIndex(int creationIndex, out int entityIndex)
    {
        return _creationIndexToEntityIndex.TryGetValue(creationIndex, out entityIndex);
    }
    private ConveyorFlow GetFlow(int entityIdx, WholeComponent whole)
    {
        ref var core = ref whole.coreComponent[entityIdx];
        ref var move = ref whole.moveComponent[entityIdx];

        // 我们假设传送带蓝图定义：(0,-1)是入，(0,1)是出
        // 旋转后的流向
        Vector2Int forward = IndustrialSystem.Instance.RotateDirection(Vector2Int.up, core.Rotation);

        return new ConveyorFlow
        {
            WorldPos = move.LogicalPosition,
            InPos = move.LogicalPosition - forward,  // 背后那一格
            OutPos = move.LogicalPosition + forward, // 前面那一格
            Direction = forward
        };
    }
    /// <summary>
    /// 标记网络需要重构，不立即执行，等待下一帧或手动刷新
    /// </summary>
    public void MarkDirty()
    {
        _isNetworkDirty = true;
    }

    /// <summary>
    /// 只有在脏了的情况下才重构
    /// </summary>
    public void RebuildIfDirty(WholeComponent whole)
    {
        if (_isNetworkDirty)
        {
            RebuildNetwork(whole);
            _isNetworkDirty = false;
        }
    }
    // =========================================================
    // 1. 网络构建 (更新了数组初始化)
    // =========================================================
    public void RebuildNetwork(WholeComponent whole)
    {
        // --- 阶段 1: 备份所有物品 ---
        List<PersistItem> backups = new List<PersistItem>();
        foreach (var line in _lines.Values)
        {
            for (int i = 0; i < line.ItemCount; i++)
            {
                var item = line.Items[i];
                // 【懒人修复】：用 Clamp 保证即使 item.Distance 等于 line.Length 也不会索引越界
                int tileIdx = Mathf.Clamp(Mathf.FloorToInt(item.Distance), 0, line.ConveyorCreationIndices.Count - 1);

                int cIndex = line.ConveyorCreationIndices[tileIdx];
                backups.Add(new PersistItem
                {
                    ItemId = item.ItemId,
                    ItemType = item.ItemType,
                    ConveyorCreationIndex = cIndex,
                    Fraction = Mathf.Clamp01(item.Distance - tileIdx), // 保证进度在 0~1
                    WorldPos = CalculateItemWorldPos(line, item.Distance, whole) // 备份物理位置
                });
            }
        }

        // --- 阶段 2: 清理旧数据 (完全原样) ---
        _lines.Clear();
        _gridToLineID.Clear();
        _creationIndexToEntityIndex.Clear();
        _incomingCounts.Clear();

        // --- 阶段 3: 重新构建拓扑 (完全原样) ---
        for (int i = 0; i < whole.entityCount; i++)
        {
            if (whole.workComponent[i].WorkType != WorkType.Conveyor || !whole.coreComponent[i].Active) continue;
            _creationIndexToEntityIndex[whole.coreComponent[i].CreationIndex] = i;

            var flow = GetFlow(i, whole);
            int targetId = GridSystem.Instance.GetOccupantId(flow.OutPos);
            if (targetId != -1)
            {
                var h = EntitySystem.Instance.GetHandleFromId(targetId);
                int targetIdx = EntitySystem.Instance.GetIndex(h);
                if (targetIdx != -1 && whole.workComponent[targetIdx].WorkType == WorkType.Conveyor)
                {
                    if (!_incomingCounts.ContainsKey(flow.OutPos)) _incomingCounts[flow.OutPos] = 0;
                    _incomingCounts[flow.OutPos]++;
                }
            }
        }

        HashSet<int> visited = new HashSet<int>();
        int nextLineID = 1;
        foreach (var entityIdx in _creationIndexToEntityIndex.Values)
        {
            if (visited.Contains(whole.coreComponent[entityIdx].CreationIndex)) continue;
            int inDegree = _incomingCounts.ContainsKey(whole.moveComponent[entityIdx].LogicalPosition) ? _incomingCounts[whole.moveComponent[entityIdx].LogicalPosition] : 0;
            if (inDegree != 1) BuildAndWalkSingleLine(entityIdx, nextLineID++, whole, visited);
        }
        foreach (var entityIdx in _creationIndexToEntityIndex.Values)
        {
            if (visited.Contains(whole.coreComponent[entityIdx].CreationIndex)) continue;
            BuildAndWalkSingleLine(entityIdx, nextLineID++, whole, visited);
        }
        LinkLines(whole);

        // --- 阶段 4: 恢复物品 (双重路径：身份证为主，坐标兜底) ---
        foreach (var pItem in backups)
        {
            bool restored = false;

            // 路径 A：身份证精准匹配（只要传送带没拆，哪怕它改了方向、换了Line，我也能找到它！）
            if (_creationIndexToEntityIndex.TryGetValue(pItem.ConveyorCreationIndex, out int entIdx))
            {
                ref var conv = ref whole.conveyorComponent[entIdx];
                if (conv.LineID != -1 && _lines.TryGetValue(conv.LineID, out var newLine))
                {
                    float finalDist = conv.SegmentIndex + pItem.Fraction;
                    ForceAddItemToLine(newLine, pItem.ItemType, finalDist, pItem.ItemId);
                    restored = true;
                }
            }

            // 路径 B：身份证失效（可能格子被拆了重盖），尝试用物理坐标找新传送带
            if (!restored)
            {
                Vector2Int gridPos = GridSystem.Instance.WorldToGrid(pItem.WorldPos);
                if (GetLineIDAtGrid(gridPos, out int newLineID, out float segCenterDist))
                {
                    var newLine = _lines[newLineID];
                    // 放到该格子的中心点 (segCenterDist)
                    ForceAddItemToLine(newLine, pItem.ItemType, segCenterDist + 0.5f, pItem.ItemId);
                    restored = true;
                }
            }

            // 路径 C：如果还是失败，说明身下真没带子了，逻辑留空给未来的掉落系统喵
        }
    }

    // --- 辅助 1：根据 Distance 反算世界坐标 (用于备份) ---
    private Vector3 CalculateItemWorldPos(TransportLine line, float distance, WholeComponent whole)
    {
        int tileIdx = Mathf.FloorToInt(distance);
        // 边界保护
        if (tileIdx < 0) tileIdx = 0;
        if (tileIdx >= line.ConveyorCreationIndices.Count) tileIdx = line.ConveyorCreationIndices.Count - 1;

        // 获取格子中心
        int cIdx = line.ConveyorCreationIndices[tileIdx];
        if (_creationIndexToEntityIndex.TryGetValue(cIdx, out int entityIdx))
        {
            Vector3 center = whole.coreComponent[entityIdx].Position;
            Vector2 dir = line.Directions[tileIdx]; // 这一节的流向

            // 0.5 是中心，<0.5 是前半段，>0.5 是后半段
            float offsetFromCenter = distance - tileIdx - 0.5f;

            return center + (Vector3)(dir * offsetFromCenter);
        }
        return Vector3.zero;
    }

    // --- 辅助 2：强制插入物品 (忽略碰撞，用于恢复) ---
    private void ForceAddItemToLine(TransportLine line, int type, float dist, int id)
    {
        if (line.ItemCount >= line.Items.Length) return; // 数组满了丢弃

        // 依然保持 Distance 降序排列 (假设出口是 0 这种逻辑，或者按你的原逻辑排序)
        // 你的 TryAddItem 是 insertIdx 找 distance > targetDist，说明数组里存的是 [大 ... 小] ? 
        // 还是 [小 ... 大]? 
        // 看原代码：while (Items[insertIdx].Distance > targetDist) insertIdx++; 
        // 说明数组开头(index 0) 是 Distance 最大的 (靠近出口的)。

        int insertIdx = 0;
        while (insertIdx < line.ItemCount && line.Items[insertIdx].Distance > dist)
        {
            insertIdx++;
        }

        // 腾位置
        for (int i = line.ItemCount; i > insertIdx; i--)
        {
            line.Items[i] = line.Items[i - 1];
        }

        line.Items[insertIdx] = new TransportItem
        {
            ItemId = id,
            ItemType = type,
            Distance = dist
        };
        line.ItemCount++;
    }

    private void BuildAndWalkSingleLine(int startEntityIdx, int lineID, WholeComponent whole, HashSet<int> visited)
    {
        var bp = BlueprintRegistry.Get(whole.coreComponent[startEntityIdx].BlueprintName);
        var newLine = new TransportLine(lineID, bp.WorkSpeed);
        int curr = startEntityIdx;

        while (curr != -1)
        {
            ref var core = ref whole.coreComponent[curr];
            ref var move = ref whole.moveComponent[curr];
            ref var conv = ref whole.conveyorComponent[curr];

            visited.Add(core.CreationIndex);
            newLine.ConveyorCreationIndices.Add(core.CreationIndex);
            newLine.Directions.Add(IndustrialSystem.Instance.RotateDirection(Vector2Int.up, core.Rotation)); // 记录朝向
            newLine.Length += 1.0f;
            _gridToLineID[move.LogicalPosition] = newLine.ID;
            conv.LineID = newLine.ID;
            conv.SegmentIndex = (int)newLine.Length - 1;

            // 猫娘注释：达到最大长度就强制切断，LinkLines会把它们重新连起来。
            if (newLine.Length >= MAX_LINE_LENGTH)
            {
                break;
            }

            curr = FindNextStrictConveyor(curr, whole, visited);
        }

        if (newLine.ConveyorCreationIndices.Count > 0)
        {
            newLine.InitializeItemsArray();
            _lines.Add(newLine.ID, newLine);
        }
    }

    // 【新增】替代 FindNextConveyorInPath
    private int FindNextStrictConveyor(int currentIdx, WholeComponent whole, HashSet<int> visited)
    {
        var myFlow = GetFlow(currentIdx, whole);
        int nextOccupant = GridSystem.Instance.GetOccupantId(myFlow.OutPos);
        if (nextOccupant == -1) return -1;

        EntityHandle h = EntitySystem.Instance.GetHandleFromId(nextOccupant);
        int nextIdx = EntitySystem.Instance.GetIndex(h);
        if (nextIdx == -1 || whole.workComponent[nextIdx].WorkType != WorkType.Conveyor) return -1;
        if (visited.Contains(whole.coreComponent[nextIdx].CreationIndex)) return -1;

        var nextFlow = GetFlow(nextIdx, whole);

        // 只有“我的出口”正对“你的入口”
        if (nextFlow.InPos == myFlow.WorldPos)
        {
            // 且“我的方向”等于“你的方向”，才视为同一条直线 Line
            if (nextFlow.Direction == myFlow.Direction)
            {
                // 且没有其他侧向支线连入这个格子（入度为1）
                int nextInDegree = _incomingCounts.ContainsKey(myFlow.OutPos) ? _incomingCounts[myFlow.OutPos] : 0;
                if (nextInDegree <= 1)
                {
                    return nextIdx;
                }
            }
        }

        return -1; // 只要转弯，或者有合流，就必须断开成为新的 Line
    }

    // 判断一个传送带是不是整条线的“起始点”
    private bool IsLineHead(int index, WholeComponent whole)
    {
        var myFlow = GetFlow(index, whole);

        // 看看我背后那一格
        int prevOccupant = GridSystem.Instance.GetOccupantId(myFlow.InPos);
        if (prevOccupant == -1) return true; // 后面没人，我是头

        EntityHandle h = EntitySystem.Instance.GetHandleFromId(prevOccupant);
        int prevIdx = EntitySystem.Instance.GetIndex(h);

        if (prevIdx != -1 && whole.workComponent[prevIdx].WorkType == WorkType.Conveyor)
        {
            var prevFlow = GetFlow(prevIdx, whole);
            // 如果后面那个传送带的出口正对着我，且方向一致，说明我不是头
            if (prevFlow.OutPos == myFlow.WorldPos && prevFlow.Direction == myFlow.Direction)
            {
                return false;
            }
        }
        return true;
    }

    // =========================================================
    // 【新增】核心查询与预测接口
    // =========================================================

    /// <summary>
    /// 查询某个格子是否属于传送带，并返回 LineID 和该格子中心的 Distance
    /// </summary>
    public bool GetLineIDAtGrid(Vector2Int gridPos, out int lineID, out float distanceCenter)
    {
        lineID = -1;
        distanceCenter = 0f;

        if (!_gridToLineID.TryGetValue(gridPos, out int id)) return false;
        if (!_lines.TryGetValue(id, out var line)) return false;

        int segIdx = FindSegmentIndex(line, gridPos);
        if (segIdx == -1) return false;

        lineID = id;
        distanceCenter = segIdx;
        return true;
    }

    public float GetLineSpeed(int lineID)
    {
        if (_lines.TryGetValue(lineID, out var line)) return line.Speed;
        return 0f;
    }

    /// <summary>
    /// 【第一次校验】预测：在 time 秒后，targetDist 位置是否会有空位？
    /// </summary>
    public bool PredictSpace(int lineID, float targetDist, float timeAhead)
    {
        if (!_lines.TryGetValue(lineID, out var line)) return false;

        // 逻辑：计算 timeAhead 秒前，那个可能会挡路的东西现在在哪里？
        // 比如我要去 5.5 的位置，需要 1秒。如果现在 4.5 的位置有个东西，1秒后它正好堵在我脸上。
        float lookbackDist = line.Speed * timeAhead;
        float checkPos = targetDist - lookbackDist;

        // 边界检查：如果回溯距离小于0，说明那个潜在的阻挡者还没进线，或者刚刚进线
        // 这里简化处理：只扫描当前线上的物品
        for (int i = 0; i < line.ItemCount; i++)
        {
            // 如果某物品现在的位置，恰好等于我预测的时间差位置，那么未来就会撞车
            if (Mathf.Abs(line.Items[i].Distance - checkPos) < ITEM_SPACING)
            {
                return false; // 预测冲突
            }
        }
        return true;
    }

    /// <summary>
    /// 【核心】侧向强行插入逻辑
    /// </summary>
    public bool TryAddItemAtDistance(int lineID, int itemType, float targetDist, int existingId = -1)
    {
        if (!_lines.TryGetValue(lineID, out var line)) return false;
        if (line.ItemCount >= line.Items.Length) return false;

        // 1. 碰撞检测：检查目标位置前后是否有物品
        // 我们定义一个 collisionRadius，通常是 ITEM_SPACING / 2
        float radius = ITEM_SPACING * 0.55f; // 稍微大一点，防止穿模

        for (int i = 0; i < line.ItemCount; i++)
        {
            if (Mathf.Abs(line.Items[i].Distance - targetDist) < radius * 2)
            {
                // 发生碰撞，无法插入
                return false;
            }
        }

        // 2. 寻找插入点 (保持数组有序)
        // 假设 line.Items 是按 Distance 降序排列的吗？
        // 原逻辑似乎是 Update 里先更新 Index 0 (最远的)。这通常意味着 Index 0 是 Distance 最大的。
        // 如果你的数组顺序是 [Distance 大 (出口) ... Distance 小 (入口)]：

        int insertIdx = 0;
        // 我们要找到第一个 Distance < targetDist 的位置，把新物品插在它前面
        while (insertIdx < line.ItemCount && line.Items[insertIdx].Distance > targetDist)
        {
            insertIdx++;
        }

        // 3. 数组位移 (腾出 insertIdx 的位置)
        for (int i = line.ItemCount; i > insertIdx; i--)
        {
            line.Items[i] = line.Items[i - 1];
        }

        // 4. 写入
        // 决定 ID：如果有现成的（搬运），就用现成的；否则生成新的
        int finalId = (existingId != -1) ? existingId : _nextItemId++;

        line.Items[insertIdx] = new TransportItem
        {
            ItemId = finalId,
            ItemType = itemType,
            Distance = targetDist
        };
        line.ItemCount++;
        return true;
    }


    // =========================================================
    // 2. 添加物品 (数组位移插入)
    // =========================================================
    public bool TryAddItem(Vector2Int gridPos, int itemType)
    {
        if (!_gridToLineID.TryGetValue(gridPos, out int lineID)) return false;

        var line = _lines[lineID];
        if (line.ItemCount >= line.Items.Length) return false; // 数组满了

        int segmentIndex = FindSegmentIndex(line, gridPos);
        if (segmentIndex == -1) return false;

        float targetDist = segmentIndex;

        // 检查间距
        for (int i = 0; i < line.ItemCount; i++)
        {
            if (Mathf.Abs(line.Items[i].Distance - targetDist) < ITEM_SPACING) return false;
        }

        // 寻找插入位置 (保持 Distance 从大到小或从小到大，这里假设 0 是出口方向)
        int insertIdx = 0;
        while (insertIdx < line.ItemCount && line.Items[insertIdx].Distance > targetDist)
        {
            insertIdx++;
        }

        // 数组后移
        for (int i = line.ItemCount; i > insertIdx; i--)
        {
            line.Items[i] = line.Items[i - 1];
        }

        line.Items[insertIdx] = new TransportItem { ItemId = _nextItemId++, ItemType = itemType, Distance = targetDist };
        line.ItemCount++;
        return true;
    }

    // =========================================================
    // 3. 取走物品 (数组前移删除)
    // =========================================================
    public (bool success, int itemType, float actualDist) TryTakeItem(Vector2Int gridPos)
    {
        if (!_gridToLineID.TryGetValue(gridPos, out int lineID)) return (false, 0, 0);

        var line = _lines[lineID];
        int segIdx = FindSegmentIndex(line, gridPos);
        if (segIdx == -1) return (false, 0, 0);

        for (int i = 0; i < line.ItemCount; i++)
        {
            // 只要物品在这个格子范围内 (segIdx ~ segIdx + 1)
            if (line.Items[i].Distance >= segIdx && line.Items[i].Distance <= segIdx + 1.0f)
            {
                int type = line.Items[i].ItemType;
                float actualDist = line.Items[i].Distance; // 记录它死掉时的位置喵！

                // 数组前移删除
                for (int j = i; j < line.ItemCount - 1; j++)
                {
                    line.Items[j] = line.Items[j + 1];
                }
                line.ItemCount--;
                return (true, type, actualDist);
            }
        }
        return (false, 0, 0);
    }

    // TransportSystem.cs
    public (bool success, int itemType, int itemId) TryTakeItemAtCenter(Vector2Int gridPos)
    {
        if (!_gridToLineID.TryGetValue(gridPos, out int lineID)) return (false, 0, 0);
        var line = _lines[lineID];

        // 1. 找到这个格子在带子上的序号
        int segIdx = FindSegmentIndex(line, gridPos);
        if (segIdx == -1) return (false, 0, 0);

        // 2. 这里的中心点 Distance 就是 segIdx + 0.5
        float targetCenterDist = segIdx + 0.5f;

        for (int i = 0; i < line.ItemCount; i++)
        {
            // 【核心】：只有当物品的距离 >= 中心点，且没跑出这个格子时，才允许抓取
            // 使用一个很小的容差 (例如 0.2)，确保在高速移动下也能捕捉到
            if (line.Items[i].Distance >= targetCenterDist && line.Items[i].Distance < segIdx + 1.0f)
            {
                int type = line.Items[i].ItemType;
                int id = line.Items[i].ItemId;

                // 逻辑移除
                for (int j = i; j < line.ItemCount - 1; j++)
                {
                    line.Items[j] = line.Items[j + 1];
                }
                line.ItemCount--;

                return (true, type, id);
            }
        }
        return (false, 0, 0);
    }

    // =========================================================
    // 4. 更新逻辑 (真正利用 ref 的地方喵！)
    // =========================================================
    public void UpdateNetwork(float deltaTime)
    {
        foreach (var line in _lines.Values)
        {
            // 从离出口最近的物品(0)开始更新
            for (int i = 0; i < line.ItemCount; i++)
            {
                // 【黑魔法】直接引用数组元素，无拷贝修改
                ref var current = ref line.Items[i];

                float potentialDist = current.Distance + line.Speed * deltaTime;

                // 堵塞判断：参考前一个物品
                if (i > 0)
                {
                    float safeDist = line.Items[i - 1].Distance - ITEM_SPACING;
                    if (potentialDist > safeDist) potentialDist = safeDist;
                }

                if (potentialDist > line.Length) potentialDist = line.Length;

                current.Distance = potentialDist;
            }

            // 线路交接逻辑 (处理 line.Items[0])
            HandleLineTransfer(line);
        }
    }

    private void HandleLineTransfer(TransportLine sourceLine)
    {
        if (sourceLine.ItemCount == 0) return;

        ref var headItem = ref sourceLine.Items[0];
        if (headItem.Distance < sourceLine.Length) return;

        var conn = sourceLine.OutputConnection;
        if (conn.TargetLineID == 0 || !_lines.TryGetValue(conn.TargetLineID, out var targetLine))
        {
            headItem.Distance = sourceLine.Length; // 阻塞
            return;
        }

        float overflow = headItem.Distance - sourceLine.Length;
        float targetDist;

        // 根据对齐方式决定目标距离
        if (conn.Alignment == MergeAlignment.Straight)
        {
            // 【正接】：物理位置连续。
            // 如果接入的是 Target 的开头，则从 0 + overflow 开始
            // 如果接入的是 Target 的中间（虽然当前Rebuild会切断，但逻辑应完备），则从 Index + overflow 开始
            targetDist = conn.TargetSegmentIndex + overflow;
        }
        else
        {
            // 【侧接】：物理位置不连续（90度转弯）。
            // 物品必须“瞬间”到达目标格子的中心轴线上，否则会从侧面穿模。
            // 所以目标位置是该格子的中心。
            targetDist = conn.TargetSegmentIndex + 0.5f;
        }

        // 执行尝试添加（复用 TryAddItemAtDistance 进行空间校验）
        if (TryAddItemAtDistance(targetLine.ID, headItem.ItemType, targetDist, headItem.ItemId))
        {
            // 成功转移，移除源物品
            for (int j = 0; j < sourceLine.ItemCount - 1; j++)
            {
                sourceLine.Items[j] = sourceLine.Items[j + 1];
            }
            sourceLine.ItemCount--;
        }
        else
        {
            // 目标位置被堵塞，钳制在出口边缘
            headItem.Distance = sourceLine.Length;
        }
    }

    // --- 辅助方法保持逻辑不变，只需适配数组检索 ---

    private void WalkAndBuildLine(int startIdx, TransportLine line, WholeComponent whole, HashSet<int> visited)
    {
        int curr = startIdx;
        while (curr != -1)
        {
            ref var core = ref whole.coreComponent[curr];
            ref var move = ref whole.moveComponent[curr];
            ref var conv = ref whole.conveyorComponent[curr];

            visited.Add(core.CreationIndex);
            line.ConveyorCreationIndices.Add(core.CreationIndex);
            line.Length += 1.0f; // 假设每格长度为 1
            _gridToLineID[move.LogicalPosition] = line.ID;

            conv.LineID = line.ID;
            conv.SegmentIndex = (int)line.Length - 1;

            curr = FindNextConveyorInPath(curr, whole, visited);
        }
    }

    private int FindNextConveyorInPath(int currentIndex, WholeComponent whole, HashSet<int> visited)
    {
        // 1. 获取当前传送带的流向信息
        var myFlow = GetFlow(currentIndex, whole);

        // 2. 检查我的“正前方”出口位置
        int forwardOccupantIdx = GetConveyorIndexAt(myFlow.OutPos, whole);
        if (forwardOccupantIdx != -1 && !visited.Contains(whole.coreComponent[forwardOccupantIdx].CreationIndex))
        {
            var forwardFlow = GetFlow(forwardOccupantIdx, whole);

            // 【双向握手 - 规则A】: "我指向你，你也面向我" (完美直线)
            // 如果前方的传送带的入口，正好是我现在的位置，说明我们完美对接。
            if (forwardFlow.InPos == myFlow.WorldPos)
            {
                // 检查入度，防止合流点并线
                int nextInDegree = _incomingCounts.ContainsKey(myFlow.OutPos) ? _incomingCounts[myFlow.OutPos] : 0;
                if (nextInDegree <= 1)
                {
                    return forwardOccupantIdx;
                }
            }
        }

        // 3. 【智能拐角兼容 - 规则B】: "即使你没面向我，只要你挡在我前面，而且你能继续走下去，我就把东西硬塞给你！"
        // 这个逻辑用来处理"拐角归属前半段"的遗留问题。
        // 我们需要扫描当前传送带出口周围的三个方向（左前、正前、右前）
        Vector2Int[] potentialTurnDirs = GetPotentialTurnDirections(myFlow.Direction);
        foreach (var dir in potentialTurnDirs)
        {
            Vector2Int checkPos = myFlow.WorldPos + dir;
            int neighborIdx = GetConveyorIndexAt(checkPos, whole);

            if (neighborIdx != -1 && !visited.Contains(whole.coreComponent[neighborIdx].CreationIndex))
            {
                var neighborFlow = GetFlow(neighborIdx, whole);

                // 关键兼容逻辑：如果这个邻居的入口是我现在的位置，即使它的朝向不对，也认为是“强行拐弯”
                if (neighborFlow.InPos == myFlow.WorldPos)
                {
                    int neighborInDegree = _incomingCounts.ContainsKey(checkPos) ? _incomingCounts[checkPos] : 0;
                    if (neighborInDegree <= 1)
                    {
                        // 在这里，我们可以选择“纠正”当前格子的旋转，但这会修改源数据，暂时不做。
                        // RebuildNetwork只负责识别拓扑。
                        // Debug.Log($"<color=orange>智能拐角修正: {myFlow.WorldPos} -> {checkPos}</color>");
                        return neighborIdx;
                    }
                }
            }
        }

        return -1; // 找不到任何合法的下一个节点
    }

    // 【新增】辅助方法：根据坐标获取传送带在ECS中的索引
    private int GetConveyorIndexAt(Vector2Int pos, WholeComponent whole)
    {
        int occupantId = GridSystem.Instance.GetOccupantId(pos);
        if (occupantId == -1) return -1;

        EntityHandle handle = EntitySystem.Instance.GetHandleFromId(occupantId);
        int index = EntitySystem.Instance.GetIndex(handle);

        if (index != -1 && whole.workComponent[index].WorkType == WorkType.Conveyor)
        {
            return index;
        }
        return -1;
    }

    // 【新增】辅助方法：获取可能的转弯方向
    private Vector2Int[] GetPotentialTurnDirections(Vector2Int forward)
    {
        // 返回 前、左前、右前 三个方向
        if (forward.x != 0) // 横向
        {
            return new Vector2Int[] { forward, new Vector2Int(forward.x, 1), new Vector2Int(forward.x, -1) };
        }
        else // 纵向
        {
            return new Vector2Int[] { forward, new Vector2Int(1, forward.y), new Vector2Int(-1, forward.y) };
        }
    }

    // =========================================================
    // 补完：建立线路之间的连接 (Next/Prev Line)
    // =========================================================
    private void LinkLines(WholeComponent whole)
    {
        foreach (var line in _lines.Values)
        {
            // 1. 获取本线路最后一节的信息
            int lastSegIdx = line.ConveyorCreationIndices.Count - 1;
            int lastCIndex = line.ConveyorCreationIndices[lastSegIdx];
            if (!_creationIndexToEntityIndex.TryGetValue(lastCIndex, out int entityIdx)) continue;

            var myFlow = GetFlow(entityIdx, whole);
            Vector2Int myExitDir = myFlow.Direction; // 最后一格的流向

            // 2. 查找出口格子的归属
            if (!_gridToLineID.TryGetValue(myFlow.OutPos, out int targetLineID))
            {
                line.OutputConnection = new TransportConnection { Alignment = MergeAlignment.Orthogonal }; // 默认
                continue;
            }

            if (targetLineID == line.ID) continue; // 忽略闭环（由Update处理）

            var targetLine = _lines[targetLineID];
            int targetSegIdx = FindSegmentIndex(targetLine, myFlow.OutPos);
            if (targetSegIdx == -1) continue;

            // 3. 【核心重构】判定对齐方式
            // 获取目标格子的流向（从 targetLine.Directions 中取）
            Vector2Int targetDir = targetLine.Directions[targetSegIdx];

            MergeAlignment alignment = (myExitDir == targetDir)
                ? MergeAlignment.Straight
                : MergeAlignment.Orthogonal;

            // 4. 保存连接
            line.OutputConnection = new TransportConnection
            {
                TargetLineID = targetLineID,
                TargetSegmentIndex = targetSegIdx,
                Alignment = alignment,
                WorldPos = GridSystem.Instance.GridToWorld(myFlow.OutPos)
            };

            if (!targetLine.IncomingLineIDs.Contains(line.ID))
                targetLine.IncomingLineIDs.Add(line.ID);
        }
    }


    // =========================================================
    // 补完：根据逻辑坐标找到该格子在线路中的序号 (0 ~ Length-1)
    // =========================================================
    private int FindSegmentIndex(TransportLine line, Vector2Int gridPos)
    {
        // 遍历线路包含的所有格子
        for (int i = 0; i < line.ConveyorCreationIndices.Count; i++)
        {
            int cIndex = line.ConveyorCreationIndices[i];

            // 快速通过 CreationIndex 映射回 ECS 的实体数据
            if (_creationIndexToEntityIndex.TryGetValue(cIndex, out int entityIdx))
            {
                // 检查这个实体的逻辑格子坐标是否匹配
                // 注意：这里用 wholeComponent.moveComponent，因为那是实时位置数据喵
                if (EntitySystem.Instance.wholeComponent.moveComponent[entityIdx].LogicalPosition == gridPos)
                {
                    return i; // 找到了，这就是第 i 段
                }
            }
        }

        // 如果该坐标不在这一条线上，返回 -1
        return -1;
    }

    public string GetNetworkDebugInfo()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"Total Lines: {_lines.Count}");

        int totalItems = 0;
        foreach (var line in _lines.Values)
        {
            totalItems += line.ItemCount;
            sb.AppendLine($" - Line {line.ID}: Len={line.Length}, Items={line.ItemCount}, NextLines={line.IncomingLineIDs.Count}");
        }
        sb.AppendLine($"Total Items on Belts: {totalItems}");
        return sb.ToString();
    }
}
