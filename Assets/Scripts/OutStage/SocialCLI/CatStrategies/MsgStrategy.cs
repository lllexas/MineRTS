using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using NekoGraph;
using Newtonsoft.Json;
using SpaceTUI;

namespace CatStrategies
{
    /// <summary>
    /// 遗留的社交消息 (.msg) 策略。
    /// 这套实现把 pack 加载、运行时事件、TUI 渲染和输入会话耦合在同一个类里，
    /// 仅保留作历史参考；其中 TUI 排版与选择器接管逻辑仍可借鉴。
    /// </summary>
    [Obsolete("MsgStrategy 已废弃。vfs.msg 正在迁移到 VFSResource + VFSExecute + VFSQuery 新协议，此类后续将被删除。", false)]
    public class MsgStrategy : CatStrategyBase
    {
        private const int BleedX = 4;
        private const int BleedY = 1;

        private int BoxWidth
        {
            get
            {
                int width = Mathf.Max(_cli.ConsoleWidth - BleedX * 2, 42);
                if (width % 2 != 0)
                {
                    width--;
                }

                return width;
            }
        }

        private int LeftPad => (_cli.ConsoleWidth - BoxWidth) / 2;

        private static TSSStyle MsgBoxStyle => new TSSStyle
        {
            bleedX = 0,
            bleedY = 0,
            paddingX = 1,
            paddingY = 1,
            borderColor = new Color(0.5f, 0.5f, 0.5f),
            contentColor = Color.white,
            alignment = SpaceTUI.TextAlignment.Left,
            expandArtSpaces = false
        };

        private static TSSStyle OptionStyle(bool highlighted) => new TSSStyle
        {
            contentColor = highlighted ? new Color(0.4f, 1f, 0.4f) : new Color(0.6f, 0.6f, 0.6f)
        };

        private static TSSStyle HelpStyle => new TSSStyle
        {
            contentColor = new Color(0.35f, 0.35f, 0.35f)
        };

        private readonly DeveloperConsole _cli;

        private string _vfsPath;
        private string _packID;
        private string _instanceID;

        private string _currentSpeaker = "???";
        private string _currentBody = string.Empty;
        private readonly Dictionary<int, string> _currentOptions = new Dictionary<int, string>();
        private readonly List<int> _sortedOptionKeys = new List<int>();

        public MsgStrategy(DeveloperConsole cli)
        {
            _cli = cli;
        }

        public override void Execute(string vfsPath, string packID)
        {
            _vfsPath = vfsPath;
            _packID = packID;

            var analyser = GraphHub.Instance?.DefaultAnalyser;
            if (analyser == null)
            {
                _cli.Log("错误：GraphAnalyser 实例为空", Color.red);
                _cli.CloseActiveStrategy();
                return;
            }

            // 从 GraphRunner 获取当前执行主体的权限喵~
            int subjectLevel = GraphHub.Instance?.DefaultRunner?.GetSubjectLevel() ?? PackAccessSubjects.Player;
            var node = analyser.GetNode(SocialManager.SOCIAL_PACK_ID, vfsPath, subjectLevel);
            if (node is VFSNodeData vfs && !string.IsNullOrEmpty(vfs.DataJson))
            {
                var pack = JsonConvert.DeserializeObject<BasePackData>(vfs.DataJson, MetaLib.JsonSettings);
                if (pack != null)
                {
                    pack.System = NodeSystem.Social;
                    if (GraphHub.Instance?.DefaultRunner == null)
                    {
                        _cli.Log("错误：GraphRunner 未就绪", Color.red);
                        _cli.CloseActiveStrategy();
                        return;
                    }

                    GraphHub.Instance.DefaultRunner.SetPackTable(MainModel.Instance.CurrentUser.PackDataDict);
                    _instanceID = GraphHub.Instance.DefaultRunner.LoadPack(pack);

                    if (_instanceID != null)
                    {
                        PostSystem.Instance.Register(this);
                        GraphHub.Instance.DefaultRunner.InjectSignalFromRoot(_instanceID);
                        _cli.Log($"[系统] 正在建立加密连接以读取：{VFSPathResolver.GetFileName(vfsPath)}...", Color.gray);
                        return;
                    }
                }
            }

            _cli.Log($"错误：加载图包失败 (PackID: {_packID})", Color.red);
            _cli.CloseActiveStrategy();
        }

