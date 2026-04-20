using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using SpaceTUI;
using NekoGraph;

/// <summary>
/// ═══════════════════════════════════════════════════════════════
/// SocialCLI.Commands - 社交命令定义喵~
/// ═══════════════════════════════════════════════════════════════
///
/// 所有社交命令都在这里定义，使用 [SocialCommand] + [CommandInfo] 双标记
/// 集成 GraphAnalyser 实现 VFS 路径管理喵~
/// ═══════════════════════════════════════════════════════════════
/// </summary>
public static class SocialCommandBricks
{
    // =========================================================
    //  辅助方法
    // =========================================================

    /// <summary>
    /// 获取当前用户的社交数据
    /// </summary>
    private static SocialData GetSocialData()
    {
        if (MainModel.Instance?.CurrentUser != null)
        {
            return MainModel.Instance.CurrentUser.Social;
        }
        return null;
    }

    // =========================================================
    //  📋 系统命令（GraphAnalyser 集成版）喵~
    // =========================================================

    [CommandInfo("pwd", "📍 显示当前路径", "Social", null,
        Tooltip = "显示当前目录路径喵~",
        Color = "0.3,0.5,0.9")]
    public static CommandOutput Pwd(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        var dc = console as DeveloperConsole;
        return CommandOutput.Success($"当前目录：{dc.FullCurrentPath}");
    }

    [CommandInfo("cd", "📂 切换目录", "Social", new[] { "path" },
        Tooltip = "切换当前目录喵~\n示例：cd friends  cd A:/messages  cd ..",
        Color = "0.3,0.5,0.8")]
    public static CommandOutput Cd(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        var dc = console as DeveloperConsole;
        if (args.Length < 1)
        {
            dc.SetCurrentPath("/");
            return CommandOutput.Success($"当前目录：{dc.FullCurrentPath}");
        }

        string path = args[0];

        if (path == "..")
        {
            string parentPath = VFSPathResolver.GetParentPath(dc.CurrentPath);
            dc.SetCurrentPath(parentPath);
        }
        else if (path == "/" || path == "~")
        {
            dc.SetCurrentPath("/");
        }
        else
        {
            var (packID, vfsPath) = dc.ResolveDrivePath(path);
            if (packID == null)
                return CommandOutput.Fail($"未知盘符：{path}");

            // 跨盘则先切盘（SetCurrentPackID 会把路径重置到 /）
            if (packID != dc.CurrentVFSPackID)
                dc.SetCurrentPackID(packID);

            dc.SetCurrentPath(vfsPath);
        }

        return CommandOutput.Success($"当前目录：{dc.FullCurrentPath}");
    }

