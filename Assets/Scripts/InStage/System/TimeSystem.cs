using UnityEngine;

public class TimeSystem : SingletonMono<TimeSystem>
{
    private float _totalElapsed = 0f;
    private float _oneSecondTimer = 0f;
    private bool _isPaused = false;

    // Tick推进相关
    private float _tickAccumulator = 0f;
    private int _ticksProcessedThisFrame = 0;

    public float TotalElapsedSeconds => _totalElapsed;
    public bool IsPaused => _isPaused;
    public int TicksProcessedThisFrame => _ticksProcessedThisFrame;

    public void UpdateGameTick(float deltaTime)
    {
        if (_isPaused) return;

        // 1. 累积时间并推进tick
        _tickAccumulator += deltaTime;
        _ticksProcessedThisFrame = 0;

        while (_tickAccumulator >= TimeTicker.SecondsPerTick)
        {
            _ticksProcessedThisFrame++;
            _tickAccumulator -= TimeTicker.SecondsPerTick;
            TimeTicker.GlobalTick++;

            // 通知GridSystem清理门户预约
            GridSystem.Instance.AdvanceNavMeshTick(TimeTicker.GlobalTick);
        }

        TimeTicker.SubTickOffset = _tickAccumulator;

        // 2. 原有计时器逻辑（保留）
        _totalElapsed += deltaTime;
        _oneSecondTimer += deltaTime;

        if (_oneSecondTimer >= 1.0f)
        {
            _oneSecondTimer -= 1.0f;

            // 使用高性能 MissionArgs 广播喵！
            var args = MissionArgs.Get();
            args.Amount = 1; // 增加 1 秒
            args.StringKey = "Seconds";

            PostSystem.Instance.Send("生存时间增加", args);

            MissionArgs.Release(args);
        }
    }

    private void Update()
    {
        // 保留原有Update方法，但不再使用
        // TimeSystem现在由EntitySystem显式调用UpdateGameTick
        // 为了向后兼容，这里可以留空或调用UpdateGameTick
        // 但为了确保tick推进顺序正确，我们选择留空
        // 实际tick推进由EntitySystem.UpdateSystem中的UpdateGameTick调用
    }

    public void SetPaused(bool paused) => _isPaused = paused;

    public void ResetTimer()
    {
        _totalElapsed = 0f;
        _oneSecondTimer = 0f;
        _tickAccumulator = 0f;
        _ticksProcessedThisFrame = 0;
    }
}