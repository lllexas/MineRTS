using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using NekoGraph;

namespace CatStrategies
{
    /// <summary>
    /// 社交消息 (.msg) 特化处理策略喵~
    /// 充当 SocialCLI (前端) 与 GraphRunner (后端) 之间的适配器
    /// 支持 ASCII TUI 文本框 + ↑↓ 导航 + Enter 确认 + 数字直选
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

        // │(1) + " "(1) + "  "(2) + content + "  "(2) + " "(1) + │(1) = BoxWidth  →  content ≤ BoxWidth-8 喵~
        private int InnerWidth => BoxWidth - 8;

        // ── 颜色定义 ───────────────────────────────────────────────
        private static readonly Color ColorBorder  = new Color(0.5f, 0.5f, 0.5f);   // 灰色边框
        private static readonly Color ColorSpeaker = new Color(0f, 1f, 1f);          // 青色发言人
        private static readonly Color ColorBody    = Color.white;                    // 正文白色
        private static readonly Color ColorOption  = new Color(0.6f, 0.6f, 0.6f);   // 普通选项灰
        private static readonly Color ColorHighlight = new Color(0.4f, 1f, 0.4f);   // 高亮绿
        private static readonly Color ColorHelpLine = new Color(0.35f, 0.35f, 0.35f); // 提示行深灰

        // ── 核心引用 ──────────────────────────────────────────────
        private readonly DeveloperConsole _cli;

        // ── 图实例信息 ────────────────────────────────────────────
        private string _vfsPath;
        private string _packID;
        private string _instanceID;

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

            var pack = MetaLib.GetPack<MsgPackData>(_packID);
            if (pack == null)
            {
                _cli.Log($"错误：加载图包失败 (PackID: {_packID})", Color.red);
                _cli.CloseActiveStrategy();
                return;
            }

            _instanceID = "TUI_" + Guid.NewGuid().ToString("N").Substring(0, 4);
            var instance = GraphLoader.LoadFromPackGeneric(pack, _instanceID, "Social", packID);

            if (instance != null)
            {
                GraphRunner.Instance.RegisterInstance(instance);
                PostSystem.Instance.Register(this);
                GraphRunner.Instance.InjectSignal(_instanceID, new SignalContext());

                _cli.Log($"[系统] 正在建立加密连接以读取：{VFSPathResolver.GetFileName(vfsPath)}...", Color.gray);
            }
            else
            {
                _cli.Log($"错误：实例化图失败 (PackID: {_packID})", Color.red);
                _cli.CloseActiveStrategy();
            }
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

            if (!string.IsNullOrEmpty(_instanceID))
                GraphRunner.Instance.UnregisterInstance(_instanceID);

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
            if (finishedID == _instanceID)
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
        /// 渲染 ASCII 消息文本框
        /// ┌─[ Speaker ]──────────────────────────┐
        /// │                                      │
        /// │  正文内容（自动换行）                  │
        /// │                                      │
        /// └──────────────────────────────────────┘
        /// </summary>
        private void RenderMessageBox(string speaker, string body)
        {
            int innerWidth = InnerWidth;

            // ── 上方出血空行（用空格占位，避免 TMP 折叠纯空字符串行）
            for (int i = 0; i < BleedY; i++) _cli.Log(" ", Color.clear);

            // ── 顶部边框：┌─[ Speaker ]─────────────────┐
            // Box Drawing 字符宽=2：┌(2)+─(2)+label+─(2)+─×fill(各2)+┐(2) = BoxWidth 喵~
            // 当 remaining 为奇数时，追加1个ASCII空格（1列）补齐，保证与底线等宽喵~
            string speakerLabel = $"[ {speaker} ]";
            int labelWidth = GetVisualWidth(speakerLabel);   // CJK/BoxDrawing=2, 其余=1
            int remaining = Mathf.Max(0, BoxWidth - 8 - labelWidth);
            int fillLen   = remaining / 2;
            string extra  = (remaining % 2 == 1) ? " " : "";
            string topLine = "┌─" + speakerLabel + "─" + new string('─', fillLen) + extra + "┐";
            LogBoxLine(topLine, ColorBorder);

            // ── 空行
            LogBoxLine(BorderLine(""), ColorBorder);

            // ── 正文（自动换行）
            var lines = WrapText(body, innerWidth);
            foreach (var line in lines)
            {
                LogBoxLine(BorderLine("  " + line), ColorBody);
            }

            // ── 空行
            LogBoxLine(BorderLine(""), ColorBorder);

            // ── 底部边框：└─────────────────────────────┘
            // BoxWidth 已保证偶数 → (BoxWidth-4) 必然为偶数 → 永远整除喵~
            // └(2) + ─×n(各2) + ┘(2) = BoxWidth  →  n = (BoxWidth-4)/2
            string bottomLine = "└" + new string('─', (BoxWidth - 4) / 2) + "┘";
            LogBoxLine(bottomLine, ColorBorder);

            // ── 下方出血空行（用空格占位，避免 TMP 折叠纯空字符串行）
            for (int i = 0; i < BleedY; i++) _cli.Log(" ", Color.clear);
        }

