using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AIBrain
{
    public class AIBrainServerData
    {
        public Dictionary<int, List<AIBrainBar>> AiBrains = new();

        public string GetId()
        {
            return "AIBrainServerData";
        }
        public string GetPersistentId()
        {
            return "AIBrainServerData";
        }
    }

    public class AIBrainServerDataUpdatedArgs : EventArgs
    {
        public AIBrainServerData NewData { get; private set; }
        public AIBrainServerDataUpdatedArgs(AIBrainServerData newData)
        {
            NewData = newData;
        }
    }
}