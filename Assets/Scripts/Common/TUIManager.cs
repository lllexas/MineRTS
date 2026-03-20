using System;
using UnityEngine;

/// <summary>
/// TUI 管理层基类 —— 所有可被 ConsoleDisplayBase 驱动的逻辑层均继承此类。
/// 提供三个 ConsoleDisplayBase 必需的最小接口：
///   · OnClearRequested  清屏请求事件
///   · ConsoleWidth      终端列宽（由面板层在 Start 后注入）
///   · ConsoleHeight     终端可见行高（由面板层在 Start 后注入）
/// </summary>
public abstract class TUIManager : MonoBehaviour
{
    /// <summary>请求清空显示缓冲区</summary>
    public event Action OnClearRequested;

    /// <summary>终端列宽（由 ConsoleDisplayBase 在 Start 中注入）</summary>
    private int _consoleWidth = 80;

    public virtual int ConsoleWidth
    {
        get => _consoleWidth;
        set
        {
            if (_consoleWidth == value) return;
            _consoleWidth = value;
            OnConsoleWidthChanged(value);
        }
    }

    /// <summary>ConsoleWidth 被注入新值时调用（子类可重写以响应宽度变化）</summary>
    protected virtual void OnConsoleWidthChanged(int newWidth) { }

    /// <summary>终端可见行高（由 ConsoleDisplayBase 在 Start 中注入）</summary>
    private int _consoleHeight = 25;

    public virtual int ConsoleHeight
    {
        get => _consoleHeight;
        set
        {
            if (_consoleHeight == value) return;
            _consoleHeight = value;
            OnConsoleHeightChanged(value);
        }
    }

    /// <summary>ConsoleHeight 被注入新值时调用（子类可重写以响应高度变化）</summary>
    protected virtual void OnConsoleHeightChanged(int newHeight) { }

    /// <summary>子类调用此方法触发清屏（事件只能由声明类内部 invoke）</summary>
    protected void InvokeClearRequested() => OnClearRequested?.Invoke();
}
