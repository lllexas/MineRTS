using System.Collections.Generic;
using UnityEngine;

public class OverlayPowerSystem : SingletonMono<OverlayPowerSystem>
{
    public bool ShowPowerOverlay = false;

    [Header("圆圈设置")]
    public Sprite circleSprite;
    public Material flatOverlayMaterial; // 上一步做的 Stencil 材质
    public Color poweredColor = new Color(0f, 1f, 1f, 0.4f);
    public Color unpoweredColor = new Color(1f, 0f, 0f, 0.4f);

    [Header("连线设置")]
    public Material dashedLineMaterial; // 【新增】拖入刚才做的虚线材质
    public float lineWidth = 0.15f;     // 线宽
    public float textureScale = 2.0f;   // 虚线密度 (数字越大越密)

    // --- 对象池 ---
    private List<SpriteRenderer> _circlePool = new List<SpriteRenderer>();
    private List<LineRenderer> _linePool = new List<LineRenderer>(); // 【新增】
    private Transform _poolRoot;
    private Vector3? _previewPos;
    private EntityBlueprint? _previewBp;

    protected override void Awake()
    {
        base.Awake();
        _poolRoot = new GameObject("--- PowerOverlayPool ---").transform;
        _poolRoot.SetParent(this.transform);
    }

    public void SetOverlayActive(bool active)
    {
        ShowPowerOverlay = active;
        if (!active)
        {
            HideAll(); // 立刻隐藏所有池子里的对象，不留残影喵！
        }
    }
    /// <summary>
    /// 由 BuildingController 调用，设置当前拿在手里的建筑预览信息
    /// </summary>
    public void UpdatePreview(Vector3? pos, EntityBlueprint? bp)
    {
        _previewPos = pos;
        _previewBp = bp;
    }
    private void Update()
    {
        // 同时也兼容 F5 手动切换
        if (Input.GetKeyDown(KeyCode.F5))
        {
            SetOverlayActive(!ShowPowerOverlay);
            Debug.Log($"<color=cyan>Power Overlay 手动切换: {ShowPowerOverlay}</color>");
        }

        if (ShowPowerOverlay)
        {
            RenderOverlay();
        }
    }

