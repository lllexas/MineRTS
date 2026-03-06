using UnityEngine;
using System.Collections.Generic;

namespace MineRTS.BigMap
{
    /// <summary>
    /// 【扩散场效果桥接器】双模型并行架构
    /// 功能：支持扩散模型和空间 SDF 模型的切换/叠加
    /// 架构：继承 ViewportShaderBridge，管理 Ping-Pong 缓冲区和 SDF 缓冲区
    /// 视觉效果：可切换柔和扩散/硬朗 SDF/两者混合
    /// </summary>
    public class DiffusionFieldBridge : ViewportShaderBridge
    {
        [Header("ComputeShader 设置")]
        [Tooltip("扩散场 ComputeShader 资源")]
        [SerializeField] private ComputeShader _computeShader;

        [Tooltip("线程组大小（需与 ComputeShader 一致）")]
        [SerializeField] private int _threadGroupSize = 8;

        [Header("扩散参数")]
        [Tooltip("梯度下降步长，越大坡度越陡")]
        [SerializeField, Range(0.0001f, 0.005f)] private float _gradientStep = 0.02f;

        [Tooltip("每秒扩散迭代次数")]
        [SerializeField, Range(1f, 480f)] private float _diffusionSpeed = 240f;

        [Tooltip("注入强度，节点处的最大高度")]
        [SerializeField, Range(0.1f, 5.0f)] private float _injectStrength = 1.0f;

        [Tooltip("注入半径（世界空间单位）")]
        [SerializeField, Range(0.01f, 0.5f)] private float _injectRadius = 0.05f;

        [Tooltip("参考正交尺寸：在此尺寸下扩散速度为 1.0")]
        [SerializeField] private float _referenceOrthoSize = 5.0f;

        // 时间债累加器
        private float _diffusionTimer = 0f;

        // 单帧最大迭代次数（防止掉帧时卡死）
        private const int MaxStepsPerFrame = 10;

        // 初始化标记（确保第一帧至少执行一次扩散）
        private bool _isFirstFrame = true;

        [Header("密度注入设置")]
        [Tooltip("启用则在相机移动时禁止注入（只有静止时才注入）")]
        [SerializeField] private bool _blockInjectionWhenCameraMoving = true;

        [Tooltip("相机静止阈值")]
        [SerializeField] private float _cameraStillThreshold = 0.001f;

        [Header("扩散各向异性校正")]
        [Tooltip("X 方向艺术调整（1=无调整，<1=X 方向扩散变慢，>1=X 方向扩散变快）")]
        [SerializeField, Range(0.1f, 3f)] private float _diffusionArtisticX = 1.0f;

        [Tooltip("Y 方向艺术调整（1=无调整，<1=Y 方向扩散变慢，>1=Y 方向扩散变快）")]
        [SerializeField, Range(0.1f, 3f)] private float _diffusionArtisticY = 1.0f;

        [Header("呼吸灯效果")]
        [Tooltip("启用呼吸灯效果")]
        [SerializeField] private bool _enableBreathing = false;

        [Tooltip("X 轴呼吸周期（秒）")]
        [SerializeField, Range(0.5f, 10f)] private float _breathingPeriodX = 3.0f;

        [Tooltip("Y 轴呼吸周期（秒）")]
        [SerializeField, Range(0.5f, 10f)] private float _breathingPeriodY = 3.0f;

        [Tooltip("X 轴呼吸振幅")]
        [SerializeField, Range(0f, 1f)] private float _breathingAmplitudeX = 0.3f;

        [Tooltip("Y 轴呼吸振幅")]
        [SerializeField, Range(0f, 1f)] private float _breathingAmplitudeY = 0.3f;

        [Tooltip("X 轴呼吸相位（π的倍数，0=0°，0.5=90°，1=180°，1.5=270°）")]
        [SerializeField, Range(0f, 2f)] private float _breathingPhaseX = 0f;

        [Tooltip("Y 轴呼吸相位（π的倍数，0=0°，0.5=90°，1=180°，1.5=270°）")]
        [SerializeField, Range(0f, 2f)] private float _breathingPhaseY = 0f;

        [Header("鼠标注入设置")]
        [Tooltip("启用鼠标位置注入")]
        [SerializeField] private bool _enableMouseInject = true;

        [Tooltip("鼠标注入强度（独立于普通节点，始终注入）")]
        [SerializeField, Range(0.1f, 5.0f)] 
        private float _mouseInjectStrength = 0.5f;

        [Tooltip("鼠标注入半径")]
        [SerializeField, Range(0.01f, 3.0f)]
        private float _mouseInjectRadius = 0.1f;

