using UnityEngine;
using UnityEngine.UIElements;

namespace MineRTS.BigMap
{
    /// <summary>
    /// 【已废弃】运行时节点元素 - UI Toolkit 版本
    /// 架构：上下弓形（圆形被匾额截断）+ 横向匾额
    /// 风格：平面设计，纯色填充，无描边
    /// 
    /// 注意：此类已废弃，请使用 NodeController（GameObject 版本）
    /// </summary>
    [System.Obsolete("RuntimeNodeElement 已废弃，请使用 NodeController（GameObject 版本）")]
    public class RuntimeNodeElement : VisualElement
    {
        // 节点数据
        private BigMapNodeData _nodeData;

        // 组件引用
        private VisualElement _labelPlaque;    // 文字匾额
        private Label _nameLabel;              // 文字标签

        // 尺寸常量
        private const float CIRCLE_DIAMETER = 80f;
        private const float CIRCLE_RADIUS = 40f;
        private const float PLAQUE_WIDTH = 120f;
        private const float PLAQUE_HEIGHT = 30f;

        // 颜色定义（扁平化涂鸦风格 - 严肃风格）
        private static readonly Color CIRCLE_COLOR = new Color(0.15f, 0.15f, 0.18f, 1f);       // 深灰藏青 #26262E
        private static readonly Color CIRCLE_HOVER_COLOR = new Color(0.2f, 0.2f, 0.25f, 1f);   // 亮灰 #333340
        private static readonly Color PLAQUE_BACKGROUND_COLOR = new Color(0.95f, 0.95f, 0.95f, 1f); // 近白 #F2F2F2
        private static readonly Color PLAQUE_HOVER_COLOR = new Color(1f, 1f, 1f, 1f);          // 纯白
        private static readonly Color TEXT_COLOR = new Color(0.1f, 0.1f, 0.1f, 1f);            // 近黑 #1A1A1A
        private static readonly Color DISABLED_COLOR = new Color(0.5f, 0.5f, 0.5f, 0.5f);

        // 悬停状态
        private bool _isHovered = false;
        
        // 选中状态
        private bool _isSelected = false;

        /// <summary>
        /// 节点数据（只读）
        /// </summary>
        public BigMapNodeData NodeData => _nodeData;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="nodeData">节点世界空间数据</param>
        /// <param name="ppu">像素每单位比例</param>
        public RuntimeNodeElement(BigMapNodeData nodeData, float ppu = 1.0f)
        {
            _nodeData = nodeData;

            // 设置节点尺寸
            style.width = CIRCLE_DIAMETER;
            style.height = CIRCLE_DIAMETER + PLAQUE_HEIGHT;

            // 创建匾额
            CreateLabelPlaque();

            // 注册绘制回调（绘制半圆）
            generateVisualContent += OnGenerateVisualContent;

            // 应用局部空间坐标变换
            ApplyLocalPosition(ppu);

            // 注册交互事件
            RegisterInteractionEvents();

            name = $"Node_{nodeData.DisplayName}_{nodeData.StageID.Substring(0, 8)}";

            Debug.Log($"RuntimeNodeElement: 节点创建完成 - {nodeData.DisplayName} (世界位置：{nodeData.Position})");
        }

        /// <summary>
        /// 创建文字匾额（直角矩形，纯色背景）
        /// </summary>
        private void CreateLabelPlaque()
        {
            _labelPlaque = new VisualElement();
            _labelPlaque.name = "LabelPlaque";

            _labelPlaque.style.width = PLAQUE_WIDTH;
            _labelPlaque.style.height = PLAQUE_HEIGHT;
            _labelPlaque.style.position = Position.Absolute;
            _labelPlaque.style.left = -PLAQUE_WIDTH / 2f;
            _labelPlaque.style.top = (CIRCLE_DIAMETER + PLAQUE_HEIGHT) / 2f - PLAQUE_HEIGHT / 2f; // 垂直居中

            // 纯色背景（无圆角，无描边）
            _labelPlaque.style.backgroundColor = PLAQUE_BACKGROUND_COLOR;

            // 文本标签
            _nameLabel = new Label(_nodeData.DisplayName);
            _nameLabel.style.color = TEXT_COLOR;
            _nameLabel.style.fontSize = 16f;
            _nameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _nameLabel.style.width = Length.Percent(100);
            _nameLabel.style.height = Length.Percent(100);
            _nameLabel.pickingMode = PickingMode.Ignore;

            _labelPlaque.Add(_nameLabel);
            Add(_labelPlaque);
        }

        /// <summary>
        /// 绘制上下弓形（圆形被匾额截断后的弓形区域）
        /// 设计：一个完整圆被中间的匾额遮挡，只露出上下两个弓形
        /// 注意：Unity UI Toolkit Painter2D 不支持 ScissorRect，需要手动计算弓形路径
        /// </summary>
        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var painter = ctx.painter2D;
            if (painter == null) return;

