#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using NekoGraph;

/// <summary>
/// GraphView 画布逻辑 - Mission 系统专用喵~
/// 【彻底拥抱混沌·主语驱动重构版】
/// 删除所有死板的端口校验，把自由还给开发者喵！
/// </summary>
[GraphViewType(NodeSystem.Mission)]
public class MissionGraphView : BaseGraphView<MissionPackData>
{
    // 当前地图 ID（用于联动）
    private string _currentMapId = "";
    public string CurrentMapId => _currentMapId;

    /// <summary>
    /// 设置当前地图 ID，并更新所有地图节点喵~
    /// </summary>
    public void SetCurrentMapId(string mapId)
    {
        if (_currentMapId == mapId) return;

        _currentMapId = mapId;

        // 更新所有地图节点的数据和 UI
        foreach (var node in NodeMap.Values)
        {
            if (node is MapNode mapNode)
            {
                mapNode.UpdateMapId(mapId);
            }
            else if (node is BoundMapNode boundMapNode)
            {
                boundMapNode.UpdateMapId(mapId);
            }
        }
    }
}
#endif