        [Tooltip("启用则在鼠标按键按下时暂停注入（拖拽时防止场变乱）")]
        [SerializeField] private bool _pauseInjectOnMousePress = true;

        // 鼠标世界坐标（每帧更新）
        private Vector2 _mouseWorldPosition;

        [Header("SDF 模式")]
        [Tooltip("启用 SDF 模式")]
        [SerializeField] private bool _enableSDF = false;

        [Tooltip("SDF 梯度斜率，越大下降越快")]
        [SerializeField, Range(0.01f, 1.0f)] private float _sdfGradient = 0.1f;

        [Tooltip("混合系数（0=纯扩散，1=纯 SDF）")]
        [SerializeField, Range(0f, 1f)] private float _sdfBlendFactor = 0.5f;

        [Tooltip("使用模式")]
        [SerializeField] private SDFMode _useSDFMode = SDFMode.DiffusionOnly;

        [Header("SDF 时间平滑设置")]
        [Tooltip("SDF 时间平滑模式")]
        [SerializeField] private SDFTemporalMode _temporalMode = SDFTemporalMode.InertialDamping;

        [Tooltip("单一平滑速度 (仅 SimpleSmooth 模式有效)")]
        [SerializeField, Range(0.1f, 20f)] private float _sdfSmoothSpeed = 5.0f;

        [Tooltip("建立速度 Attack (仅 InertialDamping 模式有效，建议较大值：15)")]
        [SerializeField, Range(0.1f, 100f)] private float _sdfAttackSpeed = 15.0f;

        [Tooltip("衰减速度 Decay (仅 InertialDamping 模式有效，建议较小值：2，越小拖影越长)")]
        [SerializeField, Range(0.1f, 50f)] private float _sdfDecaySpeed = 2.0f;

        public enum SDFMode
        {
            DiffusionOnly,   // 仅扩散
            SDFOnly,         // 仅 SDF
            Blend            // 混合
        }

        public enum SDFTemporalMode
        {
            None,               // 无平滑，纯生硬 SDF
            SimpleSmooth,       // 对称平滑
            InertialDamping     // 非对称惯性阻尼
        }

        [Header("纹理设置")]
        [Tooltip("模拟纹理宽度（512=低配，1024=标配，2048=高配）")]
        [SerializeField] private int _textureWidth = 1024;

        [Tooltip("模拟纹理高度")]
        [SerializeField] private int _textureHeight = 1024;

        [Header("显示模式")]
        [Tooltip("启用世界空间对齐，禁用则固定在屏幕空间")]
        [SerializeField] private bool _useWorldSpace = true;

        [Header("调试")]
        [Tooltip("显示调试信息")]
        [SerializeField] private bool _showDebugInfo = false;

        // Kernel Handle
        private int _kernelDiffuse = -1;
        private int _kernelInject = -1;
        private int _kernelDiffuseInject = -1;
        private int _kernelInit = -1;
        private int _kernelSpatialSDF = -1;
        private int _kernelTemporalSmooth = -1;
        private int _kernelInertialDamping = -1;

        // Ping-Pong 缓冲区
        private RenderTexture _bufferA;
        private RenderTexture _bufferB;

        // SDF 缓冲区
        private RenderTexture _sdfBuffer;
        
        // SDF 平滑缓冲区（持久化）
        private RenderTexture _sdfSmoothBuffer;

        // 当前使用哪个 buffer 作为输入
        private bool _useBufferAAsInput = true;
        
        // 相机状态追踪（用于静止检测）
        private Vector3 _lastCameraPosition;
        private float _lastCameraOrthoSize;
        private bool _cameraInitialized = false;

        // 属性 ID 缓存（性能优化）
        private static readonly int NodesID = Shader.PropertyToID("_Nodes");
        private static readonly int NodeCountID = Shader.PropertyToID("_NodeCount");
        private static readonly int ScreenSizeID = Shader.PropertyToID("_ScreenSize");
        private static readonly int ScreenSizeInvID = Shader.PropertyToID("_ScreenSizeInv");
        private static readonly int WorldMinID = Shader.PropertyToID("_WorldMin");
        private static readonly int WorldMaxID = Shader.PropertyToID("_WorldMax");
        private static readonly int TimeID = Shader.PropertyToID("_Time");
        private static readonly int GradientStepID = Shader.PropertyToID("_GradientStep");
        private static readonly int InjectStrengthID = Shader.PropertyToID("_InjectStrength");
        private static readonly int InjectRadiusID = Shader.PropertyToID("_InjectRadius");
        private static readonly int DeltaTimeID = Shader.PropertyToID("_DeltaTime");
        private static readonly int InputBufferID = Shader.PropertyToID("_InputBuffer");
        private static readonly int OutputBufferID = Shader.PropertyToID("_OutputBuffer");
        private static readonly int ZoomCorrectionID = Shader.PropertyToID("_ZoomCorrection");

