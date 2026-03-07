using UnityEngine;
using System;

namespace MineRTS.BigMap.UI
{
    /// <summary>
    /// 世界空间 UI 管理器（单例）
    /// 职责：管理所有世界空间 UI 面板的显示/隐藏和交互逻辑
    /// </summary>
    public class WorldSpaceUIManager : SingletonMono<WorldSpaceUIManager>
    {
        [Header("Canvas 引用")]
        [Tooltip("世界空间 UI Canvas 控制器")]
        [SerializeField] private WorldSpaceCanvasController _canvasController;

        [Header("UI 面板")]
        [Tooltip("节点信息面板")]
        [SerializeField] private NodeInfoPanel _nodeInfoPanel;

        [Tooltip("剧情面板")]
        [SerializeField] private StoryPanel _storyPanel;

        [Tooltip("奖励面板")]
        [SerializeField] private RewardPanel _rewardPanel;

        [Tooltip("提示面板")]
        [SerializeField] private TipPanel _tipPanel;

        // 当前激活的面板
        private IMenuPanel _activePanel;

        protected override void Awake()
        {
            base.Awake();

            // 自动获取 Canvas 控制器
            if (_canvasController == null)
            {
                _canvasController = GetComponent<WorldSpaceCanvasController>();
            }

            // 自动获取面板组件
            if (_nodeInfoPanel == null)
                _nodeInfoPanel = GetComponentInChildren<NodeInfoPanel>(true);

            if (_storyPanel == null)
                _storyPanel = GetComponentInChildren<StoryPanel>(true);

            if (_rewardPanel == null)
                _rewardPanel = GetComponentInChildren<RewardPanel>(true);

            if (_tipPanel == null)
                _tipPanel = GetComponentInChildren<TipPanel>(true);

            Debug.Log("<color=cyan>[WorldSpaceUIManager]</color> 初始化完成");
        }

        private void Start()
        {
            // 注册事件监听
            PostSystem.Instance.Register(this);

            // 初始隐藏所有面板
            HideAllPanels();
        }

        private void OnDestroy()
        {
            if (PostSystem.Instance != null)
                PostSystem.Instance.Unregister(this);
        }

        // =========================================================
        //  面板控制方法
        // =========================================================

        /// <summary>
        /// 隐藏所有面板
        /// </summary>
        public void HideAllPanels()
        {
            _nodeInfoPanel?.Close();
            _storyPanel?.Close();
            _rewardPanel?.Close();
            _tipPanel?.gameObject.SetActive(false);
            _activePanel = null;
        }

        /// <summary>
        /// 显示节点信息面板
        /// </summary>
        public void ShowNodeInfo(BigMapNodeData nodeData)
        {
            HideAllPanels();

            if (_nodeInfoPanel != null)
            {
                _nodeInfoPanel.Setup(nodeData);
                _nodeInfoPanel.Open();
                _activePanel = _nodeInfoPanel;
            }
            else
            {
                Debug.LogWarning("<color=orange>[WorldSpaceUIManager]</color> NodeInfoPanel 未找到");
            }
        }

        /// <summary>
        /// 显示剧情面板
        /// </summary>
        public void ShowStory(string storyID)
        {
            HideAllPanels();

            if (_storyPanel != null)
            {
                _storyPanel.LoadStory(storyID);
                _storyPanel.Open();
                _activePanel = _storyPanel;
            }
            else
            {
                Debug.LogWarning("<color=orange>[WorldSpaceUIManager]</color> StoryPanel 未找到");
            }
        }

        /// <summary>
        /// 显示奖励面板
        /// </summary>
        public void ShowReward(MissionReward rewardData)
        {
            HideAllPanels();

            if (_rewardPanel != null)
            {
                _rewardPanel.Setup(rewardData);
                _rewardPanel.Open();
                _activePanel = _rewardPanel;
            }
            else
            {
                Debug.LogWarning("<color=orange>[WorldSpaceUIManager]</color> RewardPanel 未找到");
            }
        }

        /// <summary>
        /// 显示提示
        /// </summary>
        public void ShowTip(string message, float duration = 3f)
        {
            if (_tipPanel != null)
            {
                _tipPanel.Show(message, duration);
            }
        }

        // =========================================================
        //  事件监听
        // =========================================================

        /// <summary>
        /// 监听节点点击事件
        /// </summary>
        [Subscribe(BigMapEvents.NodeClicked)]
        private void OnNodeClicked(object data)
        {
            if (data is NodeClickEvent e)
            {
                Debug.Log($"<color=cyan>[WorldSpaceUIManager]</color> 收到节点点击事件：{e.DisplayName} ({e.StageId})");

                // 获取节点数据
                var node = BigMapRuntimeRenderer.Instance?.GetNode(e.StageId);
                if (node != null && node.NodeData != null)
                {
                    ShowNodeInfo(node.NodeData);
                }
            }
        }

        /// <summary>
        /// 监听任务完成事件
        /// </summary>
        [Subscribe("UI_MISSION_COMPLETE")]
        private void OnMissionComplete(object data)
        {
            if (data is MissionData mission)
            {
                Debug.Log($"<color=cyan>[WorldSpaceUIManager]</color> 任务完成：{mission.Title}");

                if (mission.Reward != null)
                {
                    ShowReward(mission.Reward);
                }
            }
        }

        // =========================================================
        //  公共访问器
        // =========================================================

        /// <summary>
        /// 获取当前激活的面板
        /// </summary>
        public IMenuPanel GetActivePanel()
        {
            return _activePanel;
        }

        /// <summary>
        /// 获取节点信息面板
        /// </summary>
        public NodeInfoPanel GetNodeInfoPanel()
        {
            return _nodeInfoPanel;
        }

        /// <summary>
        /// 获取剧情面板
        /// </summary>
        public StoryPanel GetStoryPanel()
        {
            return _storyPanel;
        }
    }
}
