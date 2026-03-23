using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class TUISelectSlot : IConsoleInputHandler, IConsoleInputLineState
{
    private TUISelectionConfig _config;
    private readonly int _startLine;
    private int _renderedHeight;
    private int _selectedIndex = -1;

    protected TUISelectSlot(TUISelectionConfig config)
    {
        _config = config;
        _startLine = config.console != null ? config.console.InputHandleStartLine : 0;

        NormalizeSelection(useInitialKey: true);
        Render();
    }

    public int StartLine => _startLine;
    public int RenderedHeight => _renderedHeight;
    public int SelectedIndex => _selectedIndex;

    protected TUISelectionConfig Config => _config;
    protected DeveloperConsole Console => _config.console;
    protected int ItemCount => _config.items?.Count ?? 0;
    protected bool HasItems => ItemCount > 0;
    protected int ConsoleWidth => Console?.ConsoleWidth ?? 0;
    public virtual bool ShouldRenderInputLine => false;

    public void UpdateConfig(TUISelectionConfig config, bool resetSelection = false)
    {
        _config = config;
        NormalizeSelection(useInitialKey: resetSelection);
        Render();
    }

    public void Refresh()
    {
        NormalizeSelection(useInitialKey: false);
        Render();
    }

    public virtual bool HandleKey(KeyInfo key)
    {
        // 回车键 → HandleConfirm
        if (key.keyCode == KeyCode.Return || key.keyCode == KeyCode.KeypadEnter)
        {
            if (!key.isShiftDown) // Shift+Enter 不确认
            {
                return HandleConfirm();
            }
            return false; // Shift+Enter 交给输入框处理
        }

        // Esc 键 → HandleCancel
        if (key.keyCode == KeyCode.Escape)
        {
            return HandleCancel();
        }

        // 方向键 → HandleNavigation
        if (key.keyCode == KeyCode.UpArrow)
        {
            return HandleNavigation(ConsoleNavKey.Up);
        }
        if (key.keyCode == KeyCode.DownArrow)
        {
            return HandleNavigation(ConsoleNavKey.Down);
        }
        if (key.keyCode == KeyCode.Home)
        {
            return HandleNavigation(ConsoleNavKey.Home);
        }
        if (key.keyCode == KeyCode.End)
        {
            return HandleNavigation(ConsoleNavKey.End);
        }
        if (key.keyCode == KeyCode.LeftArrow)
        {
            return HandleNavigation(ConsoleNavKey.Left);
        }
        if (key.keyCode == KeyCode.RightArrow)
        {
            return HandleNavigation(ConsoleNavKey.Right);
        }

        // 其他键默认不处理
        return false;
    }

    public virtual bool HandleSubmit(string input)
    {
        string trimmed = input?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(trimmed))
        {
            return _config.interaction.allowConfirmOnEmptySubmit && HandleConfirm();
        }

        if (!_config.interaction.enableDigitSelect)
        {
            return false;
        }

        int directIndex = FindItemIndexByDirectInput(trimmed);
        if (directIndex < 0)
        {
            return false;
        }

        SetSelection(directIndex, notify: true);
        return HandleConfirm();
    }

    public virtual bool HandleNavigation(ConsoleNavKey key)
    {
        if (!HasItems)
        {
            return false;
        }

        switch (key)
        {
            case ConsoleNavKey.Home:
                SetSelection(0, notify: true);
                return true;

            case ConsoleNavKey.End:
                SetSelection(ItemCount - 1, notify: true);
                return true;

            default:
                return TryNavigate(key);
        }
    }

    public virtual bool HandleConfirm()
    {
        if (!TryGetSelectedItem(out var item))
        {
            return false;
        }

        item.onConfirm?.Invoke();
        _config.interaction.onConfirmSelection?.Invoke(_selectedIndex, item);
        return true;
    }

    public virtual bool HandleCancel()
    {
        if (_config.interaction.onCancel == null)
        {
            return false;
        }

        _config.interaction.onCancel.Invoke();
        return true;
    }

    public virtual string GetInputPrompt(string fallbackPrompt)
    {
        return fallbackPrompt;
    }

    protected void MoveSelection(int delta)
    {
        if (!HasItems)
        {
            return;
        }

        int nextIndex = _selectedIndex;
        if (nextIndex < 0)
        {
            nextIndex = 0;
        }
        else
        {
            nextIndex += delta;
        }

        if (_config.interaction.wrapNavigation)
        {
            nextIndex = (nextIndex % ItemCount + ItemCount) % ItemCount;
        }
        else
        {
            nextIndex = Mathf.Clamp(nextIndex, 0, ItemCount - 1);
        }

        SetSelection(nextIndex, notify: true);
    }

    protected void SetSelection(int index, bool notify)
    {
        if (!HasItems)
        {
            _selectedIndex = -1;
            Render();
            return;
        }

        int clampedIndex = Mathf.Clamp(index, 0, ItemCount - 1);
        if (_selectedIndex == clampedIndex)
        {
            Render();
            return;
        }

        _selectedIndex = clampedIndex;
        Render();

        if (notify && TryGetSelectedItem(out var item))
        {
            _config.interaction.onSelectionChanged?.Invoke(_selectedIndex, item);
        }
    }

    protected void Render()
    {
        if (Console == null)
        {
            return;
        }

        var lines = BuildLines();
        Console.WriteInputHandleRange(_startLine, _renderedHeight, lines);
        _renderedHeight = lines.Count;
    }

    protected string BuildLeftPadding(string plainText, TextAlignment alignment)
    {
        int width = ConsoleWidth;
        if (width <= 0)
        {
            return string.Empty;
        }

        int leftPadding = alignment switch
        {
            TextAlignment.Center => TUITool.CalcCenterPadding(plainText, width),
            TextAlignment.Right => TUITool.CalcRightPadding(plainText, width),
            _ => 0
        };

        return leftPadding > 0 ? new string(' ', leftPadding) : string.Empty;
    }

    protected static void AddBlankLines(List<string> lines, int count)
    {
        for (int i = 0; i < count; i++)
        {
            lines.Add(string.Empty);
        }
    }

    protected string RenderStyledLine(string text, TSSStyle style, bool useTitleColor)
    {
        string safeText = text ?? string.Empty;
        string leftPadding = BuildLeftPadding(safeText, style.alignment);
        Color color = useTitleColor ? style.titleColor : style.contentColor;
        string colorHex = ColorUtility.ToHtmlStringRGB(color);
        return $"{leftPadding}<color=#{colorHex}>{safeText}</color>";
    }

    protected bool TryGetSelectedItem(out TUISelectionItem item)
    {
        if (_selectedIndex >= 0 && _selectedIndex < ItemCount)
        {
            item = _config.items[_selectedIndex];
            return true;
        }

        item = default;
        return false;
    }

    private void NormalizeSelection(bool useInitialKey)
    {
        if (!HasItems)
        {
            _selectedIndex = -1;
            return;
        }

        if (_selectedIndex >= 0 && _selectedIndex < ItemCount)
        {
            return;
        }

        if (useInitialKey && _config.initialSelectedKey >= 0)
        {
            int initialIndex = FindItemIndexByKey(_config.initialSelectedKey);
            if (initialIndex >= 0)
            {
                _selectedIndex = initialIndex;
                return;
            }
        }

        _selectedIndex = 0;
    }

    private int FindItemIndexByDirectInput(string input)
    {
        for (int i = 0; i < ItemCount; i++)
        {
            var item = _config.items[i];
            if (item.key.ToString() == input)
            {
                return i;
            }

            if (!string.IsNullOrEmpty(item.indexText) &&
                string.Equals(item.indexText, input, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private int FindItemIndexByKey(int key)
    {
        for (int i = 0; i < ItemCount; i++)
        {
            if (_config.items[i].key == key)
            {
                return i;
            }
        }

        return -1;
    }

    protected abstract bool TryNavigate(ConsoleNavKey key);
    protected abstract List<string> BuildLines();
}
