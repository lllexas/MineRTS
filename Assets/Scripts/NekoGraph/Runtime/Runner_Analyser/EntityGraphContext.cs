using System.Collections.Generic;

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
        Analyser = new GraphAnalyser(PackDataDict);
        Runner = new GraphRunner(PackDataDict);
    }

    public void SetPackDataDict(Dictionary<string, BasePackData> packDataDict)
    {
        PackDataDict = packDataDict ?? new Dictionary<string, BasePackData>();
        Analyser.SetPackDataDict(PackDataDict);
        Runner.SetPackDataDict(PackDataDict);
    }
}
