using UnityEngine;

namespace MineRTS.BigMap
{
    /// <summary>
    /// 视口背景Quad控制器
    /// 职责：动态调整3D Quad的大小以完全覆盖正交相机视口
    /// 应用场景：大地图无限世界空间网格底图
    /// 架构：挂在Main Camera的子物体（3D Quad）上，跟随摄像机动态缩放
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class ViewportBackgroundQuad : MonoBehaviour
    {
        [Header("深度设置")]
        [Tooltip("Quad在相机前的Z轴位置（深度），值越大越远")]
        [SerializeField] private float _zDepth = 10f;

        [Header("网格参数")]
        [Tooltip("世界空间网格密度（每单位网格线数量）")]
        [SerializeField] private float _gridDensity = 1.0f;

        [Tooltip("主网格线颜色")]
        [SerializeField] private Color _majorGridColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);

        [Tooltip("次网格线颜色")]
        [SerializeField] private Color _minorGridColor = new Color(0.2f, 0.2f, 0.2f, 0.2f);

        [Header("调试")]
        [SerializeField] private bool _showDebugInfo = false;

        // 组件引用
        private Camera _targetCamera;
        private Material _quadMaterial;

        // 缓存上次的相机参数，避免每帧更新
        private float _lastOrthographicSize;
        private float _lastAspect;

        private void Awake()
        {
            // 获取父物体的Camera组件
            _targetCamera = transform.parent?.GetComponent<Camera>();
            if (_targetCamera == null)
            {
                Debug.LogError("ViewportBackgroundQuad: 必须作为Camera的子物体挂载！");
                enabled = false;
                return;
            }

            if (!_targetCamera.orthographic)
            {
                Debug.LogWarning("ViewportBackgroundQuad: 目标相机不是正交投影，效果可能不正确");
            }

            // 获取或创建材质
            var renderer = GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                Debug.LogError("ViewportBackgroundQuad: 需要MeshRenderer组件");
                enabled = false;
                return;
            }

            _quadMaterial = renderer.material;
            if (_quadMaterial == null)
            {
                // 尝试创建默认材质
                Shader gridShader = Shader.Find("MineRTS/InfiniteWorldGrid");
                if (gridShader != null)
                {
                    _quadMaterial = new Material(gridShader);
                    renderer.material = _quadMaterial;
                    Debug.Log($"<color=cyan>[ViewportBackgroundQuad]</color> 创建了默认材质，Shader: {gridShader.name}");
                }
                else
                {
                    Debug.LogError("ViewportBackgroundQuad: 找不到'MineRTS/InfiniteWorldGrid' Shader，请确保Shader已正确导入");

                    // 尝试使用备用Shader
                    Shader fallbackShader = Shader.Find("Unlit/Transparent");
                    if (fallbackShader != null)
                    {
                        _quadMaterial = new Material(fallbackShader);
                        _quadMaterial.color = new Color(0.1f, 0.2f, 0.5f, 0.3f); // 测试颜色
                        renderer.material = _quadMaterial;
                        Debug.LogWarning("ViewportBackgroundQuad: 使用备用Shader Unlit/Transparent");
                    }
                    else
                    {
                        Debug.LogError("ViewportBackgroundQuad: 也找不到备用Shader");
                        enabled = false;
                        return;
                    }
                }
            }
            else
            {
                Debug.Log($"<color=cyan>[ViewportBackgroundQuad]</color> 使用现有材质，Shader: {_quadMaterial.shader?.name}");
            }

