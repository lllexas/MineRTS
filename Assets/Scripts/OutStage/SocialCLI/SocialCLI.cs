using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using SocialCLIStrategies;

/// <summary>
/// ═══════════════════════════════════════════════════════════════
/// SocialCLI - 社交命令行安检员喵~ (GraphVSF 集成版)
/// ═══════════════════════════════════════════════════════════════
///
/// 继承自 DeveloperConsole，是"开发终端"在社交领域的特化形态喵！
///
/// 职责：
/// 1. 维护社交命令白名单（通过反射扫描 [SocialCommand] 标记）
/// 2. 安检：验证命令是否在白名单内
/// 3. 转发：通过安检的命令丢给基类 DeveloperConsole 处理
/// 4. 输出：重写 Log() 方法，输出到社交面板而不是主控制台
/// 5. 路径管理：使用 GraphAnalyser 管理文件树路径（CurrentPath）
///
/// 安全隔离：
/// - 只允许执行带 [SocialCommand] 标记的命令
/// - 无法通过社交 CLI 操作游戏进程
///
/// GraphVSF 集成：
/// - 使用 GraphAnalyser 单例管理 VFS 实例
/// - CurrentPath 从 GraphAnalyser 查询实时路径
/// - cd 命令通过 GraphAnalyser.GetNode() 验证路径
///
/// 初始化章法：
/// - [RuntimeInitializeOnLoadMethod] 作为静态入口，确保内存中第一时间建立白名单
/// - 严禁在 ExecuteCommand 里调用 Initialize()（初始化是系统级行为，执行是业务级行为）
/// - 内部拦截：if (_isInitialized) return; 确保只跑一次
/// ═══════════════════════════════════════════════════════════════
/// </summary>
public class SocialCLI : DeveloperConsole
{
    // =========================================================
    //  社交命令白名单
    // =========================================================
    private static HashSet<string> _socialCommands = new HashSet<string>();
    private static bool _isInitialized = false;

    // =========================================================
    //  隔离模式开关喵~
    // =========================================================
    /// <summary>
    /// 是否启用命令隔离（白名单检查）喵~
    /// true = 只允许执行白名单内的命令（默认安全模式）
    /// false = 解除隔离，可以执行任意命令（调试模式）
    /// </summary>
    public bool EnableCommandIsolation = true;

    // =========================================================
    //  策略模式：接管交互逻辑喵~
    // =========================================================
    private ICatStrategy _activeStrategy;

    /// <summary>
    /// 设置并启动一个新的 cat 策略喵~
    /// </summary>
    public void SetActiveStrategy(ICatStrategy strategy, string vfsPath, string graphPath = null)
    {
        CloseActiveStrategy();
        _activeStrategy = strategy;
        _activeStrategy.Execute(vfsPath, graphPath);
    }

    /// <summary>
    /// 关闭当前正在运行的策略喵~
    /// </summary>
    public void CloseActiveStrategy()
    {
        if (_activeStrategy != null)
        {
            _activeStrategy.Close();
            _activeStrategy = null;
        }
    }

    // =========================================================
    //  VFS 实例配置
    // =========================================================

    /// <summary>
    /// VFS 实例 ID
    /// </summary>
    [Tooltip("VFS 实例 ID")]
    public string VFSInstanceID = "Social_01";

    /// <summary>
    /// VFS 资源包路径（用于从 Resources 加载）
    /// </summary>
    [Tooltip("VFS 资源包路径（相对于 Resources 目录）")]
    public string VFSPackPath = "GraphVSF/Packs/social_tree";

    /// <summary>
    /// 是否自动加载 VFS
    /// </summary>
    [Tooltip("是否自动加载 VFS 文件树")]
    public bool AutoLoadVFS = true;

    // =========================================================
    //  当前路径（GraphAnalyser 驱动）喵~
    // =========================================================

