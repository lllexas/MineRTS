using UnityEngine;

public class BatteryStrategy : IWorkStrategy
{
    public void Tick(int index, WholeComponent whole, float deltaTime)
    {
        ref var power = ref whole.powerComponent[index];
        ref var draw = ref whole.drawComponent[index];

        // --- 表现逻辑：根据电量百分比设置动画帧 ---
        if (power.Capacity > 0)
        {
            float ratio = power.StoredEnergy / power.Capacity;

            // 假设蓄电池有 5 帧动画（0:空, 4:满）
            // 我们可以直接计算出当前应该显示哪一帧
            draw.AnimationFrame = Mathf.Clamp(Mathf.Floor(ratio * 5f), 0, 4);
        }
    }
}