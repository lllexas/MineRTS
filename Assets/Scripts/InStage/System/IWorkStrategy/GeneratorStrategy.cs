using UnityEngine;

public class GeneratorStrategy : IWorkStrategy
{
    public void Tick(int index, WholeComponent whole, float deltaTime)
    {
        ref var work = ref whole.workComponent[index];
        ref var inv = ref whole.inventoryComponent[index];
        ref var power = ref whole.powerComponent[index];

        // 1. 获取蓝图定义的发电量 (比如 50W)
        var bp = BlueprintRegistry.Get(whole.coreComponent[index].BlueprintName);
        float baseProduction = bp.EnergyGeneration;

        // 2. 燃料消耗逻辑
        // 我们用 work.Progress 表示当前这一份燃料的“剩余燃烧进度” (1.0 -> 0.0)
        if (work.Progress > 0)
        {
            // 正在燃烧中喵！
            work.Progress -= deltaTime * work.WorkSpeed; // WorkSpeed 这里代表燃烧速度
            power.Production = baseProduction;

            // 保护一下，防止变成负数
            if (work.Progress < 0) work.Progress = 0;
        }
        else
        {
            // 燃料烧完了，尝试从输入槽(Input0)吞掉一个新的燃料
            ref var fuelSlot = ref inv.GetInput(0);

            if (fuelSlot.Count > 0)
            {
                fuelSlot.TryRemove(1);
                work.Progress = 1.0f; // 填满燃烧进度
                power.Production = baseProduction;
            }
            else
            {
                // 真没燃料了，熄火喵...
                power.Production = 0;
                work.Progress = 0;
            }
        }
    }
}