using UnityEngine;
using UnityEngine.EventSystems;

namespace MineRTS.BigMap.UI
{
    /// <summary>
    /// 大地图 Canvas 动画器（SpaceUIAnimator 子类）
    ///
    /// <para>【职责】监听"进入根界面"事件，控制大地图 Canvas 的动画</para>
    ///
    /// <para>【行为组合】</para>
    /// <para>- 进入根界面 → 淡入 + 呼吸</para>
    /// <para>- 鼠标滑入 → 放大 + 旋转</para>
    /// <para>- 鼠标滑出 → 重置缩放 + 重置旋转</para>
    /// </summary>
    public class BigMapCanvasAnimator : SpaceUIAnimator
    {
        private void Start()
        {
            // 追加行为到委托链（子类决定行为内容）
            进入根界面 += OnEnterRoot;
            鼠标滑入 += OnMouseEnter;
            鼠标滑出 += OnMouseExit;
            鼠标点击 += OnMouseClick;
        }

        /// <summary>
        /// 处理"进入根界面"事件（子类自定义行为）
        /// </summary>
        private void OnEnterRoot(object data)
        {
            Debug.Log("<color=cyan>[BigMapCanvasAnimator]</color> 进入根界面");

            // 行为组合：淡入 + 呼吸
            FadeIn();
            StartBreathing();
        }

        /// <summary>
        /// 处理"鼠标滑入"事件（子类自定义行为）
        /// </summary>
        private void OnMouseEnter(PointerEventData eventData)
        {
            Debug.Log("<color=cyan>[BigMapCanvasAnimator]</color> 鼠标滑入");

            // 行为组合：放大 + 旋转到 15 度
            SetTargetScale(new Vector3(1.1f, 1.1f, 1.1f));
            PlayScaleAnimation();
            RotateTo(15f);
        }

        /// <summary>
        /// 处理"鼠标滑出"事件（子类自定义行为）
        /// </summary>
        private void OnMouseExit(PointerEventData eventData)
        {
            Debug.Log("<color=cyan>[BigMapCanvasAnimator]</color> 鼠标滑出");

            // 行为组合：重置缩放 + 重置旋转
            ResetScale();
            ResetRotation();
        }

        /// <summary>
        /// 处理"鼠标点击"事件（子类自定义行为）
        /// </summary>
        private void OnMouseClick(PointerEventData eventData)
        {
            Debug.Log("<color=cyan>[BigMapCanvasAnimator]</color> 鼠标点击");

            // 行为组合：快速缩放反馈
            SetTargetScale(new Vector3(0.95f, 0.95f, 0.95f));
            PlayScaleAnimation();
        }
    }
}
