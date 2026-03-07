#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 剧情根节点 UI - 整个剧情树的起始锚点（全图唯一）喵~
/// </summary>
public class StoryRootNode : BaseNode
{
    public Port OutputPort;

    public StoryRootNode()
    {
        GUID = "STORY_ROOT";
        title = "🌳 剧情根节点";
        style.width = 200;
        this.titleContainer.style.backgroundColor = new Color(1f, 0.8f, 0f); // 🟡 金色

        // 根节点只有输出端口，连向 SpineIDNode
        OutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
        OutputPort.portName = "输出到主干";
        outputContainer.Add(OutputPort);

        // 根节点不可删除
        capabilities &= ~Capabilities.Deletable;

        var infoLabel = new Label("这是剧情树的起点\n全图唯一，不可删除");
        infoLabel.style.fontSize = 10;
        infoLabel.style.marginTop = 5;
        extensionContainer.Add(infoLabel);

        RefreshExpandedState();
    }

    public override void UpdateData() { }
}
#endif
