using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections.Generic;

namespace MineRTS.BigMap
{
    /// <summary>
    /// 【已废弃】运行时地图容器 - UI Toolkit 版本
    /// 职责：管理节点元素，绘制连线，应用局部空间坐标变换
    /// 核心：所有节点和连线都在局部空间定义，通过容器级变换实现摄像机移动
    /// 
    /// 注意：此类已废弃，请使用 BigMapRuntimeRenderer（GameObject 版本）
    /// </summary>
    [System.Obsolete("RuntimeMapContainer 已废弃，请使用 BigMapRuntimeRenderer（GameObject 版本）")]
    public class RuntimeMapContainer : VisualElement
    {
        // 节点映射表：节点ID -> 节点元素
        private Dictionary<string, RuntimeNodeElement> _nodeElements = new Dictionary<string, RuntimeNodeElement>();

        // 连线数据（用于绘制）
        private List<BigMapEdgeData> _edgeData = new List<BigMapEdgeData>();

        // 节点点击回调
        private Action<string, string> _nodeClickCallback;

        // 样式常量
        private const float NODE_SIZE = 20f;
        private const float EDGE_WIDTH_BASE = 2f;
        private const float EDGE_WIDTH_MIN = 1f;
        private const float EDGE_WIDTH_MAX = 4f;

        // 颜色定义
        private static readonly Color BIDIRECTIONAL_EDGE_COLOR = new Color(0.4f, 0.8f, 1.0f, 0.8f); // 科技蓝
        private static readonly Color UNIDIRECTIONAL_EDGE_COLOR = new Color(1.0f, 0.6f, 0.2f, 0.8f); // 橙色
        private static readonly Color ARROW_COLOR = new Color(1.0f, 1.0f, 1.0f, 0.9f); // 白色箭头

        // 基础PPU（地图加载时的PPU，用于节点坐标计算）
        private float _basePPU = 1.0f;

        // 当前缩放比例（CurrentPPU / BasePPU），用于连线绘制
        private float _currentZoomRatio = 1.0f;

        public RuntimeMapContainer()
        {
            // 设置基本样式
            style.position = Position.Absolute;
            style.width = Length.Percent(100);
            style.height = Length.Percent(100);
            style.overflow = Overflow.Visible;

            // 注册绘制回调
            generateVisualContent += OnGenerateVisualContent;

            Debug.Log("RuntimeMapContainer: 容器创建完成");
        }

        /// <summary>
        /// 渲染地图：清空现有内容，根据数据创建节点和连线
        /// 注意：节点位置使用局部空间坐标，后续通过容器变换实现摄像机移动
        /// </summary>
        public void RenderMap(BigMapSaveData mapData, float basePPU)
        {
            if (mapData == null)
            {
                Debug.LogError("RuntimeMapContainer: 地图数据为空");
                return;
            }

            // 保存基础PPU（地图加载时的PPU）
            _basePPU = basePPU;
            _currentZoomRatio = 1.0f; // 初始缩放比例为1:1

            // 清除现有内容
            Clear();
            _nodeElements.Clear();
            _edgeData.Clear();

            // 保存连线数据用于绘制
            _edgeData.AddRange(mapData.Edges);

            // 创建节点元素
            foreach (var nodeData in mapData.Nodes)
            {
                CreateNodeElement(nodeData);
            }

            // 强制重绘（绘制连线）
            MarkDirtyRepaint();

            Debug.Log($"RuntimeMapContainer: 地图渲染完成 - {mapData.Nodes.Count}个节点，{mapData.Edges.Count}条连线 (基础PPU: {_basePPU})");
        }

        /// <summary>
        /// 创建节点元素并添加到容器
        /// 公式：Local_X = World.x * PPU, Local_Y = -World.y * PPU
        /// </summary>
        private void CreateNodeElement(BigMapNodeData nodeData)
        {
            // 创建节点元素（传递基础PPU，节点会自己计算局部空间坐标）
            var nodeElement = new RuntimeNodeElement(nodeData, _basePPU);

            // 注意：RuntimeNodeElement的构造函数已经调用了ApplyLocalPosition(ppu)
            // 设置了position=Absolute、left/top/width/height等样式

            // 注册点击事件
            nodeElement.RegisterCallback<ClickEvent>(evt => OnNodeClicked(nodeData.StageID, nodeData.DisplayName));

            // 添加到容器和映射表
            Add(nodeElement);
            _nodeElements[nodeData.StageID] = nodeElement;
        }

        /// <summary>
        /// 节点点击事件处理
        /// </summary>
        private void OnNodeClicked(string nodeId, string displayName)
        {
            Debug.Log($"RuntimeMapContainer: 节点被点击 - ID: {nodeId}, 名称: {displayName}");

            // 调用外部回调
            _nodeClickCallback?.Invoke(nodeId, displayName);
        }

        /// <summary>
        /// 生成可视化内容：绘制连线
        /// 注意：连线也在局部空间绘制，使用相同的坐标变换公式
        /// </summary>
        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            if (_edgeData == null || _edgeData.Count == 0) return;

            var painter = ctx.painter2D;
            if (painter == null) return;