        // 扩散各向异性校正参数 ID
        private static readonly int DiffusionAspectXID = Shader.PropertyToID("_DiffusionAspectX");
        private static readonly int DiffusionAspectYID = Shader.PropertyToID("_DiffusionAspectY");
        private static readonly int DiffusionArtisticXID = Shader.PropertyToID("_DiffusionArtisticX");
        private static readonly int DiffusionArtisticYID = Shader.PropertyToID("_DiffusionArtisticY");

        // SDF 参数 ID
        private static readonly int SDFBufferID = Shader.PropertyToID("_SDFBuffer");
        private static readonly int SDFGradientID = Shader.PropertyToID("_SDFGradient");
        private static readonly int UseSDFModeID = Shader.PropertyToID("_UseSDFMode");
        private static readonly int SDFBlendFactorID = Shader.PropertyToID("_SDFBlendFactor");
        private static readonly int SDFTexID = Shader.PropertyToID("_SDFTex");
        private static readonly int DiffuseTexID = Shader.PropertyToID("_DiffuseTex");
        private static readonly int CameraWorldPosID = Shader.PropertyToID("_CameraWorldPos");
        private static readonly int UseWorldSpaceID = Shader.PropertyToID("_UseWorldSpace");
        
        // SDF 时间平滑参数 ID
        private static readonly int CurrentIdealSDFID = Shader.PropertyToID("_CurrentIdealSDF");
        private static readonly int PrevSDFBufferID = Shader.PropertyToID("_PrevSDFBuffer");
        private static readonly int SDFSmoothSpeedID = Shader.PropertyToID("_SDFSmoothSpeed");
        private static readonly int SDFAttackSpeedID = Shader.PropertyToID("_SDFAttackSpeed");
        private static readonly int SDFDecaySpeedID = Shader.PropertyToID("_SDFDecaySpeed");

        // 鼠标注入参数 ID
        private static readonly int MouseWorldPositionID = Shader.PropertyToID("_MouseWorldPosition");
        private static readonly int MouseInjectStrengthID = Shader.PropertyToID("_MouseInjectStrength");
        private static readonly int MouseInjectRadiusID = Shader.PropertyToID("_MouseInjectRadius");
        private static readonly int EnableMouseInjectID = Shader.PropertyToID("_EnableMouseInject");

        /// <summary>
        /// 当材质准备就绪时调用
        /// </summary>
        public override void OnMaterialReady(Material material)
        {
            base.OnMaterialReady(material);

            // 重置状态标记
            _isFirstFrame = true;
            _diffusionTimer = 0f;

            // 初始化 ComputeShader
            InitializeComputeShader();

            // 创建 RenderTexture
            CreateRenderTextures();

            // 初始化缓冲区
            InitializeBuffers();

            // 设置世界空间开关（在材质初始化时立即设置）
            if (material != null)
            {
                material.SetFloat(UseWorldSpaceID, _useWorldSpace ? 1f : 0f);
            }

            Debug.Log($"<color=cyan>[DiffusionFieldBridge]</color> 初始化完成 - 纹理：{_textureWidth}x{_textureHeight}, SDF: {_enableSDF}");
        }

        /// <summary>
        /// 初始化 ComputeShader
        /// </summary>
        private void InitializeComputeShader()
        {
            if (_computeShader == null)
            {
                Debug.LogError("DiffusionFieldBridge: ComputeShader 未分配！");
                enabled = false;
                return;
            }

            try
            {
                // 获取 Kernel Handle
                _kernelDiffuse = _computeShader.FindKernel("CSDiffuse");
                _kernelInject = _computeShader.FindKernel("CSInject");
                _kernelDiffuseInject = _computeShader.FindKernel("CSDiffuseInject");
                _kernelInit = _computeShader.FindKernel("CSInit");
                _kernelSpatialSDF = _computeShader.FindKernel("CSSpatialSDF");
                _kernelTemporalSmooth = _computeShader.FindKernel("CSSTemporalSmoothSDF");
                _kernelInertialDamping = _computeShader.FindKernel("CSInertialSDFDamping");

                // 获取线程组大小
                _computeShader.GetKernelThreadGroupSizes(_kernelDiffuse, out uint x, out _, out _);
                _threadGroupSize = (int)x;

                Debug.Log($"<color=cyan>[DiffusionFieldBridge]</color> ComputeShader 初始化成功！");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"<color=red>[DiffusionFieldBridge]</color> ComputeShader 初始化失败，可能是 .compute 文件编译报错了喵！\n错误信息：{ex.Message}");
                enabled = false;
            }
        }

