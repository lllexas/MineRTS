using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AIBrain;

public class AIBrainServer : SingletonMono<AIBrainServer>
{
    private readonly List<AIBrainDecisionArgs> _activePipelines = new List<AIBrainDecisionArgs>();
    private readonly Stack<AIBrainDecisionArgs> _argsPool = new Stack<AIBrainDecisionArgs>();

    // 全局单位归属权地图 (防争抢)
    private readonly Dictionary<EntityHandle, AIBrainBar> _unitOwnership = new Dictionary<EntityHandle, AIBrainBar>();

    // 活跃的 AI 列表 (按阵营划分)
    private readonly Dictionary<int, List<AIBrainBar>> _activeBrains = new Dictionary<int, List<AIBrainBar>>();
    public bool IsUnitControlled(EntityHandle handle)
    {
        return _unitOwnership.ContainsKey(handle);
    }
    public void AddBrain(int teamId, string identifier)
    {
        var brain = AIBrainManager.Instance.GetBrainClone(identifier, teamId);
        if (brain == null) return;

        if (!_activeBrains.ContainsKey(teamId)) _activeBrains[teamId] = new List<AIBrainBar>();
        _activeBrains[teamId].Add(brain);
    }
    public void AddAIBrainBar(int teamId, AIBrainBar brain)
    {
        if (brain == null) return;
        if (!_activeBrains.ContainsKey(teamId)) _activeBrains[teamId] = new List<AIBrainBar>();
        _activeBrains[teamId].Add(brain);
    }
    public void ApplyWaveAI(int team, string brainId, Vector2Int targetPos, List<EntityHandle> recruits)
    {
        if (recruits == null || recruits.Count == 0) return;

        var waveBrain = AIBrainManager.Instance.GetBrainClone(brainId, team) as AttackWaveBrain;
        if (waveBrain != null)
        {
            waveBrain.SetTarget(targetPos);
            waveBrain.InitializeAsSquad(team, brainId, recruits);
            AIBrainServer.Instance.AddAIBrainBar(team, waveBrain);
        }
    }
    public void ClearAll()
    {
        foreach (var args in _activePipelines)
        {
            args.Reset();
            _argsPool.Push(args);
        }
        _activePipelines.Clear();
        _unitOwnership.Clear();
        _activeBrains.Clear();
        Debug.Log("<color=red>[AIBrainServer]</color> 已执行最高指令：全局脑死亡净化。");
    }

    private void Update()
    {
        if (EntitySystem.Instance == null || !EntitySystem.Instance.IsInitialized) return;
        var whole = EntitySystem.Instance.wholeComponent;
        float dt = Time.deltaTime;

        PruneDeadUnits(); // 清理死掉的单位
        LaunchNewPipelines(dt);
        ProcessActivePipelines(whole);
    }

    private void PruneDeadUnits()
    {
        var deadKeys = _unitOwnership.Keys.Where(h => !EntitySystem.Instance.IsValid(h)).ToList();
        foreach (var key in deadKeys)
        {
            // 如果单位死了，不仅要从权属表中移除，还要从控制者的私有列表中移除
            if (_unitOwnership.TryGetValue(key, out var owner))
            {
                owner.ControlledUnits.Remove(key);
            }
            _unitOwnership.Remove(key);
        }

        foreach (var teamBrains in _activeBrains.Values)
        {
            for (int i = teamBrains.Count - 1; i >= 0; i--)
            {
                var brain = teamBrains[i];
                // 如果是一次性小队，且人都死光了，直接将其从活跃列表中抹除
                if (brain.IsOneOffSquad && brain.ControlledUnits.Count == 0)
                {
                    Debug.Log($"<color=gray>[AIBrainServer]</color> 波次大脑 {brain.Identifier} 编制打光，已自动销毁。");
                    teamBrains.RemoveAt(i);
                }
            }
        }
    }

    private void LaunchNewPipelines(float dt)
    {
        foreach (var brainList in _activeBrains.Values)
        {
            foreach (var brain in brainList)
            {
                brain.Tick(dt);
                if (brain.IsReadyForDecision() && !_activePipelines.Any(p => p.Owner == brain))
                {
                    brain.ResetDecisionCooldown();
                    var newArgs = _argsPool.Count > 0 ? _argsPool.Pop() : new AIBrainDecisionArgs();
                    newArgs.Assign(brain);
                    _activePipelines.Add(newArgs);
                }
            }
        }
    }

