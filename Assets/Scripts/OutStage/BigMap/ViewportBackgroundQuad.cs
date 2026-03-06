using UnityEngine;
using System.Collections.Generic;

namespace MineRTS.BigMap
{
    /// <summary>
    /// 【桥接架构】视口背景 Quad 控制器 - 单例 + 桥接模式
    /// 职责：几何变换协调器 + 桥接管理器 + GPU 缓冲区同步
    /// 架构：
    ///   - 单例：全局唯一访问点
    ///   - 桥接：几何变换（父）与 Shader 逻辑（子）解耦
    ///   - 高性能：仅在相机参数变化时更新变换
    /// 特性：
    ///   - [ExecuteAlways] 支持编辑器实时预览
    ///   - 自动发现并管理子物体上的 Shader 桥接器
    ///   - 支持动态材质切换和桥接通知
    ///   - 集成 BigMapGPUBufferManager 的 GPU 缓冲区同步
    /// 设计模式：单例模式 + 桥接模式
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshRenderer))]
    public class ViewportBackgroundQuad : SingletonMono<ViewportBackgroundQuad>
    {
        /// <summary>
        /// 模式材质映射结构体
        /// </summary>
        [System.Serializable]
        public struct ModeMaterialMap
        {
            public ViewportMode mode;
            public Material material;
        }
        [Header("基础设置")]
        [Tooltip("Quad 在相机前的 Z 轴位置（深度），值越大越远")]
        [SerializeField] private float _zDepth = 10f;

        [Header("模式配置")]
        [Tooltip("当前激活的视口模式")]
        [SerializeField] private ViewportMode _currentMode = ViewportMode.DefaultGrid; // 修改这里

        [Tooltip("模式与材质的映射关系")]
        [SerializeField] private List<ModeMaterialMap> _modeMaterials = new List<ModeMaterialMap>();

        [Header("材质设置")]
        [Tooltip("启用则创建材质实例，禁用则直接使用原始材质资源（方便 Inspector 调整）")]
        [SerializeField] private bool _instantiateMaterial = true;

        [Header("调试")]
        [SerializeField] private bool _showDebugInfo = false;

        // 组件引用
        private MeshRenderer _meshRenderer;
        private Camera _targetCamera;

        // 桥接管理
        private List<ViewportShaderBridge> _shaderBridges;
        private bool _bridgesInitialized = false;

        // 材质管理
        private Material _currentMaterial;

        // 相机参数缓存（性能优化）
        private float _lastOrthographicSize;
        private float _lastAspect;

        /// <summary>
        /// 获取当前目标相机
        /// </summary>
        public Camera TargetCamera => _targetCamera;

        /// <summary>
        /// 获取或设置 Z 深度
        /// </summary>
        public float ZDepth
        {
            get => _zDepth;
            set
            {
                _zDepth = value;
                UpdateTransform();
            }
        }

        /// <summary>
        /// 获取当前使用的材质
        /// </summary>
        public Material CurrentMaterial => _currentMaterial;

        protected override void Awake()
        {
            // 调用基类 Awake（单例初始化）
            base.Awake();

            // 确保不跨场景保持（因为是相机子物体）
            DontDestroyOnLoadEnabled = false;

            // 获取组件引用
            _meshRenderer = GetComponent<MeshRenderer>();
            if (_meshRenderer == null)
            {
                Debug.LogError("ViewportBackgroundQuad: 需要 MeshRenderer 组件");
                enabled = false;
                return;
            }

            // 获取父物体的 Camera 组件
            _targetCamera = transform.parent?.GetComponent<Camera>();
            if (_targetCamera == null)
            {
                Debug.LogError("ViewportBackgroundQuad: 必须作为 Camera 的子物体挂载！");
                enabled = false;
                return;
            }

            if (!_targetCamera.orthographic)
            {
                Debug.LogWarning("ViewportBackgroundQuad: 目标相机不是正交投影，效果可能不正确");
            }

            // 初始化材质
            InitializeMaterial();

            // 初始化 Shader 桥接器
            InitializeShaderBridges();

            Debug.Log($"<color=cyan>[ViewportBackgroundQuad]</color> 初始化完成 - 目标相机：{_targetCamera.name}, Z 深度：{_zDepth}, 桥接器：{_shaderBridges?.Count ?? 0}个");

            // 应用初始模式
            ApplyMode(_currentMode);
        }

        /// <summary>
        /// 初始化 Shader 桥接器（搜集所有子物体上的桥接组件）
        /// </summary>
        private void InitializeShaderBridges()
        {
            // 初始化桥接列表
            _shaderBridges = new List<ViewportShaderBridge>();

            // 搜集所有子物体上的桥接器（包括非激活的）
            var bridges = GetComponentsInChildren<ViewportShaderBridge>(true);
            foreach (var bridge in bridges)
            {
                // 跳过自身（如果脚本错误地挂在自身）
                if (bridge.gameObject == gameObject)
                {
                    Debug.LogWarning("ViewportBackgroundQuad: 发现挂在自身的 Shader 桥接器，建议移到子物体上");
                    continue;
                }

                // 初始化桥接器
                bridge.InternalInitialize(this);
                _shaderBridges.Add(bridge);

                // 如果当前材质已存在，通知桥接器
                if (_currentMaterial != null)
                {
                    bridge.OnMaterialReady(_currentMaterial);
                }

                // 根据当前模式设置桥接器激活状态
                bool shouldBeActive = bridge.ActiveInModes.Contains(_currentMode);
                bridge.enabled = shouldBeActive;
            }

            _bridgesInitialized = true;
            Debug.Log($"<color=cyan>[ViewportBackgroundQuad]</color> Shader 桥接器初始化完成 - 找到 {_shaderBridges.Count} 个桥接器，当前模式：{_currentMode}");
        }

        /// <summary>
        /// 重新搜集桥接器（用于动态添加桥接器时）
        /// </summary>
        public void RefreshShaderBridges()
        {
            InitializeShaderBridges();
        }

        private void Start()
        {
            // 初始变换设置
            UpdateTransform();
        }

        private void OnValidate()
        {
            // 在 Inspector 中修改 Z 深度时立即更新
            if (Application.isPlaying || _targetCamera != null)
            {
                UpdateTransform();
            }

#if UNITY_EDITOR
            // 编辑器模式下验证模式材质配置
            ValidateModeMaterials();

            // 编辑器模式下使用延迟调用初始化桥接器（用于预览）
            // 这样可以避免 OnValidate 执行时对象处于"不稳定"状态的问题
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.delayCall -= InitializeEditorBridges;
                UnityEditor.EditorApplication.delayCall += InitializeEditorBridges;
            }
#endif
        }

