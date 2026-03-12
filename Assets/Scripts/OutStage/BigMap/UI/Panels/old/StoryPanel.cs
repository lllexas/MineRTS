#if false   // 【OLD】已废弃，保留参考喵~
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace MineRTS.BigMap.UI
{
    /// <summary>
    /// 剧情面板
    /// 显示关卡进入剧情或过场剧情
    /// 【Newtonsoft.Json + TypeNameHandling.Auto 驱动】
    /// </summary>
    public class StoryPanel : AnimatedPanelBase
    {
        /// <summary>
        /// Newtonsoft.Json 序列化设置 - 自动读取类型信息喵~
        /// </summary>
        private static readonly JsonSerializerSettings GraphJsonSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            NullValueHandling = NullValueHandling.Ignore
        };
        [Header("剧情文本区域")]
        [SerializeField] private TextMeshProUGUI _storyTitleText;
        [SerializeField] private TextMeshProUGUI _storyContentText;

        [Header("按钮")]
        [SerializeField] private Button _nextButton;
        [SerializeField] private Button _skipButton;

        [Header("配置")]
        [Tooltip("自动隐藏延迟（秒）")]
        [SerializeField] private float _autoHideDelay = 1f;

        // 当前剧情数据
        private StoryPackData _currentStory;
        private int _currentSequenceIndex = 0;
        private int _currentLineIndex = 0;

        // 回调事件
        private System.Action _onStoryComplete;

        private void Awake()
        {
            base.Awake();

            // 绑定按钮事件
            if (_nextButton != null)
                _nextButton.onClick.AddListener(OnNextClicked);

            if (_skipButton != null)
                _skipButton.onClick.AddListener(OnSkipClicked);

            Debug.Log("<color=cyan>[StoryPanel]</color> 初始化完成");
        }

        /// <summary>
        /// 加载剧情
        /// 【Newtonsoft.Json + TypeNameHandling.Auto 驱动】
        /// </summary>
        public void LoadStory(string storyID)
        {
            // 从 Resources 加载剧情数据
            TextAsset jsonAsset = Resources.Load<TextAsset>($"Story/{storyID}");
            if (jsonAsset == null)
            {
                Debug.LogError($"<color=red>[StoryPanel]</color> 找不到剧情数据：{storyID}");
                return;
            }

            _currentStory = JsonConvert.DeserializeObject<StoryPackData>(jsonAsset.text, GraphJsonSettings);
            if (_currentStory == null || _currentStory.Sequences == null || _currentStory.Sequences.Count == 0)
            {
                Debug.LogError($"<color=red>[StoryPanel]</color> 剧情数据格式错误：{storyID}");
                return;
            }

            // 重置索引
            _currentSequenceIndex = 0;
            _currentLineIndex = 0;

            // 显示第一句台词
            ShowCurrentLine();

            Debug.Log($"<color=cyan>[StoryPanel]</color> 剧情加载成功：{storyID}");
        }

        /// <summary>
        /// 加载剧情（直接传入数据）
        /// </summary>
        public void LoadStory(StoryPackData storyData)
        {
            _currentStory = storyData;
            _currentSequenceIndex = 0;
            _currentLineIndex = 0;
            ShowCurrentLine();
        }

        /// <summary>
        /// 设置剧情完成回调
        /// </summary>
        public void SetOnCompleteCallback(System.Action callback)
        {
            _onStoryComplete = callback;
        }

        /// <summary>
        /// 显示当前台词
        /// </summary>
        private void ShowCurrentLine()
        {
            if (_currentStory == null) return;
            if (_currentSequenceIndex >= _currentStory.Sequences.Count)
            {
                OnStoryComplete();
                return;
            }

            var sequence = _currentStory.Sequences[_currentSequenceIndex];
            if (_currentLineIndex >= sequence.Lines.Count)
            {
                // 当前序列结束，进入下一个序列
                _currentSequenceIndex++;
                _currentLineIndex = 0;

                if (_currentSequenceIndex >= _currentStory.Sequences.Count)
                {
                    OnStoryComplete();
                    return;
                }

                sequence = _currentStory.Sequences[_currentSequenceIndex];
            }

            var line = sequence.Lines[_currentLineIndex];

            // 更新 UI
            if (_storyTitleText != null)
                _storyTitleText.text = sequence.Title;

            if (_storyContentText != null)
            {
                _storyContentText.text = line.Text;
                // 可以添加打字机效果
            }

            Debug.Log($"<color=cyan>[StoryPanel]</color> 显示台词 {_currentSequenceIndex}:{_currentLineIndex} - {line.Speaker}");
        }

        // =========================================================
        //  按钮事件处理
        // =========================================================

        /// <summary>
        /// 下一句按钮点击
        /// </summary>
        private void OnNextClicked()
        {
            _currentLineIndex++;
            ShowCurrentLine();
        }

        /// <summary>
        /// 跳过按钮点击
        /// </summary>
        private void OnSkipClicked()
        {
            OnStoryComplete();
        }

        /// <summary>
        /// 剧情播放完成
        /// </summary>
        private void OnStoryComplete()
        {
            Debug.Log("<color=cyan>[StoryPanel]</color> 剧情播放完成");

            // 调用回调
            _onStoryComplete?.Invoke();

            // 隐藏面板
            Close();
        }
    }
}
#endif
