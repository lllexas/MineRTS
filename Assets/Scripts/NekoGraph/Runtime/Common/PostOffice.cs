using System;
using UnityEngine;

/// <summary>
/// 💌【NekoGraph 官方邮局】
/// 职责：它是 NekoGraph 系统唯一的强类型发货入口喵！
/// 所有的 TriggerEvent 都必须在这里"挂号、检查、投递"，严禁私自绕过邮局走底层 PostSystem 喵~
/// </summary>
public static class PostOffice
{
    /// <summary>
    /// 【标准投递窗口】
    /// 自动根据 TriggerEvent 关联的协议进行校验喵！
    /// </summary>
    /// <param name="evt">事件枚举</param>
    /// <param name="payload">符合协议的参数对象</param>
    public static void Send(TriggerEvent evt, object payload)
    {
        var meta = TriggerRegistry.GetMeta(evt);
        if (meta == null)
        {
            Debug.LogError($"[PostOffice] 试图发送未定义的事件：{evt}！请先在 TriggerEvent 枚举中注册喵~");
            return;
        }

        // --- 运行时契约检查 ---
#if UNITY_EDITOR || DEBUG
        if (!ValidatePayload(meta.Info.Protocol, payload))
        {
            Debug.LogError($"<color=red>[PostOffice] 契约违例！</color> 事件 [{evt}] 预期的协议是 [{meta.Info.Protocol}]，但实际传了 [{payload?.GetType().Name ?? "null"}] 喵！\n" +
                           $"提示：请检查发送该事件的代码，确保参数类型正确喵~");
            return;
        }
#endif

        // 转译枚举为字符串，走【授权】物流总线喵~
        PostSystem.Instance.SendFromPostOffice(evt.ToString(), payload);
    }

    /// <summary>
    /// 【空载投递窗口】适用于 None 协议的事件喵~
    /// </summary>
    public static void Send(TriggerEvent evt) => Send(evt, null);

    private static bool ValidatePayload(EventProtocol protocol, object payload)
    {
        switch (protocol)
        {
            case EventProtocol.None: return payload == null;
            case EventProtocol.Numeric: return payload is float || payload is int || payload is double || payload is long;
            case EventProtocol.String: return payload is string;
            case EventProtocol.Vector: return payload is Vector3 || payload is Vector2;
            case EventProtocol.Boolean: return payload is bool;
            case EventProtocol.Entity: return payload != null; // 这里可以根据项目需求细化喵~
            default: return true;
        }
    }
}