        /// <summary>
        /// │(2) + content + spaces + │(2) = BoxWidth 喵~
        /// Box Drawing 字符宽=2，所以两侧竖线共占4列喵~
        /// </summary>
        private string BorderLine(string content)
        {
            int padLen = BoxWidth - 4 - GetVisualWidth(content);
            if (padLen < 0) padLen = 0;
            return "│" + content + new string(' ', padLen) + "│";
        }

        /// <summary>
        /// 输出带居中左边距的一行，保证 TUI 框在终端内水平居中喵~
        /// </summary>
        private void LogBoxLine(string line, Color color)
        {
            string prefix = LeftPad > 0 ? new string(' ', LeftPad) : "";
            _cli.Log(prefix + line, color);
        }

        /// <summary>
        /// 渲染选项列表
        /// </summary>
        private void RenderOptions()
        {
            LogBoxLine("", Color.clear);

            foreach (int key in _sortedOptionKeys)
            {
                if (!_currentOptions.TryGetValue(key, out string label)) continue;

                bool isHighlighted = (key == _selectedIndex);
                string prefix = isHighlighted ? "▶ " : "  ";
                string line = $"{prefix}[ {key} ]  {label}";

                LogBoxLine(line, isHighlighted ? ColorHighlight : ColorOption);
            }
        }

        /// <summary>
        /// 渲染帮助提示行
        /// </summary>
        private void RenderHelpLine()
        {
            LogBoxLine("", Color.clear);
            LogBoxLine("  ↑↓ 切换   Enter 确认   数字直选", ColorHelpLine);
        }

        // =========================================================
        //  工具：文本换行 & 视觉宽度
        // =========================================================

        /// <summary>
        /// 按视觉宽度对文本进行换行喵~
        /// ASCII 字符宽度 = 1，CJK 字符宽度 = 2
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

                    int cw = IsCJK(c) ? 2 : 1;
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

        /// <summary>
        /// 计算字符串的视觉宽度（CJK=2，Box Drawing=2，其余=1），自动跳过富文本标签喵~
        /// </summary>
        private static int GetVisualWidth(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            int w = 0;
            bool inTag = false;
            foreach (char c in s)
            {
                if (c == '<') { inTag = true; continue; }
                if (c == '>') { inTag = false; continue; }
                if (inTag) continue;
                w += (IsCJK(c) || IsBoxDrawing(c)) ? 2 : 1;
            }
            return w;
        }

        /// <summary>
        /// 剥离富文本标签，返回纯可见字符串喵~
        /// </summary>
        private static string StripTags(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new StringBuilder();
            bool inTag = false;
            foreach (char c in s)
            {
                if (c == '<') { inTag = true; continue; }
                if (c == '>') { inTag = false; continue; }
                if (!inTag) sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// 判断是否为 CJK 字符（包含常用汉字、全角标点、日文假名）
        /// </summary>
        private static bool IsCJK(char c)
        {
            return (c >= 0x4E00 && c <= 0x9FFF)   // CJK 统一汉字
                || (c >= 0x3040 && c <= 0x30FF)   // 平假名 + 片假名
                || (c >= 0xFF00 && c <= 0xFFEF)   // 全角字符
                || (c >= 0x3000 && c <= 0x303F);  // CJK 符号和标点
        }

        /// <summary>
        /// 判断是否为 Box Drawing 字符（U+2500-U+257F），字体里这些字符 advance=2× 喵~
        /// </summary>
        private static bool IsBoxDrawing(char c)
        {
            return c >= 0x2500 && c <= 0x257F;
        }
    }
}
