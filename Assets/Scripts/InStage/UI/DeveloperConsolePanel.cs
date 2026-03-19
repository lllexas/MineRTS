using System;
using MineRTS.BigMap.UI.Panels;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ═══════════════════════════════════════════════════════════════
/// DeveloperConsolePanel - 开发者控制台 UI 面板
/// ═══════════════════════════════════════════════════════════════
///
/// 职责：
/// 1. 持有 DeveloperConsole 的 UI 引用
/// 2. 订阅 DeveloperConsole 的输出事件
/// 3. 处理输入提交和键盘监听
/// 4. 面板显示/隐藏/切换管理（带淡入淡出动画）
///
/// 继承关系：
///   SpaceUIAnimator
///   ↑
///   ConsolePanelBase<DeveloperConsole>
///   ↑
///   DeveloperConsolePanel (开发终端特化)
///
/// ═══════════════════════════════════════════════════════════════
/// </summary>
public class DeveloperConsolePanel : ConsolePanelBase<DeveloperConsole>
{
    // =========================================================
    //  配置
    // =========================================================
    [Header("Developer Console Settings")]
    [Tooltip("是否使用波浪键 (~) 切换面板显示")]
    [SerializeField] private bool enableTildeKeyToggle = true;

    [Header("Developer Console")] 
    [SerializeField] private DeveloperConsole _developerConsole; // 直接在 Inspector 中拖拽引用

    // =========================================================
    //  抽象接口实现
    // =========================================================

    protected override void Start()
    {
        base.Start();
        if (_developerConsole == null)
        {
            _developerConsole = GetComponent<DeveloperConsole>();
            if (_developerConsole == null)
            {
                Debug.LogError("[DeveloperConsolePanel] Start: DeveloperConsole reference is missing and not found on the same GameObject!");
            }
        }
    }

    protected override DeveloperConsole ConsoleLogic => _developerConsole;

    protected override string GetPrompt()
    {
        return "> ";
    }

    public override void OnSubmitCommand(string input)
    {
        var logic = _developerConsole;
        if (logic != null)
        {
            logic.ProcessCommand(input);
        }
        else
        {
            Debug.LogError("[DeveloperConsolePanel] DeveloperConsole reference is missing!");
        }
    }

    // =========================================================
    //  重写基类方法
    // =========================================================

    protected override void OnEnable()
    {
        base.OnEnable();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
    }

    protected override void Update()
    {
        base.Update();

        // 波浪键切换面板
        if (enableTildeKeyToggle && Input.GetKeyDown(KeyCode.BackQuote))
        {
            Toggle();
        }
    }

    private void Toggle()
    {
        if (_canvasGroup.blocksRaycasts)
        {
            // 隐藏前检测并删除最后一个 ` 字符
            if (inputField != null && !string.IsNullOrEmpty(inputField.text))
            {
                string text = inputField.text;
                if (text.Length > 0 && text[text.Length - 1] == '`')
                {
                    inputField.text = text.Substring(0, text.Length - 1);
                }
            }
            Hide();
        }
        else
        {
            Show();
        }
    }

    // =========================================================
    //  PostSystem 标签订阅方法
    // =========================================================

    [Subscribe("DeveloperConsole.Output")]
    private void HandleOutput(object data)
    {
        if (data is DeveloperConsole.ConsoleOutputEvent evt)
        {
            Output(evt.message, evt.color);
        }
    }
}