        /// <summary>
        /// 创建 RenderTexture
        /// </summary>
        private void CreateRenderTextures()
        {
            // 释放旧纹理
            ReleaseRenderTextures();

            // 创建 Ping-Pong 缓冲区
            _bufferA = CreateRenderTexture("DiffusionBufferA");
            _bufferB = CreateRenderTexture("DiffusionBufferB");

            // 创建 SDF 缓冲区（理论值）
            _sdfBuffer = CreateRenderTexture("SDFBuffer");
            
            // 创建 SDF 平滑缓冲区（持久化）
            _sdfSmoothBuffer = CreateRenderTexture("SDFSmoothBuffer");

            Debug.Log($"<color=cyan>[DiffusionFieldBridge]</color> RenderTexture 创建完成");
        }

        /// <summary>
        /// 创建单个 RenderTexture
        /// </summary>
        private RenderTexture CreateRenderTexture(string name)
        {
            RenderTexture rt = new RenderTexture(_textureWidth, _textureHeight, 0, RenderTextureFormat.ARGBHalf);
            rt.name = name;
            rt.enableRandomWrite = true;
            rt.filterMode = FilterMode.Bilinear;
            rt.wrapMode = TextureWrapMode.Clamp;
            rt.Create();

            return rt;
        }

        /// <summary>
        /// 初始化缓冲区（清零）
        /// </summary>
        private void InitializeBuffers()
        {
            // 检查 GPU 缓冲区管理器是否就绪
            // 如果未就绪，跳过初始化，避免 Property (_Nodes) is not set 错误
            var gpuManager = BigMapGPUBufferManager.Instance;
            if (gpuManager == null || !gpuManager.IsReady)
            {
                Debug.LogWarning("<color=orange>[DiffusionFieldBridge]</color> GPU 缓冲区未就绪，跳过初始化");
                return;
            }

            if (_kernelInit >= 0)
            {
                _computeShader.SetVector(ScreenSizeID, new Vector2(_textureWidth, _textureHeight));

                _computeShader.SetTexture(_kernelInit, OutputBufferID, _bufferA);
                _computeShader.Dispatch(_kernelInit,
                    Mathf.CeilToInt(_textureWidth / 8.0f),
                    Mathf.CeilToInt(_textureHeight / 8.0f), 1);

                _computeShader.SetTexture(_kernelInit, OutputBufferID, _bufferB);
                _computeShader.Dispatch(_kernelInit,
                    Mathf.CeilToInt(_textureWidth / 8.0f),
                    Mathf.CeilToInt(_textureHeight / 8.0f), 1);
            }

            // 初始化 SDF 缓冲区（清零）
            if (_kernelSpatialSDF >= 0)
            {
                // 设置 SDF Kernel 所需参数
                _computeShader.SetVector(ScreenSizeID, new Vector2(_textureWidth, _textureHeight));
                _computeShader.SetVector(ScreenSizeInvID, new Vector2(1.0f / _textureWidth, 1.0f / _textureHeight));

                // 获取节点数据（初始化时可能为空，但需要设置避免错误）
                ComputeBuffer nodeBuffer = gpuManager.GetNodeBuffer();
                if (nodeBuffer != null)
                {
                    _computeShader.SetBuffer(_kernelSpatialSDF, NodesID, nodeBuffer);
                }
                _computeShader.SetInt(NodeCountID, gpuManager.GetCurrentMapData()?.Nodes?.Count ?? 0);

                // 设置 SDF 参数
                _computeShader.SetFloat(SDFGradientID, _sdfGradient);
                _computeShader.SetTexture(_kernelSpatialSDF, SDFBufferID, _sdfBuffer);
                _computeShader.Dispatch(_kernelSpatialSDF,
                    Mathf.CeilToInt(_textureWidth / 8.0f),
                    Mathf.CeilToInt(_textureHeight / 8.0f), 1);
            }
            
            // 初始化 SDF 平滑缓冲区（第一帧直接用 SDF 缓冲区的值填充，避免黑屏）
            if (_kernelTemporalSmooth >= 0 && _sdfBuffer != null && _sdfSmoothBuffer != null)
            {
                // 使用 Graphics.CopyTexture 快速复制
                Graphics.CopyTexture(_sdfBuffer, _sdfSmoothBuffer);
                Debug.Log("<color=cyan>[DiffusionFieldBridge]</color> SDF 平滑缓冲区已初始化");
            }

            _useBufferAAsInput = true;
        }

