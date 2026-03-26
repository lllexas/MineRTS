using UnityEngine;
using UnityEditor;
using MineRTS.BigMap.UI;
using SpaceTUI;

namespace MineRTS.Editor
{
    /// <summary>
    /// SpaceUIAnimator 自定义编辑器
    /// <para>提供编辑器模式下的 UI 旋转控制功能</para>
    /// </summary>
    [CustomEditor(typeof(SpaceUIAnimator), true)]
    public class SpaceUIEditor : UnityEditor.Editor
    {
        // =========================================================
        //  样式定义
        // =========================================================
        private static readonly Color PreviewButtonColor = new Color(1f, 0.5f, 0.2f);  // 橘色 - 预览姿态
        private static readonly Color FlatButtonColor = new Color(0.2f, 0.6f, 1f);     // 蓝色 - 校准姿态

        private GUIStyle _previewButtonStyle;
        private GUIStyle _flatButtonStyle;

        // =========================================================
        //  缓存引用
        // =========================================================
        private SpaceUIAnimator _targetAnimator;
        private Transform _targetTransform;

        // =========================================================
        //  丝滑对齐状态
        // =========================================================
        private bool _isGliding = false;
        private float _glideStartTime;
        private float _glideDuration = 0.5f;
        private Vector3 _startPosition;
        private Quaternion _startRotation;
        private Vector3 _targetPosition;
        private Quaternion _targetRotation;
        private float _startSize;
        private float _targetSize;

        // =========================================================
        //  参考分辨率配置
        // =========================================================
        private static readonly Vector2 REFERENCE_RESOLUTION = new Vector2(1920f, 1080f);

        private void OnEnable()
        {
            _targetAnimator = (SpaceUIAnimator)target;
            _targetTransform = _targetAnimator?.transform;
        }

        private void OnDisable()
        {
            // 清理回调，防止内存泄漏
            EditorApplication.update -= OnGlideUpdate;
        }

        // =========================================================
        //  自定义 Inspector GUI
        // =========================================================
        public override void OnInspectorGUI()
        {
            // 初始化样式
            InitializeStyles();

            // 绘制默认 Inspector
            DrawDefaultInspector();

            EditorGUILayout.Space(15f);

            // 绘制分隔线
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            EditorGUILayout.Space(10f);

            // 标题
            EditorGUILayout.LabelField("🎮 编辑器控制面板", EditorStyles.boldLabel);

            EditorGUILayout.Space(5f);

            EditorGUILayout.HelpBox("在此处可以快速切换 UI 的旋转姿态，方便在编辑器中排布 UI 元素喵~", MessageType.Info);

            EditorGUILayout.Space(10f);

            // 按钮区域
            EditorGUILayout.BeginVertical(GUI.skin.box);

            // 显示当前角度
            float currentY = _targetAnimator != null ? _targetAnimator.GetEditorRotationY() : 0f;
            EditorGUILayout.LabelField($"当前 Y 轴角度：{currentY:F2}°", EditorStyles.boldLabel);

            EditorGUILayout.Space(5f);

            // 按钮布局
            EditorGUILayout.BeginHorizontal();

            // 按钮 A：预览姿态（橘色）
            if (GUILayout.Button("🔍 预览姿态 (Target Y)", _previewButtonStyle, GUILayout.Height(40)))
            {
                PreviewTargetRotation();
            }

            // 按钮 B：校准姿态（蓝色）
            if (GUILayout.Button("📐 校准姿态 (Flat 0°)", _flatButtonStyle, GUILayout.Height(40)))
            {
                ResetToFlatRotation();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            // =========================================================
            //  Frame Perfect 校准区域
            // =========================================================
            EditorGUILayout.Space(10f);

            EditorGUILayout.LabelField("📐 Frame Perfect Alignment", EditorStyles.boldLabel);

            EditorGUILayout.Space(5f);

            EditorGUILayout.HelpBox($"丝滑过渡到 {REFERENCE_RESOLUTION.x}x{REFERENCE_RESOLUTION.y} 的 UI 画布完美视角喵~", MessageType.Info);

            EditorGUILayout.Space(10f);

            // 按钮区域：Frame Perfect
            EditorGUILayout.BeginVertical(GUI.skin.box);

            // 按钮：丝滑完美展示（蓝色）
            if (GUILayout.Button("✨ 丝滑完美展示", _flatButtonStyle, GUILayout.Height(45)))
            {
                GlideToPerfectFrame();
            }

            EditorGUILayout.EndVertical();

            // 标记为脏，确保修改被保存
            if (GUI.changed)
            {
                EditorUtility.SetDirty(_targetAnimator);
            }
        }

        // =========================================================
        //  样式初始化
        // =========================================================
        private void InitializeStyles()
        {
            if (_previewButtonStyle == null)
            {
                _previewButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white },
                    hover = { background = MakeTexture(PreviewButtonColor) }
                };
            }

