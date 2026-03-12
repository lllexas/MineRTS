#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using NekoGraph;

/// <summary>
/// 节点基类 - 所有编辑器节点的抽象基类喵~
/// 【跨平台安全·类型转换重构版】
/// 封装了 GUID 同步、位置更新、端口自动生成等通用逻辑喵~
/// </summary>
public abstract class BaseNode : Node
{
    /// <summary>
    /// 节点唯一标识符喵~
    /// </summary>
    public string guid;

    /// <summary>
    /// 节点数据引用喵~
    /// </summary>
    public BaseNodeData Data;

    /// <summary>
    /// 输入端口列表 - 按索引排序喵~
    /// </summary>
    protected List<Port> InputPorts = new List<Port>();

    /// <summary>
    /// 输出端口列表 - 按索引排序喵~
    /// </summary>
    protected List<Port> OutputPorts = new List<Port>();

    /// <summary>
    /// 构造函数喵~
    /// 【重构后】无参构造函数不再自动生成端口，因为此时 Data 为 null 喵！
    /// </summary>
    protected BaseNode()
    {
        // 这里空着喵~ Data 为 null 时无法生成端口
    }

    /// <summary>
    /// 使用 BaseNodeData 初始化喵~
    /// 用于泛型工厂方法反射创建节点喵
    /// 【重构后】确保 Data 赋值后再生成端口喵！
    /// </summary>
    protected BaseNode(BaseNodeData data)
    {
        this.Data = data;
        this.guid = data.NodeID;
        // 自动生成端口喵~ 此时 Data 已经有值了！
        GeneratePortsFromMetadata();
    }

    /// <summary>
    /// 【自动组装魔法】根据 Data 类的端口标签自动生成端口 UI 喵~
    /// 子类无需再手动调用 InstantiatePort 和 container.Add！
    /// 【跨平台安全·类型转换重构版】将 NekoPortCapacity 转换为 Port.Capacity 喵！
    /// </summary>
    protected void GeneratePortsFromMetadata()
    {
        if (Data == null) return;

        // 如果已经长出端口了，就别再长一次了喵~
        if (inputContainer.childCount > 0 || outputContainer.childCount > 0) return;

        var type = Data.GetType();
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        // 收集输入端口字段喵~（使用 NekoPortCapacity 喵！）
        var inputPortFields = new List<(int Index, string Name, NekoPortCapacity Capacity, FieldInfo Field)>();
        var outputPortFields = new List<(int Index, string Name, NekoPortCapacity Capacity, FieldInfo Field)>();

        foreach (var field in fields)
        {
            // 检查 [InPort] 标签喵~
            var inPortAttr = field.GetCustomAttribute<InPortAttribute>();
            if (inPortAttr != null)
            {
                inputPortFields.Add((inPortAttr.Index, inPortAttr.PortName, inPortAttr.Capacity, field));
            }

            // 检查 [OutPort] 标签喵~
            var outPortAttr = field.GetCustomAttribute<OutPortAttribute>();
            if (outPortAttr != null)
            {
                outputPortFields.Add((outPortAttr.Index, outPortAttr.PortName, outPortAttr.Capacity, field));
            }
        }

        // 按索引排序，确保端口顺序一致喵~
        inputPortFields = inputPortFields.OrderBy(x => x.Index).ToList();
        outputPortFields = outputPortFields.OrderBy(x => x.Index).ToList();

        // 生成输入端口喵~（进行类型转换喵！）
        foreach (var portInfo in inputPortFields)
        {
            // 次元翻译：将 NekoPortCapacity 转换为 Port.Capacity 喵~
            Port.Capacity unityCapacity = portInfo.Capacity == NekoPortCapacity.Single
                ? Port.Capacity.Single
                : Port.Capacity.Multi;

            var port = InstantiatePort(Orientation.Horizontal, Direction.Input, unityCapacity, typeof(bool));
            port.portName = portInfo.Name;
            inputContainer.Add(port);
            InputPorts.Add(port);
        }

        // 生成输出端口喵~（进行类型转换喵！）
        foreach (var portInfo in outputPortFields)
        {
            // 次元翻译：将 NekoPortCapacity 转换为 Port.Capacity 喵~
            Port.Capacity unityCapacity = portInfo.Capacity == NekoPortCapacity.Single
                ? Port.Capacity.Single
                : Port.Capacity.Multi;

            var port = InstantiatePort(Orientation.Horizontal, Direction.Output, unityCapacity, typeof(bool));
            port.portName = portInfo.Name;
            outputContainer.Add(port);
            OutputPorts.Add(port);
        }
    }