            // 计算当前缩放比例（用于自适应线宽）
            float currentZoom = GetCurrentZoom();
            float edgeWidth = Mathf.Clamp(EDGE_WIDTH_BASE / currentZoom, EDGE_WIDTH_MIN, EDGE_WIDTH_MAX);

            // 绘制所有连线
            foreach (var edge in _edgeData)
            {
                DrawEdge(painter, edge, edgeWidth);
            }
        }

        /// <summary>
        /// 绘制单条连线
        /// 坐标必须使用：Local_X = World.x * PPU, Local_Y = -World.y * PPU
        /// </summary>
        private void DrawEdge(Painter2D painter, BigMapEdgeData edge, float edgeWidth)
        {
            // 获取起点和终点的节点数据
            BigMapNodeData fromNodeData = GetNodeData(edge.FromNodeID);
            BigMapNodeData toNodeData = GetNodeData(edge.ToNodeID);

            if (fromNodeData == null || toNodeData == null)
            {
                // 节点数据可能不存在，跳过绘制
                return;
            }

            // 计算局部空间坐标（遵循正交投影公式）
            Vector2 fromPos = new Vector2(
                fromNodeData.Position.x * _basePPU,
                -fromNodeData.Position.y * _basePPU
            );

            Vector2 toPos = new Vector2(
                toNodeData.Position.x * _basePPU,
                -toNodeData.Position.y * _basePPU
            );

            // 设置连线颜色
            Color edgeColor = edge.Direction == EdgeDirection.Bidirectional
                ? BIDIRECTIONAL_EDGE_COLOR
                : UNIDIRECTIONAL_EDGE_COLOR;

            // 绘制连线
            painter.strokeColor = edgeColor;
            painter.fillColor = edgeColor;
            painter.lineWidth = edgeWidth;
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;

            painter.BeginPath();
            painter.MoveTo(fromPos);
            painter.LineTo(toPos);
            painter.Stroke();

            // 绘制箭头（单向连线需要箭头）
            if (edge.Direction == EdgeDirection.Unidirectional)
            {
                DrawArrow(painter, fromPos, toPos, edgeWidth);
            }
        }

        /// <summary>
        /// 根据节点ID获取节点数据
        /// </summary>
        private BigMapNodeData GetNodeData(string nodeId)
        {
            if (_nodeElements.TryGetValue(nodeId, out var nodeElement))
            {
                return nodeElement.NodeData;
            }
            return null;
        }

        /// <summary>
        /// 绘制箭头（单向连线）
        /// </summary>
        private void DrawArrow(Painter2D painter, Vector2 from, Vector2 to, float edgeWidth)
        {
            // 计算连线方向
            Vector2 direction = (to - from).normalized;
            float distance = Vector2.Distance(from, to);

            // 如果连线太短，不绘制箭头
            if (distance < NODE_SIZE * 2) return;

            // 箭头参数
            float arrowLength = edgeWidth * 3f;
            float arrowWidth = edgeWidth * 2f;

            // 箭头尖端位置（稍微远离终点节点）
            Vector2 arrowTip = to - direction * (NODE_SIZE / 2 + edgeWidth * 2);

            // 计算箭头两侧点
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            Vector2 arrowLeft = arrowTip - direction * arrowLength + perpendicular * arrowWidth;
            Vector2 arrowRight = arrowTip - direction * arrowLength - perpendicular * arrowWidth;

            // 绘制箭头
            painter.strokeColor = ARROW_COLOR;
            painter.fillColor = ARROW_COLOR;
            painter.lineWidth = edgeWidth;

            painter.BeginPath();
            painter.MoveTo(arrowTip);
            painter.LineTo(arrowLeft);
            painter.LineTo(arrowRight);
            painter.ClosePath();
            painter.Fill();
        }

        /// <summary>
        /// 获取当前缩放比例（CurrentPPU / BasePPU）
        /// 用于连线绘制时的线宽计算
        /// </summary>
        private float GetCurrentZoom()
        {
            return _currentZoomRatio;
        }

        /// <summary>
        /// 设置当前缩放比例（由BigMapRuntimeRenderer在每帧更新时调用）
        /// </summary>
        public void SetZoomRatio(float zoomRatio)
        {
            _currentZoomRatio = zoomRatio;
        }

        /// <summary>
        /// 根据节点ID获取节点元素
        /// </summary>
        public RuntimeNodeElement GetNodeElement(string nodeId)
        {
            _nodeElements.TryGetValue(nodeId, out var node);
            return node;
        }

        /// <summary>
        /// 设置节点点击回调
        /// </summary>
        public void SetNodeClickCallback(Action<string, string> callback)
        {
            _nodeClickCallback = callback;
        }

        /// <summary>
        /// 清除所有节点和连线
        /// </summary>
        public new void Clear()
        {
            base.Clear();
            _nodeElements.Clear();
            _edgeData.Clear();
        }

        /// <summary>
        /// 添加连线数据（动态添加）
        /// </summary>
        public void AddEdge(BigMapEdgeData edge)
        {
            _edgeData.Add(edge);
            MarkDirtyRepaint();
        }

        /// <summary>
        /// 移除连线数据（动态移除）
        /// </summary>
        public void RemoveEdge(BigMapEdgeData edge)
        {
            _edgeData.Remove(edge);
            MarkDirtyRepaint();
        }

    }
}