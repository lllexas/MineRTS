using TMPro;
using UnityEngine;

public class UI_TimeDisplay : MonoBehaviour
{
    [Header("UI 引用")]
    public TextMeshProUGUI timeText;

    [Header("显示设置")]
    public string prefix = "";
    public bool alwaysShowHours = false; // 是否总是显示小时栏位
    public bool showMilliseconds = false; // 是否显示毫秒

    private void Awake()
    {
        if (timeText == null) timeText = GetComponent<TextMeshProUGUI>();
    }

    private void OnEnable()
    {
        // 注册到事件系统
        PostSystem.Instance.Register(this);

        // 初始同步显示
        UpdateDisplay();
    }

    private void OnDisable()
    {
        // 从事件系统注销
        if (PostSystem.Instance != null)
            PostSystem.Instance.Unregister(this);
    }

    // 监听时间增加事件
    [Subscribe("生存时间增加")]
    public void OnTimeIncreased(object args)
    {
        // 事件触发时更新显示
        UpdateDisplay();
    }

    // 更新显示文本
    private void UpdateDisplay()
    {
        if (timeText == null) return;
        if (TimeSystem.Instance == null) return;

        float totalSeconds = TimeSystem.Instance.TotalElapsedSeconds;

        int hours = (int)(totalSeconds / 3600);
        int minutes = (int)((totalSeconds % 3600) / 60);
        int seconds = (int)(totalSeconds % 60);
        int milliseconds = (int)((totalSeconds * 1000) % 1000);

        string timeString;

        if (alwaysShowHours || hours > 0)
        {
            // 显示小时:分钟:秒
            if (showMilliseconds)
                timeString = $"{hours}:{minutes:00}:{seconds:00}.{milliseconds:000}";
            else
                timeString = $"{hours}:{minutes:00}:{seconds:00}";
        }
        else
        {
            // 不显示小时，只显示分钟:秒
            if (showMilliseconds)
                timeString = $"{minutes}:{seconds:00}.{milliseconds:000}";
            else
                timeString = $"{minutes}:{seconds:00}";
        }

        timeText.text = prefix + timeString;
    }

    // 可选：如果需要更平滑的更新（例如显示毫秒），可以在Update中直接读取TimeSystem
    // 但这样会增加每帧的消耗，根据需求选择
    /*
    private void Update()
    {
        UpdateDisplay();
    }
    */
}