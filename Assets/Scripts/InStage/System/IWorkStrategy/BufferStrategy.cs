public class BufferStrategy : IWorkStrategy
{
    public void Tick(int index, WholeComponent whole, float deltaTime)
    {
        ref var inv = ref whole.inventoryComponent[index];

        // 简单的把 Input 转到 Output (瞬间转移)
        ref var inSlot = ref inv.GetInput(0);
        ref var outSlot = ref inv.GetOutput(0);

        if (inSlot.Count > 0 && outSlot.AvailableSpace > 0)
        {
            int t = inSlot.ItemType;
            inSlot.TryRemove(1);
            outSlot.TryAdd(t, 1);
        }
    }
}