    /// <summary>
    /// 根据索引获取输入端口喵~
    /// </summary>
    protected Port GetInputPort(int index)
    {
        if (index >= 0 && index < InputPorts.Count)
        {
            return InputPorts[index];
        }
        return null;
    }

    /// <summary>
    /// 根据索引获取输出端口喵~
    /// </summary>
    protected Port GetOutputPort(int index)
    {
        if (index >= 0 && index < OutputPorts.Count)
        {
            return OutputPorts[index];
        }
        return null;
    }

    /// <summary>
    /// 设置节点位置喵~
    /// </summary>
    public void SetNodePosition(Vector2 position)
    {
        SetPosition(new Rect(position, Vector2.zero));
        if (Data != null)
        {
            Data.EditorPosition = position;
        }
    }

    /// <summary>
    /// 获取节点位置喵~
    /// </summary>
    public Vector2 GetNodePosition()
    {
        return GetPosition().position;
    }

    /// <summary>
    /// 同步 GUID 到数据喵~
    /// </summary>
    public void SyncGUID(string guid)
    {
        this.guid = guid;
        if (Data != null)
        {
            Data.NodeID = guid;
        }
    }

    /// <summary>
    /// 更新节点数据喵~
    /// 子类必须实现此方法来保存 UI 状态到数据
    /// </summary>
    public abstract void UpdateData();

    /// <summary>
    /// 从数据初始化 UI 喵~
    /// </summary>
    public virtual void InitializeFromData()
    {
        if (Data != null)
        {
            SetPosition(new Rect(Data.EditorPosition, Vector2.zero));
        }
    }

    /// <summary>
    /// 克隆节点数据喵~
    /// </summary>
    public abstract BaseNodeData CloneData();
}

/// <summary>
/// 泛型节点基类 - 绑定特定数据类型喵~
/// </summary>
public abstract class BaseNode<T> : BaseNode where T : BaseNodeData, new()
{
    /// <summary>
    /// 强类型数据引用喵~
    /// </summary>
    public T TypedData;

    /// <summary>
    /// 构造函数喵~
    /// 【重构后】手动创建数据并触发生成端口喵！
    /// </summary>
    protected BaseNode() : base() // 显式调用基类无参构造
    {
        TypedData = new T();
        Data = TypedData;
        // 手动补一次生成喵！此时 Data 已经有值了！
        GeneratePortsFromMetadata();
    }

    /// <summary>
    /// 使用现有数据初始化喵~
    /// 【重构后】通过 : base(data) 把数据传给基类，让基类负责生成端口喵！
    /// </summary>
    protected BaseNode(T data) : base(data)
    {
        TypedData = data;
        // 注意：base(data) 已经触发过 GeneratePortsFromMetadata 了喵！
    }

    /// <summary>
    /// 克隆节点数据喵~
    /// </summary>
    public override BaseNodeData CloneData()
    {
        var cloned = new T();
        cloned.CopyFrom(TypedData);
        CloneCustomData(cloned);
        return cloned;
    }

    /// <summary>
    /// 克隆自定义数据字段喵~
    /// 子类可以重写此方法来克隆特定字段
    /// </summary>
    protected virtual void CloneCustomData(T cloned)
    {
        // 默认实现，子类可以重写
    }
}
#endif