        /// <summary>
        /// 释放 RenderTexture
        /// </summary>
        private void ReleaseRenderTextures()
        {
            if (_bufferA != null)
            {
                _bufferA.Release();
                if (Application.isPlaying)
                    Destroy(_bufferA);
                else
                    DestroyImmediate(_bufferA);
                _bufferA = null;
            }

            if (_bufferB != null)
            {
                _bufferB.Release();
                if (Application.isPlaying)
                    Destroy(_bufferB);
                else
                    DestroyImmediate(_bufferB);
                _bufferB = null;
            }

            if (_sdfBuffer != null)
            {
                _sdfBuffer.Release();
                if (Application.isPlaying)
                    Destroy(_sdfBuffer);
                else
                    DestroyImmediate(_sdfBuffer);
                _sdfBuffer = null;
            }
            
            if (_sdfSmoothBuffer != null)
            {
                _sdfSmoothBuffer.Release();
                if (Application.isPlaying)
                    Destroy(_sdfSmoothBuffer);
                else
                    DestroyImmediate(_sdfSmoothBuffer);
                _sdfSmoothBuffer = null;
            }
        }

        /// <summary>
        /// 更新 Shader 属性（每帧调用）
        /// </summary>
        public override void UpdateShaderProperties()
        {
            if (!CanUpdate()) return;

            if (_computeShader == null) return;

            // 获取 GPU 缓冲区管理器
            var gpuManager = BigMapGPUBufferManager.Instance;
            if (gpuManager == null || !gpuManager.AreBuffersInitialized())
            {
                if (_showDebugInfo && Time.frameCount % 60 == 0)
                {
                    Debug.LogWarning("<color=orange>[DiffusionFieldBridge]</color> BigMapGPUBufferManager 未就绪");
                }
                return;
            }

            // ==================== 时间债模型：计算本帧需要执行的扩散迭代次数 ====================
            _diffusionTimer += Time.deltaTime * _diffusionSpeed;
            
            int stepsToRun = Mathf.FloorToInt(_diffusionTimer);
            stepsToRun = Mathf.Min(stepsToRun, MaxStepsPerFrame);
            
            // 第一帧强制执行至少一次扩散（避免缓冲区全黑）
            if (_isFirstFrame)
            {
                stepsToRun = Mathf.Max(stepsToRun, 1);
                _isFirstFrame = false;
            }
            
            // 执行扩散迭代
            for (int i = 0; i < stepsToRun; i++)
            {
                ExecuteDiffuseIteration(gpuManager);
                _diffusionTimer -= 1f;
            }

            // 更新材质纹理（使用最新的输出缓冲）
            UpdateMaterialTexture();
        }

