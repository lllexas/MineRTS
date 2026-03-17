using System;
using System.Collections.Generic;
using UnityEngine;
using NekoGraph;

namespace SocialCLIStrategies
{
    /// <summary>
    /// 社交消息 (.msg) 特化处理策略喵~
    /// 充当 SocialCLI (前端) 与 GraphRunner (后端) 之间的适配器
    /// </summary>
    public class MsgStrategy : ICatStrategy
    {
        private readonly SocialCLI _cli;
        private string _vfsPath;
        private string _packID;
        private string _instanceID;
        private readonly Dictionary<int, string> _currentOptions = new Dictionary<int, string>();

        public MsgStrategy(SocialCLI cli)
        {
            _cli = cli;
        }

        public void Execute(string vfsPath, string packID)
        {
            _vfsPath = vfsPath;
            _packID = packID;

            // 1. 从智能仓库获取 PackData
            var pack = MetaLib.GetPack<MsgPackData>(_packID);
            if (pack == null)
            {
                _cli.Log($"错误：加载图包失败 (PackID: {_packID})", Color.red);
                _cli.CloseActiveStrategy();
                return;
            }

            // 2. 实例化运行环境
            _instanceID = "TUI_" + Guid.NewGuid().ToString("N").Substring(0, 4);
            var instance = GraphLoader.LoadFromPackGeneric(pack, _instanceID, "Social", packID);

            if (instance != null)
            {
                // 3. 注册并启动
                GraphRunner.Instance.RegisterInstance(instance);

                // 4. 注册事件监听
                PostSystem.Instance.Register(this);

                // 5. 【起跑信号】注入初始信号
                GraphRunner.Instance.InjectSignal(_instanceID, new SignalContext());

                _cli.Log($"[系统] 正在建立加密连接以读取：{VFSPathResolver.GetFileName(vfsPath)}...", Color.gray);
            }
            else
            {
                _cli.Log($"错误：实例化图失败 (PackID: {_packID})", Color.red);
                _cli.CloseActiveStrategy();
            }
        }

        public void OnInput(string input)
        {
            if (int.TryParse(input.Trim(), out int choice))
            {
                if (_currentOptions.ContainsKey(choice))
                {
                    _cli.Log($"> {choice}", Color.cyan);
                    // 广播选项点击事件，唤醒 NekoGraph 里的 TriggerNode 喵~
                    PostSystem.Instance.Send($"SocialCLI.Option{choice}", choice);
                }
                else
                {
                    _cli.Log($"选项 {choice} 不存在，请重新选择喵~", Color.yellow);
                }
            }
            else
            {
                _cli.Log("无效输入，请输入数字编号（如 1, 2...）喵~", Color.yellow);
            }
        }

        public void Close()
        {
            _cli.Log("\n--- 会话结束，返回命令行模式 ---", Color.gray);
            
            // 1. 标记已读
            if (!string.IsNullOrEmpty(_vfsPath))
            {
                SocialManager.Instance.MarkAsRead(_vfsPath);
            }

            // 2. 注销图实例
            if (!string.IsNullOrEmpty(_instanceID))
            {
                GraphRunner.Instance.UnregisterInstance(_instanceID);
            }

            // 3. 注销监听
            PostSystem.Instance.Unregister(this);
        }

        // =========================================================
        //  事件监听：将后端逻辑信号转接为前端 UI 输出
        // =========================================================

        [Subscribe("Social.ShowBody")]
        private void OnShowBody(object data)
        {
            // 注意：这里需要根据具体的事件数据结构进行解析喵~
            // 假设使用了 SocialMsgContentNodeStrategy.SocialBodyEvent
            // 如果反射拿不到类型，可以用动态解析或者约定好的 object[]
            
            // 为了保持简单，我们假设事件发送的是一个通用的 DTO 或解析后的字符串
            // 实际上应该去读取对应的 Strategy 定义
            _currentOptions.Clear(); // 刷新正文时清空旧选项
            
            // 这里我们先做一个健壮性处理
            string speaker = "???";
            string body = data?.ToString();

            // 尝试通过反射或动态类型获取字段（为了兼容性喵~）
            var type = data.GetType();
            var speakerField = type.GetField("Speaker");
            var bodyField = type.GetField("Body");

            if (speakerField != null) speaker = speakerField.GetValue(data).ToString();
            if (bodyField != null) body = bodyField.GetValue(data).ToString();

            _cli.Log($"\n[来自 {speaker} 的消息]", Color.cyan);
            _cli.Log(body, Color.white);
            _cli.Log("--------------------------------", Color.gray);
        }

        [Subscribe("Social.RegisterOption")]
        private void OnRegisterOption(object data)
        {
            var type = data.GetType();
            var indexField = type.GetField("Index");
            var labelField = type.GetField("Label");

            if (indexField != null && labelField != null)
            {
                int index = (int)indexField.GetValue(data);
                string label = labelField.GetValue(data).ToString();
                
                _currentOptions[index] = label;
                _cli.Log($"{index}) {label}", Color.green);
            }
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
    }
}
