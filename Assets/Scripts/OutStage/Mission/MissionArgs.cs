using System.Collections.Generic;

public class MissionArgs
{
    public string StringKey; // 比如 "Iron_Ore"
    public int IntKey;       // 也可以传数字 ID，性能更高
    public long Amount;       // 数量
    public int Faction;      // 阵营

    // 静态池子
    private static readonly Stack<MissionArgs> Pool = new Stack<MissionArgs>(64);

    public static MissionArgs Get() => Pool.Count > 0 ? Pool.Pop() : new MissionArgs();

    public static void Release(MissionArgs args)
    {
        args.StringKey = null;
        args.IntKey = 0;
        args.Amount = 0;
        args.Faction = 0;
        Pool.Push(args);
    }
}