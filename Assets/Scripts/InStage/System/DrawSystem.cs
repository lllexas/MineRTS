using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class DrawSystem : SingletonMono<DrawSystem>
{
    [Header("调试模式")]
    public bool UseDebugSpriteRenderers = false;
    [Header("血条设置")]
    public Material healthBarMaterial;
    private Material _hbMaterialInstance; // 【新增】血条材质副本，用于运行时修改 Queue
    private Mesh _quadMesh;
    private List<Matrix4x4> _hbMatrices = new List<Matrix4x4>(1024);
    private List<float> _hbFillAmounts = new List<float>(1024); // 存储每个实例的血量百分比
    private MaterialPropertyBlock _hbPropertyBlock; // 【关键】血条专用，防止与单位属性冲突


    // --- 修改点 1：将矩阵池拆分为两组，对应不同的 Sorting Layer ---
    private Dictionary<int, List<Matrix4x4>> _conveyorMatrices; // 对应 "Conveyor" 层
    private Dictionary<int, List<Matrix4x4>> _unitMatrices;     // 对应 "Units" 层

    private Dictionary<int, Material> _conveyorMatCache = new Dictionary<int, Material>();
    private Dictionary<int, Material> _unitMatCache = new Dictionary<int, Material>();


    // 缓存 Layer ID，避免每帧 String 转 Int 的开销
    private int _layerIdConveyor;
    private int _layerIdUnits;

    private MaterialPropertyBlock _propertyBlock;
    private List<GameObject> _debugProxies;
    private Transform _debugRoot;
    private SpriteLib _spriteLib;


    protected override void Awake()
    {
        base.Awake();

        // 初始化两个字典
        _conveyorMatrices = new Dictionary<int, List<Matrix4x4>>();
        _unitMatrices = new Dictionary<int, List<Matrix4x4>>();

        // 获取 Sorting Layer 的 ID
        _layerIdConveyor = SortingLayer.NameToID("Conveyor");
        _layerIdUnits = SortingLayer.NameToID("Units");

        _propertyBlock = new MaterialPropertyBlock();
        _hbPropertyBlock = new MaterialPropertyBlock(); // 初始化血条属性块
        _debugProxies = new List<GameObject>();
        _debugRoot = new GameObject("--- [Debug] DrawSystem Proxies ---").transform;
        _spriteLib = SpriteLib.Instance;

        if (healthBarMaterial != null)
        {
            _hbMaterialInstance = new Material(healthBarMaterial);
            // 显式设置队列：Conveyor(3000) < Unit(3010) < HealthBar(3020)
            _hbMaterialInstance.enableInstancing = true;
            _hbMaterialInstance.renderQueue = 3020;
        }

        // 创建一个简单的 Quad 供血条使用
        _quadMesh = new Mesh();
        _quadMesh.vertices = new Vector3[] {
            new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0),
            new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0)
        };
        _quadMesh.uv = new Vector2[] {
            new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(0, 1), new Vector2(1, 1)
        };
        _quadMesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
    }

    public void UpdateDraws(WholeComponent whole, float deltaTime)
    {
        if (UseDebugSpriteRenderers) UpdateWithSpriteRenderers(whole);
        else UpdateWithInstancing(whole);
    }

    private void UpdateWithSpriteRenderers(WholeComponent whole)
    {
        int count = whole.entityCount;
        if (!_debugRoot.gameObject.activeSelf) _debugRoot.gameObject.SetActive(true);

        // 扩充池子
        while (_debugProxies.Count < count)
        {
            GameObject go = new GameObject($"DebugProxy_{_debugProxies.Count}");
            go.transform.SetParent(_debugRoot);
            go.AddComponent<SpriteRenderer>();
            _debugProxies.Add(go);
        }

        for (int i = 0; i < _debugProxies.Count; i++)
        {
            GameObject go = _debugProxies[i];

            // 超过数量隐藏
            if (i >= count)
            {
                if (go.activeSelf) go.SetActive(false);
                continue;
            }

            ref var core = ref whole.coreComponent[i];
            // 非激活隐藏
            if (!core.Active)
            {
                if (go.activeSelf) go.SetActive(false);
                continue;
            }

            if (!go.activeSelf) go.SetActive(true);

            ref var health = ref whole.healthComponent[i];
            ref var draw = ref whole.drawComponent[i];

            // 1. 同步 Transform
            // 注意 Z 轴：Debug 模式下设为 0 或者 -0.1 以防被背景遮挡
            go.transform.position = new Vector3(core.Position.x, core.Position.y, 0);
            go.transform.rotation = Quaternion.Euler(0, 0, Vector2.SignedAngle(Vector2.up, (Vector2Int)core.Rotation));
            go.transform.localScale = new Vector3(core.VisualScale.x, core.VisualScale.y, 1);

            // 2. 同步 SpriteRenderer
            var sr = go.GetComponent<SpriteRenderer>();
            if (whole.workComponent[i].WorkType == WorkType.Conveyor)
            {
                sr.sortingLayerName = "Conveyor";
            }
            else
            {
                sr.sortingLayerName = "Units";
            }

            // 设置 Sprite
            if (draw.SpriteId >= 0 && draw.SpriteId < _spriteLib.unitSprites.Count)
                sr.sprite = _spriteLib.unitSprites[draw.SpriteId];
            else if (_spriteLib.unitSprites.Count > 0)
                sr.sprite = _spriteLib.unitSprites[0];

            // 【修改】去掉了队伍染色逻辑，统一为白色
            sr.color = Color.white;

            // 3. 同步头顶数字
            TextMesh tm = go.GetComponentInChildren<TextMesh>();
            if (tm == null)
            {
                GameObject textObj = new GameObject("ValueText");
                textObj.transform.SetParent(go.transform);
                tm = textObj.AddComponent<TextMesh>();
                tm.characterSize = 0.12f;
                tm.fontSize = 50;
                tm.fontStyle = FontStyle.Bold;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;

                var mr = textObj.GetComponent<MeshRenderer>();
                mr.sortingLayerName = "Number";
                mr.sortingOrder = 10;
            }

            // 只有英雄或小兵显示血量
            bool shouldShowHealth = (core.Type & (UnitType.Hero | UnitType.Minion)) != 0;

            if (shouldShowHealth)
            {
                if (!tm.gameObject.activeSelf) tm.gameObject.SetActive(true);

                int displayVal = Mathf.Max(0, Mathf.CeilToInt(health.Health));
                tm.text = displayVal.ToString();
                // 字体颜色可以保留一点区分，或者也改成统一白色
                tm.color = (core.Team == 1) ? Color.yellow : Color.white;
                tm.transform.rotation = Quaternion.identity;
                tm.transform.position = go.transform.position + new Vector3(0, 1.2f, -0.1f);
            }
            else
            {
                if (tm.gameObject.activeSelf) tm.gameObject.SetActive(false);
            }
        }
    }


    private void UpdateWithInstancing(WholeComponent whole)
    {
        int count = whole.entityCount;

        // 如果开启了 Instancing，把 Debug 的节点全关掉以节省性能
        if (_debugRoot.gameObject.activeSelf) _debugRoot.gameObject.SetActive(false);
        if (count == 0) return;

        // 1. 清理上一帧的数据
        foreach (var list in _conveyorMatrices.Values) list.Clear();
        foreach (var list in _unitMatrices.Values) list.Clear();
        _hbMatrices.Clear();
        _hbFillAmounts.Clear();

        // 2. 收集矩阵数据
        for (int i = 0; i < count; i++)
        {
            ref var core = ref whole.coreComponent[i];
            if (!core.Active) continue;

            ref var draw = ref whole.drawComponent[i];
            ref var move = ref whole.moveComponent[i];
            int spriteId = draw.SpriteId;

            bool isConveyor = whole.workComponent[i].WorkType == WorkType.Conveyor;
            var targetDict = isConveyor ? _conveyorMatrices : _unitMatrices;

            if (!targetDict.ContainsKey(spriteId))
                targetDict[spriteId] = new List<Matrix4x4>(1024);

            float zPos = isConveyor ? -1f : -3f;

            // --- 🔥【修正后的特技逻辑：拒绝抽搐】 ---
            float jumpOffset = 0f;
            float stretchX = 1f;
            float stretchY = 1f;

            // 判定条件：只要逻辑上还在“跨格子”（Previous != Logical），就说明在动
            bool isStepping = move.LogicalPosition != move.PreviousLogicalPosition;
            bool isCreature = (core.Type & (UnitType.Hero | UnitType.Minion)) != 0;

            // 只要是在跨格子，或者是由于 SubTick 延迟导致 Timer 还没完全归零，就继续计算动画
            if (isCreature && (isStepping || move.Timer > 0))
            {
                // 重新计算丝滑的 t (0 -> 1)
                // 此时 move.Timer 已经是带 SubTickOffset 的平滑浮点数了
                float interval = move.MoveInterval;
                if (interval > 0)
                {
                    float t = 1.0f - Mathf.Clamp01(move.Timer / interval);

                    // 🔥【移除 move.Timer > 0 的硬判断】
                    // 只要 t 在有效范围内，我们就让 Sin 曲线自己走完
                    if (t > 0.0001f && t < 0.9999f)
                    {
                        jumpOffset = Mathf.Sin(t * Mathf.PI) * 0.35f;
                        float stretchFactor = 1.0f + (jumpOffset * 0.4f);
                        stretchY = stretchFactor;
                        stretchX = 1.0f / stretchFactor;
                    }
                }
            }
            // ----------------------------------------------------

            // 1. 应用偏移到位置 (Position 使用 core.Position，它已经是插值后的了)
            Vector3 pos = new Vector3(core.Position.x, core.Position.y + jumpOffset, zPos);

            // 2. 旋转逻辑保持不变
            Quaternion rot = Quaternion.Euler(0, 0, Vector2.SignedAngle(Vector2.up, (Vector2Int)core.Rotation));

            // 3. 应用形变到缩放
            Vector3 scaleVal = new Vector3(
                core.VisualScale.x * stretchX,
                core.VisualScale.y * stretchY,
                1
            );

            // 基础安全检查
            if (scaleVal.sqrMagnitude < 0.001f) scaleVal = Vector3.one;

            targetDict[spriteId].Add(Matrix4x4.TRS(pos, rot, scaleVal));

            // --- 血条逻辑 (同步 jumpOffset) ---
            bool shouldShowHB = (core.Type & (UnitType.Hero | UnitType.Minion | UnitType.Building)) != 0;
            if (shouldShowHB)
            {
                ref var health = ref whole.healthComponent[i];
                if (health.IsAlive && health.Health < health.MaxHealth)
                {
                    // 血条跟随单位的 Position 和 jumpOffset
                    Vector3 hbPos = new Vector3(core.Position.x, core.Position.y + 0.55f + jumpOffset, -5f);
                    Vector3 hbScale = new Vector3(core.LogicSize.x * 0.8f, 0.12f, 1f);

                    _hbMatrices.Add(Matrix4x4.TRS(hbPos, Quaternion.identity, hbScale));
                    _hbFillAmounts.Add(Mathf.Clamp01(health.Health / health.MaxHealth));
                }
            }
        }


        // --- 准备 PropertyBlock (单位用)---
        if (_propertyBlock == null) _propertyBlock = new MaterialPropertyBlock();
        _propertyBlock.Clear();
        _propertyBlock.SetVector("_BaseColor", Color.white);
        _propertyBlock.SetVector("_Color", Color.white);

        // 3. 绘制阶段
        // 核心逻辑：先画传送带，再画单位，这样单位就会压在传送带上面

        // 传送带底层: Priority 10, Queue 3000
        DrawBatch(_conveyorMatrices, 10, 3000, _conveyorMatCache);

        // 单位顶层: Priority 30, Queue 3010 (比物品的 3005 更高)
        DrawBatch(_unitMatrices, 30, 3010, _unitMatCache);

        if (_hbMatrices.Count > 0)
        {
            DrawHealthBars();
        }
    }

    private void DrawBatch(Dictionary<int, List<Matrix4x4>> batchDict, int priority, int renderQueue, Dictionary<int, Material> cache)
    {
        foreach (var batch in batchDict)
        {
            int spriteId = batch.Key;
            List<Matrix4x4> matrices = batch.Value;
            if (matrices.Count == 0) continue;

            // --- 【核心逻辑：材质副本处理】 ---
            if (!cache.TryGetValue(spriteId, out Material layeredMat))
            {
                Material baseMat = _spriteLib.GetMaterial(spriteId);
                if (baseMat == null) continue;

                // 克隆材质并修改队列
                layeredMat = new Material(baseMat);
                layeredMat.renderQueue = renderQueue;
                cache[spriteId] = layeredMat;
            }

            Mesh mesh = _spriteLib.GetMesh(spriteId);
            if (mesh == null) continue;

            RenderParams rp = new RenderParams(layeredMat) // 使用带队列偏移的材质
            {
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 10000),
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                matProps = _propertyBlock,
                rendererPriority = priority
            };

            Graphics.RenderMeshInstanced(rp, mesh, 0, matrices);
        }
    }

    private void DrawHealthBars()
    {
        if (_hbMaterialInstance == null) return;

        // 填充实例化属性
        _hbPropertyBlock.Clear();
        _hbPropertyBlock.SetFloatArray("_FillAmount", _hbFillAmounts);

        // 【关键修改】使用 _hbMaterialInstance 而不是原始的材质资源
        RenderParams rp = new RenderParams(_hbMaterialInstance)
        {
            worldBounds = new Bounds(Vector3.zero, Vector3.one * 10000),
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false,
            matProps = _hbPropertyBlock,
            rendererPriority = 50
        };

        Graphics.RenderMeshInstanced(rp, _quadMesh, 0, _hbMatrices);
    }
}