    /// <summary>
    /// 当前路径（只读，从 GraphAnalyser 查询）喵~
    /// </summary>
    public string CurrentPath { get; private set; } = "/social/";

    /// <summary>
    /// 设置当前路径喵~
    /// </summary>
    /// <param name="path">新路径</param>
    /// <returns>是否成功</returns>
    public bool SetCurrentPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            Log("路径不能为空", Color.red);
            return false;
        }

        // 使用 GraphAnalyser 验证路径
        var analyser = GraphAnalyser.Instance;
        if (analyser == null)
        {
            Log("GraphAnalyser 实例不存在", Color.red);
            return false;
        }

        // 检查路径是否存在
        if (!analyser.PathExists(VFSInstanceID, path))
        {
            Log($"路径不存在：{path}", Color.red);
            return false;
        }

        // 检查是否是目录（通过 VFSNodeData 判断）
        var node = analyser.GetNode(VFSInstanceID, path);
        if (node is VFSNodeData vfs && !vfs.IsDirectory)
        {
            Log($"不是目录：{path}", Color.red);
            return false;
        }

        CurrentPath = path;
        Log($"路径已切换到：{CurrentPath}", Color.green);
        return true;
    }

    // =========================================================
    //  初始化（反射扫描）
    // =========================================================
    [RuntimeInitializeOnLoadMethod]
    private static void Initialize()
    {
        if (_isInitialized) return;

        _socialCommands.Clear();

        // 扫描 CommandRegistry 中所有带 [SocialCommand] 标记的静态方法
        var type = typeof(CommandRegistry);
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

        foreach (var method in methods)
        {
            // 检查是否有 [SocialCommand] 标记
            if (method.GetCustomAttribute<SocialCommandAttribute>() != null)
            {
                // 获取 [CommandInfo] 获取命令名
                var cmdAttr = method.GetCustomAttribute<CommandInfoAttribute>();
                if (cmdAttr != null)
                {
                    _socialCommands.Add(cmdAttr.Name.ToLower());
                    Debug.Log($"[SocialCLI] 注册社交命令：{cmdAttr.Name}");
                }
            }
        }

        _isInitialized = true;
        Debug.Log($"[SocialCLI] 初始化完成，共 {_socialCommands.Count} 个社交命令");
    }

    // =========================================================
    //  命令执行入口（安检员模式 + 重定向增强）
    // =========================================================

    /// <summary>
    /// 执行命令字符串（安检 → 重定向识别 → 转发）
    /// </summary>
    public override void ProcessCommand(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return;

        // 0. 策略模式拦截喵~
        if (_activeStrategy != null)
        {
            _activeStrategy.OnInput(input);
            return;
        }

        // 0.5 隔离模式检查喵~（如果关闭隔离，则跳过白名单检查）
        if (!EnableCommandIsolation)
        {
            // 解除隔离模式：直接转发给基类，不检查白名单
            base.ProcessCommand(input);
            return;
        }

        // 1. Tokenize & Validate：先做安检喵~
        var commandTokens = ExtractCommandTokens(input);
        foreach (var token in commandTokens)
        {
            if (!_socialCommands.Contains(token.ToLower()))
            {
                Log($"命令 '{token}' 不允许在社交终端执行喵~", Color.red);
                return;
            }
        }

        // 2. 识别重定向符号喵~ (支持 >> 和 >)
        if (input.Contains(">"))
        {
            HandleRedirection(input);
            return;
        }

        // 3. 普通命令直接转发给基类喵~
        base.ProcessCommand(input);
    }

    /// <summary>
    /// 处理重定向逻辑喵~
    /// </summary>
    private void HandleRedirection(string input)
    {
        bool isAppend = input.Contains(">>");
        string separator = isAppend ? ">>" : ">";

        // 分割命令和路径
        int sepIndex = input.IndexOf(separator);
        string commandPart = input.Substring(0, sepIndex).Trim();
        string pathPart = input.Substring(sepIndex + separator.Length).Trim();

        if (string.IsNullOrEmpty(pathPart))
        {
            Log("未指定重定向目标路径喵！", Color.red);
            return;
        }

        // 执行命令获取 Payload
        // 由于这里不支持复杂的管道组合重定向（目前先做单命令），直接手动调 CommandRegistry
        string[] cmdParts = commandPart.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (cmdParts.Length == 0) return;

        string cmdName = cmdParts[0].ToLower();
        string[] args = cmdParts.Skip(1).ToArray();

        Log($"> {input}", Color.cyan);

        var output = CommandRegistry.Execute(cmdName, args, null, this);
        if (output.Result == CommandRegistry.CommandResult.Failed)
        {
            Log($"命令执行失败，重定向已终止喵：{output.Message}", Color.red);
            return;
        }

        // 处理写入内容：优先使用 Payload，没有 Payload 就用 Message 凑合一下喵~
        string contentToWrite = (output.Payload?.ToString()) ?? output.Message ?? "";
        string fullPath = pathPart.StartsWith("/") ? pathPart : VFSPathResolver.Combine(CurrentPath, pathPart);

        var analyser = GraphAnalyser.Instance;
        if (analyser == null) return;

        // 追加模式逻辑喵~
        if (isAppend)
        {
            var existing = analyser.GetNode(VFSInstanceID, fullPath);
            if (existing is VFSNodeData vfs)
            {
                contentToWrite = vfs.DataJson + "\n" + contentToWrite;
            }
        }

        // 写入文件喵~
        if (analyser.WriteFile(VFSInstanceID, fullPath, contentToWrite))
        {
            Log($"内容已{(isAppend ? "追加" : "写入")}到：{fullPath}", Color.green);
        }
        else
        {
            Log($"写入失败，请检查路径是否正确喵：{fullPath}", Color.red);
        }
    }

    /// <summary>
    /// 从输入字符串中提取所有命令名 Token
    /// </summary>
    private static List<string> ExtractCommandTokens(string input)
    {
        var tokens = new List<string>();

        // 支持分号、换行符作为指令分隔符
        string[] commandQueue = input.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var commandLine in commandQueue)
        {
            string trimmedLine = commandLine.Trim();
            if (string.IsNullOrWhiteSpace(trimmedLine)) continue;

            // 检查是否有管道符 |
            if (trimmedLine.Contains('|'))
            {
                // 管道模式：分割每个管道段
                string[] parts = trimmedLine.Split('|');
                foreach (var part in parts)
                {
                    string trimmed = part.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed)) continue;

                    string[] tokensInPart = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (tokensInPart.Length > 0)
                    {
                        tokens.Add(tokensInPart[0]);
                    }
                }
            }
            else
            {
                // 普通模式：直接取第一个词
                string[] parts = trimmedLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    tokens.Add(parts[0]);
                }
            }
        }

        return tokens;
    }

    // =========================================================
    //  重写 Log 方法（输出到社交面板）喵~
    // =========================================================

    /// <summary>
    /// 重写基类的 Log，输出到社交面板而不是主控制台喵~
    /// 注意：不调用 base.Log()，直接发送 SocialCLI 专属事件
    /// </summary>
    public override void Log(string message, Color color)
    {
        Debug.Log(message);
        // 发送 SocialCLI 专属事件，与 DeveloperConsole 隔离（只发送一个事件，避免重复）
        PostSystem.Instance.Send("SocialCLI.Output", new DeveloperConsole.ConsoleOutputEvent { message = message, color = color });
    }

    // =========================================================
    //  Unity 生命周期
    // =========================================================
    protected override void Awake()
    {
        base.Awake();
        // 注册到事件系统，接收社交协议事件喵~
        PostSystem.Instance.Register(this);

        // 不再自动加载 VFS，等待用户手动调用 vfs_load 命令
        CurrentPath = "/social/";
        Debug.Log("[SocialCLI] 已启动，使用 vfs_load 命令加载 VFS 数据包喵~");
    }


    // =========================================================
    //  GraphVSF 初始化（GraphAnalyser 集成版）喵~
    // =========================================================

    /// <summary>
    /// 初始化 GraphVSF 文件树喵~
    /// 使用 GraphAnalyser 单例加载和管理 VFS 实例
    /// </summary>
    private void InitializeGraphVSF()
    {
        // 获取 GraphAnalyser 单例
        var analyser = GraphAnalyser.Instance;
        if (analyser == null)
        {
            Debug.LogError("[SocialCLI] GraphAnalyser 实例不存在，无法初始化 VFS 喵~");
            return;
        }

        // 尝试从 Resources 加载 VFS Pack
        var pack = VFSLoader.LoadPackFromResources(VFSPackPath);
        if (pack == null)
        {
            Debug.LogWarning($"[SocialCLI] 找不到 VFS Pack：{VFSPackPath}，创建默认社交文件树喵~");
            pack = CreateDefaultSocialTree();
        }

        // 使用 GraphAnalyser 加载并初始化 VFS 实例
        var instance = analyser.LoadVFSFromPack(pack, VFSInstanceID);
        if (instance != null)
        {
            // 设置默认实例 ID
            analyser.SetDefaultInstanceId(VFSInstanceID);

            // 设置当前路径为根目录
            if (instance.RootNodeIds.Count > 0)
            {
                CurrentPath = "/social/";
                Debug.Log($"[SocialCLI] GraphVSF 初始化完成，当前路径：{CurrentPath}");
                Debug.Log(analyser.GetDebugInfo());
            }
        }
        else
        {
            Debug.LogError("[SocialCLI] VFS 实例加载失败");
        }
    }

    /// <summary>
    /// 创建默认的社交文件树喵~
    /// </summary>
    private VFSPackData CreateDefaultSocialTree()
    {
        var pack = new VFSPackData
        {
            PackID = "social_tree_default",
            DisplayName = "社交文件树（默认）",
            Description = "SocialCLI 默认文件树结构"
        };

        // 创建根节点 /social/
        var rootNode = new RootNodeData
        {
            NodeID = "root_social",
            Name = "social",
        };
        pack.AddRootNode(rootNode);

        // 创建 friends 目录
        var friendsFolder = CreateFolderNode("friends", "好友列表", rootNode, pack);

        // 创建 requests 目录
        var requestsFolder = CreateFolderNode("requests", "好友请求", rootNode, pack);

        // 创建 blocks 目录
        var blocksFolder = CreateFolderNode("blocks", "黑名单", rootNode, pack);

        // 创建 groups 目录
        var groupsFolder = CreateFolderNode("groups", "群组", rootNode, pack);

        // 创建 messages 目录
        var messagesFolder = CreateFolderNode("messages", "消息历史", rootNode, pack);

        return pack;
    }

    /// <summary>
    /// 创建目录节点喵~
    /// </summary>
    private VFSNodeData CreateFolderNode(string name, string desc, RootNodeData parent, VFSPackData pack)
    {
        var folder = new VFSNodeData
        {
            NodeID = "folder_" + name,
            Name = name,
            Extension = "", // 空扩展名 = 目录
            Description = desc
        };

        // 添加到父节点的输出连线
        parent.OutputConnections.Add(new ConnectionData(0, folder.NodeID, 0));
        pack.Nodes.Add(folder);

        return folder;
    }

    // =========================================================
    //  调试方法喵~
    // =========================================================

    /// <summary>
    /// 获取 VFS 调试信息喵~
    /// </summary>
    public string GetVFSDebugInfo()
    {
        var analyser = GraphAnalyser.Instance;
        if (analyser == null) return "GraphAnalyser 实例不存在";

        var instance = analyser.GetInstance(VFSInstanceID);
        if (instance == null) return $"VFS 实例不存在：{VFSInstanceID}";

        return instance.GetDebugInfo();
    }
}
