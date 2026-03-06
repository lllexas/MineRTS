using UnityEngine;

namespace MineRTS.BigMap
{
    /// <summary>
    /// 节点控制器 - MonoBehaviour 版本
    /// 视觉：使用 SpriteRenderer 实现扁平化涂鸦风格（上下弓形 + 横向匾额）
    /// </summary>
    public class NodeController : MonoBehaviour
    {
        // 节点数据
        private BigMapNodeData _nodeData;

        // 组件引用
        [Header("视觉组件")]
        [SerializeField] private SpriteRenderer _upperArcRenderer;    // 上弓形
        [SerializeField] private SpriteRenderer _lowerArcRenderer;    // 下弓形
        [SerializeField] private SpriteRenderer _plaqueRenderer;      // 匾额背景
        [SerializeField] private TMPro.TextMeshPro _nameText;         // 节点名称

        // 配色方案（可在 Inspector 中配置）
        [Header("配色方案")]
        [Tooltip("圆形背景正常状态颜色")]
        [SerializeField] private Color 圆形正常颜色 = new Color(1f, 170f / 255f, 0f, 1f);  // 橙色

        [Tooltip("圆形背景悬停状态颜色")]
        [SerializeField] private Color 圆形悬停颜色 = new Color(0.2f, 0.2f, 0.25f, 1f);   // 亮灰

        [Tooltip("匾额背景正常状态颜色")]
        [SerializeField] private Color 匾额正常颜色 = new Color(0.95f, 0.95f, 0.95f, 1f);  // 近白

        [Tooltip("匾额背景悬停状态颜色")]
        [SerializeField] private Color 匾额悬停颜色 = Color.white;

        [Tooltip("文字颜色")]
        [SerializeField] private Color 文字颜色 = new Color(0.1f, 0.1f, 0.1f, 1f);  // 近黑

        [Tooltip("选中状态颜色（金色）")]
        [SerializeField] private Color 选中颜色 = new Color(1f, 0.9f, 0f, 1f);

        // 状态
        private bool _isHovered = false;
        private bool _isSelected = false;

        // 节点数据（只读）
        public BigMapNodeData NodeData => _nodeData;

        /// <summary>
        /// 初始化节点
        /// </summary>
        public void Init(BigMapNodeData data, float zPosition)
        {
            _nodeData = data;

            // 设置位置（世界坐标直接对应）
            transform.position = new Vector3(data.Position.x, data.Position.y, zPosition);

            // 设置名称
            if (_nameText != null)
            {
                _nameText.text = data.DisplayName;
            }

            // 设置对象名称（便于调试）
            gameObject.name = $"Node_{data.DisplayName}_{data.StageID.Substring(0, 8)}";

            // 应用初始颜色
            ApplyColors();
        }

        /// <summary>
        /// 应用颜色（根据状态）
        /// </summary>
        private void ApplyColors()
        {
            Color circleColor;
            Color plaqueColor;

            if (_isSelected)
            {
                // 选中状态：金色
                circleColor = 选中颜色;
                plaqueColor = 选中颜色;
            }
            else if (_isHovered)
            {
                // 悬停状态：亮色
                circleColor = 圆形悬停颜色;
                plaqueColor = 匾额悬停颜色;
            }
            else
            {
                // 正常状态
                circleColor = 圆形正常颜色;
                plaqueColor = 匾额正常颜色;
            }

            if (_upperArcRenderer != null)
                _upperArcRenderer.color = circleColor;

            if (_lowerArcRenderer != null)
                _lowerArcRenderer.color = circleColor;

            if (_plaqueRenderer != null)
                _plaqueRenderer.color = plaqueColor;

            // 设置 TMP Text 的文字颜色
            if (_nameText != null)
            {
                _nameText.color = 文字颜色;
            }
        }

        /// <summary>
        /// 鼠标进入事件
        /// </summary>
        private void OnMouseEnter()
        {
            _isHovered = true;
            ApplyColors();
        }

        /// <summary>
        /// 鼠标离开事件
        /// </summary>
        private void OnMouseExit()
        {
            _isHovered = false;
            ApplyColors();
        }

        /// <summary>
        /// 鼠标点击事件
        /// 通过 PostSystem 事件总线发送节点点击事件
        /// </summary>
        private void OnMouseDown()
        {
            Debug.Log($"NodeController: 节点 '{_nodeData.DisplayName}' 被点击 (世界位置：{_nodeData.Position})");

            // 创建事件数据
            var eventData = new NodeClickEvent
            {
                StageId = _nodeData.StageID,
                DisplayName = _nodeData.DisplayName
            };

            // 通过事件总线发送
            PostSystem.Instance.Send(BigMapEvents.NodeClicked, eventData);
        }

        /// <summary>
        /// 设置选中状态
        /// </summary>
        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            ApplyColors();
        }

        /// <summary>
        /// 获取节点 Transform（用于连线）
        /// </summary>
        public Transform GetTransform()
        {
            return transform;
        }
    }
}