        /// <summary>
        /// 执行一次扩散迭代（Ping-Pong 缓冲 + Dispatch）
        /// </summary>
        private void ExecuteDiffuseIteration(BigMapGPUBufferManager gpuManager)
        {
            // 选择 Kernel（使用合并版本）
            int kernel = _kernelDiffuseInject;

            // 节点数据
            ComputeBuffer nodeBuffer = gpuManager.GetNodeBuffer();
            if (nodeBuffer != null)
            {
                _computeShader.SetBuffer(kernel, NodesID, nodeBuffer);
            }
            _computeShader.SetInt(NodeCountID, gpuManager.GetCurrentMapData()?.Nodes?.Count ?? 0);

            // 屏幕/相机参数
            _computeShader.SetVector(ScreenSizeID, new Vector4(_textureWidth, _textureHeight, 0, 0));
            _computeShader.SetVector(ScreenSizeInvID, new Vector2(1.0f / _textureWidth, 1.0f / _textureHeight));

            // 计算世界边界（从相机参数）
            var camera = GetTargetCamera();
            Vector2 worldMin = Vector2.zero;
            Vector2 worldMax = Vector2.zero;

            if (camera != null)
            {
                float orthoSize = camera.orthographicSize;
                float aspect = camera.aspect;
                Vector3 cameraPos = camera.transform.position;

                float worldHeight = orthoSize * 2f;
                float worldWidth = worldHeight * aspect;

                worldMin = new Vector2(cameraPos.x - worldWidth / 2f, cameraPos.y - worldHeight / 2f);
                worldMax = new Vector2(cameraPos.x + worldWidth / 2f, cameraPos.y + worldHeight / 2f);

                _computeShader.SetVector(WorldMinID, worldMin);
                _computeShader.SetVector(WorldMaxID, worldMax);

                // 缩放校正
                float zoomCorrection = orthoSize / _referenceOrthoSize;
                _computeShader.SetFloat(ZoomCorrectionID, zoomCorrection);

                // 各向异性校正
                float autoAspectX = aspect;
                float autoAspectY = 1.0f;
                _computeShader.SetFloat(DiffusionAspectXID, autoAspectX);
                _computeShader.SetFloat(DiffusionAspectYID, autoAspectY);

                // 呼吸灯效果
                float finalArtX = _diffusionArtisticX;
                float finalArtY = _diffusionArtisticY;

                if (_enableBreathing)
                {
                    float breathingX = Mathf.Sin(2 * Mathf.PI * Time.time / _breathingPeriodX + _breathingPhaseX * Mathf.PI);
                    finalArtX = _diffusionArtisticX + breathingX * _breathingAmplitudeX;
                    finalArtX = Mathf.Max(0.1f, finalArtX);

                    float breathingY = Mathf.Sin(2 * Mathf.PI * Time.time / _breathingPeriodY + _breathingPhaseY * Mathf.PI);
                    finalArtY = _diffusionArtisticY + breathingY * _breathingAmplitudeY;
                    finalArtY = Mathf.Max(0.1f, finalArtY);
                }

                _computeShader.SetFloat(DiffusionArtisticXID, finalArtX);
                _computeShader.SetFloat(DiffusionArtisticYID, finalArtY);

                // 注入参数：根据相机静止状态决定是否注入
                float currentInjectStrength = _injectStrength;
                if (_blockInjectionWhenCameraMoving)
                {
                    if (!IsCameraStill(camera))
                    {
                        currentInjectStrength = 0f;
                    }
                }
                _computeShader.SetFloat(InjectStrengthID, currentInjectStrength);

                // 鼠标注入
                if (_enableMouseInject)
                {
                    bool isMousePressed = _pauseInjectOnMousePress && IsAnyMouseButtonPressed();
                    bool shouldInject = !isMousePressed;

                    if (shouldInject)
                    {
                        Vector3 mouseScreenPos = Input.mousePosition;
                        float mouseDepth = Mathf.Abs(transform.position.z - camera.transform.position.z);
                        Vector3 mouseWorldPos = camera.ScreenToWorldPoint(
                            new Vector3(mouseScreenPos.x, mouseScreenPos.y, mouseDepth)
                        );
                        _mouseWorldPosition = mouseWorldPos;

                        _computeShader.SetVector(MouseWorldPositionID, _mouseWorldPosition);
                        _computeShader.SetFloat(MouseInjectStrengthID, _mouseInjectStrength);
                        _computeShader.SetFloat(MouseInjectRadiusID, _mouseInjectRadius);
                        _computeShader.SetInt(EnableMouseInjectID, 1);
                    }
                    else
                    {
                        _computeShader.SetInt(EnableMouseInjectID, 0);
                    }
                }
                else
                {
                    _computeShader.SetInt(EnableMouseInjectID, 0);
                }
            }
            else
            {
                _computeShader.SetFloat(ZoomCorrectionID, 1.0f);
                _computeShader.SetFloat(DiffusionAspectXID, 1.0f);
                _computeShader.SetFloat(DiffusionAspectYID, 1.0f);
                _computeShader.SetFloat(DiffusionArtisticXID, 1.0f);
                _computeShader.SetFloat(DiffusionArtisticYID, 1.0f);
                _computeShader.SetFloat(InjectStrengthID, _injectStrength);
                _computeShader.SetInt(EnableMouseInjectID, 0);
            }

            // 时间和梯度参数
            _computeShader.SetFloat(TimeID, Time.time);
            _computeShader.SetFloat(GradientStepID, _gradientStep);
            _computeShader.SetFloat(InjectRadiusID, _injectRadius);
            _computeShader.SetFloat(DeltaTimeID, Time.deltaTime);

            // ==================== Ping-Pong 缓冲设置 ====================
            RenderTexture inputBuffer = _useBufferAAsInput ? _bufferA : _bufferB;
            RenderTexture outputBuffer = _useBufferAAsInput ? _bufferB : _bufferA;

            _computeShader.SetTexture(kernel, InputBufferID, inputBuffer);
            _computeShader.SetTexture(kernel, OutputBufferID, outputBuffer);

            // ==================== Dispatch ====================
            int threadGroupsX = Mathf.CeilToInt(_textureWidth / (float)_threadGroupSize);
            int threadGroupsY = Mathf.CeilToInt(_textureHeight / (float)_threadGroupSize);

            _computeShader.Dispatch(kernel, threadGroupsX, threadGroupsY, 1);

            // 交换 Buffer 状态
            _useBufferAAsInput = !_useBufferAAsInput;

            // ==================== SDF 处理（每帧执行一次，不在循环内） ====================
            // 注意：SDF 计算只在最后一次迭代后执行，避免重复计算
            // 这里我们用一个技巧：只在循环的最后一次迭代后执行 SDF
            // 但由于我们不知道这是不是最后一次，所以把 SDF 逻辑移到 UpdateMaterialTexture 中
        }

