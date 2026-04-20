using UnityEngine;

[TriggerEventInfo("UnitSpawnRequest", EventProtocol.SpawnRequest, "召唤单位请求", "战斗",
    Tooltip = "Payload 必须是 SpawnRequestArgs，用于把生成坐标和阵营附着到 signal 上。")]
public static class EntityTriggerEvents
{
    public static void Handle(object payload)
    {
        // 当前只承担事件契约注册职责，不做默认业务处理。
        // 真正的召唤仍由图中的 TriggerNode / VFS .entity Execute 链完成。
        if (payload is not SpawnRequestArgs)
        {
            Debug.LogWarning("[EntityTriggerEvents] UnitSpawnRequest 收到的 payload 不是 SpawnRequestArgs。");
        }
    }
}
