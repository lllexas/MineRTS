using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MineRTS.BigMap.UI.Panels
{
    /// <summary>
    /// ═══════════════════════════════════════════════════════════════
    /// LineRendererPool - 行渲染器对象池
    /// ═══════════════════════════════════════════════════════════════
    ///
    /// 职责：
    /// 1. 管理 TextMeshProUGUI 行对象池
    /// 2. 动态视口：只激活 VisibleRows + 2 个行对象
    /// 3. 循环滚动：最上面的 Text 挪到最下面
    ///
    /// ═══════════════════════════════════════════════════════════════
    /// </summary>
    public class LineRendererPool
    {
        // =========================================================
        //  配置
        // =========================================================
        private RectTransform _contentRect;
        private RectTransform _viewportRect;
        private TextMeshProUGUI _linePrefab;
        private VerticalLayoutGroup _layoutGroup;

        private int _visibleRows = 20; // 可见行数
        private float _lineHeight = 20f; // 行高

        // =========================================================
        //  对象池
        // =========================================================
        private List<TextMeshProUGUI> _pool = new List<TextMeshProUGUI>();
        private List<TextMeshProUGUI> _activeLines = new List<TextMeshProUGUI>();

        // =========================================================
        //  循环滚动状态
        // =========================================================
        private int _scrollOffset = 0; // 滚动偏移（行索引）
        private int _totalLines = 0; // 总行数（来自 ScreenBuffer）

        // =========================================================
        //  初始化
        // =========================================================

        public LineRendererPool(RectTransform content, RectTransform viewport, TextMeshProUGUI linePrefab)
        {
            _contentRect = content;
            _viewportRect = viewport;
            _linePrefab = linePrefab;

            // 获取 VerticalLayoutGroup
            _layoutGroup = content.GetComponent<VerticalLayoutGroup>();
            if (_layoutGroup == null)
            {
                _layoutGroup = content.gameObject.AddComponent<VerticalLayoutGroup>();
            }

            // 计算行高
            if (_layoutGroup != null)
            {
                _lineHeight = _layoutGroup.spacing + 20f; // 估算行高
            }

            // 计算可见行数
            CalculateVisibleRows();

            // 创建对象池
            CreatePool(_visibleRows + 2);
        }

        /// <summary>
        /// 计算可见行数
        /// </summary>
        private void CalculateVisibleRows()
        {
            if (_viewportRect != null)
            {
                float viewportHeight = _viewportRect.rect.height;
                _visibleRows = Mathf.CeilToInt(viewportHeight / _lineHeight);
            }
        }

        /// <summary>
        /// 创建对象池
        /// </summary>
        private void CreatePool(int capacity)
        {
            for (int i = 0; i < capacity; i++)
            {
                TextMeshProUGUI line = CreateLine();
                _pool.Add(line);
            }
        }

        /// <summary>
        /// 创建单个行对象
        /// </summary>
        private TextMeshProUGUI CreateLine()
        {
            TextMeshProUGUI line = Object.Instantiate(_linePrefab, _contentRect);
            line.gameObject.SetActive(false);
            return line;
        }

        // =========================================================
        //  滚动控制
        // =========================================================

        /// <summary>
        /// 设置滚动偏移
        /// </summary>
        public void SetScrollOffset(int offset)
        {
            _scrollOffset = Mathf.Max(0, offset);
        }

        /// <summary>
        /// 滚动到底部
        /// </summary>
        public void ScrollToBottom()
        {
            _scrollOffset = Mathf.Max(0, _totalLines - _visibleRows);
        }

        /// <summary>
        /// 获取当前滚动偏移
        /// </summary>
        public int GetScrollOffset()
        {
            return _scrollOffset;
        }

        // =========================================================
        //  清理
        // =========================================================

        /// <summary>
        /// 回收所有行对象
        /// </summary>
        public void RecycleAll()
        {
            foreach (var line in _activeLines)
            {
                line.gameObject.SetActive(false);
                _pool.Add(line);
            }
            _activeLines.Clear();
        }

        /// <summary>
        /// 清空对象池
        /// </summary>
        public void Clear()
        {
            RecycleAll();

            foreach (var line in _pool)
            {
                Object.Destroy(line.gameObject);
            }
            _pool.Clear();
        }
    }
}
