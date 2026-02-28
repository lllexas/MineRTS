using UnityEngine;
using UnityEngine.UIElements;

namespace MineRTS.BigMap
{
    /// <summary>
    /// 运行时节点元素 - 基于正交投影的单个节点可视化
    /// 职责：将世界空间坐标转换为局部空间坐标，实现硬几何平面风格
    /// 核心：Local_X = World.x * PPU, Local_Y = -World.y * PPU
    /// </summary>
    public class RuntimeNodeElement : VisualElement
    {
        // 节点数据
        private BigMapNodeData _nodeData;

        // 样式常量
        private const float NODE_SIZE = 20f;
        private const float BORDER_WIDTH = 2f;
        private const float LABEL_OFFSET = 25f;

        // 颜色定义（使用合法的颜色设置）
        private static readonly Color NORMAL_BACKGROUND_COLOR = new Color(0.2f, 0.4f, 0.8f, 1.0f); // 科技蓝
        private static readonly Color HOVER_BACKGROUND_COLOR = new Color(0.3f, 0.5f, 0.9f, 1.0f);  // 高亮蓝
        private static readonly Color BORDER_COLOR = Color.white;
        private static readonly Color TEXT_COLOR = Color.white;
        private static readonly Color DISABLED_COLOR = new Color(0.5f, 0.5f, 0.5f, 0.7f);

        // 子元素
        private Label _nameLabel;

        /// <summary>
        /// 节点数据（只读）
        /// </summary>
        public BigMapNodeData NodeData => _nodeData;

        /// <summary>
        /// 构造函数
        /// 公式：Local_X = World.x * PPU, Local_Y = -World.y * PPU
        /// </summary>
        /// <param name="nodeData">节点世界空间数据</param>
        /// <param name="ppu">像素每单位比例</param>
        public RuntimeNodeElement(BigMapNodeData nodeData, float ppu = 1.0f)
        {
            _nodeData = nodeData;

            // 设置基本样式（只使用合法API）
            SetupBaseStyle();

            // 应用局部空间坐标变换
            ApplyLocalPosition(ppu);

            // 添加文本标签（简化版本，不使用阴影）
            AddNameLabel();

            // 注册交互事件
            RegisterInteractionEvents();

            name = $"Node_{nodeData.DisplayName}_{nodeData.StageID.Substring(0, 8)}";

            Debug.Log($"RuntimeNodeElement: 节点创建完成 - {nodeData.DisplayName} (世界位置: {nodeData.Position})");
        }

        /// <summary>
        /// 应用局部空间坐标变换
        /// 公式：Local_X = World.x * PPU, Local_Y = -World.y * PPU
        /// </summary>
        private void ApplyLocalPosition(float ppu)
        {
            // 计算局部空间坐标（遵循正交投影公式）
            Vector2 localPosition = new Vector2(
                _nodeData.Position.x * ppu,    // Local_X = World.x * PPU
                -_nodeData.Position.y * ppu    // Local_Y = -World.y * PPU (抵消UI Y轴向下的差异)
            );

            // 设置绝对定位和位置（居中）
            style.position = Position.Absolute;
            style.left = localPosition.x - NODE_SIZE / 2;
            style.top = localPosition.y - NODE_SIZE / 2;
            style.width = NODE_SIZE;
            style.height = NODE_SIZE;
        }

        /// <summary>
        /// 设置基础样式（硬几何平面风格）- 只使用合法API
        /// </summary>
        private void SetupBaseStyle()
        {
            // 背景颜色
            style.backgroundColor = NORMAL_BACKGROUND_COLOR;

            // 边框样式：2px白色实线边框（合法API）
            style.borderTopWidth = BORDER_WIDTH;
            style.borderBottomWidth = BORDER_WIDTH;
            style.borderLeftWidth = BORDER_WIDTH;
            style.borderRightWidth = BORDER_WIDTH;

            style.borderTopColor = BORDER_COLOR;
            style.borderBottomColor = BORDER_COLOR;
            style.borderLeftColor = BORDER_COLOR;
            style.borderRightColor = BORDER_COLOR;

            // 圆角：轻微圆角（合法API）
            style.borderTopLeftRadius = 2f;
            style.borderTopRightRadius = 2f;
            style.borderBottomLeftRadius = 2f;
            style.borderBottomRightRadius = 2f;

            // 注意：不使用boxShadow、zIndex、textShadow等非法API
        }

        /// <summary>
        /// 添加名称标签（简化版本）
        /// </summary>
        private void AddNameLabel()
        {
            _nameLabel = new Label(_nodeData.DisplayName);

            // 标签样式：居中对齐，清晰文本
            _nameLabel.style.position = Position.Absolute;
            _nameLabel.style.top = LABEL_OFFSET;
            _nameLabel.style.left = -50f; // 临时值，会在布局完成后调整
            _nameLabel.style.width = 100f;
            _nameLabel.style.height = 20f;

            // 文本样式（基本属性，不使用阴影）
            _nameLabel.style.color = TEXT_COLOR;
            _nameLabel.style.fontSize = 12;
            _nameLabel.style.unityTextAlign = TextAnchor.UpperCenter;
            _nameLabel.style.whiteSpace = WhiteSpace.Normal;
            _nameLabel.style.textOverflow = TextOverflow.Ellipsis;

            // 注意：不使用textShadow，因为Runtime不支持
            // 替代方案：使用对比色或背景色增强可读性

            // 添加到节点
            Add(_nameLabel);

            // 在布局完成后调整标签位置（使其居中）
            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        /// <summary>
        /// 几何变化事件：调整标签位置使其居中
        /// </summary>
        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            // 计算标签位置使其在节点下方居中
            float labelWidth = _nameLabel.resolvedStyle.width;
            float nodeCenterX = layout.width / 2;
            float labelLeft = nodeCenterX - labelWidth / 2;

            _nameLabel.style.left = labelLeft;
        }