    private void ProcessActivePipelines(WholeComponent whole)
    {
        for (int i = _activePipelines.Count - 1; i >= 0; i--)
        {
            var args = _activePipelines[i];
            var brain = args.Owner;

            switch (args.CurrentState)
            {
                case AIBrainDecisionArgs.State.A1_ReadyForThinking:
                    brain.ThinkingPass(args, whole);
                    args.CurrentState = AIBrainDecisionArgs.State.B0_Thinking;
                    break;
                case AIBrainDecisionArgs.State.B0_Thinking:
                    if (brain.ThinkingPass(args, whole))
                    {
                        args.CurrentState = AIBrainDecisionArgs.State.B1_Thinked;
                        goto case AIBrainDecisionArgs.State.B1_Thinked;
                    }
                    break;
                case AIBrainDecisionArgs.State.B1_Thinked:
                    args.CurrentState = AIBrainDecisionArgs.State.C0_ReadyForSelection;
                    break;

                case AIBrainDecisionArgs.State.C0_ReadyForSelection:
                    brain.DeselectAndMindSelectPass(args, whole);
                    args.CurrentState = AIBrainDecisionArgs.State.C1_Selecting;
                    break;
                case AIBrainDecisionArgs.State.C1_Selecting:
                    if (brain.DeselectAndMindSelectPass(args, whole))
                    {
                        args.CurrentState = AIBrainDecisionArgs.State.C2_Selected;
                        goto case AIBrainDecisionArgs.State.C2_Selected;
                    }
                    break;

                case AIBrainDecisionArgs.State.C2_Selected:
                    // 【Server 仲裁】处理抢占逻辑
                    ResolveProposal(args);
                    args.CurrentState = AIBrainDecisionArgs.State.D0_ReadyForConfirm;
                    goto case AIBrainDecisionArgs.State.D0_ReadyForConfirm;

                case AIBrainDecisionArgs.State.D0_ReadyForConfirm:
                    if (brain.ConfirmAndSelectPass(args, whole))
                    {
                        args.CurrentState = AIBrainDecisionArgs.State.D2_Confirmed;
                        goto case AIBrainDecisionArgs.State.D2_Confirmed;
                    }
                    args.CurrentState = AIBrainDecisionArgs.State.D1_Confirming;
                    break;
                case AIBrainDecisionArgs.State.D1_Confirming:
                    if (brain.ConfirmAndSelectPass(args, whole))
                    {
                        args.CurrentState = AIBrainDecisionArgs.State.D2_Confirmed;
                        goto case AIBrainDecisionArgs.State.D2_Confirmed;
                    }
                    break;
                case AIBrainDecisionArgs.State.D2_Confirmed:
                    args.CurrentState = AIBrainDecisionArgs.State.E0_ReadyForExecution;
                    break;

                case AIBrainDecisionArgs.State.E0_ReadyForExecution:
                    brain.ExecutePass(args, whole);
                    args.CurrentState = AIBrainDecisionArgs.State.E1_Executing;
                    break;
                case AIBrainDecisionArgs.State.E1_Executing:
                    if (brain.ExecutePass(args, whole))
                    {
                        args.CurrentState = AIBrainDecisionArgs.State.F0_Done;
                        goto case AIBrainDecisionArgs.State.F0_Done;
                    }
                    break;

                case AIBrainDecisionArgs.State.F0_Done:
                    args.Reset();
                    _argsPool.Push(args);
                    _activePipelines.RemoveAt(i);
                    break;
            }
        }
    }

    private void ResolveProposal(AIBrainDecisionArgs args)
    {
        var requester = args.Owner;
        args.FinalUnits.Clear(); // 准备装入最终批准的单位

        // 1. 踢掉不需要的
        var unitsToDeselect = new HashSet<EntityHandle>(requester.ControlledUnits);
        unitsToDeselect.ExceptWith(args.UnitsToSelect);
        foreach (var handle in unitsToDeselect)
        {
            if (_unitOwnership.TryGetValue(handle, out var currentOwner) && currentOwner == requester)
            {
                _unitOwnership.Remove(handle);
            }
        }

        // 2. 抢占想要的
        foreach (var handle in args.UnitsToSelect)
        {
            if (_unitOwnership.TryGetValue(handle, out var currentOwner))
            {
                if (currentOwner != requester && requester.Priority > currentOwner.Priority)
                {
                    _unitOwnership[handle] = requester;
                    args.FinalUnits.Add(handle);
                }
            }
            else
            {
                _unitOwnership[handle] = requester;
                args.FinalUnits.Add(handle);
            }
        }
    }
}