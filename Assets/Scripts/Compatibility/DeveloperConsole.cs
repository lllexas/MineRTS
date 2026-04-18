using SpaceTUI;

/// <summary>
/// Back-compat shim for project code that still derives from the old console type.
/// Keep this in the main project so NekoGraph can continue evolving independently.
/// </summary>
public class DeveloperConsole : ConsoleManager
{
}
