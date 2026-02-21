using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 这个东西的作用应当是，在副本内部地图中，处理各种地图数据，比如地面、实体、特效等格子信息。
/// 接受来自上级的指令，唯一功能是把数据刷新到Grid上，因为玩家理论上同一时间只会观看到一个地图。
/// 这个类不会管理UI视图或者玩家输入，甚至不应该管理Grid是否显示到屏幕上。
/// 仅！负责，让Grid拿到正确的数据。同时，作为当前地图数据的访问入口。
/// </summary>
public class InnerMapManager : SingletonMono<InnerMapManager>
{
    public Dictionary<string, InnerMapData> innerMapDict = new Dictionary<string, InnerMapData>();

    /// <summary>
    /// 这是进入地图的总入口，功能需要包含：
    /// 接受一个地图名称参数
    /// 查字典，找到对应的地图数据，并加载。
    /// </summary>
    /// <param name="mapName"></param>
    public void ShowMap(string mapName)
    {

    }
}

public class InnerMapData : SingletonData<InnerMapData>
{
    public int currentInnerMapID = -1;
    public struct InnerMapInfo
    {
        public int mapID;
        public string mapName;
        public int[] groundGrids;
        public int[] entityGrids;
        public int[] effectGrids;
    }
}
