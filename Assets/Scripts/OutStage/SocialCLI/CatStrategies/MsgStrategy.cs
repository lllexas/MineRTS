using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using NekoGraph;
using Newtonsoft.Json;

namespace CatStrategies
{
    /// <summary>
    /// 社交消息 (.msg) 特化处理策略喵~
    /// 充当 SocialCLI (前端) 与 GraphRunner (后端) 之间的适配器
    /// 支持 ASCII TUI 文本框 + ↑↓ 导航 + Enter 确认 + 数字直选
    ///
    /// ── 运行约定 ────────────────────────────────────────────────────
    /// 【单实例约定】
    ///   当前设计假设同一时刻只有一个 MsgStrategy 处于活跃状态。
    ///   Social.ShowBody / Social.RegisterOption / Social.MsgFinished
    ///   均为全局广播事件（无实例隔离），多实例并行会导致 UI 互相覆写。
    ///   如需支持对话栈，需在事件名中携带 _instanceID 后缀以隔离。
    ///
    /// 【PackID vs InstanceID】
    ///   _packID    = MsgPackData.PackID，来自 JSON 元数据（如 "msg_intro_01"）。
    ///                用于业务标识：MsgFinished 事件的过滤、失败日志。
    ///   _instanceID = GraphRunner.LoadPack() 返回的运行时 GUID。
    ///                用于引擎驱动：InjectSignalFromRoot / UnloadPack。
    ///   两者不可混用：MsgFinished 比对用 _packID，引擎操作用 _instanceID。
    /// </summary>
    public class MsgStrategy : CatStrategyBase
    {
        // ── 出血边常量
        private const int BleedX = 4;   // 左右各 4 列出血边（居中留白）
        private const int BleedY = 1;   // 上下各 1 行空行出血边

        // BoxWidth = ConsoleWidth - 2*BleedX，向下取偶，最小 42
        // 向下取偶确保 InnerWidth 为偶数 → CJK字符（宽2）填满后无"半格"残留喵~
        private int BoxWidth
        {
            get
            {
                int w = Mathf.Max(_cli.ConsoleWidth - BleedX * 2, 42);
                if (w % 2 != 0) w--;   // 向下取偶
                return w;
            }
        }

        // 居中左边距 = (ConsoleWidth - BoxWidth) / 2，自然地板除
        private int LeftPad => (_cli.ConsoleWidth - BoxWidth) / 2;

        // ── TSSStyle 定义 ──────────────────────────────────────────
        // 消息框样式：bleedY=0（出血由外部手动处理），paddingY=1（边框内各留一行空行）
        // contentColor 同时作用于 GenerateTopBorder 标题 和 FormatBoxLine 正文
        private static TSSStyle MsgBoxStyle => new TSSStyle
        {
            bleedX = 0, bleedY = 0,
            paddingX = 1, paddingY = 1,
            borderColor  = new Color(0.5f, 0.5f, 0.5f),
            contentColor = Color.white,
            alignment    = TextAlignment.Left,
            expandArtSpaces = false
        };

        // 选项行样式（纯文本着色，不加边框）
        private static TSSStyle OptionStyle(bool highlighted) => new TSSStyle
        {
            contentColor = highlighted ? new Color(0.4f, 1f, 0.4f) : new Color(0.6f, 0.6f, 0.6f),
        };

        // 帮助行样式（深灰色提示文字）
        private static TSSStyle HelpStyle => new TSSStyle
        {
            contentColor = new Color(0.35f, 0.35f, 0.35f),
        };

        // ── 核心引用 ──────────────────────────────────────────────
        private readonly DeveloperConsole _cli;

        // ── 图实例信息 ────────────────────────────────────────────
        private string _vfsPath;      // VFS 路径，用于 MarkAsRead
        private string _packID;       // 业务 ID（= MsgPackData.PackID），用于 MsgFinished 过滤
        private string _instanceID;   // 引擎运行时 GUID（由 GraphRunner.LoadPack 返回），用于驱动/卸载

