using UnityEngine;
using System.Collections.Generic;

namespace MineRTS.BigMap
{
    /// <summary>
    /// 视口模式枚举 - 定义不同的渲染模式
    /// </summary>
    public enum ViewportMode
    {
        /// <summary>无模式 - 此模式下隐藏quad</summary>
        None,
        /// <summary>基础模式 - 基础正交网格渲染</summary>
        DefaultGrid,
        /// <summary>大地图模式 - 炫酷底板扩散梯度图</summary>
        BigMap,
        /// <summary>主菜单模式 - 还没想好怎么写</summary>
        MainMenu
    }

    /// <summary>
    /// 【抽象基类】视口Shader桥接器
    /// 职责：将ViewportBackgroundQuad的几何信息同步到特定Shader的材质属性
    /// 架构：作为ViewportBackgroundQuad子物体上的组件，专注特定Shader的参数填充
    /// 设计模式：桥接模式（Bridge Pattern），解耦几何变换与Shader逻辑
    /// 使用方式：
    ///   1. 继承此类并实现抽象方法
    ///   2. 将脚本挂在ViewportBackgroundQuad的子物体上
    ///   3. 父Quad会自动发现并管理所有桥接器
    /// </summary>
    public abstract class ViewportShaderBridge : MonoBehaviour
    {
        /// <summary>
        /// 父Quad引用（由ViewportBackgroundQuad自动设置）
        /// </summary>
        protected ViewportBackgroundQuad ParentQuad { get; private set; }

        /// <summary>
        /// 目标材质引用（当前使用的材质实例）
        /// </summary>
        protected Material TargetMaterial { get; private set; }

        [Header("模式激活配置")]
        [Tooltip("此桥接器在哪些视口模式下激活")]
        [SerializeField] private List<ViewportMode> _activeInModes = new List<ViewportMode> { ViewportMode.None };

        /// <summary>
        /// 获取激活模式列表（只读）
        /// </summary>
        public List<ViewportMode> ActiveInModes => _activeInModes;

        /// <summary>
        /// 是否已初始化
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// 内部初始化方法（由ViewportBackgroundQuad调用）
        /// </summary>
        internal void InternalInitialize(ViewportBackgroundQuad parent)
        {
            if (parent == null)
            {
                Debug.LogError("ViewportShaderBridge: 父Quad不能为空");
                return;
            }

            ParentQuad = parent;
            IsInitialized = true;

            Debug.Log($"<color=cyan>[ViewportShaderBridge]</color> {GetType().Name} 已初始化，父对象: {parent.name}");
        }

        /// <summary>
        /// 当材质准备就绪时调用（由ViewportBackgroundQuad在材质切换后触发）
        /// </summary>
        /// <param name="material">新的材质实例</param>
        public virtual void OnMaterialReady(Material material)
        {
            if (material == null)
            {
                Debug.LogError($"ViewportShaderBridge({GetType().Name}): 材质不能为空");
                return;
            }

            TargetMaterial = material;
            Debug.Log($"<color=cyan>[ViewportShaderBridge]</color> {GetType().Name} 材质已更新，Shader: {material.shader?.name}");
        }

        /// <summary>
        /// 更新Shader属性（每帧调用）
        /// 子类应在此方法中设置特定Shader所需的材质属性
        /// 例如：相机位置、网格密度、颜色参数等
        /// </summary>
        public abstract void UpdateShaderProperties();

        /// <summary>
        /// 获取当前目标相机（快捷方法）
        /// </summary>
        protected Camera GetTargetCamera()
        {
            return ParentQuad?.TargetCamera;
        }

        /// <summary>
        /// 获取当前材质（快捷方法）
        /// </summary>
        protected Material GetCurrentMaterial()
        {
            return TargetMaterial;
        }

        /// <summary>
        /// 检查是否具备运行条件
        /// </summary>
        protected virtual bool CanUpdate()
        {
            return IsInitialized && ParentQuad != null && TargetMaterial != null;
        }

        /// <summary>
        /// 调试：输出桥接器状态信息
        /// </summary>
        public virtual void DebugLogStatus()
        {
            if (!IsInitialized)
            {
                Debug.Log($"[ViewportShaderBridge] {GetType().Name} - 未初始化");
                return;
            }

            string cameraInfo = ParentQuad?.TargetCamera != null ?
                $"相机: {ParentQuad.TargetCamera.name} (正交: {ParentQuad.TargetCamera.orthographicSize:F2})" :
                "相机: 无";

            string materialInfo = TargetMaterial != null ?
                $"材质: {TargetMaterial.name} (Shader: {TargetMaterial.shader?.name})" :
                "材质: 无";

            Debug.Log($"[ViewportShaderBridge] {GetType().Name} - {cameraInfo}, {materialInfo}");
        }

        /// <summary>
        /// 编辑器模式下的快速验证
        /// </summary>
        private void OnValidate()
        {
#if UNITY_EDITOR
            // 在编辑器模式下，如果父对象存在但未初始化，尝试自动初始化
            if (!Application.isPlaying && !IsInitialized)
            {
                var parent = GetComponentInParent<ViewportBackgroundQuad>();
                if (parent != null)
                {
                    // 模拟初始化（编辑器预览用）
                    ParentQuad = parent;
                    Debug.Log($"<color=yellow>[ViewportShaderBridge]</color> 编辑器模式 - {GetType().Name} 已连接到父Quad");
                }
            }
#endif
        }
    }
}