#if UNITY_EDITOR
        /// <summary>
        /// 验证模式材质配置
        /// </summary>
        private void ValidateModeMaterials()
        {
            if (_modeMaterials == null) return;

            // 检查重复的模式配置
            var modeSet = new HashSet<ViewportMode>();
            var duplicateModes = new List<ViewportMode>();

            foreach (var map in _modeMaterials)
            {
                if (modeSet.Contains(map.mode))
                {
                    if (!duplicateModes.Contains(map.mode))
                    {
                        duplicateModes.Add(map.mode);
                    }
                }
                else
                {
                    modeSet.Add(map.mode);
                }
            }

            if (duplicateModes.Count > 0)
            {
                Debug.LogWarning($"<color=orange>[ViewportBackgroundQuad]</color> 发现重复的模式配置：{string.Join(", ", duplicateModes)}。将使用第一个匹配项。");
            }

            // 检查当前模式是否有材质配置（None 模式不需要材质）
            if (_currentMode == ViewportMode.None) return; // None 模式不需要材质检查

            bool hasCurrentModeMaterial = false;
            foreach (var map in _modeMaterials)
            {
                if (map.mode == _currentMode && map.material != null)
                {
                    hasCurrentModeMaterial = true;
                    break;
                }
            }

            if (!hasCurrentModeMaterial && _modeMaterials.Count > 0)
            {
                Debug.LogWarning($"<color=orange>[ViewportBackgroundQuad]</color> 当前模式 {_currentMode} 没有对应的材质配置。");
            }
        }

        /// <summary>
        /// 编辑器模式下应用模式（用于预览）
        /// </summary>
        private void ApplyModeInEditor()
        {
            if (this == null) return; // 防止对象在延迟期间被销毁

            if (_currentMode == ViewportMode.None)
            {
                if (_meshRenderer != null) _meshRenderer.enabled = false;

                // 禁用所有桥接器
                if (_bridgesInitialized && _shaderBridges != null)
                {
                    foreach (var bridge in _shaderBridges) bridge.enabled = false;
                }
                Debug.Log($"<color=yellow>[ViewportBackgroundQuad]</color> 编辑器模式 - 已预览模式：None (隐藏)");
                return;
            }

            // 如果不是 None 模式，确保 Renderer 开启
            if (_meshRenderer != null && !_meshRenderer.enabled)
            {
                _meshRenderer.enabled = true;
            }

            // 步骤 1：查找并应用对应材质（编辑器模式使用 sharedMaterial）
            Material targetMaterial = null;
            foreach (var map in _modeMaterials)
            {
                if (map.mode == _currentMode && map.material != null)
                {
                    targetMaterial = map.material;
                    break;
                }
            }

            // 如果找不到材质，隐藏 Quad（编辑器预览）
            if (targetMaterial == null)
            {
                Debug.LogWarning($"<color=orange>[ViewportBackgroundQuad]</color> 编辑器模式 - 模式 {_currentMode} 没有对应的材质配置，隐藏 Quad");
                if (_meshRenderer != null) _meshRenderer.enabled = false;
                if (_bridgesInitialized && _shaderBridges != null)
                {
                    foreach (var bridge in _shaderBridges) bridge.enabled = false;
                }
                return;
            }

            if (targetMaterial != null && _meshRenderer != null)
            {
                // 编辑器模式下直接使用共享材质（不创建实例）
                _meshRenderer.sharedMaterial = targetMaterial;
                _currentMaterial = targetMaterial;

                // 通知桥接器材质已更新
                if (_bridgesInitialized && _shaderBridges != null)
                {
                    foreach (var bridge in _shaderBridges)
                    {
                        try
                        {
                            bridge.OnMaterialReady(targetMaterial);
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogError($"<color=red>[ViewportBackgroundQuad]</color> 编辑器模式 - 桥接器材质通知失败 ({bridge.GetType().Name}): {ex.Message}");
                        }
                    }
                }
            }

            // 步骤 2：激活/禁用桥接器
            if (_bridgesInitialized && _shaderBridges != null)
            {
                foreach (var bridge in _shaderBridges)
                {
                    try
                    {
                        bool shouldBeActive = bridge.ActiveInModes.Contains(_currentMode);
                        bridge.enabled = shouldBeActive;
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"<color=red>[ViewportBackgroundQuad]</color> 编辑器模式 - 设置桥接器状态失败 ({bridge.GetType().Name}): {ex.Message}");
                    }
                }
            }

            Debug.Log($"<color=yellow>[ViewportBackgroundQuad]</color> 编辑器模式 - 已预览模式：{_currentMode}");
        }

        /// <summary>
        /// 编辑器模式下的桥接器初始化（用于预览）
        /// </summary>
        private void InitializeEditorBridges()
        {
            // 防止对象在延迟调用期间被销毁
            if (this == null) return;

            // 确保组件引用已获取
            if (_meshRenderer == null)
                _meshRenderer = GetComponent<MeshRenderer>();

            if (_targetCamera == null)
                _targetCamera = transform.parent?.GetComponent<Camera>();

            // 初始化桥接器（如果尚未初始化）
            if (_shaderBridges == null && _targetCamera != null)
            {
                _shaderBridges = new List<ViewportShaderBridge>();
                var bridges = GetComponentsInChildren<ViewportShaderBridge>(true);

                foreach (var bridge in bridges)
                {
                    if (bridge.gameObject == gameObject) continue;

                    // 模拟初始化
                    bridge.InternalInitialize(this);
                    _shaderBridges.Add(bridge);

                    // 设置当前材质（如果有）
                    if (_meshRenderer != null && _meshRenderer.sharedMaterial != null)
                    {
                        bridge.OnMaterialReady(_meshRenderer.sharedMaterial);
                    }

                    // 根据当前模式设置桥接器激活状态（编辑器预览）
                    bool shouldBeActive = bridge.ActiveInModes.Contains(_currentMode);
                    bridge.enabled = shouldBeActive;
                }

                if (_shaderBridges.Count > 0)
                {
                    Debug.Log($"<color=yellow>[ViewportBackgroundQuad]</color> 编辑器模式 - 发现 {_shaderBridges.Count} 个桥接器");
                }
            }

            // 延迟调用后应用当前模式（仅限编辑器预览）
            if (_bridgesInitialized && _shaderBridges != null)
            {
                ApplyModeInEditor();
            }
        }
