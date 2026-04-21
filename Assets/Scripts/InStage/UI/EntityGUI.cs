using TMPro;
using UnityEngine;

public class EntityGUI : MonoBehaviour
{
    [System.Serializable]
    public sealed class DisplayData
    {
        public string Title;
        public string[] Lines;
        public string Footer;
    }

    [Header("Entity Window")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private TextMeshProUGUI footerText;

    public DisplayData CurrentData { get; private set; }

    public void Render(DisplayData data)
    {
        CurrentData = data ?? new DisplayData();

        if (titleText != null)
            titleText.text = CurrentData.Title ?? string.Empty;

        if (bodyText != null)
            bodyText.text = CurrentData.Lines == null ? string.Empty : string.Join("\n", CurrentData.Lines);

        if (footerText != null)
            footerText.text = CurrentData.Footer ?? string.Empty;
    }

    public void ClearView()
    {
        Render(new DisplayData());
    }
}