        // ── 当前消息状态 ──────────────────────────────────────────
        private string _currentSpeaker = "???";
        private string _currentBody    = "";
        private readonly Dictionary<int, string> _currentOptions = new Dictionary<int, string>();
        private List<int> _sortedOptionKeys = new List<int>();
        private int _selectedIndex = -1;   // 当前高亮选项 key（-1 = 未选中）

        public MsgStrategy(DeveloperConsole cli)
        {
            _cli = cli;
        }

        // =========================================================
        //  ICatStrategy 实现
        // =========================================================

        public override void Execute(string vfsPath, string packID)
        {
            _vfsPath = vfsPath;
            _packID  = packID;

            // 直接从 VFS 读取并反序列化喵！✨
            var analyser = GraphAnalyser.Instance;
            if (analyser == null)
            {
                _cli.Log("错误：GraphAnalyser 实例为空喵~", Color.red);
                _cli.CloseActiveStrategy();
                return;
            }

            var node = analyser.GetNode(SocialManager.SOCIAL_PACK_ID, vfsPath);
            if (node is VFSNodeData vfs && !string.IsNullOrEmpty(vfs.DataJson))
            {
                var pack = JsonConvert.DeserializeObject<MsgPackData>(vfs.DataJson, MetaLib.JsonSettings);
                if (pack != null)
                {
                    // 使用新的 GraphRunner API 加载喵~
                    if (GraphRunner.Instance == null)
                    {
                        _cli.Log("错误：GraphRunner 未就绪喵~", Color.red);
                        _cli.CloseActiveStrategy();
                        return;
                    }
                    GraphRunner.Instance.SetPackDataDict(MainModel.Instance.CurrentUser.PackDataDict);
                    _instanceID = GraphRunner.Instance.LoadPack(pack);

                    if (_instanceID != null)
                    {
                        PostSystem.Instance.Register(this);
                        GraphRunner.Instance.InjectSignalFromRoot(_instanceID);

                        _cli.Log($"[系统] 正在建立加密连接以读取：{VFSPathResolver.GetFileName(vfsPath)}...", Color.gray);
                        return;
                    }
                }
            }

            // 失败处理喵~
            _cli.Log($"错误：加载图包失败 (PackID: {_packID})", Color.red);
            _cli.CloseActiveStrategy();
        }

        public override void OnInput(string input)
        {
            string trimmed = input.Trim();
            if (int.TryParse(trimmed, out int choice))
            {
                if (_currentOptions.ContainsKey(choice))
                {
                    SelectOption(choice);
                }
                else
                {
                    _cli.Log($"选项 {choice} 不存在，请重新选择喵~", Color.yellow);
                }
            }
            else if (!string.IsNullOrEmpty(trimmed))
            {
                _cli.Log("请输入数字编号（如 1, 2...）或用 ↑↓ 导航后按 Enter 确认喵~", Color.yellow);
            }
        }

        public override void Close()
        {
            _cli.Log("┄┄┄ 会话结束 ┄┄┄", new Color(0.4f, 0.4f, 0.4f));
            _cli.Log(" ", Color.clear);
            _cli.Log("  消息已标记为已读。", Color.gray);
            _cli.Log("  输入 <color=#88AAFF>ls</color> 查看其他消息，<color=#88AAFF>cd ..</color> 返回上级目录，<color=#88AAFF>help</color> 查看全部命令。", Color.gray);
            _cli.Log(" ", Color.clear);

            if (!string.IsNullOrEmpty(_vfsPath))
                SocialManager.Instance.MarkAsRead(_vfsPath);

            // 使用 GraphRunner 卸载 Pack 喵~
            if (!string.IsNullOrEmpty(_instanceID) && GraphRunner.Instance != null)
                GraphRunner.Instance.UnloadPack(_instanceID);

            PostSystem.Instance.Unregister(this);
        }

        // =========================================================
        //  方向键 + Enter（TUI 导航）
        // =========================================================