        /// <summary>
        /// 更新材质纹理（在扩散迭代完成后调用）
        /// </summary>
        private void UpdateMaterialTexture()
        {
            var gpuManager = BigMapGPUBufferManager.Instance;
            if (gpuManager == null) return;

            var camera = GetTargetCamera();
            int threadGroupsX = Mathf.CeilToInt(_textureWidth / (float)_threadGroupSize);
            int threadGroupsY = Mathf.CeilToInt(_textureHeight / (float)_threadGroupSize);

            // ==================== SDF 处理 ====================
            bool useSDF = _enableSDF || _useSDFMode != SDFMode.DiffusionOnly;
            if (useSDF && _kernelSpatialSDF >= 0)
            {
                ComputeBuffer nodeBuffer = gpuManager.GetNodeBuffer();

                _computeShader.SetVector(ScreenSizeID, new Vector4(_textureWidth, _textureHeight, 0, 0));
                _computeShader.SetVector(ScreenSizeInvID, new Vector2(1.0f / _textureWidth, 1.0f / _textureHeight));

                if (nodeBuffer != null)
                {
                    _computeShader.SetBuffer(_kernelSpatialSDF, NodesID, nodeBuffer);
                }
                _computeShader.SetInt(NodeCountID, gpuManager.GetCurrentMapData()?.Nodes?.Count ?? 0);

                // 重新计算世界边界
                Vector2 worldMin = Vector2.zero;
                Vector2 worldMax = Vector2.zero;
                if (camera != null)
                {
                    float orthoSize = camera.orthographicSize;
                    float aspect = camera.aspect;
                    Vector3 cameraPos = camera.transform.position;

                    float worldHeight = orthoSize * 2f;
                    float worldWidth = worldHeight * aspect;
                    worldMin = new Vector2(cameraPos.x - worldWidth / 2f, cameraPos.y - worldHeight / 2f);
                    worldMax = new Vector2(cameraPos.x + worldWidth / 2f, cameraPos.y + worldHeight / 2f);

                    _computeShader.SetVector(WorldMinID, worldMin);
                    _computeShader.SetVector(WorldMaxID, worldMax);
                }

                _computeShader.SetFloat(SDFGradientID, _sdfGradient);
                _computeShader.SetTexture(_kernelSpatialSDF, SDFBufferID, _sdfBuffer);
                _computeShader.Dispatch(_kernelSpatialSDF, threadGroupsX, threadGroupsY, 1);

                // SDF 时间平滑
                if (_temporalMode != SDFTemporalMode.None && _sdfSmoothBuffer != null)
                {
                    int activeKernel = -1;

                    if (_temporalMode == SDFTemporalMode.SimpleSmooth && _kernelTemporalSmooth >= 0)
                    {
                        activeKernel = _kernelTemporalSmooth;
                        _computeShader.SetFloat(SDFSmoothSpeedID, _sdfSmoothSpeed);
                    }
                    else if (_temporalMode == SDFTemporalMode.InertialDamping && _kernelInertialDamping >= 0)
                    {
                        activeKernel = _kernelInertialDamping;
                        _computeShader.SetFloat(SDFAttackSpeedID, _sdfAttackSpeed);
                        _computeShader.SetFloat(SDFDecaySpeedID, _sdfDecaySpeed);
                    }

                    if (activeKernel >= 0)
                    {
                        _computeShader.SetFloat(DeltaTimeID, Time.deltaTime);
                        _computeShader.SetVector(ScreenSizeID, new Vector4(_textureWidth, _textureHeight, 0, 0));
                        _computeShader.SetTexture(activeKernel, CurrentIdealSDFID, _sdfBuffer);
                        _computeShader.SetTexture(activeKernel, PrevSDFBufferID, _sdfSmoothBuffer);
                        _computeShader.Dispatch(activeKernel, threadGroupsX, threadGroupsY, 1);
                    }
                }
            }

            // ==================== 根据模式选择输出纹理 ====================
            RenderTexture diffusionOutput = _useBufferAAsInput ? _bufferA : _bufferB;

            RenderTexture sdfTextureSource;
            if (_temporalMode != SDFTemporalMode.None && _sdfSmoothBuffer != null)
            {
                sdfTextureSource = _sdfSmoothBuffer;
            }
            else
            {
                sdfTextureSource = _sdfBuffer;
            }

            switch (_useSDFMode)
            {
                case SDFMode.DiffusionOnly:
                    TargetMaterial.SetTexture("_EffectTex", diffusionOutput);
                    TargetMaterial.SetTexture(SDFTexID, diffusionOutput);
                    break;

                case SDFMode.SDFOnly:
                    TargetMaterial.SetTexture("_EffectTex", sdfTextureSource);
                    TargetMaterial.SetTexture(SDFTexID, sdfTextureSource);
                    break;

                case SDFMode.Blend:
                    TargetMaterial.SetTexture(DiffuseTexID, diffusionOutput);
                    TargetMaterial.SetTexture(SDFTexID, sdfTextureSource);
                    TargetMaterial.SetFloat(SDFBlendFactorID, _sdfBlendFactor);
                    break;
            }

            // 设置相机世界位置
            if (camera != null)
            {
                TargetMaterial.SetVector(CameraWorldPosID, new Vector4(camera.transform.position.x, camera.transform.position.y, 0, 0));
            }
        }

