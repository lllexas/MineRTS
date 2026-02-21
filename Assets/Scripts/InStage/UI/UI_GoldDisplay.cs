using TMPro;
using UnityEngine;
using UnityEngine.UI; // 如果你用 TextMeshPro，请换成 using TMPro;

public class UI_GoldDisplay : MonoBehaviour
{
    [Header("UI 引用")]
    public TextMeshProUGUI goldText; // 或者 public TextMeshProUGUI goldText;

    [Header("显示设置")]
    public string prefix = "GOLD: ";

    private void Awake()
    {
        if (goldText == null) goldText = GetComponent<TextMeshProUGUI>();
    }

    private void OnEnable()
    {
        // 1. 告诉邮局：我上班啦，请把我的信件送过来喵！
        PostSystem.Instance.Register(this);

        // 初始同步一次（防止 UI 刚显示时没数字）
        UpdateDisplay(IndustrialSystem.Instance.Gold);
    }

    private void OnDisable()
    {
        // 2. 告诉邮局：我下班啦，别给我寄信了喵！
        if (PostSystem.Instance != null)
            PostSystem.Instance.Unregister(this);
    }

    // 3. 核心监听方法
    [Subscribe("更新总金币")]
    public void OnGoldChanged(object args)
    {
        // args 就是我们 Send 出来的那个 Gold (long)
        if (args is long currentGold)
        {
            UpdateDisplay(currentGold);
        }
    }

    private void UpdateDisplay(long amount)
    {
        if (goldText != null)
        {
            // 用 "N0" 可以让数字带千分位分隔符，更有暴发户的感觉喵！
            goldText.text = prefix + amount.ToString("N0");
        }
    }
}