        public override void OnArrowKey(bool isUp)
        {
            if (_sortedOptionKeys == null || _sortedOptionKeys.Count == 0) return;

            // 第一次按方向键时从第一个选项开始
            if (_selectedIndex < 0 || !_sortedOptionKeys.Contains(_selectedIndex))
            {
                _selectedIndex = isUp
                    ? _sortedOptionKeys[_sortedOptionKeys.Count - 1]
                    : _sortedOptionKeys[0];
            }
            else
            {
                int idx = _sortedOptionKeys.IndexOf(_selectedIndex);
                idx = isUp ? idx - 1 : idx + 1;
                idx = (idx + _sortedOptionKeys.Count) % _sortedOptionKeys.Count; // 循环
                _selectedIndex = _sortedOptionKeys[idx];
            }

            RenderFull(scrollToTop: false);
        }

        public override void OnConfirm()
        {
            if (_selectedIndex > 0 && _currentOptions.ContainsKey(_selectedIndex))
                SelectOption(_selectedIndex);
        }

        // =========================================================
        //  PostSystem 事件监听
        // =========================================================

        [Subscribe("Social.ShowBody")]
        private void OnShowBody(object data)
        {
            _currentOptions.Clear();
            _sortedOptionKeys.Clear();
            _selectedIndex = -1;

            _currentSpeaker = "???";
            _currentBody    = data?.ToString() ?? "";

            var type = data?.GetType();
            if (type != null)
            {
                var speakerField = type.GetField("Speaker");
                var bodyField    = type.GetField("Body");
                if (speakerField != null) _currentSpeaker = speakerField.GetValue(data)?.ToString() ?? "???";
                if (bodyField    != null) _currentBody    = bodyField.GetValue(data)?.ToString()    ?? "";
            }

            // 先只渲染消息框，选项稍后由 OnRegisterOption 补充喵~
            RenderFull();
        }

        [Subscribe("Social.RegisterOption")]
        private void OnRegisterOption(object data)
        {
            var type       = data?.GetType();
            var indexField = type?.GetField("Index");
            var labelField = type?.GetField("Label");

            if (indexField != null && labelField != null)
            {
                int    index = (int)indexField.GetValue(data);
                string label = labelField.GetValue(data)?.ToString() ?? "";
                _currentOptions[index] = label;

                // 初次添加此 key 时设为高亮（第一个选项默认高亮）
                if (!_sortedOptionKeys.Contains(index))
                {
                    _sortedOptionKeys.Add(index);
                    _sortedOptionKeys.Sort();
                }
                if (_selectedIndex < 0)
                    _selectedIndex = _sortedOptionKeys[0];
            }

            // 每次注册选项后刷新，保证最终显示完整喵~
            RenderFull();
        }

        [Subscribe("Social.MsgFinished")]
        private void OnMsgFinished(object data)
        {
            string finishedID = data as string;
            if (finishedID == _packID)
            {
                _cli.CloseActiveStrategy();
            }
        }

        // =========================================================
        //  私有：选项执行
        // =========================================================

        private void SelectOption(int choice)
        {
            _cli.Log($"> {choice}", Color.cyan);

            TriggerEvent evt = choice switch
            {
                1 => TriggerEvent.SocialOption1,
                2 => TriggerEvent.SocialOption2,
                3 => TriggerEvent.SocialOption3,
                4 => TriggerEvent.SocialOption4,
                _ => TriggerEvent.GameStarted
            };

            if (evt != TriggerEvent.GameStarted)
            {
                PostOffice.Send(evt);
            }
            else
            {
                _cli.Log($"选项 {choice} 超出了系统支持的剧情分支范围喵！", Color.red);
            }
        }

        // =========================================================
        //  私有：ASCII TUI 渲染
        // =========================================================

        /// <summary>
        /// 清屏并重绘完整 TUI 界面
        /// </summary>
        private void RenderFull(bool scrollToTop = true)
        {
            _cli.ClearConsole();
            RenderMessageBox(_currentSpeaker, _currentBody);
            if (_currentOptions.Count > 0)
            {
                RenderOptions();
                RenderHelpLine();
            }
            if (scrollToTop)
                _cli.ScrollConsoleToTop();
        }

