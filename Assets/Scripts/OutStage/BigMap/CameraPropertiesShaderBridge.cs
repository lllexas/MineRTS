using UnityEngine;

namespace MineRTS.BigMap
{
    /// <summary>
    /// 【示例实现】相机属性Shader桥接器
    /// 功能：将相机的正交尺寸、宽高比和世界位置同步到Shader属性
    /// 使用方式：将此脚本挂在ViewportBackgroundQuad的子物体上
    /// 对应的Shader需要以下属性：
    ///   _CameraOrthoSize (Float) - 相机正交尺寸
    ///   _CameraAspect (Float) - 相机宽高比
    ///   _CameraWorldPos (Vector) - 相机世界位置
    /// 设计模式：桥接模式的具体实现，专注特定Shader的参数同步
    /// </summary>
    public class CameraPropertiesShaderBridge : ViewportShaderBridge
    {
        [Header("调试选项")]
        [SerializeField] private bool _logPropertyUpdates = false;

        // 上次的相机参数（用于优化，避免每帧设置相同值）
        private float _lastOrthoSize = -1f;
        private float _lastAspect = -1f;
        private Vector3 _lastCameraPosition = Vector3.negativeInfinity;

        /// <summary>
        /// 当材质准备就绪时调用
        /// </summary>
        public override void OnMaterialReady(Material material)
        {
            base.OnMaterialReady(material);

            if (_logPropertyUpdates)
            {
                Debug.Log($"<color=cyan>[CameraPropertiesShaderBridge]</color> 材质已连接 - Shader: {material.shader?.name}");
            }

            // 重置缓存，确保下一帧更新所有属性
            _lastOrthoSize = -1f;
            _lastAspect = -1f;
            _lastCameraPosition = Vector3.negativeInfinity;
        }

        /// <summary>
        /// 更新Shader属性（每帧调用）
        /// </summary>
        public override void UpdateShaderProperties()
        {
            if (!CanUpdate()) return;

            var camera = GetTargetCamera();
            var material = GetCurrentMaterial();

            if (camera == null || material == null) return;

            bool needsUpdate = false;

            // 检查相机参数变化
            if (Mathf.Abs(camera.orthographicSize - _lastOrthoSize) > 0.001f)
            {
                _lastOrthoSize = camera.orthographicSize;
                needsUpdate = true;
            }

            if (Mathf.Abs(camera.aspect - _lastAspect) > 0.001f)
            {
                _lastAspect = camera.aspect;
                needsUpdate = true;
            }

            if (Vector3.Distance(camera.transform.position, _lastCameraPosition) > 0.001f)
            {
                _lastCameraPosition = camera.transform.position;
                needsUpdate = true;
            }

            // 如果参数有变化，更新Shader属性
            if (needsUpdate)
            {
                UpdateShaderPropertiesInternal(camera, material);
            }
        }

        /// <summary>
        /// 实际更新Shader属性的内部方法
        /// </summary>
        private void UpdateShaderPropertiesInternal(Camera camera, Material material)
        {
            // 设置相机正交尺寸
            if (material.HasProperty("_CameraOrthoSize"))
            {
                material.SetFloat("_CameraOrthoSize", camera.orthographicSize);
            }

            // 设置相机宽高比
            if (material.HasProperty("_CameraAspect"))
            {
                material.SetFloat("_CameraAspect", camera.aspect);
            }

            // 设置相机世界位置
            if (material.HasProperty("_CameraWorldPos"))
            {
                Vector3 cameraPos = camera.transform.position;
                material.SetVector("_CameraWorldPos", new Vector4(cameraPos.x, cameraPos.y, cameraPos.z, 0));
            }

            // 调试输出
            if (_logPropertyUpdates && Time.frameCount % 60 == 0)
            {
                string properties = "";
                if (material.HasProperty("_CameraOrthoSize")) properties += $" 尺寸: {camera.orthographicSize:F2}";
                if (material.HasProperty("_CameraAspect")) properties += $" 宽高比: {camera.aspect:F2}";
                if (material.HasProperty("_CameraWorldPos")) properties += $" 位置: {camera.transform.position:F2}";

                Debug.Log($"<color=yellow>[CameraPropertiesShaderBridge]</color> Shader属性已更新{properties}");
            }
        }

        /// <summary>
        /// 强制立即更新所有Shader属性（忽略缓存）
        /// </summary>
        public void ForceUpdateProperties()
        {
            if (!CanUpdate()) return;

            var camera = GetTargetCamera();
            var material = GetCurrentMaterial();

            if (camera == null || material == null) return;

            _lastOrthoSize = -1f;
            _lastAspect = -1f;
            _lastCameraPosition = Vector3.negativeInfinity;

            UpdateShaderPropertiesInternal(camera, material);

            Debug.Log("<color=cyan>[CameraPropertiesShaderBridge]</color> 强制更新Shader属性");
        }

        /// <summary>
        /// 调试：输出当前桥接器状态
        /// </summary>
        public void DebugLogCurrentState()
        {
            if (!IsInitialized)
            {
                Debug.Log("[CameraPropertiesShaderBridge] 未初始化");
                return;
            }

            var camera = GetTargetCamera();
            var material = GetCurrentMaterial();

            string cameraInfo = camera != null ?
                $"相机: {camera.name} (正交: {camera.orthographicSize:F2}, 宽高比: {camera.aspect:F2})" :
                "相机: 无";

            string materialInfo = material != null ?
                $"材质: {material.name} (Shader: {material.shader?.name})" :
                "材质: 无";

            string propertyInfo = "";
            if (material != null)
            {
                if (material.HasProperty("_CameraOrthoSize")) propertyInfo += " [有 _CameraOrthoSize]";
                if (material.HasProperty("_CameraAspect")) propertyInfo += " [有 _CameraAspect]";
                if (material.HasProperty("_CameraWorldPos")) propertyInfo += " [有 _CameraWorldPos]";
                if (string.IsNullOrEmpty(propertyInfo)) propertyInfo = " [无相机属性]";
            }

            Debug.Log($"[CameraPropertiesShaderBridge] 状态 - {cameraInfo}, {materialInfo}{propertyInfo}");
        }
    }
}