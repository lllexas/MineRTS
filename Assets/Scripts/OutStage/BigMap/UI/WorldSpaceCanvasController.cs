using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace MineRTS.BigMap.UI
{
    /// <summary>
    /// 世界空间 UI 动画单元
    ///
    /// <para>【职责】控制 World Space UI 元素的动画效果（淡入淡出、缩放、旋转等）</para>
    ///
    /// <para>【动效原子化】可挂载到任何 UI 元素上：Canvas 根部、子面板、按钮等</para>
    ///
    /// <para>【三层 Tween 协议】</para>
    /// <para>轨道 A：状态转换轨 - FadeIn/FadeOut（alpha + position）</para>
    /// <para>轨道 B：旋转轨 - 绕 Y 轴旋转（由 Update 监控 - 触发模式控制）</para>
    /// <para>轨道 C：呼吸循环轨 - 微小缩放循环（作用于 _content）</para>
    ///
    /// <example>使用场景：
    /// <code>
    /// 1. 挂在 Canvas 根部：控制整个大盘子的入场、旋转和整体呼吸
    /// 2. 挂在子面板 (Panel) 上：控制面板在盘子里的独立倾斜、弹出和呼吸
    /// 3. 挂在按钮 (Button) 上：实现"悬浮时微微翘起并呼吸"的高级 3D 交互
    /// </code>
    /// </example>
    ///
    /// <example>父子补间叠加：
    /// <code>
    /// // 父级（Canvas）在做 0.5s 的整体慢速入场旋转
    /// canvasController.FadeIn(0f);
    /// // 子级（Panel）同时做 0.2s 的快速弹出旋转
    /// panelController.FadeIn(90f);
    /// // 结果：两个旋转在 3D 空间中叠加，产生细腻的"机械展开动画"
    /// </code>
    /// </example>
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class WorldSpaceUIAnimateUnit : MonoBehaviour
    {
        [Header("动画配置")]
        [Tooltip("淡入/淡出动画时长（秒）")]
        [SerializeField] private float _fadeDuration = 0.3f;

        [Tooltip("缩放动画时长（秒）")]
        [SerializeField] private float _scaleDuration = 0.2f;

        [Tooltip("目标缩放值")]
        [SerializeField] private Vector3 _targetScale = Vector3.one;

        [Tooltip("浮现动画位移量（Y 轴向上）")]
        [SerializeField] private float _slideInOffset = 30f;

        [Header("旋转配置")]
        [Tooltip("目标旋转角度（Y 轴，度数）")]
        [SerializeField] private float _targetRotationY = 0f;

        [Tooltip("旋转动画时长（秒）")]
        [SerializeField] private float _rotationDuration = 0.5f;

        [Tooltip("旋转缓动")]
        [SerializeField] private Ease _rotationEase = Ease.OutCubic;

        [Tooltip("旋转容差（角度，小于此值不触发旋转）")]
        [SerializeField] private float _rotationTolerance = 0.1f;

        [Header("呼吸效果配置")]
        [Tooltip("呼吸效果缩放幅度")]
        [SerializeField] private float _breathScaleAmplitude = 0.05f;

        [Tooltip("呼吸效果周期（秒）")]
        [SerializeField] private float _breathDuration = 1.5f;

        [Header("缓动曲线")]
        [Tooltip("淡入缓动")]
        [SerializeField] private Ease _fadeInEase = Ease.OutCubic;

        [Tooltip("淡出缓动")]
        [SerializeField] private Ease _fadeOutEase = Ease.InCubic;

        [Header("组件引用")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Transform _content;  // 呼吸效果作用的子物体

        // 缓存的初始状态（锚点）
        private Vector3 _initialPosition;
        private Quaternion _initialRotation;
        private Vector3 _initialScale;

        // 滑动偏移
        private Vector3 _slideInStartPosition;

        // 三层 Tween 隔离设计
        // 轨道 A：状态转换轨 - 负责 FadeIn/FadeOut（alpha + position）
        private Sequence _stateSequence;

        // 轨道 B：动态交互轨 - 负责 RotateTo/ScaleTo
        private Tween _rotationTween;

        // 轨道 C：呼吸循环轨 - 负责微小呼吸感（作用于 _content）
        private Tween _loopTween;

        // 旋转数据层
        private Quaternion _targetRotation;

        /// <summary>
        /// Canvas 是否可见
        /// </summary>
        public bool IsVisible { get; private set; } = true;

        private void Awake()
        {
            // 尝试获取组件（不是强制要求）
            _canvas = GetComponent<Canvas>();
            _canvasGroup = GetComponent<CanvasGroup>();

            // 如果没有指定 _content，使用自身 transform
            if (_content == null)
                _content = transform;

            // 记录锚点：所有动画基于这些初始值做相对偏移
            _initialPosition = transform.localPosition;
            _initialRotation = transform.localRotation;
            _initialScale = transform.localScale;
            _targetScale = _initialScale;
            _slideInStartPosition = _initialPosition;
            _targetRotation = _initialRotation;

            // 只有是根 Canvas 时才配置相机
            if (_canvas != null)
            {
                _canvas.worldCamera = Camera.main;
                _canvas.sortingOrder = 10; // UI 层在底图之上
            }

            // 初始状态：alpha = 0, active = false
            if (_canvasGroup != null)
                _canvasGroup.alpha = 0f;
            gameObject.SetActive(false);

            Debug.Log($"<color=cyan>[WorldSpaceUIAnimateUnit]</color> 初始化完成：{gameObject.name}");
        }

        private void OnDestroy()
        {
            // 清理所有 Tween 轨道
            _stateSequence?.Kill();
            _rotationTween?.Kill();
            _loopTween?.Kill();
        }

        private void Update()
        {
            // 监控层：每帧检查旋转角度差
            // 只有当角度差超过容差，且当前没有正在运行的旋转 Tween 时，才触发新的旋转
            if (Quaternion.Angle(transform.localRotation, _targetRotation) > _rotationTolerance)
            {
                // 如果当前没有正在跑的旋转 Tween，才触发
                if (_rotationTween == null || !_rotationTween.IsActive() || !_rotationTween.IsPlaying())
                {
                    ApplyRotationTween();
                }
            }
        }

        /// <summary>
        /// 淡入 Canvas（透明度渐变 + 从下往上滑动 + 旋转到目标角度）
        /// </summary>
        /// <param name="targetRotationY">目标旋转角度（Y 轴），默认使用配置值</param>
        public void FadeIn(float? targetRotationY = null)
        {
            // Kill 掉所有正在运行的 Tween
            _stateSequence?.Kill();
            _rotationTween?.Kill();
            StopBreathing();

            // 设置目标旋转角度
            float targetY = targetRotationY ?? _targetRotationY;
            _targetRotation = Quaternion.Euler(0, targetY, 0);

            gameObject.SetActive(true);

            // 设置初始状态：透明 + 偏移位置
            _canvasGroup.alpha = 0f;
            transform.localPosition = _slideInStartPosition - Vector3.up * _slideInOffset;

            // 构建 Sequence：轨道 A - 状态转换轨
            _stateSequence = DOTween.Sequence();

            // Join: Alpha 0 -> 1
            _stateSequence.Join(_canvasGroup
                .DOFade(1f, _fadeDuration)
                .SetEase(_fadeInEase));

            // Join: Position (Offset) -> InitialPos
            _stateSequence.Join(transform
                .DOLocalMove(_slideInStartPosition, _fadeDuration)
                .SetEase(_fadeInEase));

            // 回调 OnComplete：执行 StartBreathing()
            _stateSequence.OnComplete(() =>
            {
                IsVisible = true;
                StartBreathing();
            });

            Debug.Log("<color=cyan>[WorldSpaceUIAnimateUnit]</color> 淡入动画开始");
        }

        /// <summary>
        /// 淡出 Canvas（透明度渐变 + 向上滑动消失）
        /// </summary>
        public void FadeOut()
        {
            // 停止呼吸：立即 Kill 呼吸循环轨
            StopBreathing();

            // Kill 掉状态转换轨和旋转轨
            _stateSequence?.Kill();
            _rotationTween?.Kill();

            // 构建 Sequence：轨道 A - 状态转换轨
            _stateSequence = DOTween.Sequence();

            // Alpha 1 -> 0
            _stateSequence.Join(_canvasGroup
                .DOFade(0f, _fadeDuration)
                .SetEase(_fadeOutEase));

            // 位移消失动画
            _stateSequence.Join(transform
                .DOLocalMove(_slideInStartPosition - Vector3.up * _slideInOffset, _fadeDuration)
                .SetEase(_fadeOutEase));

            // 回调 OnComplete：SetActive(false)
            _stateSequence.OnComplete(() =>
            {
                gameObject.SetActive(false);
                IsVisible = false;
            });

            Debug.Log("<color=cyan>[WorldSpaceUIAnimateUnit]</color> 淡出动画开始");
        }

        /// <summary>
        /// 立即显示 Canvas（无动画）
        /// </summary>
        public void Show()
        {
            _stateSequence?.Kill();
            _rotationTween?.Kill();
            StopBreathing();

            gameObject.SetActive(true);
            _canvasGroup.alpha = 1f;
            transform.localPosition = _slideInStartPosition;
            transform.localRotation = _initialRotation;
            _targetRotation = _initialRotation;
            if (_content != null)
                _content.localScale = Vector3.one;
            IsVisible = true;
            StartBreathing();
        }

        /// <summary>
        /// 立即隐藏 Canvas（无动画）
        /// </summary>
        public void Hide()
        {
            _stateSequence?.Kill();
            _rotationTween?.Kill();
            StopBreathing();

            gameObject.SetActive(false);
            _canvasGroup.alpha = 0f;
            IsVisible = false;
        }

        /// <summary>
        /// 播放缩放动画（轨道 B：动态交互轨）
        /// 注意：缩放作用于 _content 子物体，与呼吸效果解耦
        /// </summary>
        public void PlayScaleAnimation()
        {
            _loopTween?.Kill();  // 先停止呼吸效果

            _loopTween = _content
                .DOScale(_targetScale, _scaleDuration)
                .SetEase(Ease.OutBack);
        }

        /// <summary>
        /// 重置缩放（轨道 B）
        /// </summary>
        public void ResetScale()
        {
            _loopTween?.Kill();
            if (_content != null)
                _content.localScale = Vector3.one;
        }

        /// <summary>
        /// 设置目标缩放值
        /// </summary>
        public void SetTargetScale(Vector3 scale)
        {
            _targetScale = scale;
        }

        /// <summary>
        /// 执行层：应用旋转补间
        /// 使用 DOLocalRotateQuaternion 确保平滑旋转（局部空间）
        /// </summary>
        private void ApplyRotationTween()
        {
            // 确保在开启新 Tween 前，先 Kill 掉旧的旋转 Tween
            _rotationTween?.Kill();

            _rotationTween = transform
                .DOLocalRotateQuaternion(_targetRotation, _rotationDuration)
                .SetEase(_rotationEase);
        }

        /// <summary>
        /// 旋转到目标角度（Y 轴）
        /// 设置目标值，由 Update 监控层自动触发旋转
        /// </summary>
        /// <param name="targetRotationY">目标 Y 轴角度</param>
        public void RotateTo(float targetRotationY)
        {
            _targetRotation = Quaternion.Euler(0, targetRotationY, 0);
        }

        /// <summary>
        /// 旋转到目标角度（四元数版本）
        /// 设置目标值，由 Update 监控层自动触发旋转
        /// </summary>
        public void RotateTo(Quaternion targetRotation)
        {
            _targetRotation = targetRotation;
        }

        /// <summary>
        /// 重置旋转
        /// </summary>
        public void ResetRotation()
        {
            _rotationTween?.Kill();
            _targetRotation = _initialRotation;
            transform.localRotation = _initialRotation;
        }

        /// <summary>
        /// 设置目标旋转角度（Y 轴）
        /// </summary>
        public void SetTargetRotationY(float rotationY)
        {
            _targetRotationY = rotationY;
        }

        /// <summary>
        /// 开始呼吸效果（轨道 C：呼吸循环轨）
        /// 呼吸效果独立作用于子物体 Content，与旋转代码解耦
        /// 使用 DOScale 在初始值和放大值之间做 Yoyo 循环
        /// </summary>
        private void StartBreathing()
        {
            // 先停止之前的呼吸效果
            StopBreathing();

            // 轨道 C：呼吸循环轨 - 作用于 _content 子物体
            // 在初始缩放和放大值之间循环，SetLoops(-1, LoopType.Yoyo) 实现无限往复
            _loopTween = _content
                .DOScale(_initialScale * (1f + _breathScaleAmplitude), _breathDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }

        /// <summary>
        /// 停止呼吸效果（轨道 C）
        /// UI 开始隐藏前必须彻底 Kill
        /// </summary>
        private void StopBreathing()
        {
            _loopTween?.Kill();

            // 重置缩放为初始值
            if (_content != null)
                _content.localScale = Vector3.one;
        }
    }
}
