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
        [SerializeField, Range(0.001f, 0.5f)] private float _gradientStep = 0.02f;

        [Tooltip("注入强度，节点处的最大高度")]
        [SerializeField, Range(0.1f, 5.0f)] private float _injectStrength = 1.0f;

        [Tooltip("注入半径（世界空间单位）")]
        [SerializeField, Range(0.01f, 0.5f)] private float _injectRadius = 0.05f;

        [Header("SDF 模式")]
        [Tooltip("启用 SDF 模式")]
        [SerializeField] private bool _enableSDF = false;

        [Tooltip("SDF 梯度斜率，越大下降越快")]
        [SerializeField, Range(0.01f, 1.0f)] private float _sdfGradient = 0.1f;

        [Tooltip("混合系数（0=纯扩散，1=纯 SDF）")]
        [SerializeField, Range(0f, 1f)] private float _sdfBlendFactor = 0.5f;

        [Tooltip("使用模式")]
        [SerializeField] private SDFMode _useSDFMode = SDFMode.DiffusionOnly;

        public enum SDFMode
        {
            DiffusionOnly,   // 仅扩散
            SDFOnly,         // 仅 SDF
            Blend            // 混合
        }

        [Header("纹理设置")]
        [Tooltip("模拟纹理宽度")]
        [SerializeField] private int _textureWidth = 512;

        [Tooltip("模拟纹理高度")]
        [SerializeField] private int _textureHeight = 512;

        [Header("调试")]
        [Tooltip("显示调试信息")]
        [SerializeField] private bool _showDebugInfo = false;

        // Kernel Handle
        private int _kernelDiffuse = -1;
        private int _kernelInject = -1;
        private int _kernelDiffuseInject = -1;
        private int _kernelInit = -1;
        private int _kernelSpatialSDF = -1;

        // Ping-Pong 缓冲区
        private RenderTexture _bufferA;
        private RenderTexture _bufferB;

        // SDF 缓冲区
        private RenderTexture _sdfBuffer;

        // 当前使用哪个 buffer 作为输入
        private bool _useBufferAAsInput = true;

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

        // SDF 参数 ID
        private static readonly int SDFBufferID = Shader.PropertyToID("_SDFBuffer");
        private static readonly int SDFGradientID = Shader.PropertyToID("_SDFGradient");
        private static readonly int UseSDFModeID = Shader.PropertyToID("_UseSDFMode");
        private static readonly int SDFBlendFactorID = Shader.PropertyToID("_SDFBlendFactor");
        private static readonly int SDFTexID = Shader.PropertyToID("_SDFTex");
        private static readonly int DiffuseTexID = Shader.PropertyToID("_DiffuseTex");
        private static readonly int CameraWorldPosID = Shader.PropertyToID("_CameraWorldPos");

        /// <summary>
        /// 当材质准备就绪时调用
        /// </summary>
        public override void OnMaterialReady(Material material)
        {
            base.OnMaterialReady(material);

            // 初始化 ComputeShader
            InitializeComputeShader();

            // 创建 RenderTexture
            CreateRenderTextures();

            // 初始化缓冲区
            InitializeBuffers();

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

            // 创建 SDF 缓冲区
            _sdfBuffer = CreateRenderTexture("SDFBuffer");

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
                _computeShader.SetVector(ScreenSizeID, new Vector2(_textureWidth, _textureHeight));
                _computeShader.SetTexture(_kernelSpatialSDF, SDFBufferID, _sdfBuffer);
                _computeShader.Dispatch(_kernelSpatialSDF,
                    Mathf.CeilToInt(_textureWidth / 8.0f),
                    Mathf.CeilToInt(_textureHeight / 8.0f), 1);
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

            // ==================== 设置 ComputeShader 参数 ====================

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
            _computeShader.SetVector(ScreenSizeID, new Vector4(_textureWidth, _textureHeight, 0, 0));  // uint2 用 Vector4 传递
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
            }

            // 时间
            _computeShader.SetFloat(TimeID, Time.time);

            // 梯度参数
            _computeShader.SetFloat(GradientStepID, _gradientStep);
            _computeShader.SetFloat(InjectStrengthID, _injectStrength);
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

            // 1. Dispatch 扩散模型（始终执行）
            _computeShader.Dispatch(_kernelDiffuseInject, threadGroupsX, threadGroupsY, 1);

            // 2. Dispatch SDF 模型（如果启用）
            bool useSDF = _enableSDF || _useSDFMode != SDFMode.DiffusionOnly;
            if (useSDF && _kernelSpatialSDF >= 0)
            {
                // 设置屏幕尺寸参数（SDF Kernel 需要）
                _computeShader.SetVector(ScreenSizeID, new Vector4(_textureWidth, _textureHeight, 0, 0));
                _computeShader.SetVector(ScreenSizeInvID, new Vector2(1.0f / _textureWidth, 1.0f / _textureHeight));

                // 设置节点数据（SDF Kernel 也需要，复用上面定义的 nodeBuffer）
                _computeShader.SetBuffer(_kernelSpatialSDF, NodesID, nodeBuffer);
                _computeShader.SetInt(NodeCountID, gpuManager.GetCurrentMapData()?.Nodes?.Count ?? 0);

                // 设置世界边界（SDF Kernel 需要）
                _computeShader.SetVector(WorldMinID, worldMin);
                _computeShader.SetVector(WorldMaxID, worldMax);

                // 设置 SDF 参数
                _computeShader.SetFloat(SDFGradientID, _sdfGradient);
                _computeShader.SetTexture(_kernelSpatialSDF, SDFBufferID, _sdfBuffer);
                _computeShader.Dispatch(_kernelSpatialSDF, threadGroupsX, threadGroupsY, 1);
            }

            // ==================== 交换 Ping-Pong 缓冲 ====================
            _useBufferAAsInput = !_useBufferAAsInput;

            // ==================== 根据模式选择输出纹理 ====================

            // 获取扩散输出纹理（Ping-Pong 的 output buffer）
            RenderTexture diffusionOutput = _useBufferAAsInput ? _bufferA : _bufferB;

            switch (_useSDFMode)
            {
                case SDFMode.DiffusionOnly:
                    // 仅扩散：使用扩散缓冲区
                    TargetMaterial.SetTexture("_EffectTex", diffusionOutput);
                    // SDF 纹理也需要设置（用于三角形采样）
                    TargetMaterial.SetTexture(SDFTexID, diffusionOutput);
                    break;

                case SDFMode.SDFOnly:
                    // 仅 SDF：使用 SDF 缓冲区
                    TargetMaterial.SetTexture("_EffectTex", _sdfBuffer);
                    // 关键修复：SDF 纹理也要设置，否则三角形采样不到数据！
                    TargetMaterial.SetTexture(SDFTexID, _sdfBuffer);
                    break;

                case SDFMode.Blend:
                    // 混合：把两个纹理都传给材质，让 Shader 自己混合
                    TargetMaterial.SetTexture(DiffuseTexID, diffusionOutput);
                    TargetMaterial.SetTexture(SDFTexID, _sdfBuffer);
                    TargetMaterial.SetFloat(SDFBlendFactorID, _sdfBlendFactor);
                    break;
            }

            // 设置相机世界位置（用于 DiffusionDisplay.shader 的世界空间对齐）
            if (camera != null)
            {
                TargetMaterial.SetVector(CameraWorldPosID, new Vector4(camera.transform.position.x, camera.transform.position.y, 0, 0));
            }

            // 调试输出
            if (_showDebugInfo && Time.frameCount % 60 == 0)
            {
                int nodeCount = gpuManager.GetCurrentMapData()?.Nodes?.Count ?? 0;
                Debug.Log($"<color=cyan>[DiffusionFieldBridge]</color> 已更新 - 节点：{nodeCount}, " +
                         $"梯度步长：{_gradientStep:F3}, 注入强度：{_injectStrength:F2}");
            }
        }

        /// <summary>
        /// 强制重新初始化
        /// </summary>
        public void ForceReinitialize()
        {
            InitializeComputeShader();
            CreateRenderTextures();
            InitializeBuffers();
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
            _gradientStep = Mathf.Clamp(_gradientStep, 0.001f, 0.5f);
            _injectStrength = Mathf.Clamp(_injectStrength, 0.1f, 5.0f);
            _injectRadius = Mathf.Clamp(_injectRadius, 0.01f, 0.5f);
        }
    }
}
