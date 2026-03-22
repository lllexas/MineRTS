#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using NekoGraph;

/// <summary>
/// ═══════════════════════════════════════════════════════════════
/// VFSNode - VFS 节点编辑器 UI 喵~
/// ═══════════════════════════════════════════════════════════════
///
/// 继承自 BaseNode<VFSNodeData>
/// 端口会自动根据 Data 类的 [InPort]/[OutPort] 标签生成喵~
/// ═══════════════════════════════════════════════════════════════
/// </summary>
[NodeMenuItem("📁 VFS/VFS 节点", typeof(VFSNodeData))]
[NodeType(NodeSystem.VFS)]
public class VFSNode : BaseNode<VFSNodeData>
{
    private TextField _nameField;
    private TextField _extensionField;
    private TextField _descriptionField;
    private Toggle _enabledToggle;

    /// <summary>
    /// 无参构造函数 - 用于从菜单创建节点喵~
    /// </summary>
    public VFSNode() : base()
    {
        InitializeUI();
    }

    /// <summary>
    /// 带参数构造函数 - 用于从数据加载节点喵~
    /// </summary>
    public VFSNode(VFSNodeData data) : base(data)
    {
        InitializeUI();
    }

    /// <summary>
    /// 初始化 UI 元素喵~
    /// 端口会自动根据 Data 类的标签"长"出来，这里只画特殊控件喵！
    /// </summary>
    private void InitializeUI()
    {
        UpdateTitle();
        style.width = 250;

        var foldout = new Foldout() { text = "节点配置", value = true };

        // 节点名称
        _nameField = new TextField("名称");
        _nameField.value = TypedData.Name;
        _nameField.RegisterValueChangedCallback(evt =>
        {
            TypedData.Name = evt.newValue;
            UpdateTitle();
        });
        foldout.Add(_nameField);

        // 扩展名（空=目录）
        _extensionField = new TextField("扩展名");
        _extensionField.value = TypedData.Extension;
        _extensionField.RegisterValueChangedCallback(evt =>
        {
            TypedData.Extension = evt.newValue;
            _extensionField.SetValueWithoutNotify(TypedData.Extension);
            UpdateTitle();
        });
        foldout.Add(_extensionField);

        // 描述
        _descriptionField = new TextField("描述") { multiline = true };
        _descriptionField.value = TypedData.Description;
        _descriptionField.RegisterValueChangedCallback(evt =>
            TypedData.Description = evt.newValue);
        foldout.Add(_descriptionField);

        // 启用状态
        _enabledToggle = new Toggle("已启用");
        _enabledToggle.value = TypedData.IsEnabled;
        _enabledToggle.RegisterValueChangedCallback(evt =>
            TypedData.IsEnabled = evt.newValue);
        foldout.Add(_enabledToggle);

        extensionContainer.Add(foldout);
        RefreshExpandedState();
    }

    /// <summary>
    /// 更新节点标题喵~
    /// 目录显示 📂，文件显示 📄
    /// </summary>
    private void UpdateTitle()
    {
        if (TypedData.IsDirectory)
            title = $"📂 {TypedData.Name}";
        else
            title = $"📄 {TypedData.Name}{TypedData.Extension}";
    }

    public override void UpdateData()
    {
        TypedData.Name = _nameField.value;
        TypedData.Extension = _extensionField.value;
        TypedData.Description = _descriptionField.value;
        TypedData.IsEnabled = _enabledToggle.value;
    }
}
#endif
