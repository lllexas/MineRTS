using System;
using UnityEngine.UI;
using UnityEngine;

public class InStageUIManager : SingletonMono<InStageUIManager>, IMenuPanel
{
    public GameObject _panelRoot;
    private bool _isPanelOpen = false;
    public GameObject PanelRoot => _panelRoot;

    public bool IsOpen => _isPanelOpen;

    public void Open()
    {
        if (_panelRoot.activeInHierarchy)
        {
            return;
        }
        _panelRoot.SetActive(true);
        _isPanelOpen = true;
    }
    public void Close()
    {
        if (!_panelRoot.activeInHierarchy)
        {
            return;
        }
        _panelRoot.SetActive(false);
        _isPanelOpen = false;
    }
}