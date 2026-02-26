using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

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

    // --- GPU实例化数据结构 ---
    private Mesh _circleMesh;
    private Mesh _lineMesh;
    private List<Matrix4x4> _circleMatrices = new List<Matrix4x4>();
    private List<Vector4> _circleColors = new List<Vector4>();      // 圆圈颜色 (Shader会自动处理双层叠加)
    private List<Matrix4x4> _lineMatrices = new List<Matrix4x4>();
    private List<Vector4> _lineColors = new List<Vector4>();
    private List<float> _lineLengths = new List<float>(); // 连线物理长度
    private MaterialPropertyBlock _propertyBlock;
    private Material _instancedCircleMaterial;     // 圆圈材质 (包含双Pass)
    private Material _instancedLineMaterial;
    private Vector3? _previewPos;
    private EntityBlueprint? _previewBp;

    protected override void Awake()
    {
        base.Awake();

        // 创建Mesh
        _circleMesh = CreateCircleMesh();
        _lineMesh = CreateLineMesh();

        // 初始化材质属性块
        _propertyBlock = new MaterialPropertyBlock();

        // 创建支持实例化的材质副本
        // 圆圈材质：使用支持每实例颜色的Shader（包含双Pass）
        Shader circleShader = Shader.Find("Custom/Effects/SolidCircleInstanced");
        if (circleShader != null)
        {
            // 单个材质实例，包含双Pass
            _instancedCircleMaterial = new Material(circleShader);
            _instancedCircleMaterial.enableInstancing = true;
            _instancedCircleMaterial.renderQueue = 3050; // 能量场渲染队列

            // 设置圆圈纹理
            if (circleSprite != null && circleSprite.texture != null)
            {
                _instancedCircleMaterial.SetTexture("_MainTex", circleSprite.texture);
                Debug.Log("PowerOverlay: 使用自定义实例化圆圈Shader，已设置圆圈纹理");
            }
            else
            {
                Debug.LogWarning("PowerOverlay: circleSprite纹理为空，圆圈可能显示为方形");
            }
        }
        else if (flatOverlayMaterial != null)
        {
            _instancedCircleMaterial = new Material(flatOverlayMaterial);
            _instancedCircleMaterial.enableInstancing = true;
            _instancedCircleMaterial.renderQueue = 3050; // 能量场渲染队列
            Debug.LogWarning("PowerOverlay: 使用flatOverlayMaterial，可能不支持每实例颜色");
        }

        // 连线材质：使用支持每实例颜色的虚线Shader
        Shader lineShader = Shader.Find("Custom/Effects/DashedLineInstancedPerInstance");
        if (lineShader != null)
        {
            _instancedLineMaterial = new Material(lineShader);
            _instancedLineMaterial.enableInstancing = true;
            _instancedLineMaterial.renderQueue = 3051; // 虚线渲染队列（在能量场之上）
            Debug.Log("PowerOverlay: 使用自定义实例化虚线Shader");
        }
        else if (dashedLineMaterial != null)
        {
            _instancedLineMaterial = new Material(dashedLineMaterial);
            _instancedLineMaterial.enableInstancing = true;
            _instancedLineMaterial.renderQueue = 3051; // 虚线渲染队列（在能量场之上）
            Debug.LogWarning("PowerOverlay: 使用dashedLineMaterial，可能不支持每实例颜色");
        }
    }

    /// <summary>
    /// 创建圆圈Mesh：标准Quad，中心在原点，尺寸1x1
    /// </summary>
    private Mesh CreateCircleMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "PowerCircleQuad";

        // 4个顶点：中心在原点，尺寸1x1
        Vector3[] vertices = new Vector3[4]
        {
            new Vector3(-0.5f, -0.5f, 0),
            new Vector3(0.5f, -0.5f, 0),
            new Vector3(-0.5f, 0.5f, 0),
            new Vector3(0.5f, 0.5f, 0)
        };

        // UV
        Vector2[] uvs = new Vector2[4]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(0, 1),
            new Vector2(1, 1)
        };

        // 三角形：2个三角形组成Quad
        int[] triangles = new int[6] { 0, 2, 1, 2, 3, 1 };

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;

        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        return mesh;
    }

    /// <summary>
    /// 创建连线Mesh：Quad，Pivot在底部中心，尺寸1x1
    /// 底部中心在原点，向上延伸
    /// </summary>
    private Mesh CreateLineMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "PowerLineQuad";

        // 4个顶点：底部中心在原点，向上延伸
        // 顶点布局：左下(-0.5,0), 右下(0.5,0), 左上(-0.5,1), 右上(0.5,1)
        Vector3[] vertices = new Vector3[4]
        {
            new Vector3(-0.5f, 0f, 0),   // 左下
            new Vector3(0.5f, 0f, 0),    // 右下
            new Vector3(-0.5f, 1f, 0),   // 左上
            new Vector3(0.5f, 1f, 0)     // 右上
        };

        // UV：与顶点对应
        Vector2[] uvs = new Vector2[4]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(0, 1),
            new Vector2(1, 1)
        };

        // 三角形：2个三角形组成Quad
        int[] triangles = new int[6] { 0, 2, 1, 2, 3, 1 };

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;

        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        return mesh;
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

        // 清空上一帧数据
        _circleMatrices.Clear();
        _circleColors.Clear();
        _lineMatrices.Clear();
        _lineColors.Clear();
        _lineLengths.Clear();

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

        // 3. 收集圆圈实例数据
        for (int i = 0; i < activeNodes.Count; i++)
        {
            var node = activeNodes[i];
            if (node.supplyRange > 0.1f)
            {
                // 位置 Z 轴设为 -6.0f（在血条之上，选择框之下）
                Vector3 position = new Vector3(node.pos.x, node.pos.y, -6.0f);
                // 缩放 Vector3.one * diameter
                float diameter = node.supplyRange * 2f;
                Vector3 scale = Vector3.one * diameter;

                Matrix4x4 matrix = Matrix4x4.TRS(position, Quaternion.identity, scale);
                _circleMatrices.Add(matrix);

                // 颜色计算
                Color color = (node.netID != -1) ? poweredColor : unpoweredColor;

                // 如果是预览，降低透明度
                if (i == previewIndex)
                {
                    color.a *= 0.8f;
                }

                // Shader会自动处理双层叠加（Pass 1的Alpha会乘以0.5）
                // 这里只需设置基础颜色，不需要分层Alpha
                _circleColors.Add(color);
            }
        }

        // 4. 收集连线实例数据
        for (int i = 0; i < activeNodes.Count; i++)
        {
            var nodeA = activeNodes[i];
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
                    // 连线计算 (关键点)
                    Vector3 pointA = new Vector3(nodeA.pos.x, nodeA.pos.y, -6.1f); // 在圆圈之上（更靠近相机）
                    Vector3 pointB = new Vector3(nodeB.pos.x, nodeB.pos.y, -6.1f);
                    Vector3 direction = pointB - pointA;

                    // Position = A 点的坐标（因为 Mesh 的 Pivot 在底部）
                    Vector3 position = pointA;

                    // Rotation = 从 Vector3.up 到 (B-A) 的旋转
                    Quaternion rotation = Quaternion.FromToRotation(Vector3.up, direction);

                    // Scale = Vector3(lineWidth, (B-A).magnitude, 1f)
                    float length = direction.magnitude;
                    Vector3 scale = new Vector3(lineWidth, length, 1f);

                    Matrix4x4 matrix = Matrix4x4.TRS(position, rotation, scale);
                    _lineMatrices.Add(matrix);

                    // 颜色：预览连线用黄色，普通连线用白色
                    Color lineColor = (i == previewIndex || j == previewIndex) ? Color.yellow : Color.white;
                    _lineColors.Add(lineColor);
                    _lineLengths.Add(length); // 存储连线物理长度
                }
            }
        }

        // 5. 实例化绘制 - 电力圆圈（单Pass Stencil增量判定架构）
        // 使用单Pass Stencil逻辑：Ref 2设定最大重叠次数，Comp Less确保Stencil值小于2时绘制
        // Pass IncrSat使绘制后Stencil值+1，实现重叠次数计数和上限控制
        if (_circleMatrices.Count > 0 && _instancedCircleMaterial != null && _circleMesh != null)
        {
            _propertyBlock.Clear();
            _propertyBlock.SetVectorArray("_BaseColor", _circleColors);

            // 准备通用的绘制参数
            RenderParams rp = new RenderParams(_instancedCircleMaterial)
            {
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 10000),
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                matProps = _propertyBlock,
                rendererPriority = 100
            };

            // 单次绘制调用（Shader已合并为单Pass）
            // 参数含义：(绘制参数, Mesh, 子网格索引, 矩阵列表, 实例数量)
            Graphics.RenderMeshInstanced(rp, _circleMesh, 0, _circleMatrices, _circleMatrices.Count);
        }

        if (_lineMatrices.Count > 0 && _instancedLineMaterial != null && _lineMesh != null)
        {
            _propertyBlock.Clear();
            _propertyBlock.SetVectorArray("_BaseColor", _lineColors);
            _propertyBlock.SetFloatArray("_LineLength", _lineLengths); // 传递连线物理长度
            // 设置虚线密度
            _propertyBlock.SetFloat("_DashRatio", 0.5f);
            _propertyBlock.SetFloat("_ScrollSpeed", 2.0f);
            _propertyBlock.SetFloat("_TextureScale", textureScale);

            RenderParams rp = new RenderParams(_instancedLineMaterial)
            {
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 10000),
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = false,
                matProps = _propertyBlock,
                rendererPriority = 101 // 比圆圈高一层
            };

            // 使用List重载避免GC分配
            Graphics.RenderMeshInstanced(rp, _lineMesh, 0, _lineMatrices);
        }
    }

    private void HideAll()
    {
        // GPU实例化方案下，只需清空矩阵列表即可
        _circleMatrices.Clear();
        _circleColors.Clear();
        _lineMatrices.Clear();
        _lineColors.Clear();
        _lineLengths.Clear();
    }
}