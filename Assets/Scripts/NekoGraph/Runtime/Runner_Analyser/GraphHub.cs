using System;
using System.Collections.Generic;
using UnityEngine;

public class GraphHub : SingletonMono<GraphHub>
{
    private readonly Dictionary<GraphInstanceSlot, EntityGraphContext> _contexts =
        new Dictionary<GraphInstanceSlot, EntityGraphContext>();

    public GraphAnalyser DefaultAnalyser => GetContext(GraphInstanceSlot.Player)?.Analyser;
    public GraphRunner DefaultRunner => GetContext(GraphInstanceSlot.Player)?.Runner;

    protected override void Awake()
    {
        base.Awake();
        InitializeAllContexts();
    }

    private void Update()
    {
        foreach (var context in _contexts.Values)
        {
            context.Runner.Tick();
        }
    }

    public EntityGraphContext GetContext(GraphInstanceSlot slot)
    {
        if (!_contexts.TryGetValue(slot, out var context))
        {
            context = new EntityGraphContext(slot);
            _contexts[slot] = context;
        }

        return context;
    }

    public Dictionary<string, BasePackData> GetPackDataDict(GraphInstanceSlot slot)
    {
        return GetContext(slot).PackDataDict;
    }

    public void ApplyUser(UserModel user)
    {
        InitializeAllContexts();

        if (user == null)
        {
            foreach (var context in _contexts.Values)
            {
                context.SetPackDataDict(new Dictionary<string, BasePackData>());
                context.Analyser.RebuildIndex();
            }
            return;
        }

        user.PackDataDict ??= new Dictionary<string, BasePackData>();

        foreach (GraphInstanceSlot slot in Enum.GetValues(typeof(GraphInstanceSlot)))
        {
            var context = GetContext(slot);
            Dictionary<string, BasePackData> packDict = slot == GraphInstanceSlot.Player
                ? user.PackDataDict
                : user.GetEntityPackDict(slot, createIfMissing: true);

            context.SetPackDataDict(packDict);
            context.Analyser.RebuildIndex();
            context.Runner.OnPackDataDictLoaded(packDict);
        }
    }

    private void InitializeAllContexts()
    {
        foreach (GraphInstanceSlot slot in Enum.GetValues(typeof(GraphInstanceSlot)))
        {
            GetContext(slot);
        }
    }
}
