using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// ═══════════════════════════════════════════════════════════════
/// SocialCLI.Commands - 社交命令定义喵~
/// ═══════════════════════════════════════════════════════════════
///
/// 所有社交命令都在这里定义，使用 [SocialCommand] + [CommandInfo] 双标记
/// 集成 GraphAnalyser 实现 VFS 路径管理喵~
/// ═══════════════════════════════════════════════════════════════
/// </summary>
public static partial class CommandRegistry
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
    [SocialCommand]
    public static CommandOutput Pwd(DeveloperConsole console, string[] args, object payload)
    {
        return CommandOutput.Success($"当前目录：{console.CurrentPath}");
    }

    [CommandInfo("cd", "📂 切换目录", "Social", new[] { "path" },
        Tooltip = "切换当前目录喵~\n示例：cd friends",
        Color = "0.3,0.5,0.8")]
    [SocialCommand]
    public static CommandOutput Cd(DeveloperConsole console, string[] args, object payload)
    {
        if (args.Length < 1)
        {
            console.SetCurrentPath("/");
            return CommandOutput.Success($"当前目录：{console.CurrentPath}");
        }

        string path = args[0];

        if (path == "..")
        {
            string parentPath = VFSPathResolver.GetParentPath(console.CurrentPath);
            console.SetCurrentPath(parentPath);
        }
        else if (path == "/" || path == "~")
        {
            console.SetCurrentPath("/");
        }
        else
        {
            string targetPath = path.StartsWith("/")
                ? VFSPathResolver.Normalize(path)
                : VFSPathResolver.Combine(console.CurrentPath, path);
            console.SetCurrentPath(targetPath);
        }

        return CommandOutput.Success($"当前目录：{console.CurrentPath}");
    }

    [CommandInfo("ls", "📋 列出目录", "Social", new[] { "path" },
        Tooltip = "列出社交目录内容喵~\n示例：ls /social/friends/",
        Color = "0.3,0.5,0.7")]
    [SocialCommand]
    public static CommandOutput List(DeveloperConsole console, string[] args, object payload)
    {
        if (string.IsNullOrEmpty(console.CurrentVFSPackID))
            return CommandOutput.Fail("未挂载文件系统喵！");

        string path = args.Length > 0 ? args[0] : console.CurrentPath;

        var analyser = GraphAnalyser.Instance;
        if (analyser == null)
        {
            return CommandOutput.Fail("GraphAnalyser 未初始化喵~");
        }

        // 检查路径是否存在
        if (!analyser.PathExists(console.CurrentVFSPackID, path))
        {
            return CommandOutput.Fail($"路径不存在：{path}");
        }

        // 检查是否是目录
        var node = analyser.GetNode(console.CurrentVFSPackID, path);
        if (node is VFSNodeData vfs && !vfs.IsDirectory)
        {
            return CommandOutput.Fail($"不是目录：{path}");
        }

        // 获取子节点列表
        var children = analyser.GetChildren(console.CurrentVFSPackID, path);

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"目录：{path}");

        if (children.Count == 0)
        {
            sb.AppendLine("  (空)");
        }
        else
        {
            foreach (var child in children)
            {
                // 跳过未启用的节点
                if (child is VFSNodeData vfsChild && !vfsChild.IsEnabled) continue;

                // 判断是目录还是文件
                bool isDir = child is VFSNodeData v && v.IsDirectory;
                string icon = isDir ? "[DIR]" : "[FILE]";
                
                // --- 社交消息特化：检查 [NEW] 标签喵 ---
                string prefix = "";
                if (!isDir && child is VFSNodeData fileNode && fileNode.Extension == ".msg")
                {
                    try 
                    {
                        var msgData = JsonUtility.FromJson<SocialManager.SocialMessageVFSData>(fileNode.DataJson);
                        if (msgData != null && !msgData.IsRead)
                        {
                            prefix = "<color=red>[NEW]</color> ";
                        }
                    } catch { /* 忽略损坏的消息喵 */ }
                }

                // 构建显示名称
                string name = child.Name;
                if (child is VFSNodeData vfsNode)
                {
                    if (vfsNode.IsDirectory)
                        name += "/";
                    else
                        name += vfsNode.Extension;
                }

                sb.AppendLine($"  {prefix}{icon} {name}");
            }
        }

        return CommandOutput.Success(sb.ToString());
    }

    [CommandInfo("help", "❓ 显示帮助", "Social", new[] { "command" },
        Tooltip = "显示帮助信息喵~\n示例：help ls",
        Color = "0.5,0.5,0.5")]
    [SocialCommand]
    public static CommandOutput SocialHelp(DeveloperConsole console, string[] args, object payload)
    {
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
        Tooltip = "读取 VFS 文件内容喵~\n示例：cat message.txt",
        Color = "0.3,0.7,0.3")]
    [SocialCommand]
    public static CommandOutput Cat(DeveloperConsole console, string[] args, object payload)
    {
        if (string.IsNullOrEmpty(console.CurrentVFSPackID))
            return CommandOutput.Fail("未挂载文件系统喵！");

        if (args.Length < 1) return CommandOutput.Fail("请指定要读取的文件路径喵~");

        string path = args[0];
        string targetPath = path.StartsWith("/") ? path : VFSPathResolver.Combine(console.CurrentPath, path);

        var analyser = GraphAnalyser.Instance;
        var node = analyser.GetNode(console.CurrentVFSPackID, targetPath);

        if (node == null) return CommandOutput.Fail($"文件不存在：{targetPath}");
        if (node is VFSNodeData vfs)
        {
            if (vfs.IsDirectory) return CommandOutput.Fail($"无法读取目录：{targetPath}");

            // --- 社交消息特化处理喵 ---
            if (targetPath.EndsWith(".msg"))
            {
                try
                {
                    var msgData = JsonUtility.FromJson<SocialManager.SocialMessageVFSData>(vfs.DataJson);
                    if (msgData != null && !string.IsNullOrEmpty(msgData.PackID))
                    {
                        // 使用策略模式接管喵！✨
                        console.SetActiveStrategy(new CatStrategies.MsgStrategy(console), targetPath, msgData.PackID);
                        return CommandOutput.Success(""); // 逻辑已接管，无需额外输出
                    }
                }
                catch
                {
                    return CommandOutput.Fail("消息文件损坏，无法读取源码喵~");
                }
            }

            return CommandOutput.Success(vfs.DataJson, vfs.DataJson);
        }

        return CommandOutput.Fail("无法识别的节点类型喵~");
    }

    [CommandInfo("echo", "🗣️ 输出文本", "Social", new[] { "text" },
        Tooltip = "输出指定文本喵~\n示例：echo \"Hello World\"",
        Color = "0.8,0.8,0.3")]
    [SocialCommand]
    public static CommandOutput Echo(DeveloperConsole console, string[] args, object payload)
    {
        string content = string.Join(" ", args);
        // 如果是从上游传下来的 payload 且 args 为空，则优先显示 payload
        if (string.IsNullOrEmpty(content) && payload != null)
        {
            content = payload.ToString();
        }

        return CommandOutput.Success(content, content);
    }

    [CommandInfo("social_isolation", "🔓 切换 CLI 隔离模式", "Debug", new[] { "enable (0/1)" },
        Tooltip = "解除/启用社交 CLI 的命令隔离喵~\n此命令只能在大控制台执行！\n示例：social_isolation 0 (解除隔离)\nsocial_isolation 1 (启用隔离)",
        Color = "0.8,0.4,0.2")]
    public static CommandOutput SocialIsolation(DeveloperConsole console, string[] args, object payload)
    {
        // 此命令只能在大控制台（DeveloperConsole）执行，不能在社交终端执行喵~
        if (console is SocialCLI)
        {
            return CommandOutput.Fail("此命令只能在大控制台执行喵！社交终端无法修改隔离设置。");
        }

        // 从场景中查找 SocialCLI 实例
        var scli = UnityEngine.Object.FindFirstObjectByType<SocialCLI>();
        if (scli == null)
        {
            return CommandOutput.Fail("找不到 SocialCLI 实例喵~");
        }

        if (args.Length < 1)
        {
            // 不传参数则显示当前状态
            string status = scli.EnableCommandIsolation ? "已启用（安全模式）" : "已解除（调试模式）";
            return CommandOutput.Success($"当前 CLI 隔离状态：{status}");
        }

        bool enable = args[0] == "1" || args[0].ToLower() == "true" || args[0].ToLower() == "on";
        scli.EnableCommandIsolation = enable;

        string newState = enable ? "已启用（安全模式）" : "已解除（调试模式）";
        string warning = enable ? "" : "\n<color=yellow>警告：解除隔离后可以执行任意命令，请谨慎操作喵~</color>";

        return CommandOutput.Success($"CLI 隔离模式：{newState}{warning}");
    }
}