        public override void OnInput(string input)
        {
            if (!string.IsNullOrWhiteSpace(input))
            {
                _cli.Log("请输入数字编号，或使用方向键后按 Enter 确认。", Color.yellow);
            }
        }

        public override void Close()
        {
            _cli.UnmountInputHandler();
            _cli.Log("──── 会话结束 ────", new Color(0.4f, 0.4f, 0.4f));
            _cli.Log(" ", Color.clear);
            _cli.Log("  消息已标记为已读。", Color.gray);
            _cli.Log("  输入 <color=#88AAFF>ls</color> 查看其他消息，<color=#88AAFF>cd ..</color> 返回上级目录，<color=#88AAFF>help</color> 查看全部命令。", Color.gray);
            _cli.Log(" ", Color.clear);

            if (!string.IsNullOrEmpty(_vfsPath))
            {
                SocialManager.Instance.MarkAsRead(_vfsPath);
            }

            if (!string.IsNullOrEmpty(_instanceID) && GraphHub.Instance?.DefaultRunner != null)
            {
                GraphHub.Instance.DefaultRunner.UnloadPack(_instanceID);
            }

            PostSystem.Instance.Unregister(this);
        }

        public override void OnArrowKey(bool isUp) { }

        public override void OnConfirm() { }

        [Subscribe("Social.ShowBody")]
        private void OnShowBody(object data)
        {
            _currentOptions.Clear();
            _sortedOptionKeys.Clear();
            _cli.UnmountInputHandler();

            _currentSpeaker = "???";
            _currentBody = data?.ToString() ?? string.Empty;

            var type = data?.GetType();
            if (type != null)
            {
                var speakerField = type.GetField("Speaker");
                var bodyField = type.GetField("Body");
                if (speakerField != null)
                {
                    _currentSpeaker = speakerField.GetValue(data)?.ToString() ?? "???";
                }

                if (bodyField != null)
                {
                    _currentBody = bodyField.GetValue(data)?.ToString() ?? string.Empty;
                }
            }

            RenderFull();
        }

        [Subscribe("Social.RegisterOption")]
        private void OnRegisterOption(object data)
        {
            var type = data?.GetType();
            var indexField = type?.GetField("Index");
            var labelField = type?.GetField("Label");

            if (indexField == null || labelField == null)
            {
                return;
            }

            int index = (int)indexField.GetValue(data);
            string label = labelField.GetValue(data)?.ToString() ?? string.Empty;
            _currentOptions[index] = label;

            if (!_sortedOptionKeys.Contains(index))
            {
                _sortedOptionKeys.Add(index);
                _sortedOptionKeys.Sort();
            }

            MountOptionHandle();
        }

        [Subscribe("Social.MsgFinished")]
        private void OnMsgFinished(object data)
        {
            if ((data as string) == _packID)
            {
                _cli.CloseActiveStrategy();
            }
        }