        /// <summary>
        /// 注册交互事件
        /// </summary>
        private void RegisterInteractionEvents()
        {
            // 鼠标悬停效果
            RegisterCallback<MouseEnterEvent>(OnMouseEnter);
            RegisterCallback<MouseLeaveEvent>(OnMouseLeave);

            // 点击事件
            RegisterCallback<ClickEvent>(OnClick);
        }

        /// <summary>
        /// 鼠标进入事件：高亮节点
        /// </summary>
        private void OnMouseEnter(MouseEnterEvent evt)
        {
            style.backgroundColor = HOVER_BACKGROUND_COLOR;
            // 增强边框颜色作为悬停反馈
            style.borderTopColor = new Color(1f, 1f, 1f, 0.9f);
            style.borderBottomColor = new Color(1f, 1f, 1f, 0.9f);
            style.borderLeftColor = new Color(1f, 1f, 1f, 0.9f);
            style.borderRightColor = new Color(1f, 1f, 1f, 0.9f);

            evt.StopPropagation();
        }

        /// <summary>
        /// 鼠标离开事件：恢复正常样式
        /// </summary>
        private void OnMouseLeave(MouseLeaveEvent evt)
        {
            style.backgroundColor = NORMAL_BACKGROUND_COLOR;
            style.borderTopColor = BORDER_COLOR;
            style.borderBottomColor = BORDER_COLOR;
            style.borderLeftColor = BORDER_COLOR;
            style.borderRightColor = BORDER_COLOR;

            evt.StopPropagation();
        }

        /// <summary>
        /// 点击事件：视觉反馈
        /// </summary>
        private void OnClick(ClickEvent evt)
        {
            Debug.Log($"RuntimeNodeElement: 节点 '{_nodeData.DisplayName}' 被点击 (世界位置: {_nodeData.Position})");

            // 添加点击动画效果（缩放）- 使用合法的scale API
            style.scale = new Scale(new Vector3(0.9f, 0.9f, 1f));

            // 延迟恢复
            schedule.Execute(() =>
            {
                style.scale = new Scale(Vector3.one);
            }).StartingIn(100); // 100ms后恢复

            evt.StopPropagation();
        }

        /// <summary>
        /// 更新节点位置（PPU变化时调用）
        /// 公式：Local_X = World.x * PPU, Local_Y = -World.y * PPU
        /// </summary>
        public void UpdatePosition(float ppu)
        {
            // 重新计算局部空间坐标
            Vector2 localPosition = new Vector2(
                _nodeData.Position.x * ppu,
                -_nodeData.Position.y * ppu
            );

            // 更新位置（居中）
            style.left = localPosition.x - NODE_SIZE / 2;
            style.top = localPosition.y - NODE_SIZE / 2;
        }

        /// <summary>
        /// 更新节点数据（动态更新）
        /// </summary>
        public void UpdateNodeData(BigMapNodeData newData, float ppu)
        {
            _nodeData = newData;

            // 更新位置
            UpdatePosition(ppu);

            // 更新标签文本
            if (_nameLabel != null)
            {
                _nameLabel.text = newData.DisplayName;
            }

            // 更新节点名称
            name = $"Node_{newData.DisplayName}_{newData.StageID.Substring(0, 8)}";
        }

        /// <summary>
        /// 设置节点选中状态
        /// </summary>
        public void SetSelected(bool selected)
        {
            if (selected)
            {
                // 选中样式：金色边框和背景
                Color selectedColor = new Color(1f, 0.9f, 0f, 1f); // 金色
                style.borderTopColor = selectedColor;
                style.borderBottomColor = selectedColor;
                style.borderLeftColor = selectedColor;
                style.borderRightColor = selectedColor;
                style.borderTopWidth = 3f;
                style.borderBottomWidth = 3f;
                style.borderLeftWidth = 3f;
                style.borderRightWidth = 3f;
            }
            else
            {
                // 恢复正常样式
                style.borderTopColor = BORDER_COLOR;
                style.borderBottomColor = BORDER_COLOR;
                style.borderLeftColor = BORDER_COLOR;
                style.borderRightColor = BORDER_COLOR;
                style.borderTopWidth = BORDER_WIDTH;
                style.borderBottomWidth = BORDER_WIDTH;
                style.borderLeftWidth = BORDER_WIDTH;
                style.borderRightWidth = BORDER_WIDTH;
            }
        }

        /// <summary>
        /// 设置节点激活状态（可点击）
        /// </summary>
        public void SetActive(bool active)
        {
            style.opacity = active ? 1.0f : 0.5f;

            if (!active)
            {
                // 非激活状态：灰色外观
                style.backgroundColor = DISABLED_COLOR;
            }
            else
            {
                style.backgroundColor = NORMAL_BACKGROUND_COLOR;
            }
        }

        /// <summary>
        /// 获取节点中心位置（局部空间坐标）
        /// </summary>
        public Vector2 GetLocalCenterPosition()
        {
            Rect layout = this.layout;
            return new Vector2(layout.x + layout.width / 2, layout.y + layout.height / 2);
        }

        /// <summary>
        /// 获取节点世界位置（只读）
        /// </summary>
        public Vector2 GetWorldPosition()
        {
            return _nodeData.Position;
        }
    }
}