#endif

        private void LateUpdate()
        {
            // 检测相机参数变化
            // if (_targetCamera == null) return;

            bool needsTransformUpdate = false;

            // 检查正交尺寸变化
            if (Mathf.Abs(_targetCamera.orthographicSize - _lastOrthographicSize) > 0.001f)
            {
                _lastOrthographicSize = _targetCamera.orthographicSize;
                needsTransformUpdate = true;
            }

            // 检查宽高比变化
            if (Mathf.Abs(_targetCamera.aspect - _lastAspect) > 0.001f)
            {
                _lastAspect = _targetCamera.aspect;
                needsTransformUpdate = true;
            }

            // 按需更新变换
            if (needsTransformUpdate)
            {
                UpdateTransform();
            }

            // 应用 GPU 缓冲区到材质
            ApplyGPUBuffersToMaterial();

            // 更新所有 Shader 桥接器
            UpdateShaderBridges();

            // 调试信息
            if (_showDebugInfo && Time.frameCount % 60 == 0)
            {
                Debug.Log($"<color=yellow>[ViewportBackgroundQuad]</color> 状态 - 正交尺寸：{_targetCamera.orthographicSize:F2}, 宽高比：{_targetCamera.aspect:F2}, " +
                         $"缩放：{transform.localScale.x:F2}x{transform.localScale.y:F2}, Z 深度：{_zDepth:F2}, 桥接器：{_shaderBridges?.Count ?? 0}个");
            }
        }

        /// <summary>
        /// 更新所有 Shader 桥接器（每帧调用）
        /// 只更新当前模式下激活的桥接器
        /// </summary>
        private void UpdateShaderBridges()
        {
            if (!_bridgesInitialized || _shaderBridges == null || _shaderBridges.Count == 0)
                return;

            foreach (var bridge in _shaderBridges)
            {
                // 只更新启用的桥接器
                if (!bridge.enabled) continue;

                try
                {
                    bridge.UpdateShaderProperties();
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"<color=red>[ViewportBackgroundQuad]</color> 桥接器更新失败 ({bridge.GetType().Name}): {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 更新 Quad 的变换以完全覆盖相机视口
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

            // 确保 Quad 在相机前方指定深度
            transform.localPosition = new Vector3(0f, 0f, _zDepth);
        }

        /// <summary>
        /// 初始化材质
        /// </summary>
        private void InitializeMaterial()
        {
            // 首先检查模式材质映射中是否有当前模式的材质
            Material modeMaterial = null;
            if (_modeMaterials != null && _modeMaterials.Count > 0)
            {
                foreach (var map in _modeMaterials)
                {
                    if (map.mode == _currentMode && map.material != null)
                    {
                        modeMaterial = map.material;
                        break;
                    }
                }
            }

            if (modeMaterial != null)
            {
                // 根据开关决定是否创建实例
                if (_instantiateMaterial)
                {
                    _currentMaterial = new Material(modeMaterial);
                    _currentMaterial.name = $"{modeMaterial.name}_Instance";
                }
                else
                {
                    _currentMaterial = modeMaterial; // 直接使用原始材质
                }
                _meshRenderer.material = _currentMaterial;
                Debug.Log($"<color=cyan>[ViewportBackgroundQuad]</color> 使用模式配置材质，模式：{_currentMode}, Shader: {modeMaterial.shader?.name}, 实例化：{_instantiateMaterial}");
                return;
            }

            // 如果没有模式配置，检查当前是否有材质
            Material existingMaterial = _meshRenderer.sharedMaterial;

            if (existingMaterial == null)
            {
                // 尝试创建默认材质
                Shader gridShader = Shader.Find("MineRTS/InfiniteWorldGrid");
                if (gridShader != null)
                {
                    if (_instantiateMaterial)
                    {
                        existingMaterial = new Material(gridShader);
                        existingMaterial.name = $"{gridShader.name}_Instance";
                    }
                    else
                    {
                        // 创建临时材质用于测试
                        existingMaterial = new Material(gridShader);
                        existingMaterial.name = gridShader.name;
                    }
                    Debug.Log($"<color=cyan>[ViewportBackgroundQuad]</color> 创建了默认材质，Shader: {gridShader.name}");
                }
                else
                {
                    Debug.LogError("ViewportBackgroundQuad: 找不到'MineRTS/InfiniteWorldGrid' Shader，请确保 Shader 已正确导入");

                    // 尝试使用备用 Shader
                    Shader fallbackShader = Shader.Find("Unlit/Transparent");
                    if (fallbackShader != null)
                    {
                        if (_instantiateMaterial)
                        {
                            existingMaterial = new Material(fallbackShader);
                            existingMaterial.color = new Color(0.1f, 0.2f, 0.5f, 0.3f); // 测试颜色
                            existingMaterial.name = "FallbackMaterial_Instance";
                        }
                        else
                        {
                            existingMaterial = new Material(fallbackShader);
                            existingMaterial.color = new Color(0.1f, 0.2f, 0.5f, 0.3f);
                            existingMaterial.name = "FallbackMaterial";
                        }
                        Debug.LogWarning("ViewportBackgroundQuad: 使用备用 Shader Unlit/Transparent");
                    }
                    else
                    {
                        Debug.LogError("ViewportBackgroundQuad: 也找不到备用 Shader");
                        enabled = false;
                        return;
                    }
                }
            }
            else
            {
                Debug.Log($"<color=cyan>[ViewportBackgroundQuad]</color> 使用现有材质，Shader: {existingMaterial.shader?.name}");
            }

            // 设置材质
            _currentMaterial = existingMaterial;
            _meshRenderer.material = _currentMaterial;
        }

        /// <summary>
        /// 将 GPU 缓冲区应用到材质
        /// </summary>
        private void ApplyGPUBuffersToMaterial()
        {
            if (_currentMaterial == null) return;

            // 如果 GPU 缓冲区管理器存在，将缓冲区应用到材质
            if (BigMapGPUBufferManager.Instance != null)
            {
                if (BigMapGPUBufferManager.Instance.AreBuffersInitialized())
                {
                    BigMapGPUBufferManager.Instance.ApplyBuffersToMaterial(_currentMaterial);
                    // 仅在调试时输出日志
                    if (_showDebugInfo && Time.frameCount % 300 == 0)
                    {
                        Debug.Log("<color=cyan>[ViewportBackgroundQuad]</color> GPU 缓冲区已应用到材质");
                    }
                }
                else
                {
                    if (_showDebugInfo && Time.frameCount % 300 == 0)
                    {
                        Debug.LogWarning("<color=orange>[ViewportBackgroundQuad]</color> GPU 缓冲区未初始化，跳过应用");
                    }
                }
            }
            else
            {
                if (_showDebugInfo && Time.frameCount % 300 == 0)
                {
                    Debug.LogWarning("<color=orange>[ViewportBackgroundQuad]</color> BigMapGPUBufferManager 实例未找到，无法应用 GPU 缓冲区");
                }
            }
        }

        /// <summary>
        /// 应用指定视口模式
        /// 步骤：
        /// 1. 从_modeMaterials 里根据 mode 找到对应的材质并应用
        /// 2. 遍历所有_shaderBridges，检查 ActiveInModes 列表，启用/禁用桥接器
        /// 3. 调用 NotifyBridgesMaterialChanged
        /// </summary>
        /// <param name="mode">要应用的视口模式</param>
        public void ApplyMode(ViewportMode mode)
        {
            _currentMode = mode;

            // 1. 处理 None 模式（隐藏 Quad）
            if (mode == ViewportMode.None)
            {
                if (_meshRenderer != null) _meshRenderer.enabled = false;

                // 禁用所有桥接器
                if (_bridgesInitialized && _shaderBridges != null)
                {
                    foreach (var bridge in _shaderBridges) bridge.enabled = false;
                }
                Debug.Log($"<color=cyan>[ViewportBackgroundQuad]</color> 已切换到模式：None (隐藏背景)");
                return;
            }

            // 2. 恢复渲染状态（从 None 切换回来时需要）
            if (_meshRenderer != null && !_meshRenderer.enabled)
            {
                _meshRenderer.enabled = true;
            }

            // 3. 查找并应用对应材质
            Material targetMaterial = null;
            foreach (var map in _modeMaterials)
            {
                if (map.mode == mode && map.material != null)
                {
                    targetMaterial = map.material;
                    break;
                }
            }

            if (targetMaterial == null)
            {
                Debug.LogWarning($"<color=orange>[ViewportBackgroundQuad]</color> 模式 {mode} 没有对应的材质配置，隐藏 Quad");
                if (_meshRenderer != null) _meshRenderer.enabled = false;
                if (_bridgesInitialized && _shaderBridges != null)
                {
                    foreach (var bridge in _shaderBridges) bridge.enabled = false;
                }
                Debug.Log($"<color=cyan>[ViewportBackgroundQuad]</color> 模式设置为 {mode}，但 Quad 被隐藏（无材质配置）");
                return;
            }
            else
            {
                SwitchMaterial(targetMaterial);
            }

            // 4. 激活/禁用桥接器
            if (_bridgesInitialized && _shaderBridges != null)
            {
                foreach (var bridge in _shaderBridges)
                {
                    try
                    {
                        bool shouldBeActive = bridge.ActiveInModes.Contains(mode);
                        bridge.enabled = shouldBeActive;

                        if (_showDebugInfo)
                        {
                            Debug.Log($"<color=cyan>[ViewportBackgroundQuad]</color> 桥接器 {bridge.GetType().Name} 已{(shouldBeActive ? "激活" : "禁用")} (模式：{mode})");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"<color=red>[ViewportBackgroundQuad]</color> 设置桥接器状态失败 ({bridge.GetType().Name}): {ex.Message}");
                    }
                }
            }

            Debug.Log($"<color=cyan>[ViewportBackgroundQuad]</color> 已切换到模式：{mode}");
        }

        /// <summary>
        /// 获取当前视口模式
        /// </summary>
        public ViewportMode GetCurrentMode() => _currentMode;

        /// <summary>
        /// 设置视口模式（快捷方法）
        /// </summary>
        /// <param name="mode">新模式</param>
        public void SetMode(ViewportMode mode) => ApplyMode(mode);

        /// <summary>
        /// 获取所有可用模式（基于配置的材质映射）
        /// </summary>
        public List<ViewportMode> GetAvailableModes()
        {
            var modes = new List<ViewportMode>();
            if (_modeMaterials != null)
            {
                foreach (var map in _modeMaterials)
                {
                    if (map.material != null && !modes.Contains(map.mode))
                    {
                        modes.Add(map.mode);
                    }
                }
            }
            return modes;
        }

        /// <summary>
        /// 检查指定模式是否有材质配置
        /// </summary>
        public bool HasMaterialForMode(ViewportMode mode)
        {
            if (_modeMaterials == null) return false;

            foreach (var map in _modeMaterials)
            {
                if (map.mode == mode && map.material != null)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 添加或更新模式材质配置
        /// </summary>
        public void SetMaterialForMode(ViewportMode mode, Material material)
        {
            if (_modeMaterials == null)
                _modeMaterials = new List<ModeMaterialMap>();

            // 查找是否已存在该模式的配置
            for (int i = 0; i < _modeMaterials.Count; i++)
            {
                if (_modeMaterials[i].mode == mode)
                {
                    // 更新现有配置
                    var map = _modeMaterials[i];
                    map.material = material;
                    _modeMaterials[i] = map;
                    return;
                }
            }

            // 添加新配置
            _modeMaterials.Add(new ModeMaterialMap { mode = mode, material = material });
        }

        /// <summary>
        /// 切换材质（支持动态更换材质）
        /// 切换后会通知所有 Shader 桥接器材质已更新
        /// </summary>
        /// <param name="newMaterial">新材质</param>
        public void SwitchMaterial(Material newMaterial)
        {
            if (newMaterial == null)
            {
                Debug.LogError("ViewportBackgroundQuad: 新材质不能为空");
                return;
            }

            // 根据开关决定是否创建实例
            if (_instantiateMaterial)
            {
                Material materialInstance = new Material(newMaterial);
                materialInstance.name = $"{newMaterial.name}_Instance";
                _currentMaterial = materialInstance;
            }
            else
            {
                _currentMaterial = newMaterial; // 直接使用原始材质
            }

            _meshRenderer.material = _currentMaterial;

            Debug.Log($"<color=cyan>[ViewportBackgroundQuad]</color> 材质已切换 - Shader: {newMaterial.shader?.name}, 实例化：{_instantiateMaterial}");

            // 通知所有 Shader 桥接器材质已更新
            NotifyBridgesMaterialChanged(_currentMaterial);
        }

        /// <summary>
        /// 通知所有桥接器材质已变更
        /// </summary>
        private void NotifyBridgesMaterialChanged(Material newMaterial)
        {
            if (!_bridgesInitialized || _shaderBridges == null || _shaderBridges.Count == 0)
                return;

            foreach (var bridge in _shaderBridges)
            {
                try
                {
                    bridge.OnMaterialReady(newMaterial);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"<color=red>[ViewportBackgroundQuad]</color> 桥接器材质通知失败 ({bridge.GetType().Name}): {ex.Message}");
                }
            }

            Debug.Log($"<color=cyan>[ViewportBackgroundQuad]</color> 已通知 {_shaderBridges.Count} 个桥接器材质更新");
        }

        /// <summary>
        /// 强制立即更新变换（例如在外部修改相机参数后）
        /// </summary>
        public void ForceUpdateTransform()
        {
            if (_targetCamera != null)
            {
                _lastOrthographicSize = _targetCamera.orthographicSize;
                _lastAspect = _targetCamera.aspect;
                UpdateTransform();
            }
        }

        /// <summary>
        /// 设置 Z 深度并立即更新变换
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
        /// 检查当前相机是否为有效目标
        /// </summary>
        public bool IsTargetCameraValid()
        {
            return _targetCamera != null && _targetCamera.orthographic;
        }

        /// <summary>
        /// 获取当前视口尺寸（世界单位）
        /// 返回：Vector2(宽度，高度)
        /// </summary>
        public Vector2 GetViewportWorldSize()
        {
            if (_targetCamera == null) return Vector2.zero;

            float height = _targetCamera.orthographicSize * 2f;
            float width = height * _targetCamera.aspect;
            return new Vector2(width, height);
        }

        /// <summary>
        /// 获取当前材质（用于动态设置 Shader 属性）
        /// </summary>
        public Material GetQuadMaterial()
        {
            return _currentMaterial;
        }

        /// <summary>
        /// 获取当前相机的世界位置
        /// </summary>
        public Vector2 GetCameraWorldPosition()
        {
            if (_targetCamera != null)
            {
                return _targetCamera.transform.position;
            }
            return Vector2.zero;
        }

        /// <summary>
        /// 【示例代码】相机属性 Shader 桥接器
        /// 使用方法：
        /// 1. 创建新脚本继承 ViewportShaderBridge
        /// 2. 实现 OnMaterialReady 和 UpdateShaderProperties 方法
        /// 3. 将脚本挂在 ViewportBackgroundQuad 的子物体上
        /// 4. 系统会自动发现并管理
        ///
        /// 示例实现：
        /// public class CameraPropertiesShaderBridge : ViewportShaderBridge
        /// {
        ///     public override void OnMaterialReady(Material material)
        ///     {
        ///         base.OnMaterialReady(material);
        ///         Debug.Log($"相机属性桥接器已连接到材质：{material.shader?.name}");
        ///     }
        ///
        ///     public override void UpdateShaderProperties()
        ///     {
        ///         if (!CanUpdate()) return;
        ///
        ///         var camera = GetTargetCamera();
        ///         var material = GetCurrentMaterial();
        ///
        ///         // 设置相机参数到 Shader
        ///         if (material.HasProperty("_CameraOrthoSize"))
        ///             material.SetFloat("_CameraOrthoSize", camera.orthographicSize);
        ///
        ///         if (material.HasProperty("_CameraAspect"))
        ///             material.SetFloat("_CameraAspect", camera.aspect);
        ///
        ///         if (material.HasProperty("_CameraWorldPos"))
        ///         {
        ///             Vector3 cameraPos = camera.transform.position;
        ///             material.SetVector("_CameraWorldPos", new Vector4(cameraPos.x, cameraPos.y, cameraPos.z, 0));
        ///         }
        ///     }
        /// }
        /// </summary>
        /// <remarks>
        /// 注意：上述代码仅为示例，实际使用时请创建独立的脚本文件
        /// </remarks>
        public void ExampleShaderBridgeUsage()
        {
            // 此方法仅为文档说明，无实际功能
            Debug.Log("请参考上述注释创建自定义 Shader 桥接器");
        }
    }
}
