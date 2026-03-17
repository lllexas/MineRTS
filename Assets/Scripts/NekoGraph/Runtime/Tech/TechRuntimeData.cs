using System;
using System.Collections.Generic;
using UnityEngine;

// =========================================================
// Lab 科技树系统运行时状态定义
// =========================================================

/// <summary>
/// 科技节点状态枚举喵~
/// </summary>
public enum TechState
{
    /// <summary>
    /// 未解锁（隐藏或不可点击）
    /// </summary>
    Locked,

    /// <summary>
    /// 可研究（前置条件已满足）
    /// </summary>
    Available,

    /// <summary>
    /// 研究中（正在解锁）
    /// </summary>
    Researching,

    /// <summary>
    /// 已完成
    /// </summary>
    Completed
}

/// <summary>
/// 科技运行时数据喵~
/// 用于在内存中追踪科技的状态和进度
/// </summary>
[Serializable]
public class TechRuntimeData
{
    /// <summary>
    /// 科技 ID
    /// </summary>
    public string TechID;

    /// <summary>
    /// 当前状态
    /// </summary>
    public TechState State;

    /// <summary>
    /// 研究进度（0-1）
    /// </summary>
    public float Progress;

    /// <summary>
    /// 研究开始时间（用于计算进度）
    /// </summary>
    public double ResearchStartTime;

    /// <summary>
    /// 研究所需时间（秒）
    /// </summary>
    public float ResearchDuration;

    /// <summary>
    /// 是否已解锁
    /// </summary>
    public bool IsUnlocked => State == TechState.Completed;

    /// <summary>
    /// 是否可研究
    /// </summary>
    public bool CanResearch => State == TechState.Available;

    /// <summary>
    /// 构造函数喵~
    /// </summary>
    public TechRuntimeData(string techID)
    {
        TechID = techID;
        State = TechState.Locked;
        Progress = 0f;
        ResearchDuration = 0f;
    }

    /// <summary>
    /// 开始研究喵~
    /// </summary>
    public void StartResearch(float duration)
    {
        State = TechState.Researching;
        Progress = 0f;
        ResearchDuration = duration;
        ResearchStartTime = Time.time;
    }

    /// <summary>
    /// 更新研究进度喵~
    /// </summary>
    public void UpdateProgress()
    {
        if (State != TechState.Researching) return;

        float elapsed = (float)(Time.time - ResearchStartTime);
        Progress = Mathf.Clamp01(elapsed / ResearchDuration);

        if (Progress >= 1f)
        {
            Complete();
        }
    }

    /// <summary>
    /// 完成研究喵~
    /// </summary>
    public void Complete()
    {
        State = TechState.Completed;
        Progress = 1f;
    }

    /// <summary>
    /// 取消研究喵~
    /// </summary>
    public void Cancel()
    {
        State = TechState.Available;
        Progress = 0f;
    }
}
