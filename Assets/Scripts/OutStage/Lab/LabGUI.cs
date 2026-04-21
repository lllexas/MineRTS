using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LabGUI : MonoBehaviour
{
    [System.Serializable]
    public sealed class DisplayData
    {
        public string Title;
        public string[] Lines;
        public string Footer;
        public string PrimaryActionText;
        public bool PrimaryActionVisible;
        public bool PrimaryActionInteractable;
        public Action PrimaryAction;
    }

    [Header("Lab Window")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private TextMeshProUGUI footerText;
    [SerializeField] private Button primaryActionButton;
    [SerializeField] private TextMeshProUGUI primaryActionButtonText;

    public DisplayData CurrentData { get; private set; }

    private void Awake()
    {
        if (primaryActionButton != null)
        {
            primaryActionButton.onClick.AddListener(OnPrimaryActionClicked);
        }
    }

    public void Render(DisplayData data)
    {
        CurrentData = data ?? new DisplayData();

        if (titleText != null)
        {
            titleText.text = CurrentData.Title ?? string.Empty;
        }

        if (bodyText != null)
        {
            bodyText.text = CurrentData.Lines == null
                ? string.Empty
                : string.Join("\n", CurrentData.Lines);
        }

        if (footerText != null)
        {
            footerText.text = CurrentData.Footer ?? string.Empty;
        }

        if (primaryActionButtonText != null)
        {
            primaryActionButtonText.text = string.IsNullOrWhiteSpace(CurrentData.PrimaryActionText)
                ? "Action"
                : CurrentData.PrimaryActionText;
        }

        if (primaryActionButton != null)
        {
            primaryActionButton.gameObject.SetActive(CurrentData.PrimaryActionVisible);
            primaryActionButton.interactable = CurrentData.PrimaryActionInteractable;
        }
    }

    public void ClearView()
    {
        Render(new DisplayData());
    }

    private void OnPrimaryActionClicked()
    {
        CurrentData?.PrimaryAction?.Invoke();
    }
}