        private void MountOptionHandle()
        {
            TUISelectionConfig config = BuildSelectionConfig();

            if (_cli.CurrentInputHandler is TUIListSelectionHandler existing)
            {
                existing.UpdateConfig(config, resetSelection: false);
                _cli.MountInputHandler(existing);
                return;
            }

            _cli.MountInputHandler(new TUIListSelectionHandler(config));
        }
        private TUISelectionConfig BuildSelectionConfig()
        {
            var items = new List<TUISelectionItem>(_sortedOptionKeys.Count);
            foreach (int key in _sortedOptionKeys)
            {
                if (!_currentOptions.TryGetValue(key, out string label))
                {
                    continue;
                }

                int capturedKey = key;
                items.Add(new TUISelectionItem
                {
                    key = key,
                    indexText = key.ToString(),
                    label = label,
                    subtitle = null,
                    payload = null,
                    onConfirm = () => SelectOption(capturedKey)
                });
            }

            return new TUISelectionConfig
            {
                title = null,
                helpText = "  ↑↓ 切换   Enter 确认   数字直选",
                emptyText = string.Empty,
                initialSelectedKey = items.Count > 0 ? items[0].key : -1,
                console = _cli,
                items = items,
                viewStyle = new TUISelectionViewStyle
                {
                    normalState = new TUISelectionStateStyle
                    {
                        prefixText = "  ",
                        contentColor = OptionStyle(false).contentColor,
                        indexColor = OptionStyle(false).contentColor,
                        prefixColor = OptionStyle(false).contentColor
                    },
                    selectedState = new TUISelectionStateStyle
                    {
                        prefixText = "> ",
                        contentColor = OptionStyle(true).contentColor,
                        indexColor = OptionStyle(true).contentColor,
                        prefixColor = OptionStyle(true).contentColor
                    },
                    titleStyle = TSSStyle.Default,
                    itemStyle = new TSSStyle
                    {
                        alignment = SpaceTUI.TextAlignment.Left
                    },
                    helpStyle = HelpStyle,
                    emptyStyle = TSSStyle.Default,
                    topSpacing = 1,
                    bottomSpacing = 0
                },
                interaction = new TUISelectionInteractionConfig
                {
                    wrapNavigation = true,
                    enableDigitSelect = true,
                    allowConfirmOnEmptySubmit = true,
                    onCancel = null,
                    onSelectionChanged = null,
                    onConfirmSelection = null
                }
            };
        }

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
                _cli.Log($"选项 {choice} 超出了系统支持的剧情分支范围。", Color.red);
            }
        }

        private void RenderFull(bool scrollToTop = true)
        {
            _cli.ClearConsole();
            RenderMessageBox(_currentSpeaker, _currentBody);
            if (scrollToTop)
            {
                _cli.ScrollConsoleToTop();
            }
        }

        private void RenderMessageBox(string speaker, string body)
        {
            var style = MsgBoxStyle;
            int boxWidth = BoxWidth;

            for (int i = 0; i < BleedY; i++)
            {
                _cli.Log(" ", Color.clear);
            }

            int contentWidth = TUITool.CalcContentWidth(boxWidth, style) - 2;
            List<string> wrapped = WrapText(body, contentWidth);

            string[] contentLines = new string[wrapped.Count];
            for (int i = 0; i < wrapped.Count; i++)
            {
                contentLines[i] = "  " + wrapped[i];
            }

            foreach (string line in TUITool.GenerateTextBoxWithTitle(contentLines, speaker, boxWidth, style))
            {
                LogBoxLine(line);
            }

            for (int i = 0; i < BleedY; i++)
            {
                _cli.Log(" ", Color.clear);
            }
        }

        private void LogBoxLine(string line)
        {
            string prefix = LeftPad > 0 ? new string(' ', LeftPad) : string.Empty;
            _cli.Log(prefix + line, Color.white);
        }

        private static List<string> WrapText(string text, int maxVisualWidth)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(text))
            {
                result.Add(string.Empty);
                return result;
            }

            string[] paragraphs = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            foreach (string rawPara in paragraphs)
            {
                string para = rawPara.Replace("\t", "    ");
                if (string.IsNullOrEmpty(para))
                {
                    result.Add(string.Empty);
                    continue;
                }

                var sb = new StringBuilder();
                int width = 0;
                bool inTag = false;

                foreach (char c in para)
                {
                    if (c == '<')
                    {
                        inTag = true;
                        sb.Append(c);
                        continue;
                    }

                    if (c == '>')
                    {
                        inTag = false;
                        sb.Append(c);
                        continue;
                    }

                    if (inTag)
                    {
                        sb.Append(c);
                        continue;
                    }

                    int charWidth = TUITool.IsWideChar(c) ? 2 : 1;
                    if (width + charWidth > maxVisualWidth)
                    {
                        result.Add(sb.ToString());
                        sb.Clear();
                        width = 0;
                    }

                    sb.Append(c);
                    width += charWidth;
                }

                if (sb.Length > 0)
                {
                    result.Add(sb.ToString());
                }
            }

            return result;
        }
    }
}



