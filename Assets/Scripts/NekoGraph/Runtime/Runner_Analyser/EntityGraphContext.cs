using System.Collections.Generic;

// ═══════════════════════════════════════════════════════════════
// EntityGraphContext - 执行上下文喵~ 📋
// ═══════════════════════════════════════════════════════════════
//
// 【操作系统类比】进程控制块 PCB (Process Control Block)
// ┌─────────────────────────────────────────────────────────────┐
// │  字段              │  操作系统类比      │  说明             │
// ├─────────────────────────────────────────────────────────────┤
// │  Slot              │  PID + UID        │  进程标识 + 用户 ID│
// │  PackDataDict      │  页表/地址空间     │  进程的虚拟内存   │
// │  Analyser          │  MMU (内存管理单元) │  地址转换 + 权限 │
// │  Runner            │  CPU 上下文        │  执行环境         │
// └─────────────────────────────────────────────────────────────┘
//
// 【设计说明】
// 1. 每个执行主体 (Player/AI/System) 有一个独立的 PCB
// 2. PCB 包含独立的"地址空间"(PackDataDict) 和"执行环境"(Runner)
// 3. 权限检查由"MMU"(Analyser) 负责，使用"UID"(_subjectLevel)
// 4. CPU 和 MMU 的 UID 保持一致，确保权限一致性
//
// 【生命周期】
// - 由 GraphHub 统一管理和调度
// - ApplyUser() 时切换用户的"进程组"
// - 每个 Context 之间完全隔离
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// ═══════════════════════════════════════════════════════════════
/// EntityGraphContext - 实体图上下文（PCB）喵~ 📋
/// ═══════════════════════════════════════════════════════════════
///
/// 职责：
/// 1. 封装一个执行主体的完整运行环境
/// 2. 持有独立的 Pack 数据字典（地址空间）
/// 3. 持有 Analyser（MMU）和 Runner（CPU）
///
/// 【操作系统类比】
/// - 类似 Linux 的 task_struct (进程描述符)
/// - 包含进程的所有关键信息：地址空间、CPU 上下文、权限等
/// ═══════════════════════════════════════════════════════════════
/// </summary>
public class EntityGraphContext
{
    public GraphInstanceSlot Slot { get; }
    public Dictionary<string, BasePackData> PackDataDict { get; private set; }
    public GraphAnalyser Analyser { get; }
    public GraphRunner Runner { get; }

    public EntityGraphContext(GraphInstanceSlot slot)
    {
        Slot = slot;
        PackDataDict = new Dictionary<string, BasePackData>();
        Analyser = new GraphAnalyser(PackDataDict, (int)slot);
        Runner = new GraphRunner(PackDataDict, (int)slot);
    }

    public void SetPackDataDict(Dictionary<string, BasePackData> packDataDict)
    {
        PackDataDict = packDataDict ?? new Dictionary<string, BasePackData>();
        Analyser.SetPackDataDict(PackDataDict);
        Runner.SetPackDataDict(PackDataDict);
    }
}
