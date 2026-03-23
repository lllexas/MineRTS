using System;
using UnityEngine;
using NekoGraph;

/// <summary>
/// 正文发射器策略 - 广播内容后直接通过信号喵~
/// </summary>
public class SocialMsgContentNodeStrategy : NodeStrategy
{
    public override void OnSignalEnter(BaseNodeData data, SignalContext context, BasePackData pack, GraphRunner runner, string packInstanceID)
    {
        if (data is not SocialMsgContentNodeData contentNode) return;

        // 1. 广播显示正文事件
        PostSystem.Instance.Send("Social.ShowBody", new SocialBodyEvent
        {
            Speaker = contentNode.Speaker,
            Body = contentNode.Body
        });

        // 2. 信号直接穿透，不阻塞喵~
        Propagate(contentNode, context, pack);
    }

    public override void OnEvent(BaseNodeData data, string eventName, object eventData, BasePackData pack, GraphRunner runner, string packInstanceID) { }

    private void Propagate(SocialMsgContentNodeData node, SignalContext context, BasePackData pack)
    {
        EnqueueSignals(pack, node.OutputConnections, context);
    }

    public class SocialBodyEvent
    {
        public string Speaker;
        public string Body;
    }
}

/// <summary>
/// 选项信息节点策略 - 注册选项后通过信号喵~
/// 注意：它不负责阻塞，阻塞由下游的 TriggerNode 负责喵！
/// </summary>
public class ChoiceTextNodeStrategy : NodeStrategy
{
    public override void OnSignalEnter(BaseNodeData data, SignalContext context, BasePackData pack, GraphRunner runner, string packInstanceID)
    {
        if (data is not ChoiceTextNodeData choiceNode) return;

        // 1. 广播注册选项事件
        PostSystem.Instance.Send("Social.RegisterOption", new SocialOptionEvent
        {
            Index = choiceNode.OptionIndex,
            Label = choiceNode.Label
        });

        // 2. 信号穿透喵~
        Propagate(choiceNode, context, pack);
    }

    public override void OnEvent(BaseNodeData data, string eventName, object eventData, BasePackData pack, GraphRunner runner, string packInstanceID) { }

    private void Propagate(ChoiceTextNodeData node, SignalContext context, BasePackData pack)
    {
        EnqueueSignals(pack, node.OutputConnections, context);
    }

    public class SocialOptionEvent
    {
        public int Index;
        public string Label;
    }
}

/// <summary>
/// 社交终结节点策略 - 宣告对话圆满结束喵~
/// </summary>
public class SocialMsgEndNodeStrategy : NodeStrategy
{
    public override void OnSignalEnter(BaseNodeData data, SignalContext context, BasePackData pack, GraphRunner runner, string packInstanceID)
    {
        // 广播结束事件
        PostSystem.Instance.Send("Social.MsgFinished", pack.PackID);

        // 信号流至此自然枯竭喵~
    }

    public override void OnEvent(BaseNodeData data, string eventName, object eventData, BasePackData pack, GraphRunner runner, string packInstanceID) { }
}
