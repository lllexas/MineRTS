using UnityEngine;

public class TimeSystem : SingletonMono<TimeSystem>
{
    private float _totalElapsed = 0f;
    private float _oneSecondTimer = 0f;
    private bool _isPaused = false;

    public float TotalElapsedSeconds => _totalElapsed;
    public bool IsPaused => _isPaused;

    private void Update()
    {
        if (_isPaused) return;

        float dt = Time.deltaTime;
        _totalElapsed += dt;
        _oneSecondTimer += dt;

        // 每跳够 1 秒，发一次广播
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

    public void SetPaused(bool paused) => _isPaused = paused;

    public void ResetTimer()
    {
        _totalElapsed = 0f;
        _oneSecondTimer = 0f;
    }
}