    [CommandInfo("ls", "📋 列出目录", "Social", new[] { "[path]" },
        Tooltip = "列出目录内容喵~\n示例：ls  ls /messages/  ls A:/shop",
        Color = "0.3,0.5,0.7")]
    public static CommandOutput List(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        var dc = console as DeveloperConsole;
        if (string.IsNullOrEmpty(dc.CurrentVFSPackID))
            return CommandOutput.Fail("未挂载文件系统喵！");

        string rawPath = args.Length > 0 ? args[0] : dc.CurrentPath;
        var (packID, path) = dc.ResolveDrivePath(rawPath);
        if (packID == null)
            return CommandOutput.Fail($"未知盘符：{rawPath}");

        var analyser = GraphHub.Instance?.DefaultAnalyser;
        if (analyser == null)
        {
            return CommandOutput.Fail("GraphAnalyser 未初始化喵~");
        }

        // 检查路径是否存在
        if (!analyser.PathExists(packID, path, PackAccessSubjects.Player))
        {
            return CommandOutput.Fail($"路径不存在：{path}");
        }

        // 检查是否是目录
        var node = analyser.GetNode(packID, path, PackAccessSubjects.Player);
        if (node is VFSNodeData vfs && !vfs.IsDirectory)
        {
            return CommandOutput.Fail($"不是目录：{path}");
        }

        // 获取子节点列表
        var children = analyser.GetChildren(packID, path, PackAccessSubjects.Player);

        // 过滤启用的节点
        var validChildren = new List<VFSNodeData>();
        foreach (var child in children)
        {
            if (child is VFSNodeData vfsChild && vfsChild.IsEnabled)
            {
                validChildren.Add(vfsChild);
            }
        }

        // 构建表格
        var table = new TUITableBuilder()
            .SetTitle($"📁 {dc.FullCurrentPath}")
            .SetFooter($"共 {validChildren.Count} 项")
            .UseBorder(true)
            .SetBorderStyle(BorderStyle.Classic)
            .SetStyle(new TSSStyle
            {
                borderColor = new Color(0.4f, 0.4f, 0.4f),
                contentColor = Color.white,
                titleColor = new Color(0.2f, 0.8f, 1f),
                paddingX = 1,
                paddingY = 0,
                spacingX = 1,
                alignment = SpaceTUI.TextAlignment.Left
            })
            // 列定义：类型 (0)、时间 (1)、大小 (2)、#(3)、名称 (4,自动宽)
            .AddColumn("类型", 6, SpaceTUI.TextAlignment.Left)
            .AddColumn("时间", 12, SpaceTUI.TextAlignment.Left)
            .AddColumn("大小", 8, SpaceTUI.TextAlignment.Right)
            .AddColumn("#", 4, SpaceTUI.TextAlignment.Center)
            .AddColumn("名称", 0, SpaceTUI.TextAlignment.Left);
            // .SetConnectorColumn(4, '─');  // 名称列 (4) 通过连接器指向 #(3) - 已禁用，保留方法

        // 添加数据行
        int index = 0;
        foreach (var child in validChildren)
        {
            // 判断类型
            string type = child.IsDirectory ? "[DIR]" : "[FILE]";

            // 检查是否是新消息
            bool isNew = false;
            if (!child.IsDirectory && child.Extension == ".msg")
            {
                try
                {
                    var msgData = JsonUtility.FromJson<SocialManager.SocialMessageVFSData>(child.DataJson);
                    if (msgData != null && !msgData.IsRead)
                    {
                        type = "[NEW]";
                        isNew = true;
                    }
                } catch { }
            }

            // 时间（使用文件修改时间或默认时间）
            string time = "3/19 12:00"; // TODO: 从 VFSNodeData 读取实际时间

            // 大小（目录显示 -，文件用 DataJson 字节数）
            string size = child.IsDirectory ? "-" : (child.DataJson?.Length.ToString() ?? "0") + "B";

            // 名称（目录加/，文件加扩展名，扩展名前需要加点）
            string name = child.Name + (child.IsDirectory ? "/" : child.Extension);

            // 序号
            string num = index.ToString();

            table.AddRow(type, time, size, num, name);
            index++;
        }

        // 渲染表格
        int consoleWidth = dc.ConsoleWidth;
        string[] lines = table.Render(consoleWidth);

        // 输出
        var sb = new StringBuilder();
        foreach (var line in lines)
        {
            sb.AppendLine(line);
        }

        return CommandOutput.Success(sb.ToString());
    }

    [CommandInfo("help", "❓ 显示帮助", "Social", new[] { "command" },
        Tooltip = "显示帮助信息喵~\n示例：help ls",
        Color = "0.5,0.5,0.5")]
    public static CommandOutput SocialHelp(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        var dc = console as DeveloperConsole;
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== 社交终端帮助 ===");
        sb.AppendLine();
        sb.AppendLine("系统命令:");
        sb.AppendLine("  ls [path]       - 列出目录内容");
        sb.AppendLine("  cd [path]       - 切换当前目录");
        sb.AppendLine("  pwd             - 显示当前路径");
        sb.AppendLine("  cat [file]      - 读取文件内容");
        sb.AppendLine("  echo [text]     - 输出文本内容");
        sb.AppendLine("  help [cmd]      - 显示帮助信息");
        sb.AppendLine();
        sb.AppendLine("提示：支持重定向 > (覆盖) 和 >> (追加) 喵~");
        return CommandOutput.Success(sb.ToString());
    }

