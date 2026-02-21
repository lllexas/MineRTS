using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AIBrain
{
    #region # 决策参数包 (Decision Arguments)

    /// <summary>
    /// AI大脑决策参数包，作为一个可变的状态容器，在整个决策流水线中传递。
    /// 每个决策阶段 (Pass) 都会读取并修改这个对象，将其处理结果填充进去，供下一阶段使用。
    /// </summary>
    public class AIBrainDecisionArgs
    {
        /// <summary>
        /// 决策流水线的状态机。命名中的字母序号提供了清晰的流程标识。
        /// </summary>
        public enum State
        {
            // --- A: 准备阶段 ---
            A0_Idle,                      // 空闲，位于对象池中等待分配。
            A1_ReadyForThinking,          // 已分配，准备进入思考阶段。

            // --- B: 思考阶段 ---
            B0_Thinking,                  // 正在执行 ThinkingPass (可能跨越多帧)。
            B1_Thinked,                   // 思考完成，准备进入下一阶段。

            // --- C: 单位筛选阶段 ---
            C0_ReadyForSelection,         // 准备执行单位筛选。
            C1_Selecting,                 // 正在执行 DeselectAndMindSelectPass (可能跨越多帧)。
            C2_Selected,                  // 筛选完成，等待Server进行最终确认。

            // --- D: 确认阶段 (无仲裁等待) ---
            D0_ReadyForConfirm,           // 准备让AI确认最终结果。
            D1_Confirming,                // 正在执行 ConfirmAndSelectPass (可能跨越多帧)。
            D2_Confirmed,                 // AI确认完毕。

            // --- E: 执行阶段 ---
            E0_ReadyForExecution,         // 准备执行最终指令。
            E1_Executing,                 // 正在执行 ExecutePass (可能跨越多帧)。

            // --- F: 结束 ---
            F0_Done                       // 全部流程执行完毕，等待回收。
        }
        public State CurrentState { get; set; } = State.A0_Idle;
        public AIBrainBar Owner { get; private set; }

        // 【思考切片】：比如存一个目标坐标、当前战术模式 (进攻/防守)
        public object ThinkingPlan { get; set; }

        // 【核心修改】全部替换为 EntityHandle
        public HashSet<EntityHandle> UnitsToSelect { get; set; } = new HashSet<EntityHandle>();
        public HashSet<EntityHandle> FinalUnits { get; } = new HashSet<EntityHandle>();

        public void Assign(AIBrainBar owner)
        {
            this.Owner = owner;
            this.CurrentState = State.A1_ReadyForThinking;
        }

        public void Reset()
        {
            CurrentState = State.A0_Idle;
            Owner = null;
            ThinkingPlan = null;
            UnitsToSelect.Clear();
            FinalUnits.Clear();
        }
    }

    #endregion

    /// <summary>
    /// AI大脑的抽象基类，定义了决策流水线的接口和通用功能。
    /// 每个具体的AI行为（如：进攻型AI、防守型AI）都应继承自此类。
    /// "Bar" 可能代表一个可插拔的AI行为模块喵？
    /// </summary>
    public abstract class AIBrainBar : ICloneable
    {
        public int TeamId { get; private set; }
        public string Identifier { get; private set; }
        public int Priority { get; protected set; } = 0;

        // 【运行时状态】当前AI实际控制的单位集合
        public HashSet<EntityHandle> ControlledUnits { get; private set; } = new HashSet<EntityHandle>();

        public float MinDecisionInterval { get; protected set; } = 0.5f;
        public float MaxDecisionInterval { get; protected set; } = 1.0f;
        private float _decisionCooldown = 0f;
        public bool IsOneOffSquad { get; protected set; } = false;

        public void InitializeAsSquad(int teamId, string identifier, List<EntityHandle> initialUnits)
        {
            Initialize(teamId, identifier);
            this.IsOneOffSquad = true;

            // 直接接管导演分配的部队
            foreach (var unit in initialUnits)
            {
                this.ControlledUnits.Add(unit);
            }
        }
        public virtual void Initialize(int teamId, string identifier)
        {
            this.TeamId = teamId;
            this.Identifier = identifier;
            this.ControlledUnits = new HashSet<EntityHandle>();
            ResetDecisionCooldown();
        }

        public void Tick(float deltaTime)
        {
            if (_decisionCooldown > 0) _decisionCooldown -= deltaTime;
        }

        public bool IsReadyForDecision() => _decisionCooldown <= 0;
        public void ResetDecisionCooldown() => _decisionCooldown = UnityEngine.Random.Range(MinDecisionInterval, MaxDecisionInterval);

        // ==========================================================
        //  AI 流水线接口 (The Pipeline)
        // ==========================================================

        /// <summary>
        /// 阶段 A：纵览全局，制定计划。
        /// </summary>
        public virtual bool ThinkingPass(AIBrainDecisionArgs args, WholeComponent whole) { return true; }

        /// <summary>
        /// 阶段 B：裁员。把不需要的单位交还给大自然。
        /// </summary>
        public virtual bool DeselectAndMindSelectPass(AIBrainDecisionArgs args, WholeComponent whole)
        {
            var unitsToDeselect = new List<EntityHandle>();
            foreach (var handle in ControlledUnits)
            {
                if (!args.UnitsToSelect.Contains(handle))
                    unitsToDeselect.Add(handle);
            }

            foreach (var handle in unitsToDeselect)
            {
                ControlledUnits.Remove(handle);
                // 可以在这里重置被抛弃单位的 AIComponent (变为 Idle)
                int idx = EntitySystem.Instance.GetIndex(handle);
                if (idx != -1)
                {
                    whole.aiComponent[idx].CurrentCommand = UnitCommand.None;
                }
            }
            return true;
        }

        /// <summary>
        /// 阶段 C：接收 Server 仲裁结果。
        /// </summary>
        public virtual bool ConfirmAndSelectPass(AIBrainDecisionArgs args, WholeComponent whole)
        {
            // 将 Server 批准的最终单位纳入麾下
            foreach (var handle in args.FinalUnits)
            {
                ControlledUnits.Add(handle);
            }
            return true;
        }

        /// <summary>
        /// 阶段 D：向下属下达具体的执行指令 (写入 AIComponent)
        /// </summary>
        public virtual bool ExecutePass(AIBrainDecisionArgs args, WholeComponent whole) { return true; }

        public object Clone() => this.MemberwiseClone();
    }
}