    private void RenderOverlay()
    {
        var whole = EntitySystem.Instance.wholeComponent;
        if (whole == null) return;

        int usedCircles = 0;
        int usedLines = 0;

        // 1. 收集所有真实节点
        List<(Vector3 pos, float supplyRange, float connRange, int netID)> activeNodes = new List<(Vector3, float, float, int)>();

        for (int i = 0; i < whole.entityCount; i++)
        {
            if (whole.coreComponent[i].Active && whole.powerComponent[i].IsNode)
            {
                var p = whole.powerComponent[i];
                activeNodes.Add((whole.coreComponent[i].Position, p.SupplyRange, p.ConnRange, p.NetID));
            }
        }

        // 2. 【核心】如果有预览节点，把它作为一个特殊的“虚构节点”加进去
        int previewIndex = -1;
        if (_previewPos.HasValue && _previewBp.HasValue)
        {
            previewIndex = activeNodes.Count;
            // 预览节点的 NetID 设为 -2，表示它还没联网，但我们要强行让它跟周围连线预览
            activeNodes.Add((_previewPos.Value, _previewBp.Value.SupplyRange, _previewBp.Value.ConnectionRange, -2));
        }

        // 3. 开始渲染循环
        for (int i = 0; i < activeNodes.Count; i++)
        {
            var nodeA = activeNodes[i];

            // --- A. 画圆圈 ---
            if (nodeA.supplyRange > 0.1f)
            {
                SpriteRenderer sr = GetCircleFromPool(usedCircles++);
                sr.gameObject.SetActive(true);
                sr.transform.position = new Vector3(nodeA.pos.x, nodeA.pos.y, -2f);

                float diameter = nodeA.supplyRange * 2f;
                float spriteBaseSize = sr.sprite.bounds.size.x;
                sr.transform.localScale = Vector3.one * (diameter / spriteBaseSize);

                // 如果是 NetID != -1 (或者是预览通电 999)，就用蓝色；否则用红色
                sr.color = (nodeA.netID != -1) ? poweredColor : unpoweredColor;

                // 如果是预览，可以稍微调低一点透明度 (比如 0.8倍)，增加“虚影”感，但 Hue 保持不变
                if (i == previewIndex)
                {
                    Color c = sr.color;
                    c.a *= 0.8f;
                    sr.color = c;
                }
            }

            // --- B. 智能连线 (包含预览节点) ---
            for (int j = i + 1; j < activeNodes.Count; j++)
            {
                var nodeB = activeNodes[j];

                // 连线条件：
                // 1. 都是真实节点：必须同一个 NetID
                // 2. 其中一个是预览节点：只要在连接范围内就画（模拟未来的连接）
                bool canConnect = false;
                if (i == previewIndex || j == previewIndex)
                    canConnect = true; // 预览节点尝试连接一切邻居
                else
                    canConnect = (nodeA.netID != -1 && nodeA.netID == nodeB.netID);

                if (!canConnect) continue;

                float distSq_AB = (nodeA.pos - nodeB.pos).sqrMagnitude;
                float maxRange = Mathf.Max(nodeA.connRange, nodeB.connRange);
                if (distSq_AB > maxRange * maxRange) continue;

                // RNG 算法过滤蜘蛛网
                bool isRedundant = false;
                for (int n = 0; n < activeNodes.Count; n++)
                {
                    if (n == i || n == j) continue;
                    float distSq_AC = (nodeA.pos - activeNodes[n].pos).sqrMagnitude;
                    float distSq_BC = (nodeB.pos - activeNodes[n].pos).sqrMagnitude;
                    if (distSq_AC < distSq_AB && distSq_BC < distSq_AB)
                    {
                        isRedundant = true;
                        break;
                    }
                }

                if (!isRedundant)
                {
                    LineRenderer lr = GetLineFromPool(usedLines++);
                    lr.gameObject.SetActive(true);
                    lr.SetPosition(0, new Vector3(nodeA.pos.x, nodeA.pos.y, -1.5f));
                    lr.SetPosition(1, new Vector3(nodeB.pos.x, nodeB.pos.y, -1.5f));

                    // 预览连线用亮黄色或者虚线滚动，普通连线保持原样
                    if (i == previewIndex || j == previewIndex)
                        lr.startColor = lr.endColor = Color.yellow;
                    else
                        lr.startColor = lr.endColor = Color.white;

                    lr.material.mainTextureScale = new Vector2(textureScale, 1f);
                }
            }
        }

        CleanupPool(_circlePool, usedCircles);
        CleanupPool(_linePool, usedLines);
    }

    private void HideAll()
    {
        foreach (var sr in _circlePool) sr.gameObject.SetActive(false);
        foreach (var lr in _linePool) lr.gameObject.SetActive(false);
    }

    // --- 池子管理 ---

    private SpriteRenderer GetCircleFromPool(int index)
    {
        while (_circlePool.Count <= index)
        {
            GameObject go = new GameObject($"PowerField_{_circlePool.Count}");
            go.transform.SetParent(_poolRoot);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = circleSprite;
            if (flatOverlayMaterial != null) sr.sharedMaterial = flatOverlayMaterial;
            sr.sortingLayerName = "Power";
            sr.sortingOrder = 100;
            _circlePool.Add(sr);
        }
        return _circlePool[index];
    }

    private LineRenderer GetLineFromPool(int index)
    {
        while (_linePool.Count <= index)
        {
            GameObject go = new GameObject($"ConnLine_{_linePool.Count}");
            go.transform.SetParent(_poolRoot);
            LineRenderer lr = go.AddComponent<LineRenderer>();

            // LineRenderer 初始化配置
            lr.material = dashedLineMaterial;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.positionCount = 2;
            lr.useWorldSpace = true;

            // 【关键】开启纹理平铺模式
            lr.textureMode = LineTextureMode.Tile;

            lr.sortingLayerName = "Power";
            lr.sortingOrder = 101; // 比圆圈高一层

            _linePool.Add(lr);
        }
        return _linePool[index];
    }

    private void CleanupPool<T>(List<T> pool, int usedCount) where T : Component
    {
        for (int i = usedCount; i < pool.Count; i++)
        {
            if (pool[i].gameObject.activeSelf) pool[i].gameObject.SetActive(false);
        }
    }
}