            if (_flatButtonStyle == null)
            {
                _flatButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white },
                    hover = { background = MakeTexture(FlatButtonColor) }
                };
            }
        }

        // =========================================================
        //  按钮功能实现
        // =========================================================

        /// <summary>
        /// 预览姿态 - 旋转到配置的 _targetRotationY
        /// </summary>
        private void PreviewTargetRotation()
        {
            if (_targetAnimator == null || _targetTransform == null) return;

            // 获取目标角度（通过反射或序列化字段）
            SerializedObject serializedObject = new SerializedObject(_targetAnimator);
            float targetY = serializedObject.FindProperty("_targetRotationY").floatValue;

            // 记录撤销操作
            Undo.RecordObject(_targetTransform, "Preview UI Rotation");

            // 直接应用旋转（绕过 DOTween）
            _targetAnimator.ApplyEditorRotation(targetY);

            // 标记为脏
            EditorUtility.SetDirty(_targetTransform);

            Debug.Log($"<color=cyan>[SpaceUIEditor]</color> 已旋转到目标角度：{targetY:F2}°");
        }

        /// <summary>
        /// 校准姿态 - Y 轴归零
        /// </summary>
        private void ResetToFlatRotation()
        {
            if (_targetAnimator == null || _targetTransform == null) return;

            // 记录撤销操作
            Undo.RecordObject(_targetTransform, "Reset UI Rotation to Flat");

            // 直接应用旋转（Y 轴归零）
            _targetAnimator.ApplyEditorRotation(0f);

            // 标记为脏
            EditorUtility.SetDirty(_targetTransform);

            Debug.Log($"<color=cyan>[SpaceUIEditor]</color> 已校准为平面姿态：0°");
        }

        // =========================================================
        //  Frame Perfect Alignment 功能实现
        // =========================================================

        /// <summary>
        /// 丝滑完美展示 - 使用完美展示逻辑计算目标值，然后丝滑过渡过去
        /// </summary>
        private void GlideToPerfectFrame()
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                EditorUtility.DisplayDialog("错误", "没有激活的 Scene 视图！", "好的");
                return;
            }

            // 如果已经在滑动中，先停止
            if (_isGliding)
            {
                EditorApplication.update -= OnGlideUpdate;
            }

            // 定义 UI 矩形：中心点和尺寸
            Vector3 center = new Vector3(REFERENCE_RESOLUTION.x * 0.5f, REFERENCE_RESOLUTION.y * 0.5f, 0f);
            Vector2 rectSize = REFERENCE_RESOLUTION;

            // 获取 SceneView 宽高比
            Camera sceneCamera = sceneView.camera;
            if (sceneCamera == null) return;

            float aspect = sceneCamera.aspect;  // 宽高比（宽/高）

            // 计算目标 size（完美展示逻辑）
            float targetSize = rectSize.y * 0.5f;
            float uiAspect = rectSize.x / rectSize.y;
            if (uiAspect > aspect)
            {
                targetSize = (rectSize.x / aspect) * 0.5f;
            }

            // 初始化滑动状态
            _isGliding = true;
            _glideStartTime = (float)EditorApplication.timeSinceStartup;
            _startPosition = sceneView.pivot;
            _startRotation = sceneView.rotation;
            _startSize = sceneView.size;
            _targetPosition = center;
            _targetRotation = Quaternion.identity;
            _targetSize = targetSize;

            // 注册更新回调
            EditorApplication.update += OnGlideUpdate;

            Debug.Log($"<color=cyan>[SpaceUIEditor]</color> Scene 视图开始丝滑完美展示喵~");
            Debug.Log($"<color=cyan>[SpaceUIEditor]</color> 目标：center={center}, size={targetSize:F2}");
        }

        /// <summary>
        /// 丝滑移动的帧更新回调
        /// </summary>
        private void OnGlideUpdate()
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                EditorApplication.update -= OnGlideUpdate;
                _isGliding = false;
                return;
            }

            // 计算插值进度
            float elapsed = (float)(EditorApplication.timeSinceStartup - _glideStartTime);
            float t = Mathf.Clamp01(elapsed / _glideDuration);

            // 使用缓动函数（OutCubic）让移动更自然
            float easedT = 1 - Mathf.Pow(1 - t, 3);

            // 插值位置和旋转
            Vector3 currentPos = Vector3.Lerp(_startPosition, _targetPosition, easedT);
            Quaternion currentRot = Quaternion.Slerp(_startRotation, _targetRotation, easedT);
            float currentSize = Mathf.Lerp(_startSize, _targetSize, easedT);

            // 应用插值结果
            sceneView.pivot = currentPos;
            sceneView.rotation = currentRot;
            sceneView.size = currentSize;
            sceneView.Repaint();

            // 检查是否完成
            if (t >= 1f)
            {
                EditorApplication.update -= OnGlideUpdate;
                _isGliding = false;
                Debug.Log($"<color=cyan>[SpaceUIEditor]</color> Scene 视图已到达完美视角喵~");
            }
        }

        // =========================================================
        //  工具方法
        // =========================================================

        /// <summary>
        /// 创建纯色纹理（用于按钮背景）
        /// </summary>
        private Texture2D MakeTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}
