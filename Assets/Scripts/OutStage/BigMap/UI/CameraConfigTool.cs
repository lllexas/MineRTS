using UnityEngine;

namespace MineRTS.BigMap.UI
{
    /// <summary>
    /// Camera 快速配置工具
    /// 在 Inspector 中显示配置按钮
    /// </summary>
    [ExecuteInEditMode]
    public class CameraConfigTool : MonoBehaviour
    {
        [ContextMenu("设置为 UI Camera (Depth Only)")]
        public void SetupAsUICamera()
        {
            var camera = GetComponent<Camera>();
            if (camera == null)
            {
                Debug.LogError("未找到 Camera 组件！");
                return;
            }

            // 设置 Clear Flags 为 Depth Only
            camera.clearFlags = CameraClearFlags.Depth;

            // 设置优先级
            camera.depth = 1;

            // 只渲染 UI 层
            // camera.cullingMask = 1 << LayerMask.NameToLayer("UI");

            // 正交投影
            // camera.orthographic = true;

            Debug.Log($"<color=cyan>[CameraConfigTool]</color> 已配置为 UI Camera: {camera.name}");
        }

        [ContextMenu("设置为世界 Camera (Skybox)")]
        public void SetupAsWorldCamera()
        {
            var camera = GetComponent<Camera>();
            if (camera == null)
            {
                Debug.LogError("未找到 Camera 组件！");
                return;
            }

            // 设置 Clear Flags 为 Skybox
            camera.clearFlags = CameraClearFlags.Skybox;

            // 设置优先级
            camera.depth = 0;

            // 不渲染 UI 层
            camera.cullingMask = ~(1 << LayerMask.NameToLayer("UI"));

            Debug.Log($"<color=cyan>[CameraConfigTool]</color> 已配置为世界 Camera: {camera.name}");
        }

        [ContextMenu("打印当前相机配置")]
        public void PrintCameraConfig()
        {
            var camera = GetComponent<Camera>();
            if (camera == null) return;

            Debug.Log($"===== Camera 配置：{camera.name} =====");
            Debug.Log($"Priority: {camera.depth}");
            Debug.Log($"Clear Flags: {camera.clearFlags}");
            Debug.Log($"Culling Mask: {camera.cullingMask}");
            Debug.Log($"Orthographic: {camera.orthographic}");
            Debug.Log($"==========================================");
        }

        private void OnValidate()
        {
            // 在 Inspector 中显示提示信息
            Debug.Log("CameraConfigTool 已加载，右键点击组件名称选择配置选项");
        }
    }
}
