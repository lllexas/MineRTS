using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using System;

namespace MineRTS.BigMap.UI
{
    /// <summary>
    /// 空间 UI 动画器（行为驱动基类）
    ///
    /// <para>【设计理念】行为驱动动效基类 - 通过事件订阅组合行为</para>
    ///
    /// <para>【四层独立轨道】</para>
    /// <para>轨道 A：_stateTween - 状态转换（Alpha/Fade）</para>
    /// <para>轨道 B：_rotationTween - 旋转（Rotate）</para>
    /// <para>轨道 C：_scaleTween - 缩放/脉冲（Scale/Punch，虚拟倍率驱动）</para>
    /// <para>轨道 D：_breathTween - 呼吸循环（Looping Breath，虚拟倍率驱动）</para>
    ///
    /// <para>【缩放合成】轨道 C 和 D 通过虚拟倍率在 Update 中合成到 transform.localScale</para>
    /// <para>公式：transform.localScale = _initialScale * _scaleMultiplier * _breathMultiplier</para>
    ///
    /// <para>【事件管道】</para>
    /// <para>进入根界面 - 当进入根界面状态时触发</para>
    /// <para>期望显示面板 - 当期望显示某个面板时触发（需匹配 UI ID）</para>
    /// <para>期望隐藏面板 - 当期望隐藏某个面板时触发（需匹配 UI ID）</para>
    /// <para>期望隐藏所有面板 - 无条件触发，隐藏所有面板</para>
    /// <para>鼠标滑入/滑出/点击 - Unity 原生交互事件</para>
    ///
    /// <example>子类使用示例：
    /// <code>
    /// public class NodeInfoPanelAnimator : SpaceUIAnimator
    /// {
    ///     [SerializeField] protected string _uiID = "NodeInfoPanel";
    ///
    ///     private void Start()
    ///     {
    ///         期望显示面板 += OnShowPanel;
    ///         期望隐藏面板 += OnHidePanel;
    ///     }
    ///
    ///     private void OnShowPanel(object data)
    ///     {
    ///         FadeIn();
    ///         StartBreathing();
    ///     }
    /// }
    /// </code>
    /// </example>
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(Image))]
    public abstract class SpaceUIAnimator : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerClickHandler
    {
        // =========================================================
        //  配置参数
        // =========================================================
        [Header("UI 标识")]
        [Tooltip("UI 面板 ID（用于事件匹配）")]
        [SerializeField] protected string _uiID;

        [Header("动画配置")]
        [Tooltip("淡入/淡出动画时长（秒）")]
        [SerializeField] protected float _fadeDuration = 0.3f;

        [Tooltip("缩放动画时长（秒）")]
        [SerializeField] protected float _scaleDuration = 0.2f;

        [Tooltip("目标缩放值")]
        [SerializeField] protected Vector3 _targetScale = Vector3.one;

        [Tooltip("浮现动画位移量（Y 轴向上）")]
        [SerializeField] protected float _slideInOffset = 30f;

        [Header("旋转配置")]
        [Tooltip("目标旋转角度（Y 轴，度数）")]
        [SerializeField] protected float _targetRotationY = 0f;

        [Tooltip("旋转动画时长（秒）")]
        [SerializeField] protected float _rotationDuration = 0.5f;

        [Tooltip("旋转缓动")]
        [SerializeField] protected Ease _rotationEase = Ease.OutCubic;

        [Tooltip("旋转容差（角度，小于此值不触发旋转）")]
        [SerializeField] protected float _rotationTolerance = 0.1f;

        [Header("呼吸效果配置")]
        [Tooltip("呼吸效果缩放幅度")]
        [SerializeField] protected float _breathScaleAmplitude = 0.05f;

        [Tooltip("呼吸效果周期（秒）")]
        [SerializeField] protected float _breathDuration = 1.5f;

        [Header("缓动曲线")]
        [Tooltip("淡入缓动")]
        [SerializeField] protected Ease _fadeInEase = Ease.OutCubic;

        [Tooltip("淡出缓动")]
        [SerializeField] protected Ease _fadeOutEase = Ease.InCubic;

        [Header("组件引用")]
        [SerializeField] protected CanvasGroup _canvasGroup;

        // =========================================================
        //  四层独立轨道（Track Independence）
        // =========================================================
        // 轨道 A：状态转换轨（Alpha/Fade）
        protected Sequence _stateTween;

        // 轨道 B：旋转轨（Rotate）
        protected Tween _rotationTween;

        // 轨道 C：缩放轨（Scale/Punch）
        protected Tween _scaleTween;

        // 轨道 D：呼吸轨（Looping Breath）
        protected Tween _breathTween;

        // =========================================================
        //  虚拟倍率（Virtual Multipliers）- 用于轨道 C 和 D 的合成
        // =========================================================
        // 轨道 C 倍率：缩放动画倍率（默认 1f，PlayScaleAnimation 时改变）
        private float _scaleMultiplier = 1f;

        // 轨道 D 倍率：呼吸动画倍率（默认 1f，StartBreathing 时在 1~1+amplitude 之间循环）
        private float _breathMultiplier = 1f;

        // =========================================================
        //  事件管道（委托链 - 子类可追加）
        // =========================================================
        // 状态事件
        protected event Action<object> 进入根界面;

        // 面板显示/隐藏事件
        protected event Action<object> 期望显示面板;
        protected event Action<object> 期望隐藏面板;

        // 鼠标交互事件
        protected event Action<PointerEventData> 鼠标滑入;
        protected event Action<PointerEventData> 鼠标滑出;
        protected event Action<PointerEventData> 鼠标点击;

        // =========================================================
        //  缓存数据
        // =========================================================
        protected Vector3 _initialPosition;
        protected Quaternion _initialRotation;
        protected Vector3 _initialScale;
        protected Vector3 _slideInStartPosition;
        protected Quaternion _targetRotation;

        /// <summary>
        /// Canvas 是否可见
        /// </summary>
        public bool IsVisible { get; protected set; } = true;

        // =========================================================
        //  Unity 生命周期
        // =========================================================
        protected virtual void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();

            // 缓存初始状态
            _initialPosition = transform.localPosition;
            _initialRotation = transform.localRotation;
            _initialScale = transform.localScale;
            _targetScale = _initialScale;
            _slideInStartPosition = _initialPosition;
            _targetRotation = _initialRotation;

            // 初始状态：保持 active（为了接收事件），但 alpha = 0（隐藏且不拦截鼠标）
            // blocksRaycasts = false 表示"隐藏状态"，这是我们的状态标志位
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;  // 初始为隐藏状态
            }
            // gameObject.SetActive(false);  // 不再禁用 GameObject，否则收不到事件

            // 标签订阅：注册内部中转站
            PostSystem.Instance.Register(this);

            Debug.Log($"<color=cyan>[SpaceUIAnimator]</color> 初始化完成：{gameObject.name} (UI ID: {_uiID})");
        }

        protected virtual void OnDestroy()
        {
            // 清理所有轨道（独立 Kill）
            _stateTween?.Kill();
            _rotationTween?.Kill();
            _scaleTween?.Kill();
            _breathTween?.Kill();

            // 注销标签订阅
            if (PostSystem.Instance != null)
                PostSystem.Instance.Unregister(this);
        }

        protected virtual void Update()
        {
            // 旋转监控（自动触发）
            CheckAndApplyRotation();

            // 缩放合成：轨道 C + 轨道 D 暴力合成
            // 公式：targetScale = _initialScale * _scaleMultiplier * _breathMultiplier
            Vector3 combinedScale = _initialScale * _scaleMultiplier * _breathMultiplier;
            transform.localScale = combinedScale;
        }

        // =========================================================
        //  内部中转站（标签订阅 - 永远通电的排插）
        // =========================================================

        /// <summary>
        /// 进入根界面 - 内部中转站
        /// </summary>
        [Subscribe("进入根界面")]
        private void HandleEnterRoot(object data)
        {
            // 动态执行最新的委托链（包括子类后来加上的）
            进入根界面?.Invoke(data);
        }

        /// <summary>
        /// 期望显示面板 - 内部中转站（带 UI ID 匹配）
        /// </summary>
        [Subscribe("期望显示面板")]
        private void HandleShowPanel(object data)
        {
            Debug.Log("!");
            // 自动匹配 UI ID
            // 自动匹配 UI ID
            if (MatchUIID(data))
            {
                期望显示面板?.Invoke(data);
            }
            else
            {
                Debug.Log("?");
            }
        }

        /// <summary>
        /// 期望隐藏面板 - 内部中转站（带 UI ID 匹配）
        /// </summary>
        [Subscribe("期望隐藏面板")]
        private void HandleHidePanel(object data)
        {
            if (MatchUIID(data))
            {
                期望隐藏面板?.Invoke(data);
            }
        }

        /// <summary>
        /// 期望隐藏所有面板 - 内部中转站（跳过 ID 检查，直接广播）
        /// </summary>
        [Subscribe("期望隐藏所有面板")]
        private void HandleHideAllPanels(object data)
        {
            // 跳过 ID 检查，直接调用期望隐藏面板委托
            期望隐藏面板?.Invoke(data);
        }

        // =========================================================
        //  UI ID 匹配逻辑
        // =========================================================

        /// <summary>
        /// 匹配 UI ID（将 object 转为 string 并对比）
        /// </summary>
        protected bool MatchUIID(object data)
        {
            if (string.IsNullOrEmpty(_uiID))
            {
                Debug.LogWarning($"<color=orange>[SpaceUIAnimator]</color> {gameObject.name} 的 _uiID 为空，无法匹配事件");
                return false;
            }

            string targetID = data as string;
            if (string.IsNullOrEmpty(targetID))
            {
                Debug.LogWarning($"<color=orange>[SpaceUIAnimator]</color> 事件参数不是 string 类型：{data?.GetType().Name}");
                return false;
            }

            bool isMatch = _uiID == targetID;
            if (isMatch)
            {
                Debug.Log($"<color=cyan>[SpaceUIAnimator]</color> UI ID 匹配成功：{_uiID}");
            }
            return isMatch;
        }

        // =========================================================
        //  Unity 原生交互接口（鼠标事件）
        // =========================================================

        /// <summary>
        /// 鼠标滑入
        /// </summary>
        public virtual void OnPointerEnter(PointerEventData eventData)
        {
            鼠标滑入?.Invoke(eventData);
        }

        /// <summary>
        /// 鼠标滑出
        /// </summary>
        public virtual void OnPointerExit(PointerEventData eventData)
        {
            鼠标滑出?.Invoke(eventData);
        }

        /// <summary>
        /// 鼠标点击
        /// </summary>
        public virtual void OnPointerClick(PointerEventData eventData)
        {
            鼠标点击?.Invoke(eventData);
        }

        // =========================================================
        //  原子化动画方法（Atomic Methods）
        // =========================================================

        /// <summary>
        /// 淡入（原子方法：只负责淡入，无副作用）
        /// 保护：如果已经是显示状态（blocksRaycasts = true），禁止执行
        /// </summary>
        public virtual void FadeIn(float? targetRotationY = null)
        {
            // 保护：如果已经是显示状态，禁止执行
            if (_canvasGroup.blocksRaycasts)
            {
                Debug.LogWarning($"<color=orange>[SpaceUIAnimator]</color> {gameObject.name} 已经是显示状态，禁止重复调用 FadeIn()");
                return;
            }

            // Kill 轨道 A 和 B
            _stateTween?.Kill();
            _rotationTween?.Kill();

            // 设置目标旋转
            float targetY = targetRotationY ?? _targetRotationY;
            _targetRotation = Quaternion.Euler(0, targetY, 0);

            // 立即启用射线拦截（动画开始前）
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.alpha = 0f;
            transform.localPosition = _slideInStartPosition - Vector3.up * _slideInOffset;

            // 轨道 A：状态转换
            _stateTween = DOTween.Sequence();
            _stateTween.Join(_canvasGroup.DOFade(1f, _fadeDuration).SetEase(_fadeInEase));
            _stateTween.Join(transform.DOLocalMove(_slideInStartPosition, _fadeDuration).SetEase(_fadeInEase));
            _stateTween.OnComplete(() => IsVisible = true);

            Debug.Log("<color=cyan>[SpaceUIAnimator]</color> 淡入动画开始");
        }

        /// <summary>
        /// 淡出（原子方法：只负责淡出，无副作用）
        /// 保护：如果已经是隐藏状态（blocksRaycasts = false），禁止执行
        /// </summary>
        public virtual void FadeOut()
        {
            // 保护：如果已经是隐藏状态，禁止执行
            if (!_canvasGroup.blocksRaycasts)
            {
                Debug.LogWarning($"<color=orange>[SpaceUIAnimator]</color> {gameObject.name} 已经是隐藏状态，禁止重复调用 FadeOut()");
                return;
            }

            // Kill 轨道 A 和 B
            _stateTween?.Kill();
            _rotationTween?.Kill();

            // 轨道 A：状态转换
            _stateTween = DOTween.Sequence();
            _stateTween.Join(_canvasGroup.DOFade(0f, _fadeDuration).SetEase(_fadeOutEase));
            _stateTween.Join(transform.DOLocalMove(_slideInStartPosition - Vector3.up * _slideInOffset, _fadeDuration).SetEase(_fadeOutEase));
            _stateTween.OnComplete(() =>
            {
                IsVisible = false;
                _canvasGroup.blocksRaycasts = false;  // 动画完成后才禁用鼠标拦截
                // gameObject.SetActive(false);  // 不再禁用，保持 active 以便接收事件
            });

            Debug.Log("<color=cyan>[SpaceUIAnimator]</color> 淡出动画开始");
        }

        /// <summary>
        /// 立即显示（原子方法）
        /// 保护：如果已经是显示状态（blocksRaycasts = true），禁止执行
        /// </summary>
        public virtual void Show()
        {
            // 保护：如果已经是显示状态，禁止执行
            if (_canvasGroup.blocksRaycasts)
            {
                Debug.LogWarning($"<color=orange>[SpaceUIAnimator]</color> {gameObject.name} 已经是显示状态，禁止重复调用 Show()");
                return;
            }

            _stateTween?.Kill();
            _rotationTween?.Kill();

            // 立即启用射线拦截
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.alpha = 1f;
            transform.localPosition = _slideInStartPosition;
            transform.localRotation = _initialRotation;
            _targetRotation = _initialRotation;
            // 重置缩放倍率
            _scaleMultiplier = 1f;
            _breathMultiplier = 1f;
            IsVisible = true;
        }

        /// <summary>
        /// 立即隐藏（原子方法）
        /// 保护：如果已经是隐藏状态（blocksRaycasts = false），禁止执行
        /// </summary>
        public virtual void Hide()
        {
            // 保护：如果已经是隐藏状态，禁止执行
            if (!_canvasGroup.blocksRaycasts)
            {
                Debug.LogWarning($"<color=orange>[SpaceUIAnimator]</color> {gameObject.name} 已经是隐藏状态，禁止重复调用 Hide()");
                return;
            }

            _stateTween?.Kill();
            _rotationTween?.Kill();

            // 立即禁用射线拦截
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            IsVisible = false;
        }

        /// <summary>
        /// 旋转到目标角度（设置目标值，Update 自动触发）
        /// </summary>
        public virtual void RotateTo(float targetRotationY)
        {
            _targetRotation = Quaternion.Euler(0, targetRotationY, 0);
        }

        /// <summary>
        /// 旋转到目标角度（四元数版本）
        /// </summary>
        public virtual void RotateTo(Quaternion targetRotation)
        {
            _targetRotation = targetRotation;
        }

        /// <summary>
        /// 重置旋转（原子方法）
        /// </summary>
        public virtual void ResetRotation()
        {
            _rotationTween?.Kill();
            _targetRotation = _initialRotation;
            transform.localRotation = _initialRotation;
        }

        /// <summary>
        /// 播放缩放动画（轨道 C）
        /// 使用 DOVirtual.Float 驱动虚拟倍率，由 Update 合成到 transform.localScale
        /// </summary>
        public virtual void PlayScaleAnimation()
        {
            _scaleTween?.Kill();

            // 计算目标缩放比例（相对于 _initialScale）
            float targetScaleRatio = _targetScale.x / _initialScale.x;

            // 轨道 C：虚拟缩放动画
            _scaleTween = DOVirtual.Float(_scaleMultiplier, targetScaleRatio, _scaleDuration, v =>
            {
                _scaleMultiplier = v;
            })
            .SetEase(Ease.OutBack);
        }

        /// <summary>
        /// 重置缩放（原子方法）
        /// Kill 轨道 C，并将 _scaleMultiplier 设为 1f
        /// </summary>
        public virtual void ResetScale()
        {
            _scaleTween?.Kill();
            _scaleMultiplier = 1f;
        }

        /// <summary>
        /// 设置目标缩放值（相对于初始缩放）
        /// 注意：这是绝对值，不是比例
        /// </summary>
        public virtual void SetTargetScale(Vector3 scale)
        {
            _targetScale = scale;
        }

        /// <summary>
        /// 开始呼吸效果（轨道 D）
        /// 使用 DOVirtual.Float 驱动虚拟倍率，在 1f ~ 1f+_breathScaleAmplitude 之间循环
        /// 由 Update 合成到 transform.localScale
        /// </summary>
        public virtual void StartBreathing()
        {
            _breathTween?.Kill();

            // 轨道 D：虚拟呼吸循环
            _breathTween = DOVirtual.Float(1f, 1f + _breathScaleAmplitude, _breathDuration, v =>
            {
                _breathMultiplier = v;
            })
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
        }

        /// <summary>
        /// 停止呼吸效果（原子方法）
        /// Kill 轨道 D，并将 _breathMultiplier 设为 1f
        /// </summary>
        public virtual void StopBreathing()
        {
            _breathTween?.Kill();
            _breathMultiplier = 1f;
        }

        // =========================================================
        //  内部方法
        // =========================================================
        /// <summary>
        /// 检查并应用旋转（Update 自动调用）
        /// </summary>
        private void CheckAndApplyRotation()
        {
            if (Quaternion.Angle(transform.localRotation, _targetRotation) > _rotationTolerance)
            {
                if (_rotationTween == null || !_rotationTween.IsActive() || !_rotationTween.IsPlaying())
                {
                    ApplyRotationTween();
                }
            }
        }

        /// <summary>
        /// 应用旋转补间（轨道 B）
        /// </summary>
        private void ApplyRotationTween()
        {
            _rotationTween?.Kill();
            _rotationTween = transform.DOLocalRotateQuaternion(_targetRotation, _rotationDuration).SetEase(_rotationEase);
        }
    }
}