    [CommandInfo("cat", "📖 读取文件内容", "Social", new[] { "path" },
        Tooltip = "读取 VFS 文件内容喵~\n示例：cat message.txt  cat A:/messages/test.msg",
        Color = "0.3,0.7,0.3")]
    public static CommandOutput Cat(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        var dc = console as DeveloperConsole;
        if (string.IsNullOrEmpty(dc.CurrentVFSPackID))
            return CommandOutput.Fail("未挂载文件系统喵！");

        if (args.Length < 1) return CommandOutput.Fail("请指定要读取的文件路径喵~");

        var (packID, targetPath) = dc.ResolveDrivePath(args[0]);
        if (packID == null)
            return CommandOutput.Fail($"未知盘符：{args[0]}");

        var analyser = GraphHub.Instance?.DefaultAnalyser;
        var node = analyser.GetNode(packID, targetPath, PackAccessSubjects.Player);

        if (node == null) return CommandOutput.Fail($"文件不存在：{targetPath}");
        if (node is VFSNodeData vfs)
        {
            if (vfs.IsDirectory) return CommandOutput.Fail($"无法读取目录：{targetPath}");

            if (!string.IsNullOrWhiteSpace(vfs.Extension) &&
                VFSQueryRegistry.TryGetHandler(vfs.Extension, out var queryHandler))
            {
                var queryContext = new VFSQueryContext
                {
                    Analyser = analyser,
                    PackID = packID,
                    VfsPath = targetPath,
                    RequestName = "inspect",
                    SubjectLevel = PackAccessSubjects.Player,
                    Node = vfs,
                    FrontendContext = dc
                };

                var result = queryHandler(VFSContentResolver.Resolve(vfs), queryContext);
                if (result != null)
                {
                    if (dc.TryPresent(result))
                        return CommandOutput.Success("");

                    if (!string.IsNullOrWhiteSpace(result.Summary))
                        return CommandOutput.Success(result.Summary, result.Payload);

                    if (!string.IsNullOrWhiteSpace(result.Title))
                        return CommandOutput.Success(result.Title, result.Payload);
                }
            }

            return CommandOutput.Success(vfs.DataJson, vfs.DataJson);
        }

        return CommandOutput.Fail("无法识别的节点类型喵~");
    }

    [CommandInfo("echo", "🗣️ 输出文本", "Social", new[] { "text" },
        Tooltip = "输出指定文本喵~\n示例：echo \"Hello World\"",
        Color = "0.8,0.8,0.3")]
    public static CommandOutput Echo(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        var dc = console as DeveloperConsole;
        string content = string.Join(" ", args);
        // 如果是从上游传下来的 payload 且 args 为空，则优先显示 payload
        if (string.IsNullOrEmpty(content) && payload != null)
        {
            content = payload.ToString();
        }

        return CommandOutput.Success(content, content);
    }

    [CommandInfo("mount", "💾 浏览/切换盘符", "Social", null,
        Tooltip = "打开交互式盘符选择界面，↑↓ 导航，Enter 确认切换喵~",
        Color = "0.5,0.8,0.5")]
    public static CommandOutput Mount(IConsoleController console, int subjectLevel, string[] args, object payload)
    {
        var dc = console as DeveloperConsole;
        if (console == null) return CommandOutput.Fail("console 为空");

        var driveMap = dc.GetDriveMap();
        if (driveMap.Count == 0)
            return CommandOutput.Fail("当前无可见盘符喵~");

        var analyser = GraphHub.Instance?.DefaultAnalyser;
        var items = new List<TUISelectionItem>();
        for (int i = 0; i < driveMap.Count; i++)
        {
            var (letter, packID) = driveMap[i];
            var pack = analyser?.GetPack(packID, PackAccessSubjects.Player);
            PackAccessLevel accessLevel = GraphHub.Instance?.GetPackAccessLevel(GraphInstanceSlot.Player, pack)
                ?? analyser?.GetPackAccessLevel(pack, PackAccessSubjects.Player)
                ?? PackAccessLevel.Hidden;
            string rw = accessLevel == PackAccessLevel.ReadWrite ? "RW" : "RO";
            bool isCurrent = packID == dc.CurrentVFSPackID;
            items.Add(new TUISelectionItem
            {
                key = i,
                indexText = letter.ToString(),
                label = packID,
                subtitle = $"[{rw}]{(isCurrent ? " ◀ 当前" : "")}",
                payload = packID
            });
        }

        int currentIdx = driveMap.FindIndex(d => d.packID == dc.CurrentVFSPackID);
        var config = new TUISelectionConfig
        {
            title = "══ 选择盘符 ══",
            helpText = "↑↓ 导航  Enter 确认  Esc 取消",
            emptyText = "无可见盘符",
            initialSelectedKey = currentIdx >= 0 ? currentIdx : 0,
            console = dc,
            items = items,
            viewStyle = TUISelectionViewStyle.Default,
            interaction = new TUISelectionInteractionConfig
            {
                wrapNavigation = true,
                enableDigitSelect = false,
                allowConfirmOnEmptySubmit = true,
                onConfirmSelection = (idx, item) =>
                {
                    string pid = (string)item.payload;
                    dc.SetCurrentPackID(pid);
                    console.Log($"已切换到盘符 {item.indexText}: ({pid})", Color.green);
                },
                onCancel = () => console.Log("已取消", Color.gray)
            }
        };

        var handler = new TUIListSelectionHandler(config);
        dc.BeginSession(handler);
        return CommandOutput.Success("");
    }

}
