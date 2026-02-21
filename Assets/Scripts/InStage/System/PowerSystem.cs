using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class PowerSystem : SingletonMono<PowerSystem>
{
    // 当前地图上所有的电网，Key 是 NetID
    private Dictionary<int, PowerNet> _nets = new Dictionary<int, PowerNet>();
    private int _nextNetID = 1;
    private int _recycledNetID = -1;

    // 每秒更新次数（电力逻辑不需要每帧跑，可以降低频率优化性能喵）
    private float _updateTimer = 0;
    private const float UPDATE_INTERVAL = 0.1f;

    public void UpdatePower(WholeComponent whole, float deltaTime)
    {
        // 1. 能量平衡计算 (定频更新)
        _updateTimer += deltaTime;
        if (_updateTimer >= UPDATE_INTERVAL)
        {
            TickPowerLogic(whole, _updateTimer);
            _updateTimer = 0;
        }
    }

    // =========================================================
    // 核心逻辑 A：拓扑构建 (当新建筑造好时调用)
    // =========================================================

    /// <summary>
    /// 当玩家放置了一个电力建筑时调用，负责并网逻辑
    /// </summary>
    public void OnPowerEntityBuilt(EntityHandle handle, WholeComponent whole)
    {
        int index = EntitySystem.Instance.GetIndex(handle);
        if (index == -1) return;

        ref var power = ref whole.powerComponent[index];
        if (!power.IsNode) return;

        ref var move = ref whole.moveComponent[index];
        Vector2Int pos = move.LogicalPosition;

        // 1. 寻找连接范围内的所有邻居节点
        List<int> neighborNetIDs = FindNeighborNetIDs(pos, power.ConnRange, whole);

        if (neighborNetIDs.Count == 0)
        {
            // 情况 A: 孤儿节点，创建一个新电网
            CreateNewNet(handle, whole);
        }
        else if (neighborNetIDs.Count == 1)
        {
            // 情况 B: 只触碰到一个电网，直接加入
            JoinNet(handle, neighborNetIDs[0], whole);
        }
        else
        {
            // 情况 C: 桥接了多个电网，需要执行大合并！
            int masterNetID = neighborNetIDs[0];
            for (int i = 1; i < neighborNetIDs.Count; i++)
            {
                MergeNets(masterNetID, neighborNetIDs[i], whole);
            }
            JoinNet(handle, masterNetID, whole);
        }

        // 2. 拓扑改变后，重新扫描该节点覆盖范围内的消费者
        RefreshConsumersForNode(handle, whole);
    }

    public void OnPowerEntityRemoved(EntityHandle handle, WholeComponent whole)
    {
        int index = EntitySystem.Instance.GetIndex(handle);
        if (index == -1) return;

        ref var p = ref whole.powerComponent[index];
        int netID = p.NetID;
        if (netID == -1) return;

        if (_nets.TryGetValue(netID, out var net))
        {
            // 1. 消费者移除：无视，直接踢出
            if (!p.IsNode)
            {
                net.Consumers.Remove(handle);
                p.NetID = -1;
                return;
            }

            // 2. 节点移除：先从列表中删除自己
            net.Nodes.Remove(handle);
            p.NetID = -1; // 自己先断开

            if (net.Nodes.Count == 0)
            {
                _nets.Remove(netID);
                return;
            }

            // --- 【核心优化】智能判断是否需要重构 ---
            if (CheckIfNetworkSplit(handle, netID, whole, net.Nodes))
            {
                // 只有真的断开了，才进行暴力重构
                // Debug.Log($"<color=red>电网断裂！正在重构 Net {netID}...</color>");
                RebuildNet(netID, whole);
            }
            else
            {
                // 没断开？那就啥也不用做！省大发了喵！
                // Debug.Log($"<color=green>安全移除。电网 Net {netID} 依然稳固。</color>");
            }
        }
    }

    /// <summary>
    /// 【核心算法】检查移除 removedNode 后，它的邻居们是否还能互通
    /// </summary>
    private bool CheckIfNetworkSplit(EntityHandle removedNode, int netID, WholeComponent whole, List<EntityHandle> remainingNodes)
    {
        // 1. 找到被移除节点的所有“同网邻居”
        // 注意：因为 removedNode 已经被设置为 -1 了，我们得利用它的物理位置去回溯
        int rmIdx = EntitySystem.Instance.GetIndex(removedNode);
        Vector2Int pos = whole.moveComponent[rmIdx].LogicalPosition;
        float range = whole.powerComponent[rmIdx].ConnRange;

        // 这里的 FindNeighbors 需要返回 EntityHandle，我们下面写一个重载
        List<EntityHandle> neighbors = FindNeighborNodesInNet(pos, range, netID, whole);

        // 优化 A：如果是末梢节点（0或1个邻居），绝对不会分裂
        if (neighbors.Count <= 1) return false;

        // 2. BFS 搜索连通性
        // 目标：从 neighbors[0] 出发，看能不能遍历到 neighbors[1...n] 中的每一个
        // 如果都能找到，说明它们通过其他路径连着呢

        HashSet<EntityHandle> visited = new HashSet<EntityHandle>();
        Queue<EntityHandle> queue = new Queue<EntityHandle>();

        EntityHandle startNode = neighbors[0];
        queue.Enqueue(startNode);
        visited.Add(startNode);

        int neighborsFoundCount = 1; // 已经找到了 1 个 (起点)

        while (queue.Count > 0)
        {
            EntityHandle current = queue.Dequeue();
            int currIdx = EntitySystem.Instance.GetIndex(current);
            if (currIdx == -1) continue;

            // 获取当前探针的所有邻居
            Vector2Int cPos = whole.moveComponent[currIdx].LogicalPosition;
            float cRange = whole.powerComponent[currIdx].ConnRange;

            // 这一步虽然也有开销，但比全网重构小得多
            var nextNodes = FindNeighborNodesInNet(cPos, cRange, netID, whole);

            foreach (var next in nextNodes)
            {
                if (!visited.Contains(next))
                {
                    visited.Add(next);
                    queue.Enqueue(next);

                    // 关键检查：如果我们碰到了被移除节点的原始邻居之一
                    if (neighbors.Contains(next))
                    {
                        neighborsFoundCount++;
                    }
                }
            }

            // 提前退出：如果所有原始邻居都找齐了，说明没分裂！
            if (neighborsFoundCount == neighbors.Count) return false;
        }

        // 遍历完了还没找齐？说明断了！
        return true;
    }

    // --- 辅助方法：寻找指定位置、指定范围内、属于特定 NetID 的节点 ---
    private List<EntityHandle> FindNeighborNodesInNet(Vector2Int center, float range, int targetNetID, WholeComponent whole)
    {
        List<EntityHandle> result = new List<EntityHandle>();
        int r = Mathf.CeilToInt(range);

        for (int x = -r; x <= r; x++)
        {
            for (int y = -r; y <= r; y++)
            {
                Vector2Int checkPos = center + new Vector2Int(x, y);
                int occId = GridSystem.Instance.GetOccupantId(checkPos);
                if (occId != -1)
                {
                    EntityHandle h = EntitySystem.Instance.GetHandleFromId(occId);
                    int idx = EntitySystem.Instance.GetIndex(h);
                    if (idx != -1)
                    {
                        ref var p = ref whole.powerComponent[idx];
                        // 必须是节点，且必须在同一个网里
                        if (p.IsNode && p.NetID == targetNetID)
                        {
                            result.Add(h);
                        }
                    }
                }
            }
        }
        return result;
    }

    private void RebuildNet(int netID, WholeComponent whole)
    {
        if (!_nets.TryGetValue(netID, out var net)) return;

        // 我们只处理这个 NetID 覆盖的“这一小片区”
        List<EntityHandle> affectedNodes = new List<EntityHandle>(net.Nodes);
        List<EntityHandle> affectedConsumers = new List<EntityHandle>(net.Consumers);

        // 【新增】在销毁之前，标记这个 ID 可以被回收利用
        // 这样，下面循环中第一个重建的节点调用 CreateNewNet 时，就会继承这个 ID
        _recycledNetID = netID;
        // 销毁旧网标识
        _nets.Remove(netID);

        // 暂时重置所有受影响单位的 NetID
        foreach (var h in affectedNodes)
        {
            int idx = EntitySystem.Instance.GetIndex(h);
            if (idx != -1) whole.powerComponent[idx].NetID = -1;
        }
        foreach (var h in affectedConsumers)
        {
            int idx = EntitySystem.Instance.GetIndex(h);
            if (idx != -1) whole.powerComponent[idx].NetID = -1;
        }

        // 重新并网：这些节点会尝试寻找最近的邻居（可能是别的网，也可能互相重新成网）
        foreach (var h in affectedNodes)
        {
            OnPowerEntityBuilt(h, whole);
        }
        // 【兜底】如果重建完了这个 ID 居然没被用掉（比如所有节点都被删了），要重置一下
        _recycledNetID = -1;
    }

    // =========================================================
    // 核心逻辑 B：能量分配算法
    // =========================================================

    private void TickPowerLogic(WholeComponent whole, float dt)
    {
        // 1. 重置统计
        foreach (var net in _nets.Values) net.ResetStats();

        // 2. 累加总产出、总需求和蓄电池容量
        for (int i = 0; i < whole.entityCount; i++)
        {
            ref var p = ref whole.powerComponent[i];
            if (p.NetID == -1) continue;

            if (_nets.TryGetValue(p.NetID, out var net))
            {
                net.TotalProduction += p.Production;
                net.TotalDemand += p.Demand;
                net.TotalStorage += p.Capacity;
                // 蓄电池逻辑后续单独处理，因为我们需要汇总所有的 StoredEnergy
            }
        }

        // 3. 计算每个电网的满足率
        foreach (var net in _nets.Values)
        {
            float supply = net.TotalProduction;
            float demand = net.TotalDemand;

            // --- 逻辑调整：先执行充放电，最后统一计算储能快照 ---
            if (demand <= 0)
            {
                net.Satisfaction = 1.0f;
                ChargeBatteries(net, (supply - demand) * dt, whole);
            }
            else if (supply >= demand)
            {
                net.Satisfaction = 1.0f;
                ChargeBatteries(net, (supply - demand) * dt, whole);
            }
            else
            {
                float deficit = (demand - supply) * dt;
                float discharged = DischargeBatteries(net, deficit, whole);
                net.Satisfaction = (supply * dt + discharged) / (demand * dt);
                net.Satisfaction = Mathf.Clamp01(net.Satisfaction);
            }

            // 【核心修正】真正地更新快照：遍历网内节点，汇总最新的 StoredEnergy
            float finalStored = 0;
            foreach (var h in net.Nodes)
            {
                int idx = EntitySystem.Instance.GetIndex(h);
                if (idx != -1) finalStored += whole.powerComponent[idx].StoredEnergy;
            }
            net.CurrentStorage = finalStored;
        }

        // 4. 将结果反馈给 PowerComponent
        for (int i = 0; i < whole.entityCount; i++)
        {
            ref var work = ref whole.workComponent[i];
            // 不需要电的跳过...

            ref var p = ref whole.powerComponent[i];
            if (p.NetID != -1 && _nets.TryGetValue(p.NetID, out var net))
            {
                // 【核心修改】把满足率回写给实体
                p.CurrentSatisfaction = net.Satisfaction;

                // 只有真的有电进来，才算 IsPowered (用于UI显示灯亮不亮)
                work.IsPowered = p.CurrentSatisfaction > 0.01f;
            }
            else
            {
                p.CurrentSatisfaction = 0f;
                work.IsPowered = false;
            }
        }

    }
    /// <summary>
    /// 【万无一失版】当电力节点发生移动时调用
    /// </summary>
    public void OnPowerEntityMoved(EntityHandle handle, WholeComponent whole, Vector2Int oldPos, Vector2Int newPos)
    {
        int index = EntitySystem.Instance.GetIndex(handle);
        if (index == -1) return;

        ref var power = ref whole.powerComponent[index];

        // 如果我本来就没网，或者我压根不是节点，直接忽略
        if (power.NetID == -1 || !power.IsNode) return;

        int myNetID = power.NetID;

        // --- 步骤 1: 检查是否与“旧邻居”断连 (防止分裂) ---
        // 我们模拟一下：如果我站在 NewPos，还能勾得着 OldPos 那个圈子里的兄弟们吗？
        bool linkBroken = CheckIfLinkBroken(handle, oldPos, newPos, myNetID, whole);

        // --- 步骤 2: 检查是否接触到了“异网” (防止未合并) ---
        // 我们看看 NewPos 周围有没有别家电网的人
        bool foreignNetFound = CheckIfForeignNetNearby(newPos, power.ConnRange, myNetID, whole);

        // --- 决策时刻 ---
        if (linkBroken || foreignNetFound)
        {
            // 情况 A: 拓扑结构变了 (断开了，或者要合并了)
            // 必须执行暴力重构！
            // Debug.Log($"[Power] 拓扑改变 (断连:{linkBroken}, 异网:{foreignNetFound}) -> 重构网络");
            ForceRebuildFullProcess(handle, whole, oldPos, newPos);
        }
        else
        {
            // 情况 B: 拓扑非常稳定 (我在自家局域网里溜达)
            // 此时啥也不用做！NetID 保持不变！
            // 只需要刷新一下消费者覆盖范围即可 (把新覆盖到的机器拉进网)
            RefreshConsumersForNode(handle, whole);

            // *注：离开旧位置导致的消费者掉线，会在下一次 PowerSystem 全局清理时修正，
            // 或者你可以写一个 DisconnectConsumersAtPos(oldPos) 的辅助函数。
            // 但为了 ID 稳定，这里保留 NetID 是最关键的。
            // Debug.Log($"[Power] 平滑漫游 -> ID {myNetID} 保持不变");
        }
    }
    /// <summary>
    /// 检查从 oldPos 移动到 newPos 是否会导致与原有的邻居断开连接
    /// </summary>
    private bool CheckIfLinkBroken(EntityHandle me, Vector2Int oldPos, Vector2Int newPos, int myNetID, WholeComponent whole)
    {
        int myIdx = EntitySystem.Instance.GetIndex(me);
        float range = whole.powerComponent[myIdx].ConnRange;
        float rangeSq = range * range;

        // 1. 扫描旧位置周围，找出我的“老铁们” (同网节点)
        // 注意：因为 GridSystem 已经更新，oldPos 上现在是空的(或者被别人占了)，但不影响我们扫周围
        int r = Mathf.CeilToInt(range);

        for (int x = -r; x <= r; x++)
        {
            for (int y = -r; y <= r; y++)
            {
                Vector2Int neighborPos = oldPos + new Vector2Int(x, y);

                // 排除自己：虽然 GridSystem 里我已经走了，但为了严谨逻辑，还是判断一下
                if (neighborPos == newPos) continue;

                int occId = GridSystem.Instance.GetOccupantId(neighborPos);
                if (occId != -1 && occId != me.Id) // 排除自己
                {
                    EntityHandle h = EntitySystem.Instance.GetHandleFromId(occId);
                    int idx = EntitySystem.Instance.GetIndex(h);
                    if (idx != -1)
                    {
                        ref var p = ref whole.powerComponent[idx];
                        // 这是一个属于我方电网的节点
                        if (p.IsNode && p.NetID == myNetID)
                        {
                            // 2. 关键判断：我跑到 newPos 后，还能连上它吗？
                            float distSq = (newPos - neighborPos).sqrMagnitude;
                            if (distSq > rangeSq)
                            {
                                // 完蛋，距离太远，断连了！
                                return true;
                            }
                        }
                    }
                }
            }
        }

        // 扫了一圈，发现所有老朋友都还在射程内
        return false;
    }
    /// <summary>
    /// 检查 newPos 周围是否有“别人的”电网
    /// </summary>
    private bool CheckIfForeignNetNearby(Vector2Int newPos, float range, int myNetID, WholeComponent whole)
    {
        int r = Mathf.CeilToInt(range);

        for (int x = -r; x <= r; x++)
        {
            for (int y = -r; y <= r; y++)
            {
                Vector2Int checkPos = newPos + new Vector2Int(x, y);
                int occId = GridSystem.Instance.GetOccupantId(checkPos);
                if (occId != -1)
                {
                    EntityHandle h = EntitySystem.Instance.GetHandleFromId(occId);
                    int idx = EntitySystem.Instance.GetIndex(h);
                    if (idx != -1)
                    {
                        ref var p = ref whole.powerComponent[idx];
                        // 如果是节点，且有网，且网号跟我不一样
                        if (p.IsNode && p.NetID != -1 && p.NetID != myNetID)
                        {
                            // 发现异教徒！需要合并！
                            return true;
                        }
                    }
                }
            }
        }
        return false;
    }
    /// <summary>
    /// 暴力重构流程 (旧逻辑封装)
    /// </summary>
    private void ForceRebuildFullProcess(EntityHandle handle, WholeComponent whole, Vector2Int oldPos, Vector2Int newPos)
    {
        int index = EntitySystem.Instance.GetIndex(handle);
        ref var move = ref whole.moveComponent[index];

        // 1. 骗系统：我在旧位置 -> 执行移除逻辑
        move.LogicalPosition = oldPos;
        OnPowerEntityRemoved(handle, whole);

        // 2. 恢复真相：我在新位置 -> 执行加入逻辑
        move.LogicalPosition = newPos;
        OnPowerEntityBuilt(handle, whole);
    }
    public PowerNet GetNetDetails(int netID)
    {
        if (netID != -1 && _nets.TryGetValue(netID, out var net))
        {
            return net;
        }
        return null;
    }

    // =========================================================
    // 内部工具方法
    // =========================================================

    private List<int> FindNeighborNetIDs(Vector2Int center, float range, WholeComponent whole)
    {
        List<int> found = new List<int>();
        int r = Mathf.CeilToInt(range);

        for (int x = -r; x <= r; x++)
        {
            for (int y = -r; y <= r; y++)
            {
                Vector2Int checkPos = center + new Vector2Int(x, y);
                int occId = GridSystem.Instance.GetOccupantId(checkPos);
                if (occId != -1)
                {
                    EntityHandle h = EntitySystem.Instance.GetHandleFromId(occId);
                    int idx = EntitySystem.Instance.GetIndex(h);
                    if (idx != -1)
                    {
                        ref var otherP = ref whole.powerComponent[idx];
                        // 如果对方是电力节点，且已经有网了
                        if (otherP.IsNode && otherP.NetID != -1)
                        {
                            if (!found.Contains(otherP.NetID)) found.Add(otherP.NetID);
                        }
                    }
                }
            }
        }
        return found;
    }

    private void CreateNewNet(EntityHandle handle, WholeComponent whole)
    {
        int id;
        if (_recycledNetID != -1)
        {
            id = _recycledNetID;
            _recycledNetID = -1; // 用完就置空，防止别的网也抢着用
                                 // Debug.Log($"<color=cyan>回收利用旧 NetID: {id}</color>");
        }
        else
        {
            id = _nextNetID++;
        }
        PowerNet net = new PowerNet { NetID = id };
        net.Nodes.Add(handle);
        _nets.Add(id, net);

        int idx = EntitySystem.Instance.GetIndex(handle);
        whole.powerComponent[idx].NetID = id;
    }

    private void JoinNet(EntityHandle handle, int netID, WholeComponent whole)
    {
        if (_nets.TryGetValue(netID, out var net))
        {
            net.Nodes.Add(handle);
            int idx = EntitySystem.Instance.GetIndex(handle);
            whole.powerComponent[idx].NetID = netID;
        }
    }

    private void MergeNets(int masterID, int slaveID, WholeComponent whole)
    {
        if (masterID == slaveID) return;
        if (!_nets.TryGetValue(masterID, out var master) || !_nets.TryGetValue(slaveID, out var slave)) return;

        // 把从属电网的所有实体搬过来
        foreach (var h in slave.Nodes)
        {
            master.Nodes.Add(h);
            int idx = EntitySystem.Instance.GetIndex(h);
            if (idx != -1) whole.powerComponent[idx].NetID = masterID;
        }

        foreach (var h in slave.Consumers)
        {
            master.Consumers.Add(h);
            int idx = EntitySystem.Instance.GetIndex(h);
            if (idx != -1) whole.powerComponent[idx].NetID = masterID;
        }

        _nets.Remove(slaveID);
        Debug.Log($"<color=yellow>电网合并: Net {slaveID} 融入了 Net {masterID} 喵！</color>");
    }

    private void RefreshConsumersForNode(EntityHandle nodeHandle, WholeComponent whole)
    {
        int nodeIdx = EntitySystem.Instance.GetIndex(nodeHandle);
        ref var nodePower = ref whole.powerComponent[nodeIdx];
        ref var nodeMove = ref whole.moveComponent[nodeIdx];

        int r = Mathf.CeilToInt(nodePower.SupplyRange);
        for (int x = -r; x <= r; x++)
        {
            for (int y = -r; y <= r; y++)
            {
                Vector2Int checkPos = nodeMove.LogicalPosition + new Vector2Int(x, y);
                int occId = GridSystem.Instance.GetOccupantId(checkPos);
                if (occId != -1)
                {
                    EntityHandle h = EntitySystem.Instance.GetHandleFromId(occId);
                    int idx = EntitySystem.Instance.GetIndex(h);
                    if (idx != -1 && whole.workComponent[idx].RequiresPower)
                    {
                        // 发现消费者，将其拉入当前节点的电网
                        whole.powerComponent[idx].NetID = nodePower.NetID;
                        if (!_nets[nodePower.NetID].Consumers.Contains(h))
                            _nets[nodePower.NetID].Consumers.Add(h);
                    }
                }
            }
        }
    }

    private void ChargeBatteries(PowerNet net, float energyAmount, WholeComponent whole)
    {
        if (energyAmount <= 0) return;
        // 简单平摊充电逻辑喵
        foreach (var h in net.Nodes)
        {
            int idx = EntitySystem.Instance.GetIndex(h);
            if (idx == -1) continue;
            ref var p = ref whole.powerComponent[idx];
            if (p.Capacity > 0)
            {
                float space = p.Capacity - p.StoredEnergy;
                float toAdd = Mathf.Min(space, energyAmount); // 这里简化了，实际应根据蓄电池数量平摊
                p.StoredEnergy += toAdd;
            }
        }
    }

    private float DischargeBatteries(PowerNet net, float energyNeeded, WholeComponent whole)
    {
        float totalDischarged = 0;
        foreach (var h in net.Nodes)
        {
            int idx = EntitySystem.Instance.GetIndex(h);
            if (idx == -1) continue;
            ref var p = ref whole.powerComponent[idx];
            if (p.StoredEnergy > 0)
            {
                float canGive = Mathf.Min(p.StoredEnergy, energyNeeded - totalDischarged);
                p.StoredEnergy -= canGive;
                totalDischarged += canGive;
            }
            if (totalDischarged >= energyNeeded) break;
        }
        return totalDischarged;
    }
}