using UnityEngine;

/// <summary>
/// 召唤请求参数喵~
/// 属于游戏层 payload，不放在 NekoGraph 内核里。
/// </summary>
public sealed class SpawnRequestArgs
{
    public Vector2Int GridPosition;
    public int Faction;
}
