using SpaceTUI;

/// <summary>
/// Back-compat shim for project code that still derives from the old console type.
/// Keep this in the main project so NekoGraph can continue evolving independently.
/// </summary>
[System.Obsolete("DeveloperConsole 已迁移为 ConsoleManager。新代码请直接继承 ConsoleManager。", false)]
public class DeveloperConsole : ConsoleManager
{
}
