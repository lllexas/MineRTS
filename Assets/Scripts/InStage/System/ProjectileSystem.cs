using UnityEngine;

public class ProjectileSystem : SingletonMono<ProjectileSystem>
{
    public void UpdateProjectiles(WholeComponent whole, float deltaTime)
    {
        var entitySystem = EntitySystem.Instance;

        for (int i = whole.entityCount - 1; i >= 0; i--)
        {
            ref var core = ref whole.coreComponent[i];

            // 1. 过滤：必须是活跃的 子弹 类型
            if (!core.Active) continue;
            if ((core.Type & UnitType.Projectile) == 0) continue;

            ref var proj = ref whole.projectileComponent[i];

            // 2. 确定目标位置
            Vector2 targetPos = proj.TargetPosition;

            // 如果是追踪弹，尝试更新目标位置
            if (proj.IsHoming && proj.TargetEntityId != -1)
            {
                EntityHandle targetHandle = entitySystem.GetHandleFromId(proj.TargetEntityId);
                if (entitySystem.IsValid(targetHandle))
                {
                    int targetIdx = entitySystem.GetIndex(targetHandle);
                    targetPos = whole.coreComponent[targetIdx].Position;
                }
                else
                {
                    // 目标丢失（死了），继续飞向最后已知位置
                    proj.TargetEntityId = -1;
                }
            }

            // 3. 移动逻辑
            Vector2 currentPos = core.Position;
            Vector2 direction = targetPos - currentPos;
            float dist = direction.magnitude;

            // 计算这一帧能飞多远
            float moveStep = proj.Speed * deltaTime;

            // 4. 命中检测
            if (dist <= moveStep || dist <= proj.HitRadius)
            {
                // === 命中 ===

                // 确保飞到终点
                core.Position = targetPos;

                // 只有当目标还健在，或者是非追踪弹(炸地板)时才造成伤害
                // 这里简化：如果是追踪模式，必须重新确认目标有效性才能扣血
                if (proj.TargetEntityId != -1)
                {
                    EntityHandle targetHandle = entitySystem.GetHandleFromId(proj.TargetEntityId);
                    if (entitySystem.IsValid(targetHandle))
                    {
                        //------------------- 修改：废弃本地私有 ApplyDamage，调用 AttackSystem 的统一接口
                        // 这样才能触发阵营判定和“击败目标”广播喵！
                        // 传入参数：当前数据，目标句柄，子弹伤害，子弹的阵营(core.Team)
                        AttackSystem.Instance.ApplyDamage(whole, targetHandle, proj.Damage, core.Team);
                        //-------------------
                    }
                }
                else
                {
                    // 如果是非追踪弹，到了位置也许可以做个范围爆炸？目前先销毁
                }

                // 销毁子弹
                entitySystem.DestroyEntity(core.SelfHandle);
            }
            else
            {
                // === 继续飞行 ===
                direction.Normalize();
                core.Position += direction * moveStep;

                // 子弹朝向
                // core.Rotation 可以用来存子弹的角度，不过是 Vector2Int 比较尴尬
                // 暂时不处理子弹旋转，或者用 DrawSystem 特殊处理
            }
        }
    }
}