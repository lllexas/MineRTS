using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class DrawSystem : SingletonMono<DrawSystem>
{
    private const float BillboardFootAnchorXOffset = 0f;
    private const float BillboardFootAnchorYOffset = -0.16666667f;

    [Header("调试模式")]
    public bool UseDebugSpriteRenderers = false;
    public bool DrawAtlasBillboardGizmos = false;
    [Header("血条设置")]
    public Material healthBarMaterial;
    private Material _hbMaterialInstance; // 【新增】血条材质副本，用于运行时修改 Queue
    private Mesh _quadMesh;
    private List<Matrix4x4> _hbMatrices = new List<Matrix4x4>(1024);
    private List<float> _hbFillAmounts = new List<float>(1024); // 存储每个实例的血量百分比
    private MaterialPropertyBlock _hbPropertyBlock; // 【关键】血条专用，防止与单位属性冲突
    private List<GameObject> _debugProxies;
    private Transform _debugRoot;
    private SpriteLib _spriteLib;
    private SpriteInstanceRenderService _spriteRenderService;
    private UnitAtlasBillboardRenderService _unitAtlasBillboardRenderService;
    private readonly Dictionary<string, EntityBlueprintSO> _blueprintSoCache = new Dictionary<string, EntityBlueprintSO>();
    private readonly List<AtlasBillboardDebugInfo> _atlasBillboardDebugInfos = new List<AtlasBillboardDebugInfo>(256);


    protected override void Awake()
    {
        base.Awake();
        _hbPropertyBlock = new MaterialPropertyBlock(); // 初始化血条属性块
        _debugProxies = new List<GameObject>();
        _debugRoot = new GameObject("--- [Debug] DrawSystem Proxies ---").transform;
        _spriteLib = SpriteLib.Instance;
        _spriteRenderService = SpriteInstanceRenderService.Shared;
        _unitAtlasBillboardRenderService = UnitAtlasBillboardRenderService.Shared;

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
                tm.alignment = UnityEngine.TextAlignment.Center;

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

        // 1. 清理上一帧的数据
        _spriteRenderService.Clear();
        _unitAtlasBillboardRenderService.Clear();
        _hbMatrices.Clear();
        _hbFillAmounts.Clear();
        _atlasBillboardDebugInfos.Clear();

        // 2. 收集矩阵数据
        for (int i = 0; i < count; i++)
        {
            ref var core = ref whole.coreComponent[i];
            if (!core.Active) continue;

            ref var draw = ref whole.drawComponent[i];
            ref var move = ref whole.moveComponent[i];
            int spriteId = draw.SpriteId;

            bool isConveyor = whole.workComponent[i].WorkType == WorkType.Conveyor;
            float zPos = isConveyor ? -1f : 0f;

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

            if (TryEnqueueAtlasBillboard(ref core, pos, scaleVal))
            {
                // atlas billboard 已接管该单位主绘制
            }
            else
            {
                _spriteRenderService.Enqueue(new SpriteInstanceDrawRequest
                {
                    SpriteId = spriteId,
                    Band = isConveyor ? InStageRenderBand.Conveyor : InStageRenderBand.Unit,
                    Matrix = InStageRenderSpace.MakeSpriteMatrix(new Vector2(pos.x, pos.y), rot, scaleVal, pos.z)
                });
            }

            // --- 血条逻辑 (同步 jumpOffset) ---
            bool shouldShowHB = (core.Type & (UnitType.Hero | UnitType.Minion | UnitType.Building)) != 0;
            if (shouldShowHB)
            {
                ref var health = ref whole.healthComponent[i];
                if (health.IsAlive && health.Health < health.MaxHealth)
                {
                    // 血条跟随单位的 Position 和 jumpOffset
                    Vector3 hbPos = new Vector3(core.Position.x, core.Position.y + 0.55f + jumpOffset, 0f);
                    Vector3 hbScale = new Vector3(core.LogicSize.x * 0.8f, 0.12f, 1f);

                    _hbMatrices.Add(Matrix4x4.TRS(hbPos, Quaternion.identity, hbScale));
                    _hbFillAmounts.Add(Mathf.Clamp01(health.Health / health.MaxHealth));
                }
            }
        }

        // 3. 绘制阶段
        _spriteRenderService.Flush(_spriteLib);
        _unitAtlasBillboardRenderService.Flush();

        if (_hbMatrices.Count > 0)
        {
            DrawHealthBars();
        }
    }

    private bool TryEnqueueAtlasBillboard(ref CoreComponent core, Vector3 pos, Vector3 scaleVal)
    {
        EntityBlueprintSO blueprintSO = GetBlueprintSO(core.BlueprintName);
        if (blueprintSO == null || blueprintSO.AnimationSetSO == null)
        {
            return false;
        }

        UnitAtlasAnimationSetSO animationSet = blueprintSO.AnimationSetSO;
        if (animationSet.AtlasTexture == null)
        {
            return false;
        }

        AtlasFrameCoord frameCoord = ResolvePhaseOneDefaultFrame(animationSet);
        Rect uvRect = animationSet.GetFrameUvRect(frameCoord);
        Camera renderCamera = CameraController.Instance != null
            ? CameraController.Instance.TargetCamera
            : Camera.main;
        Vector2 footAnchor = ResolveBillboardFootAnchor(core.Position, pos.y - core.Position.y);

        Matrix4x4 matrix = InStageRenderSpace.MakeBillboardMatrix(
            footAnchor,
            scaleVal,
            renderCamera,
            pos.z);

        _unitAtlasBillboardRenderService.Enqueue(new UnitAtlasBillboardDrawRequest
        {
            AtlasTexture = animationSet.AtlasTexture,
            UvRect = uvRect,
            Matrix = matrix
        });

        if (DrawAtlasBillboardGizmos)
        {
            _atlasBillboardDebugInfos.Add(new AtlasBillboardDebugInfo
            {
                Anchor = new Vector3(pos.x, pos.y, pos.z),
                Matrix = matrix
            });
        }

        return true;
    }

    private static Vector2 ResolveBillboardFootAnchor(Vector2 logicalPosition, float verticalVisualOffset)
    {
        return new Vector2(
            logicalPosition.x + BillboardFootAnchorXOffset,
            logicalPosition.y + BillboardFootAnchorYOffset + verticalVisualOffset);
    }

    private EntityBlueprintSO GetBlueprintSO(string blueprintId)
    {
        if (string.IsNullOrWhiteSpace(blueprintId))
        {
            return null;
        }

        if (_blueprintSoCache.TryGetValue(blueprintId, out EntityBlueprintSO cached))
        {
            return cached;
        }

        EntityBlueprintSO blueprint = MetaLib.GetObject<EntityBlueprintSO>(blueprintId);
        _blueprintSoCache[blueprintId] = blueprint;
        return blueprint;
    }

    private static AtlasFrameCoord ResolvePhaseOneDefaultFrame(UnitAtlasAnimationSetSO animationSet)
    {
        if (animationSet == null)
        {
            return default;
        }

        if (animationSet.TryGetClip(UnitAnimationStateId.Idle, out UnitAtlasClipDef idleClip) &&
            idleClip.Frames != null &&
            idleClip.Frames.Length > 0)
        {
            return idleClip.Frames[0];
        }

        if (animationSet.Clips != null)
        {
            for (int i = 0; i < animationSet.Clips.Count; i++)
            {
                AtlasFrameCoord[] frames = animationSet.Clips[i].Frames;
                if (frames != null && frames.Length > 0)
                {
                    return frames[0];
                }
            }
        }

        return default;
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

    private void OnDrawGizmos()
    {
        if (!DrawAtlasBillboardGizmos || _atlasBillboardDebugInfos.Count == 0)
        {
            return;
        }

        Color previousColor = Gizmos.color;

        foreach (AtlasBillboardDebugInfo debugInfo in _atlasBillboardDebugInfos)
        {
            DrawAtlasBillboardQuad(debugInfo.Matrix);
            DrawAnchor(debugInfo.Anchor);
        }

        Gizmos.color = previousColor;
    }

    private static void DrawAtlasBillboardQuad(Matrix4x4 matrix)
    {
        Vector3 bl = matrix.MultiplyPoint3x4(new Vector3(-0.5f, 0f, 0f));
        Vector3 br = matrix.MultiplyPoint3x4(new Vector3(0.5f, 0f, 0f));
        Vector3 tl = matrix.MultiplyPoint3x4(new Vector3(-0.5f, 1f, 0f));
        Vector3 tr = matrix.MultiplyPoint3x4(new Vector3(0.5f, 1f, 0f));

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(bl, br);
        Gizmos.DrawLine(br, tr);
        Gizmos.DrawLine(tr, tl);
        Gizmos.DrawLine(tl, bl);
    }

    private static void DrawAnchor(Vector3 anchor)
    {
        const float crossHalfSize = 0.12f;
        const float normalSize = 0.2f;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(anchor + Vector3.left * crossHalfSize, anchor + Vector3.right * crossHalfSize);
        Gizmos.DrawLine(anchor + Vector3.down * crossHalfSize, anchor + Vector3.up * crossHalfSize);
        Gizmos.DrawLine(anchor, anchor + Vector3.forward * normalSize);
    }

    private struct AtlasBillboardDebugInfo
    {
        public Vector3 Anchor;
        public Matrix4x4 Matrix;
    }
}