            Debug.Log($"<color=cyan>[ViewportBackgroundQuad]</color> 初始化完成 - 目标相机: {_targetCamera.name}, Z深度: {_zDepth}");
        }

        private void Start()
        {
            // 初始位置和缩放设置
            UpdateTransform();
            UpdateMaterialProperties();

            // 调试：输出材质信息
            if (_quadMaterial != null)
            {
                Debug.Log($"<color=cyan>[ViewportBackgroundQuad]</color> 材质信息 - Shader: {_quadMaterial.shader?.name}, " +
                         $"渲染队列: {_quadMaterial.renderQueue}");

                // 检查关键属性是否存在
                CheckShaderProperties();
            }
        }

        private void LateUpdate()
        {
            // 检测相机参数变化
            if (_targetCamera == null) return;

            bool needsTransformUpdate = false;
            bool needsMaterialUpdate = false;

            // 检查正交尺寸变化
            if (Mathf.Abs(_targetCamera.orthographicSize - _lastOrthographicSize) > 0.001f)
            {
                _lastOrthographicSize = _targetCamera.orthographicSize;
                needsTransformUpdate = true;
                needsMaterialUpdate = true;
            }

            // 检查宽高比变化
            if (Mathf.Abs(_targetCamera.aspect - _lastAspect) > 0.001f)
            {
                _lastAspect = _targetCamera.aspect;
                needsTransformUpdate = true;
                needsMaterialUpdate = true;
            }

            // 按需更新
            if (needsTransformUpdate)
            {
                UpdateTransform();
            }

            if (needsMaterialUpdate)
            {
                UpdateMaterialProperties();
            }

            // 每帧将GPU缓冲区应用到材质
            ApplyGPUBuffersToMaterial();

            // 调试信息
            if (_showDebugInfo && Time.frameCount % 60 == 0)
            {
                Debug.Log($"<color=yellow>[ViewportBackgroundQuad]</color> 状态 - 正交尺寸: {_targetCamera.orthographicSize:F2}, 宽高比: {_targetCamera.aspect:F2}, " +
                         $"缩放: {transform.localScale.x:F2}x{transform.localScale.y:F2}");
            }
        }

        /// <summary>
        /// 更新Quad的变换以完全覆盖相机视口
        /// 计算公式：
        ///   localScale.y = Camera.orthographicSize * 2
        ///   localScale.x = localScale.y * Camera.aspect
        /// </summary>
        private void UpdateTransform()
        {
            if (_targetCamera == null) return;

            // 计算缩放
            float scaleY = _targetCamera.orthographicSize * 2f;
            float scaleX = scaleY * _targetCamera.aspect;

            // 应用缩放
            transform.localScale = new Vector3(scaleX, scaleY, 1f);

            // 确保Quad在相机前方指定深度
            transform.localPosition = new Vector3(0f, 0f, _zDepth);
        }

        /// <summary>
        /// 更新材质属性，传递相机和网格参数给Shader
        /// </summary>
        private void UpdateMaterialProperties()
        {
            if (_quadMaterial == null || _targetCamera == null) return;

            // 传递相机参数
            _quadMaterial.SetFloat("_CameraOrthoSize", _targetCamera.orthographicSize);
            _quadMaterial.SetFloat("_CameraAspect", _targetCamera.aspect);

            // 传递网格参数（使用更明显的值进行测试）
            float testGridDensity = _gridDensity;
            float testGridThickness = 0.1f; // 更粗的网格线
            Color testMajorColor = new Color(0.5f, 0.5f, 0.5f, 0.8f); // 更明显的主网格线
            Color testMinorColor = new Color(0.3f, 0.3f, 0.3f, 0.4f); // 更明显的次网格线

            _quadMaterial.SetFloat("_GridDensity", testGridDensity);
            _quadMaterial.SetFloat("_GridThickness", testGridThickness);
            _quadMaterial.SetColor("_MajorGridColor", testMajorColor);
            _quadMaterial.SetColor("_MinorGridColor", testMinorColor);

            // 传递世界空间参数（相机位置）
            Vector3 cameraWorldPos = _targetCamera.transform.position;
            _quadMaterial.SetVector("_CameraWorldPos", cameraWorldPos);

            // 节点参数（默认值）
            _quadMaterial.SetFloat("_NodeRadius", 0.3f);
            _quadMaterial.SetColor("_NodeColor", new Color(0.2f, 0.6f, 1.0f, 0.9f));

            // 调试网格（显示相机边界）
            _quadMaterial.SetFloat("_ShowDebugGrid", _showDebugInfo ? 1.0f : 0.0f);
            _quadMaterial.SetColor("_DebugGridColor", new Color(1.0f, 0.0f, 0.0f, 0.5f));

            // 调试信息
            if (_showDebugInfo)
            {
                Debug.Log($"<color=cyan>[ViewportBackgroundQuad]</color> 材质属性已更新 - " +
                         $"网格密度: {testGridDensity}, 网格厚度: {testGridThickness}, " +
                         $"正交尺寸: {_targetCamera.orthographicSize:F2}, 相机位置: {cameraWorldPos}");
            }
        }

        /// <summary>
        /// 设置网格密度
        /// </summary>
        public void SetGridDensity(float density)
        {
            _gridDensity = Mathf.Max(0.1f, density);
            UpdateMaterialProperties();
        }

        /// <summary>
        /// 设置主网格线颜色
        /// </summary>
        public void SetMajorGridColor(Color color)
        {
            _majorGridColor = color;
            UpdateMaterialProperties();
        }

        /// <summary>
        /// 设置次网格线颜色
        /// </summary>
        public void SetMinorGridColor(Color color)
        {
            _minorGridColor = color;
            UpdateMaterialProperties();
        }

        /// <summary>
        /// 设置Z深度
        /// </summary>
        public void SetZDepth(float depth)
        {
            _zDepth = depth;
            UpdateTransform();
        }

        /// <summary>
        /// 启用/禁用调试信息
        /// </summary>
        public void SetDebugInfoEnabled(bool enabled)
        {
            _showDebugInfo = enabled;
        }

        /// <summary>
        /// 获取当前相机引用（用于外部访问）
        /// </summary>
        public Camera GetTargetCamera()
        {
            return _targetCamera;
        }

        /// <summary>
        /// 检查Shader属性是否存在
        /// </summary>
        private void CheckShaderProperties()
        {
            if (_quadMaterial == null || _quadMaterial.shader == null) return;

            string[] criticalProperties = new string[]
            {
                "_GridDensity", "_MajorGridColor", "_MinorGridColor",
                "_CameraOrthoSize", "_CameraAspect", "_CameraWorldPos",
                "_NodeBuffer", "_NodeCount", "_EdgeBuffer", "_EdgeCount"
            };

            Debug.Log("<color=cyan>[ViewportBackgroundQuad]</color> 检查Shader属性...");
            foreach (var propName in criticalProperties)
            {
                bool hasProperty = _quadMaterial.HasProperty(propName);
                Debug.Log($"  {propName}: {(hasProperty ? "✓ 存在" : "✗ 缺失")}");
            }
        }

        /// <summary>
        /// 将GPU缓冲区应用到材质
        /// </summary>
        private void ApplyGPUBuffersToMaterial()
        {
            if (_quadMaterial == null) return;

            // 如果GPU缓冲区管理器存在，将缓冲区应用到材质
            if (BigMapGPUBufferManager.Instance != null)
            {
                if (BigMapGPUBufferManager.Instance.AreBuffersInitialized())
                {
                    BigMapGPUBufferManager.Instance.ApplyBuffersToMaterial(_quadMaterial);
                    Debug.Log("<color=cyan>[ViewportBackgroundQuad]</color> GPU缓冲区已应用到材质");
                }
                else
                {
                    Debug.LogWarning("<color=orange>[ViewportBackgroundQuad]</color> GPU缓冲区未初始化，跳过应用");
                }
            }
            else
            {
                Debug.LogWarning("<color=orange>[ViewportBackgroundQuad]</color> BigMapGPUBufferManager实例未找到，无法应用GPU缓冲区");
            }
        }

        /// <summary>
        /// 获取当前材质（用于动态设置Shader属性）
        /// </summary>
        public Material GetQuadMaterial()
        {
            return _quadMaterial;
        }
    }
}