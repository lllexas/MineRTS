using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MineRTS.BigMap
{
    /// <summary>
    /// 大地图连线渲染器 - GPU 实例化方案
    /// 职责：使用 Graphics.RenderMeshInstanced 批量渲染所有连线
    /// 架构：SingletonMono，与 OverlayDrawSystem 类似的实例化渲染
    /// </summary>
    public class BigMapEdgeRenderer : SingletonMono<BigMapEdgeRenderer>
    {
        [Header("连线 Mesh")]
        [Tooltip("连线使用的 Quad Mesh（Pivot 在底部中心）")]
        [SerializeField] private Mesh _lineMesh;

        [Header("材质")]
        [Tooltip("虚线材质（支持 GPU 实例化，ZWrite On）")]
        [SerializeField] private Material _dashedLineMaterial;

        [Header("连线设置")]
        [SerializeField] private float _lineWidth = 0.15f;
        [SerializeField] private Color _lineColor = new Color(0.4f, 0.6f, 0.7f, 0.8f);
        [SerializeField] private float _textureScale = 2.0f; // 虚线密度
        [SerializeField] private float _zOffset = -1f; // 连线 Z 轴偏移（节点 Z=-2f 时，连线 Z=-1f）

        [Header("Shader 属性")]
        [SerializeField] private string _dashRatioProp = "_DashRatio";
        [SerializeField] private string _textureScaleProp = "_TextureScale";

        // 实例化数据
        private List<Matrix4x4> _lineMatrices = new List<Matrix4x4>();
        private List<Vector4> _lineColors = new List<Vector4>();
        private List<float> _lineLengths = new List<float>();
        private MaterialPropertyBlock _propertyBlock;
        private Material _instancedLineMaterial;

        // 连线数据
        private List<BigMapEdgeData> _edges = new List<BigMapEdgeData>();
        private Dictionary<string, Vector3> _nodePositions = new Dictionary<string, Vector3>();

        // 数据状态标志
        private bool _hasData = false;

        protected override void Awake()
        {
            base.Awake();

            // 创建连线 Mesh（如果未设置）
            if (_lineMesh == null)
            {
                _lineMesh = CreateLineMesh();
            }

            // 初始化材质
            InitializeMaterial();

            // 初始化材质属性块
            _propertyBlock = new MaterialPropertyBlock();

            // 【保险】初始时禁用自己，等 BigMapManager.Open() 时再启用
            gameObject.SetActive(false);
            Debug.Log("<color=cyan>[BigMapEdgeRenderer]</color> 初始状态：已禁用");
        }

        private void OnDestroy()
        {
            // 不再清理材质，因为它是 Inspector 中配置的资源，不是运行时创建的
        }

        private void LateUpdate()
        {
            // 只有在 GameObject 激活 且 有数据时才渲染连线
            if (!gameObject.activeSelf || !_hasData || _edges.Count == 0 || _nodePositions.Count == 0)
            {
                return;
            }

            // 每帧渲染连线
            RenderEdges();
        }

        /// <summary>
        /// 设置渲染器的激活状态
        /// </summary>
        public void SetActiveRenderer(bool active)
        {
            gameObject.SetActive(active);
        }

        /// <summary>
        /// 创建连线 Mesh：Quad，Pivot 在底部中心，尺寸 1x1
        /// 底部中心在原点，向上延伸
        /// </summary>
        private Mesh CreateLineMesh()
        {
            Mesh mesh = new Mesh();
            mesh.name = "BigMapLineQuad";

            // 4 个顶点：底部中心在原点，向上延伸
            Vector3[] vertices = new Vector3[4]
            {
                new Vector3(-0.5f, 0f, 0),   // 左下
                new Vector3(0.5f, 0f, 0),    // 右下
                new Vector3(-0.5f, 1f, 0),   // 左上
                new Vector3(0.5f, 1f, 0)     // 右上
            };

            // UV
            Vector2[] uvs = new Vector2[4]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1),
                new Vector2(1, 1)
            };

            // 三角形
            int[] triangles = new int[6] { 0, 2, 1, 2, 3, 1 };

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            return mesh;
        }

        /// <summary>
        /// 初始化材质
        /// </summary>
        private void InitializeMaterial()
        {
            // 使用 Inspector 中配置的材质（已经创建好，ZWrite On 版本）
            if (_dashedLineMaterial != null)
            {
                _instancedLineMaterial = _dashedLineMaterial;
                _instancedLineMaterial.enableInstancing = true;
                Debug.Log("BigMapEdgeRenderer: 使用 Inspector 配置的虚线材质 (ZWrite On)");
            }
            else
            {
                Debug.LogError("BigMapEdgeRenderer: 未在 Inspector 中配置虚线材质！");
            }
        }

        /// <summary>
        /// 设置连线数据
        /// </summary>
        public void SetEdges(List<BigMapEdgeData> edges)
        {
            _edges = edges;
            _hasData = edges != null && edges.Count > 0;
        }

        /// <summary>
        /// 设置节点位置映射表
        /// </summary>
        public void SetNodePositions(Dictionary<string, Vector3> positions)
        {
            _nodePositions = positions;
            _hasData = _hasData && (_nodePositions.Count > 0);
        }

        /// <summary>
        /// 添加单个节点位置
        /// </summary>
        public void SetNodePosition(string nodeId, Vector3 position)
        {
            _nodePositions[nodeId] = position;
        }

        /// <summary>
        /// 移除节点位置
        /// </summary>
        public void RemoveNodePosition(string nodeId)
        {
            _nodePositions.Remove(nodeId);
        }

        /// <summary>
        /// 渲染所有连线
        /// </summary>
        private void RenderEdges()
        {
            // 清空上一帧数据
            _lineMatrices.Clear();
            _lineColors.Clear();
            _lineLengths.Clear();

            // 收集连线实例数据
            foreach (var edge in _edges)
            {
                if (!_nodePositions.TryGetValue(edge.FromNodeID, out Vector3 fromPos))
                    continue;

                if (!_nodePositions.TryGetValue(edge.ToNodeID, out Vector3 toPos))
                    continue;

                // 计算连线矩阵
                Vector3 pointA = new Vector3(fromPos.x, fromPos.y, _zOffset); // 使用 Z 轴偏移
                Vector3 pointB = new Vector3(toPos.x, toPos.y, _zOffset);
                Vector3 direction = pointB - pointA;

                // Position = A 点的坐标
                Vector3 position = pointA;

                // Rotation = 从 Vector3.up 到 (B-A) 的旋转
                Quaternion rotation = Quaternion.FromToRotation(Vector3.up, direction);

                // Scale = Vector3(lineWidth, length, 1f)
                float length = direction.magnitude;
                Vector3 scale = new Vector3(_lineWidth, length, 1f);

                Matrix4x4 matrix = Matrix4x4.TRS(position, rotation, scale);
                _lineMatrices.Add(matrix);
                _lineColors.Add(_lineColor);
                _lineLengths.Add(length);
            }

            // GPU 实例化绘制
            if (_lineMatrices.Count > 0 && _instancedLineMaterial != null && _lineMesh != null)
            {
                _propertyBlock.Clear();
                _propertyBlock.SetVectorArray("_BaseColor", _lineColors);
                _propertyBlock.SetFloatArray("_LineLength", _lineLengths);
                _propertyBlock.SetFloat(_dashRatioProp, 0.5f);
                _propertyBlock.SetFloat(_textureScaleProp, _textureScale);

                RenderParams rp = new RenderParams(_instancedLineMaterial)
                {
                    worldBounds = new Bounds(Vector3.zero, Vector3.one * 10000),
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false,
                    matProps = _propertyBlock,
                    rendererPriority = 101
                };

                Graphics.RenderMeshInstanced(rp, _lineMesh, 0, _lineMatrices);
            }
        }

        /// <summary>
        /// 清空所有连线
        /// </summary>
        public void ClearEdges()
        {
            _edges.Clear();
            _nodePositions.Clear();
            _hasData = false;
            _lineMatrices.Clear();
            _lineColors.Clear();
            _lineLengths.Clear();
        }
    }
}
