public class SellerStrategy : IWorkStrategy
{
    public void Tick(int index, WholeComponent whole, float deltaTime)
    {
        ref var inv = ref whole.inventoryComponent[index];
        ref var inSlot = ref inv.GetInput(0);

        if (inSlot.Count > 0)
        {
            //------------------- 修改：在移除前记录物品信息
            // 获取物品的类型（假设 ItemType 是 int，可能对应 "铁矿"、"铜块" 等）
            int itemType = inSlot.ItemType;
            int count = inSlot.Count;
            //-------------------

            // 假设一格矿卖 10 块
            int amountSold = inSlot.TryRemove(count); // 全部卖掉

            if (amountSold > 0)
            {
                // 1. 金钱增加 (这个照旧，long 类型不装箱喵)
                IndustrialSystem.Instance.AddGold(amountSold * 10);

                //------------------- 修改：高性能广播 -------------------
                // 从池子里拿一个参数对象
                var args = MissionArgs.Get();

                // 🔥【重点】不要在循环里调 ToString()！
                // 我们直接传 int 类型的 IntKey，让 MissionManager 去查表
                args.IntKey = itemType;
                args.Amount = amountSold;

                PostSystem.Instance.Send("出售资源", args);

                // 发送完立即回收，因为 PostSystem 的 Send 是同步执行的喵！
                MissionArgs.Release(args);
                //-------------------------------------------------------
            }
        }
    }
}