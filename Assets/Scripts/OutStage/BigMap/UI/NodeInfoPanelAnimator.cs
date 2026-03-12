using UnityEngine;
using UnityEngine.EventSystems;

namespace MineRTS.BigMap.UI
{
    /// <summary>
    /// 节点信息面板动画器（SpaceUIAnimator 子类）
    ///
    /// <para>【UI ID】NodeInfoPanel - 用于事件匹配</para>
    ///
    /// <para>【行为组合】</para>
    /// <para>- 期望显示面板 (NodeInfoPanel) → 淡入 + 呼吸</para>
    /// <para>- 期望隐藏面板 (NodeInfoPanel) → 停止呼吸 + 淡出</para>
    /// <para>- 期望隐藏所有面板 → 停止呼吸 + 淡出（无条件触发）</para>
    /// <para>- 鼠标滑入 → 放大 + 旋转</para>
    /// <para>- 鼠标滑出 → 重置缩放 + 重置旋转</para>
    /// </summary>
    public class NodeInfoPanelAnimator : SpaceUIAnimator
    {
        private void Start()
        {
            _uiID = "NodeInfoPanel";

            // 追加行为到委托链（子类决定行为内容）
            期望显示面板 += OnShowPanel;
            期望隐藏面板 += OnHidePanel;
            鼠标滑入 += OnMouseEnter_;
            鼠标滑出 += OnMouseExit_;
            鼠标点击 += OnMouseClick;
        }

        /// <summary>
        /// 处理"期望显示面板"事件（子类自定义行为）
        /// </summary>
        private void OnShowPanel(object data)
        {
            Debug.Log($"<color=cyan>[NodeInfoPanelAnimator]</color> 显示面板：{data}");

            // 行为组合：淡入 + 呼吸
            FadeIn();
            StartBreathing();
        }

        /// <summary>
        /// 处理"期望隐藏面板"事件（子类自定义行为）
        /// </summary>
        private void OnHidePanel(object data)
        {
            Debug.Log($"<color=cyan>[NodeInfoPanelAnimator]</color> 隐藏面板：{data}");

            // 行为组合：停止呼吸 + 淡出
            StopBreathing();
            FadeOut();
        }

        /// <summary>
        /// 处理"鼠标滑入"事件（子类自定义行为）
        /// </summary>
        private void OnMouseEnter_(PointerEventData eventData)
        {
            Debug.Log("<color=cyan>[NodeInfoPanelAnimator]</color> 鼠标滑入");

            // 行为组合：放大 + 旋转到 15 度
            SetTargetScale(new Vector3(1.05f, 1.05f, 1.05f));
            PlayScaleAnimation();
            RotateTo(10f);
        }

        /// <summary>
        /// 处理"鼠标滑出"事件（子类自定义行为）
        /// </summary>
        private void OnMouseExit_(PointerEventData eventData)
        {
            Debug.Log("<color=cyan>[NodeInfoPanelAnimator]</color> 鼠标滑出");

            // 行为组合：重置缩放 + 重置旋转
            ResetScale();
            ResetRotation();
        }

        /// <summary>
        /// 处理"鼠标点击"事件（子类自定义行为）
        /// </summary>
        private void OnMouseClick(PointerEventData eventData)
        {
            Debug.Log("<color=cyan>[NodeInfoPanelAnimator]</color> 鼠标点击");

            // 行为组合：快速缩放反馈
            SetTargetScale(new Vector3(0.95f, 0.95f, 0.95f));
            PlayScaleAnimation();
        }
    }
}
