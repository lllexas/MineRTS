using System.Collections.Generic;
using UnityEngine;

public class TransportDrawSystem : SingletonMono<TransportDrawSystem>
{
    private SpriteLib _spriteLib;
    private SpriteInstanceRenderService _spriteRenderService;

    // --- 视觉位置缓存 (保持平滑逻辑) ---
    private Dictionary<int, Vector3> _visualPosDict = new Dictionary<int, Vector3>();
    private HashSet<int> _currentFrameIds = new HashSet<int>();

    [Header("平滑参数")]
    public float smoothSpeed = 15.0f;
    public float snapDistance = 2.0f;

    protected override void Awake()
    {
        base.Awake();
        _spriteLib = SpriteLib.Instance;
        _spriteRenderService = SpriteInstanceRenderService.Shared;
    }

    public void UpdateTransportDraws(WholeComponent whole)
    {
        float dt = Time.deltaTime;

        // 1. 清理矩阵数据
        _spriteRenderService.Clear();
        _currentFrameIds.Clear();

        // =========================================================
        // 2. 收集传送带上的物品
        // =========================================================
        var lines = TransportSystem.Instance.GetLines();
        foreach (var line in lines)
        {
            Vector3[] pathPoints = GetLineWorldPath(line, whole);
            Vector2Int[] directions = line.Directions.ToArray();

            for (int i = 0; i < line.ItemCount; i++)
            {
                ref var item = ref line.Items[i];
                Vector3 targetPos = CalculateItemWorldPos(item.Distance, pathPoints, directions);
                Vector3 finalVisualPos = targetPos;

                // 插值平滑
                if (_visualPosDict.TryGetValue(item.ItemId, out Vector3 lastVisPos))
                {
                    float dist = Vector3.Distance(lastVisPos, targetPos);
                    if (dist < snapDistance)
                    {
                        float dynamicSpeed = (line.Speed * 1.5f) + (dist * 12f);
                        finalVisualPos = Vector3.MoveTowards(lastVisPos, targetPos, dynamicSpeed * dt);
                    }
                }

                _visualPosDict[item.ItemId] = finalVisualPos;
                _currentFrameIds.Add(item.ItemId);

                // 记录绘制数据
                RecordItemMatrix(item.ItemType, finalVisualPos);
            }
        }

        // =========================================================
        // 3. 收集飞行中的物品 (建筑端口任务)
        // =========================================================
        for (int i = 0; i < whole.entityCount; i++)
        {
            ref var work = ref whole.workComponent[i];
            for (int p = 0; p < 8; p++)
            {
                ref var task = ref work.GetTask(p);
                if (task.Active)
                {
                    Vector3 flyPos = Vector3.Lerp(task.StartPos, task.EndPos, task.Progress);
                    // 给飞行物品加一点点额外的 Z 偏移，防止和地面单位闪烁
                    RecordItemMatrix(task.ItemType, flyPos, -0.05f);
                }
            }
        }

        // =========================================================
        // 4. 执行 GPU Instancing 绘制
        // =========================================================
        _spriteRenderService.Flush(_spriteLib);

        // 5. 清理过期缓存
        CleanUpCache();
    }

    private void RecordItemMatrix(int itemType, Vector3 pos, float zExtraOffset = 0)
    {
        // 获取蓝图和 SpriteId
        string bpKey = (itemType == 1) ? "ore_iron" : "ore_copper";
        var bp = BlueprintRegistry.Get(bpKey);
        int spriteId = bp.SpriteId;

        if (spriteId < 0) return;

        // 构建 TRS 矩阵
        // --- 【关键修改：物品深度设为 -0.2】 ---
        // 这样它就正好夹在 传送带(-0.1) 和 单位(-0.3) 中间
        float finalZ = -2f + zExtraOffset;
        // 位置：使用统一的 ITEM_Z
        Vector3 finalPos = new Vector3(pos.x, pos.y, finalZ);
        // 缩放：从蓝图获取
        Vector3 scale = new Vector3(bp.VisualScale.x, bp.VisualScale.y, 1);

        _spriteRenderService.Enqueue(new SpriteInstanceDrawRequest
        {
            SpriteId = spriteId,
            Band = InStageRenderBand.Item,
            Matrix = InStageRenderSpace.MakeSpriteMatrix(new Vector2(finalPos.x, finalPos.y), Quaternion.identity, scale, finalPos.z)
        });
    }

    private void CleanUpCache()
    {
        // 简单的过期清理
        var keys = new List<int>(_visualPosDict.Keys);
        foreach (var key in keys)
        {
            if (!_currentFrameIds.Contains(key)) _visualPosDict.Remove(key);
        }
    }

    // --- 路径计算辅助方法 (保持不变) ---
    private Vector3 CalculateItemWorldPos(float distance, Vector3[] centers, Vector2Int[] dirs)
    {
        if (centers.Length == 0) return Vector3.zero;
        int tileIdx = Mathf.FloorToInt(distance);
        if (tileIdx < 0) return centers[0] - (Vector3)(Vector2)dirs[0] * 0.5f;
        if (tileIdx >= centers.Length) return centers[centers.Length - 1] + (Vector3)(Vector2)dirs[centers.Length - 1] * 0.5f;

        Vector3 tileCenter = centers[tileIdx];
        Vector3 flowDir = (Vector3)(Vector2)dirs[tileIdx];
        float offsetFromCenter = distance - tileIdx - 0.5f;
        return tileCenter + flowDir * offsetFromCenter;
    }

    private Vector3[] GetLineWorldPath(TransportLine line, WholeComponent whole)
    {
        Vector3[] path = new Vector3[line.ConveyorCreationIndices.Count];
        for (int i = 0; i < line.ConveyorCreationIndices.Count; i++)
        {
            if (TransportSystem.Instance.TryGetEntityIndex(line.ConveyorCreationIndices[i], out int entityIdx))
                path[i] = whole.coreComponent[entityIdx].Position;
        }
        return path;
    }
}
