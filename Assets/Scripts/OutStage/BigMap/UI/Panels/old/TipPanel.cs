#if false   // 【OLD】已废弃，保留参考喵~
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace MineRTS.BigMap.UI
{
    /// <summary>
    /// 提示面板
    /// 显示简短的提示信息，自动隐藏
    /// </summary>
    public class TipPanel : MonoBehaviour
    {
        [Header("面板组件")]
        [SerializeField] private Image _panelBackground;
        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("文本组件")]
        [SerializeField] private TextMeshProUGUI _tipText;

        [Header("配置")]
        [Tooltip("默认显示时长（秒）")]
        [SerializeField] private float _defaultDuration = 3f;

        [Tooltip("淡入时长（秒）")]
        [SerializeField] private float _fadeInDuration = 0.2f;

        [Tooltip("淡出时长（秒）")]
        [SerializeField] private float _fadeOutDuration = 0.3f;

        // 当前协程
        private Coroutine _currentCoroutine;

        private void Awake()
        {
            // 初始隐藏
            Hide();

            Debug.Log("<color=cyan>[TipPanel]</color> 初始化完成");
        }

        /// <summary>
        /// 显示提示
        /// </summary>
        public void Show(string message, float duration = -1f)
        {
            if (duration < 0) duration = _defaultDuration;

            // 停止之前的协程
            if (_currentCoroutine != null)
                StopCoroutine(_currentCoroutine);

            // 设置文本
            if (_tipText != null)
                _tipText.text = message;

            // 显示面板
            gameObject.SetActive(true);

            // 播放显示动画
            _currentCoroutine = StartCoroutine(ShowTipCoroutine(duration));
        }

        /// <summary>
        /// 隐藏面板
        /// </summary>
        public void Hide()
        {
            if (_currentCoroutine != null)
                StopCoroutine(_currentCoroutine);

            if (_canvasGroup != null)
                _canvasGroup.alpha = 0f;

            gameObject.SetActive(false);
        }

        // =========================================================
        //  协程实现
        // =========================================================

        private IEnumerator ShowTipCoroutine(float duration)
        {
            // 淡入
            float elapsed = 0f;
            while (elapsed < _fadeInDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _fadeInDuration);
                if (_canvasGroup != null)
                    _canvasGroup.alpha = t;
                yield return null;
            }

            if (_canvasGroup != null)
                _canvasGroup.alpha = 1f;

            // 等待指定时长
            yield return new WaitForSeconds(duration);

            // 淡出
            elapsed = 0f;
            while (elapsed < _fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _fadeOutDuration);
                if (_canvasGroup != null)
                    _canvasGroup.alpha = 1f - t;
                yield return null;
            }

            Hide();
        }
    }
}
#endif
