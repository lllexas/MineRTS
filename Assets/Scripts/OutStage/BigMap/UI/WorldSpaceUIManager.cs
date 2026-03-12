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
        [SerializeField] private WorldSpaceUIAnimateUnit _canvasController;

        [Header("UI 面板")]
        [Tooltip("节点信息面板 Prefab")]
        [SerializeField] private NodeInfoPanel _nodeInfoPanelPrefab;

        [Tooltip("剧情面板 Prefab")]
        [SerializeField] private StoryPanel _storyPanelPrefab;

        // [Tooltip("奖励面板 Prefab")]  // 已休眠，等待新奖励系统喵~
        // [SerializeField] private RewardPanel _rewardPanelPrefab;

        [Tooltip("提示面板 Prefab")]
        [SerializeField] private TipPanel _tipPanelPrefab;

        // 当前激活的面板
        private IMenuPanel _activePanel;

        // 实例化的面板缓存
        private NodeInfoPanel _cachedNodeInfoPanel;
        private StoryPanel _cachedStoryPanel;
        // private RewardPanel _cachedRewardPanel;  // 已休眠，等待新奖励系统喵~
        private TipPanel _cachedTipPanel;

        protected override void Awake()
        {
            base.Awake();

            // 自动获取 Canvas 控制器
            if (_canvasController == null)
            {
                _canvasController = GetComponent<WorldSpaceUIAnimateUnit>();
            }

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
            _cachedNodeInfoPanel?.Close();
            _cachedStoryPanel?.Close();
            // _cachedRewardPanel?.Close();  // 已休眠，等待新奖励系统喵~
            if (_cachedTipPanel != null)
                _cachedTipPanel.gameObject.SetActive(false);
            _activePanel = null;
        }

        /// <summary>
        /// 显示节点信息面板
        /// </summary>
        public void ShowNodeInfo(BigMapNodeData nodeData)
        {
            HideAllPanels();

            // 实例化或复用面板
            if (_cachedNodeInfoPanel == null && _nodeInfoPanelPrefab != null)
            {
                var go = Instantiate(_nodeInfoPanelPrefab.gameObject, transform);
                _cachedNodeInfoPanel = go.GetComponent<NodeInfoPanel>();
            }

            if (_cachedNodeInfoPanel != null)
            {
                _cachedNodeInfoPanel.Setup(nodeData);
                _cachedNodeInfoPanel.Open();
                _activePanel = _cachedNodeInfoPanel;
                Debug.Log("<color=cyan>[WorldSpaceUIManager]</color> 节点信息面板已显示");
            }
            else
            {
                Debug.LogWarning("<color=orange>[WorldSpaceUIManager]</color> NodeInfoPanel Prefab 未设置");
            }
        }

        /// <summary>
        /// 显示剧情面板
        /// </summary>
        public void ShowStory(string storyID)
        {
            HideAllPanels();

            // 实例化或复用面板
            if (_cachedStoryPanel == null && _storyPanelPrefab != null)
            {
                var go = Instantiate(_storyPanelPrefab.gameObject, transform);
                _cachedStoryPanel = go.GetComponent<StoryPanel>();
            }

            if (_cachedStoryPanel != null)
            {
                _cachedStoryPanel.LoadStory(storyID);
                _cachedStoryPanel.Open();
                _activePanel = _cachedStoryPanel;
                Debug.Log("<color=cyan>[WorldSpaceUIManager]</color> 剧情面板已显示");
            }
            else
            {
                Debug.LogWarning("<color=orange>[WorldSpaceUIManager]</color> StoryPanel Prefab 未设置");
            }
        }

        /*/// <summary>
        /// 显示奖励面板
        /// </summary>
        public void ShowReward(MissionReward rewardData)
        {
            HideAllPanels();

            // 实例化或复用面板
            if (_cachedRewardPanel == null && _rewardPanelPrefab != null)
            {
                var go = Instantiate(_rewardPanelPrefab.gameObject, transform);
                _cachedRewardPanel = go.GetComponent<RewardPanel>();
            }

            if (_cachedRewardPanel != null)
            {
                _cachedRewardPanel.Setup(rewardData);
                _cachedRewardPanel.Open();
                _activePanel = _cachedRewardPanel;
                Debug.Log("<color=cyan>[WorldSpaceUIManager]</color> 奖励面板已显示");
            }
            else
            {
                Debug.LogWarning("<color=orange>[WorldSpaceUIManager]</color> RewardPanel Prefab 未设置");
            }
        }*/

        /// <summary>
        /// 显示提示
        /// </summary>
        public void ShowTip(string message, float duration = 3f)
        {
            // 实例化或复用提示面板
            if (_cachedTipPanel == null && _tipPanelPrefab != null)
            {
                var go = Instantiate(_tipPanelPrefab.gameObject, transform);
                _cachedTipPanel = go.GetComponent<TipPanel>();
            }

            if (_cachedTipPanel != null)
            {
                _cachedTipPanel.Show(message, duration);
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
            if (data is MissionNode_A_Data mission)
            {
                Debug.Log($"<color=cyan>[WorldSpaceUIManager]</color> 任务完成：{mission.Title}");

                /*if (mission.Reward != null)
                {
                    ShowReward(mission.Reward);
                }*/
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
        /// 获取节点信息面板（实例化的）
        /// </summary>
        public NodeInfoPanel GetNodeInfoPanel()
        {
            return _cachedNodeInfoPanel;
        }

        /// <summary>
        /// 获取剧情面板（实例化的）
        /// </summary>
        public StoryPanel GetStoryPanel()
        {
            return _cachedStoryPanel;
        }
    }
}