        /// <summary>
        /// 渲染 ASCII 消息文本框（使用 TUITool.GenerateTextBoxWithTitle 一次性生成）
        /// ┌─[Speaker]──────────────────────────┐
        /// │                                    │
        /// │  正文内容（自动换行）                │
        /// │                                    │
        /// └────────────────────────────────────┘
        /// </summary>
        private void RenderMessageBox(string speaker, string body)
        {
            var style = MsgBoxStyle;
            int bw = BoxWidth;

            // 上方出血空行（Color.clear 避免 TMP 折叠纯空字符串行）
            for (int i = 0; i < BleedY; i++) _cli.Log(" ", Color.clear);

            // 计算正文内容宽度（减去边框+padding，再减2给"  "缩进）
            int contentWidth = TUITool.CalcContentWidth(bw, style) - 2;
            var wrapped = WrapText(body, contentWidth);

            string[] contentLines = new string[wrapped.Count];
            for (int i = 0; i < wrapped.Count; i++)
                contentLines[i] = "  " + wrapped[i];

            // GenerateTextBoxWithTitle 处理顶栏/paddingY空行/内容/底栏
            foreach (var line in TUITool.GenerateTextBoxWithTitle(contentLines, speaker, bw, style))
                LogBoxLine(line);

            // 下方出血空行
            for (int i = 0; i < BleedY; i++) _cli.Log(" ", Color.clear);
        }

        /// <summary>
        /// 输出带居中左边距的一行，保证 TUI 框在终端内水平居中喵~
        /// （颜色已通过 RichText 标签嵌入字符串）
        /// </summary>
        private void LogBoxLine(string line)
        {
            string prefix = LeftPad > 0 ? new string(' ', LeftPad) : "";
            _cli.Log(prefix + line, Color.white);
        }

        /// <summary>
        /// 渲染选项列表（纯彩色文本行，无边框，与消息框风格分离）
        /// </summary>
        private void RenderOptions()
        {
            LogBoxLine(""); // 分隔空行

            foreach (int key in _sortedOptionKeys)
            {
                if (!_currentOptions.TryGetValue(key, out string label)) continue;
                bool highlighted = (key == _selectedIndex);
                string prefix = highlighted ? "▶ " : "  ";
                string raw = $"{prefix}[ {key} ]  {label}";
                string hex = ColorUtility.ToHtmlStringRGB(OptionStyle(highlighted).contentColor);
                LogBoxLine($"<color=#{hex}>{raw}</color>");
            }
        }

        /// <summary>
        /// 渲染帮助提示行（纯彩色文本行）
        /// </summary>
        private void RenderHelpLine()
        {
            string hex = ColorUtility.ToHtmlStringRGB(HelpStyle.contentColor);
            LogBoxLine("");
            LogBoxLine($"<color=#{hex}>  ↑↓ 切换   Enter 确认   数字直选</color>");
        }

        // =========================================================
        //  工具：文本换行
        // =========================================================

        /// <summary>
        /// 按视觉宽度对文本进行换行喵~
        /// ASCII 字符宽度 = 1，CJK/BoxDrawing 字符宽度 = 2
        /// </summary>
        private static List<string> WrapText(string text, int maxVisualWidth)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(text))
            {
                result.Add("");
                return result;
            }

            // 先按换行符拆段，\r\n 整体匹配优先，避免拆成两刀产生多余空行喵~
            string[] paragraphs = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            foreach (var rawPara in paragraphs)
            {
                // Tab 展开为 4 个空格，统一字符宽度，避免 TMP tab stop 与列数算法不一致喵~
                string para = rawPara.Replace("\t", "    ");

                if (string.IsNullOrEmpty(para))
                {
                    result.Add("");
                    continue;
                }

                var sb    = new StringBuilder();
                int width = 0;
                bool inTag = false;

                foreach (char c in para)
                {
                    // 标签字符直接追加，不计入视觉宽度喵~
                    if (c == '<') { inTag = true;  sb.Append(c); continue; }
                    if (c == '>') { inTag = false; sb.Append(c); continue; }
                    if (inTag)    { sb.Append(c); continue; }

                    int cw = TUITool.IsWideChar(c) ? 2 : 1;
                    if (width + cw > maxVisualWidth)
                    {
                        result.Add(sb.ToString());
                        sb.Clear();
                        width = 0;
                    }
                    sb.Append(c);
                    width += cw;
                }

                if (sb.Length > 0)
                    result.Add(sb.ToString());
            }

            return result;
        }
    }
}
