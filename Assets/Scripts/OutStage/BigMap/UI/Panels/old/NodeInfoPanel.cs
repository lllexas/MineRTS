#if false   // 【OLD】已废弃，保留参考喵~
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace MineRTS.BigMap.UI
{
    /// <summary>
    /// 节点信息面板
    /// 显示节点的详细信息，包含进入关卡按钮
    /// </summary>
    public class NodeInfoPanel : AnimatedPanelBase
    {
        [Header("文本组件")]
        [SerializeField] private TextMeshProUGUI _nodeNameText;
        [SerializeField] private TextMeshProUGUI _nodeDescText;
        [SerializeField] private TextMeshProUGUI _nodeStatusText;

        [Header("按钮")]
        [SerializeField] private Button _enterStageButton;
        [SerializeField] private Button _closeButton;

        // 当前显示的节点数据
        private BigMapNodeData _currentNodeData;

        private void Awake()
        {
            base.Awake();

            // 绑定按钮事件
            if (_enterStageButton != null)
                _enterStageButton.onClick.AddListener(OnEnterStageClicked);

            if (_closeButton != null)
                _closeButton.onClick.AddListener(OnCloseClicked);

            Debug.Log("<color=cyan>[NodeInfoPanel]</color> 初始化完成");
        }

        private void OnDestroy()
        {
            base.OnDestroy();
        }

        /// <summary>
        /// 设置面板数据
        /// </summary>
        public void Setup(BigMapNodeData nodeData)
        {
            _currentNodeData = nodeData;

            // 设置节点名称
            if (_nodeNameText != null)
                _nodeNameText.text = nodeData.DisplayName;

            // 设置节点描述（从 ExtraData 解析或显示默认文本）
            if (_nodeDescText != null)
            {
                _nodeDescText.text = string.IsNullOrEmpty(nodeData.ExtraData)
                    ? "点击按钮进入关卡"
                    : nodeData.ExtraData;
            }

            // 设置节点状态
            if (_nodeStatusText != null)
            {
                string status = GetNodeStatus(nodeData.StageID);
                _nodeStatusText.text = status;
            }
        }

        /// <summary>
        /// 获取节点状态文本
        /// </summary>
        private string GetNodeStatus(string stageID)
        {
            // 检查是否已通关
            if (MainModel.Instance != null && MainModel.Instance.CurrentUser != null)
            {
                var stageData = MainModel.Instance.CurrentUser.GetStage(stageID);
                if (stageData != null && stageData.IsCleared)
                {
                    return "✓ 已通关";
                }
            }

            // 检查是否已解锁（简化逻辑：默认已解锁）
            return "● 未挑战";
        }

        // =========================================================
        //  按钮事件处理
        // =========================================================

        /// <summary>
        /// 进入关卡按钮点击
        /// </summary>
        private void OnEnterStageClicked()
        {
            if (_currentNodeData == null)
            {
                Debug.LogWarning("<color=orange>[NodeInfoPanel]</color> 当前没有节点数据");
                return;
            }

            Debug.Log($"<color=cyan>[NodeInfoPanel]</color> 进入关卡：{_currentNodeData.StageID}");

            // 关闭面板
            Close();

            // 进入关卡
            if (GameFlowController.Instance != null)
            {
                GameFlowController.Instance.EnterStage(_currentNodeData.StageID);
            }
        }

        /// <summary>
        /// 关闭按钮点击
        /// </summary>
        private void OnCloseClicked()
        {
            Close();
        }

        /// <summary>
        /// 设置面板位置（跟随节点）
        /// </summary>
        public void SetPosition(Vector3 worldPosition)
        {
            transform.position = worldPosition;
        }
    }
}
#endif