        /// <summary>
        /// 检查相机是否静止（位置和正交尺寸都未变化）
        /// </summary>
        private bool IsCameraStill(Camera camera)
        {
            if (camera == null) return false;

            if (!_cameraInitialized)
            {
                _lastCameraPosition = camera.transform.position;
                _lastCameraOrthoSize = camera.orthographicSize;
                _cameraInitialized = true;
                return true; // 第一帧视为静止
            }

            bool still = Vector3.SqrMagnitude(camera.transform.position - _lastCameraPosition) <= _cameraStillThreshold * _cameraStillThreshold &&
                         Mathf.Abs(camera.orthographicSize - _lastCameraOrthoSize) <= _cameraStillThreshold;

            // 更新上一帧状态
            _lastCameraPosition = camera.transform.position;
            _lastCameraOrthoSize = camera.orthographicSize;

            return still;
        }

        /// <summary>
        /// 强制重新初始化
        /// </summary>
        public void ForceReinitialize()
        {
            _isFirstFrame = true;
            _diffusionTimer = 0f;
            InitializeComputeShader();
            CreateRenderTextures();
            InitializeBuffers();
        }

        /// <summary>
        /// 检测任意鼠标按键是否按下（左键、右键、中键、侧键）
        /// </summary>
        private bool IsAnyMouseButtonPressed()
        {
            return Input.GetMouseButton(0) ||  // 左键
                   Input.GetMouseButton(1) ||  // 右键
                   Input.GetMouseButton(2) ||  // 中键
                   Input.GetMouseButton(3) ||  // Mouse4 (侧键 1)
                   Input.GetMouseButton(4);    // Mouse5 (侧键 2)
        }

        /// <summary>
        /// 设置梯度步长
        /// </summary>
        public void SetGradientStep(float step)
        {
            _gradientStep = Mathf.Clamp(step, 0.001f, 0.5f);
        }

        /// <summary>
        /// 设置注入强度
        /// </summary>
        public void SetInjectStrength(float strength)
        {
            _injectStrength = Mathf.Clamp(strength, 0.1f, 5.0f);
        }

        /// <summary>
        /// 调试：输出当前状态
        /// </summary>
        public void DebugLogCurrentState()
        {
            if (!IsInitialized)
            {
                Debug.Log("[DiffusionFieldBridge] 未初始化");
                return;
            }

            Debug.Log($"[DiffusionFieldBridge] 状态 - 纹理：{_textureWidth}x{_textureHeight}, " +
                     $"梯度步长：{_gradientStep:F3}, 注入强度：{_injectStrength:F2}, " +
                     $"BufferA 输入：{_useBufferAAsInput}");
        }

        private void OnDestroy()
        {
            ReleaseRenderTextures();
        }

        private void OnValidate()
        {
            // 参数范围限制
            _gradientStep = Mathf.Clamp(_gradientStep, 0.0001f, 0.5f);
            _diffusionSpeed = Mathf.Clamp(_diffusionSpeed, 1f, 480f);
            _injectStrength = Mathf.Clamp(_injectStrength, 0.1f, 5.0f);
            _injectRadius = Mathf.Clamp(_injectRadius, 0.01f, 0.5f);

            // 限制纹理最小尺寸
            _textureWidth = Mathf.Max(256, _textureWidth);
            _textureHeight = Mathf.Max(256, _textureHeight);
        }
    }
}