            // 颜色定义提升
            Color baseColor = _isSelected ? new Color(1f, 0.7f, 0.1f, 1f) : (_isHovered ? CIRCLE_HOVER_COLOR : CIRCLE_COLOR);
            Color accentColor = _isSelected ? Color.white : new Color(0.4f, 0.8f, 1f, 0.8f); // 极细的装饰蓝

            float r = CIRCLE_RADIUS;
            Vector2 center = new Vector2(CIRCLE_RADIUS, (CIRCLE_DIAMETER + PLAQUE_HEIGHT) / 2f);

            // 1. 绘制主体：不再是简单的圆弧，而是带有“加厚边缘”感的工业壳体
            painter.BeginPath();
            // 绘制上方壳体（稍微带一点点切角感）
            painter.Arc(center, r, 240, 300); // 缩小开口，制造紧凑感
            painter.LineTo(new Vector2(center.x + 35, center.y - 15));
            painter.LineTo(new Vector2(center.x - 35, center.y - 15));
            painter.ClosePath();
            painter.fillColor = baseColor;
            painter.Fill();

            // 2. 增加“刻度线”装饰 (关键的近未来元素)
            // 在壳体边缘画几条极细的平行线，模拟传感器或散热口
            painter.BeginPath();
            painter.lineWidth = 1.5f;
            painter.strokeColor = accentColor;
            painter.MoveTo(new Vector2(center.x - 20, center.y - 45));
            painter.LineTo(new Vector2(center.x + 20, center.y - 45));
            painter.Stroke();

            // 3. 匾额的设计进化：不再是死板的矩形
            // 我们在代码中通过 VisualElement 的 Border 模拟，或者直接在这里画
            // 建议：给 _labelPlaque 增加一个左侧的“类别色块”
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
                -_nodeData.Position.y * ppu    // Local_Y = -World.y * PPU (抵消 UI Y 轴向下的差异)
            );

            // 设置绝对定位和位置（居中）
            style.position = Position.Absolute;
            style.left = localPosition.x - CIRCLE_DIAMETER / 2f;
            style.top = localPosition.y - (CIRCLE_DIAMETER + PLAQUE_HEIGHT) / 2f;
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
            _isHovered = true;

            // 触发重绘（改变半圆颜色）
            MarkDirtyRepaint();

            // 匾额高亮
            _labelPlaque.style.backgroundColor = PLAQUE_HOVER_COLOR;

            evt.StopPropagation();
        }

        /// <summary>
        /// 鼠标离开事件：恢复正常样式
        /// </summary>
        private void OnMouseLeave(MouseLeaveEvent evt)
        {
            _isHovered = false;

            // 触发重绘（恢复半圆颜色）
            MarkDirtyRepaint();

            // 匾额恢复
            _labelPlaque.style.backgroundColor = PLAQUE_BACKGROUND_COLOR;

            evt.StopPropagation();
        }

        /// <summary>
        /// 点击事件：视觉反馈
        /// </summary>
        private void OnClick(ClickEvent evt)
        {
            Debug.Log($"RuntimeNodeElement: 节点 '{_nodeData.DisplayName}' 被点击 (世界位置：{_nodeData.Position})");

            // 添加点击动画效果（缩放匾额）
            _labelPlaque.style.scale = new Scale(new Vector3(0.95f, 0.95f, 1f));

            // 延迟恢复
            schedule.Execute(() =>
            {
                _labelPlaque.style.scale = new Scale(Vector3.one);
            }).StartingIn(100); // 100ms 后恢复

            evt.StopPropagation();
        }

        /// <summary>
        /// 更新节点数据（动态更新）
        /// </summary>
        public void UpdateNodeData(BigMapNodeData newData, float ppu)
        {
            _nodeData = newData;

            // 更新位置
            ApplyLocalPosition(ppu);

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
            _isSelected = selected;
            
            if (selected)
            {
                // 选中样式：金色
                _labelPlaque.style.backgroundColor = new Color(1f, 0.9f, 0f, 1f); // 金色
            }
            else
            {
                // 恢复正常样式
                _labelPlaque.style.backgroundColor = PLAQUE_BACKGROUND_COLOR;
            }
            
            // 触发重绘（改变圆颜色）
            MarkDirtyRepaint();
        }

        /// <summary>
        /// 设置节点激活状态（可点击）
        /// </summary>
        public void SetActive(bool active)
        {
            if (!active)
            {
                // 非激活状态：灰色外观
                _labelPlaque.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.7f);
                _nameLabel.style.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            }
            else
            {
                // 恢复正常样式
                _labelPlaque.style.backgroundColor = PLAQUE_BACKGROUND_COLOR;
                _nameLabel.style.color = TEXT_COLOR;
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
