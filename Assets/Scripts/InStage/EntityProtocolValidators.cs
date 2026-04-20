/// <summary>
/// MineRTS 项目层协议校验器喵~
/// 把项目具体 payload 的契约定义放在外部注册，不污染 NekoGraph 内核。
/// </summary>
public static class EntityProtocolValidators
{
    [EventProtocolValidator(EventProtocol.Entity)]
    public static bool ValidateEntityPayload(object payload)
    {
        return payload is EntityHandle;
    }

    [EventProtocolValidator(EventProtocol.SpawnRequest)]
    public static bool ValidateSpawnRequestPayload(object payload)
    {
        return payload is SpawnRequestArgs;
    }
}
