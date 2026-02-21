using System.Collections.Generic;
using UnityEngine;

public class PathPreviewSystem : SingletonMono<PathPreviewSystem>
{
    /*[Header("素材配置")]
    public Material pathMaterial; // 建议使用 Unlit 且支持平铺纹理的材质

    [Header("视觉参数")]
    public float activeWidthRatio = 0.2f;   // 选中英雄线宽比例
    public float inactiveWidthRatio = 0.1f; // 非选中英雄线宽比例
    public Color activeColor = Color.cyan;
    public Color inactiveColor = new Color(1, 1, 1, 0.3f);
    public float zOffset = 0.5f;            // 严格设定 Z=0.5

    // 对象池：为每个英雄 Handle 维护一个独立的 LineRenderer
    private Dictionary<EntityHandle, LineRenderer> _lineRenderers = new Dictionary<EntityHandle, LineRenderer>();
    private Transform _poolRoot;

    protected override void Awake()
    {
        base.Awake();
        _poolRoot = new GameObject("--- [Generated] PathPreviewPool ---").transform;
    }

    private void LateUpdate()
    {
        var entitySys = EntitySystem.Instance;
        var userCtrl = UserControlSystem.Instance;
        var whole = entitySys.wholeComponent;

        // 记录本帧活跃的句柄，用于清理失效的线
        HashSet<EntityHandle> activeThisFrame = new HashSet<EntityHandle>();
        if (whole == null) return;

        // 1. 遍历所有实体，寻找英雄
        for (int i = 0; i < whole.entityCount; i++)
        {
            // 只有标记了英雄槽位的单位才进行路径绘制
            if (whole.userControlComponent[i].HeroSlot > 0)
            {
                EntityHandle handle = whole.coreComponent[i].SelfHandle;
                var buffer = userCtrl.GetInputBuffer(handle);

                // 只有当英雄有预输入路径时才绘制
                if (buffer != null)
                {
                    activeThisFrame.Add(handle);
                    bool isActive = (handle == userCtrl.ActiveHeroHandle);
                    DrawHeroPath(handle, i, buffer, isActive);
                }
            }
        }

        // 2. 清理已经不再持有指令或已销毁的实体的线
        List<EntityHandle> toRemove = new List<EntityHandle>();
        foreach (var handle in _lineRenderers.Keys)
        {
            if (!activeThisFrame.Contains(handle))
            {
                _lineRenderers[handle].positionCount = 0;
                _lineRenderers[handle].gameObject.SetActive(false);
                // 如果实体已完全失效，则记录下来准备从字典移除
                if (!entitySys.IsValid(handle)) toRemove.Add(handle);
            }
        }
        foreach (var h in toRemove) _lineRenderers.Remove(h);
    }

    private void DrawHeroPath(EntityHandle handle, int dataIndex, IEnumerable<Vector2Int> buffer, bool isActive)
    {
        // 获取或创建 LineRenderer
        if (!_lineRenderers.TryGetValue(handle, out LineRenderer lr))
        {
            GameObject go = new GameObject($"Path_{handle.Id}");
            go.transform.SetParent(_poolRoot);
            lr = go.AddComponent<LineRenderer>();

            // 初始化配置
            lr.material = pathMaterial;
            lr.textureMode = LineTextureMode.Tile; // 允许纹理平铺
            lr.alignment = LineAlignment.TransformZ; // 贴合 XY 平面渲染
            // 直接指定到要求的层级
            lr.sortingLayerName = "PathPreview";
            lr.sortingOrder = 0; // 如果层内还有先后，可以再调这个值
            // 【核心修正】正确处理折角：
            // 增加拐角顶点数可以使折角平滑，避免 90 度转弯时出现“尖刺”或“断裂”
            lr.numCornerVertices = 4;
            lr.numCapVertices = 2;

            _lineRenderers[handle] = lr;
        }

        lr.gameObject.SetActive(true);

        // 1. 设置视觉样式
        float cellSize = GridSystem.Instance.CellSize;
        float width = cellSize * (isActive ? activeWidthRatio : inactiveWidthRatio);
        lr.startWidth = lr.endWidth = width;
        lr.startColor = lr.endColor = isActive ? activeColor : inactiveColor;

        // 2. 构建路径点
        var move = EntitySystem.Instance.wholeComponent.moveComponent[dataIndex];
        List<Vector3> pathPositions = new List<Vector3>();

        // 路径起点应该是单位“目前正要去”的那个格子中心
        Vector2Int currentPos = move.TargetGridPosition;
        pathPositions.Add(GridToWorldZ(currentPos));

        // 沿指令队列模拟未来路径
        foreach (var dir in buffer)
        {
            currentPos += dir;
            pathPositions.Add(GridToWorldZ(currentPos));
        }

        // 3. 提交顶点
        lr.positionCount = pathPositions.Count;
        lr.SetPositions(pathPositions.ToArray());

        // 4. 动态效果：选中的英雄路径产生滚动感
        if (isActive && pathMaterial != null)
        {
            // 通过修改纹理偏移实现流动感
            lr.material.mainTextureOffset -= new Vector2(Time.deltaTime * 2.0f, 0);
        }
    }

    // 辅助工具：转换坐标并锁定 Z
    private Vector3 GridToWorldZ(Vector2Int gridPos)
    {
        Vector2 w = GridSystem.Instance.GridToWorld(gridPos);
        return new Vector3(w.x, w.y, zOffset);
    }*/
}