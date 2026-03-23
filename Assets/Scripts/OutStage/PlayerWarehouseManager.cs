using System;
using System.Collections.Generic;
using UnityEngine;
using NekoGraph;

/// <summary>
/// 玩家仓库管理器：
/// 玩家仓库本体是一个 Pack。
/// VFS 是这个 Pack 的用途。
/// 本类只负责对这个仓库 Pack 做批处理预演与提交。
/// </summary>
public class PlayerWarehouseManager : SingletonData<PlayerWarehouseManager>
{
    public const string DefaultWarehousePackID = "player_warehouse";
    public const string DefaultItemsRootPath = "/";
    public const string WarehouseChangedEvent = "Warehouse.Changed";

    public string CurrentWarehousePackID { get; private set; } = DefaultWarehousePackID;

    public void SetWarehousePackID(string packID)
    {
        if (!string.IsNullOrEmpty(packID))
            CurrentWarehousePackID = packID;
    }

    public WarehouseBatch CreateBatch(string reason = null, string packID = null, string itemRootPath = null)
    {
        return new WarehouseBatch
        {
            PackID = string.IsNullOrEmpty(packID) ? CurrentWarehousePackID : packID,
            Reason = reason,
            ItemRootPath = string.IsNullOrEmpty(itemRootPath) ? DefaultItemsRootPath : itemRootPath
        };
    }

    public bool CanApply(WarehouseBatch batch)
    {
        return Preview(batch).IsValid;
    }

    public WarehouseBatchPreview Preview(WarehouseBatch batch)
    {
        var preview = new WarehouseBatchPreview
        {
            PackID = ResolvePackID(batch),
            ItemRootPath = ResolveItemRootPath(batch)
        };

        if (!TryValidateBatch(batch, preview))
            return preview;

        if (!TryGetWarehousePack(preview.PackID, requireWrite: true, out _, out _, out string packError))
        {
            preview.Message = packError;
            return preview;
        }

        var workingCounts = new Dictionary<int, long>();
        var touchedOrder = new List<int>();
        var touchedSet = new HashSet<int>();

        foreach (var op in batch.Operations)
        {
            if (!touchedSet.Contains(op.ItemType))
            {
                if (!TryReadItemCount(preview.PackID, preview.ItemRootPath, op.ItemType, out long currentCount, out string readError))
                {
                    preview.Message = readError;
                    return preview;
                }

                workingCounts[op.ItemType] = currentCount;
                touchedOrder.Add(op.ItemType);
                touchedSet.Add(op.ItemType);
            }

            long before = workingCounts[op.ItemType];
            long after = before;

            switch (op.Type)
            {
                case WarehouseOpType.Add:
                    after = before + op.Count;
                    break;

                case WarehouseOpType.Consume:
                    if (before < op.Count)
                    {
                        preview.Message = $"仓库物品不足：ItemType={op.ItemType}，需要 {op.Count}，当前 {before}";
                        return preview;
                    }
                    after = before - op.Count;
                    break;

                case WarehouseOpType.Set:
                    after = op.Count;
                    break;

                case WarehouseOpType.Delete:
                    after = 0;
                    break;

                default:
                    preview.Message = $"未知操作类型：{op.Type}";
                    return preview;
            }

            if (after < 0)
            {
                preview.Message = $"仓库数量不能为负数：ItemType={op.ItemType}";
                return preview;
            }

            workingCounts[op.ItemType] = after;
        }

        foreach (int itemType in touchedOrder)
        {
            long before = 0;
            TryReadItemCount(preview.PackID, preview.ItemRootPath, itemType, out before, out _);

            long after = workingCounts[itemType];
            preview.Changes.Add(new WarehouseChange
            {
                ItemType = itemType,
                BeforeCount = before,
                AfterCount = after,
                Delta = after - before,
                FilePath = BuildItemFilePath(preview.ItemRootPath, itemType)
            });
        }

        preview.IsValid = true;
        preview.Message = $"预演成功：{preview.Changes.Count} 项变更";
        return preview;
    }

    public WarehouseBatchResult Apply(WarehouseBatch batch)
    {
        var preview = Preview(batch);
        var result = new WarehouseBatchResult
        {
            PackID = preview.PackID,
            ItemRootPath = preview.ItemRootPath,
            Changes = new List<WarehouseChange>(preview.Changes),
            Success = preview.IsValid,
            Message = preview.Message
        };

        if (!preview.IsValid)
            return result;

        if (!TryGetWarehousePack(preview.PackID, requireWrite: true, out var analyser, out _, out string packError))
        {
            result.Success = false;
            result.Message = packError;
            return result;
        }

        if (!analyser.CreateDirectory(preview.PackID, preview.ItemRootPath, PackAccessSubjects.SystemMin))
        {
            result.Success = false;
            result.Message = $"无法创建仓库目录：{preview.ItemRootPath}";
            return result;
        }

        var originalStates = CaptureOriginalStates(analyser, preview.PackID, preview.Changes);

        foreach (var change in preview.Changes)
        {
            if (!ApplyChange(analyser, preview.PackID, change, batch?.Reason, out string applyError))
            {
                RestoreOriginalStates(analyser, preview.PackID, originalStates);
                result.Success = false;
                result.Message = applyError;
                return result;
            }
        }

        SyncPackToSave(preview.PackID, analyser);

        var payload = new WarehouseChangedPayload
        {
            PackID = preview.PackID,
            Reason = batch?.Reason,
            ItemRootPath = preview.ItemRootPath,
            Changes = result.Changes
        };
        PostSystem.Instance?.Send(WarehouseChangedEvent, payload);

        result.Success = true;
        result.Message = $"提交成功：{result.Changes.Count} 项变更";
        return result;
    }

    public long GetCount(int itemType, string packID = null, string itemRootPath = null)
    {
        string resolvedPackID = string.IsNullOrEmpty(packID) ? CurrentWarehousePackID : packID;
        string resolvedRootPath = string.IsNullOrEmpty(itemRootPath) ? DefaultItemsRootPath : itemRootPath;

        return TryReadItemCount(resolvedPackID, resolvedRootPath, itemType, out long count, out _)
            ? count
            : 0;
    }

    private static bool TryValidateBatch(WarehouseBatch batch, WarehouseBatchPreview preview)
    {
        if (batch == null)
        {
            preview.Message = "批次不能为空";
            return false;
        }

        if (batch.Operations == null || batch.Operations.Count == 0)
        {
            preview.Message = "批次没有任何操作";
            return false;
        }

        foreach (var op in batch.Operations)
        {
            if (op == null)
            {
                preview.Message = "批次中存在空操作";
                return false;
            }

            if (op.ItemType <= 0)
            {
                preview.Message = $"非法 ItemType：{op.ItemType}";
                return false;
            }

            if (op.Type != WarehouseOpType.Delete && op.Count < 0)
            {
                preview.Message = $"非法数量：ItemType={op.ItemType}, Count={op.Count}";
                return false;
            }
        }

        return true;
    }

    private static string ResolvePackID(WarehouseBatch batch)
    {
        if (batch != null && !string.IsNullOrEmpty(batch.PackID))
            return batch.PackID;

        return Instance.CurrentWarehousePackID;
    }

    private static string ResolveItemRootPath(WarehouseBatch batch)
    {
        if (batch != null && !string.IsNullOrEmpty(batch.ItemRootPath))
            return NormalizeDirectoryPath(batch.ItemRootPath);

        return DefaultItemsRootPath;
    }

    private static string NormalizeDirectoryPath(string path)
    {
        string normalized = VFSPathResolver.Normalize(path);
        return normalized.EndsWith("/") ? normalized : normalized + "/";
    }

    private static string BuildItemFilePath(string itemRootPath, int itemType)
    {
        return VFSPathResolver.Combine(NormalizeDirectoryPath(itemRootPath), itemType + ".json");
    }

    private static bool TryGetWarehousePack(string packID, bool requireWrite, out GraphAnalyser analyser, out BasePackData pack, out string error)
    {
        analyser = GraphAnalyser.Instance;
        pack = null;
        error = null;

        if (analyser == null)
        {
            error = "GraphAnalyser 未就绪";
            return false;
        }

        pack = analyser.GetPack(packID, PackAccessSubjects.Player);
        if (pack == null)
            pack = TryMountWarehousePack(packID, analyser);

        if (pack == null)
        {
            error = $"仓库 Pack 未挂载：{packID}";
            return false;
        }

        if (pack.System != NodeSystem.VFS)
        {
            error = $"目标 Pack 不是 VFS：{packID}";
            return false;
        }

        PackAccessLevel accessLevel = GraphHub.Instance != null
            ? GraphHub.Instance.GetPackAccessLevel(GraphInstanceSlot.Player, pack)
            : analyser.GetPackAccessLevel(pack, PackAccessSubjects.Player);

        if (accessLevel == PackAccessLevel.Hidden)
        {
            error = $"仓库 Pack 已隐藏，拒绝访问：{packID}";
            return false;
        }

        if (requireWrite && accessLevel != PackAccessLevel.ReadWrite)
        {
            error = $"仓库 Pack 不可写：{packID}";
            return false;
        }

        return true;
    }

    private static BasePackData TryMountWarehousePack(string packID, GraphAnalyser analyser)
    {
        var user = MainModel.Instance?.CurrentUser;
        var savedPack = user?.FindPackByPackID(packID);
        if (savedPack != null &&
            savedPack.System == NodeSystem.VFS)
        {
            return analyser.LoadVFSFromPack(savedPack);
        }

        return analyser.LoadVFS(packID);
    }

    private static bool TryReadItemCount(string packID, string itemRootPath, int itemType, out long count, out string error)
    {
        count = 0;
        error = null;

        if (!TryGetWarehousePack(packID, requireWrite: false, out var analyser, out _, out error))
            return false;

        string filePath = BuildItemFilePath(itemRootPath, itemType);
        var node = analyser.GetNode(packID, filePath, PackAccessSubjects.Player);
        if (node == null)
            return true;

        if (!(node is VFSNodeData vfsNode))
        {
            error = $"仓库路径不是 VFS 节点：{filePath}";
            return false;
        }

        if (vfsNode.IsDirectory)
        {
            error = $"仓库路径被目录占用：{filePath}";
            return false;
        }

        if (string.IsNullOrEmpty(vfsNode.DataJson))
            return true;

        try
        {
            var record = JsonUtility.FromJson<WarehouseItemRecord>(vfsNode.DataJson);
            if (record == null)
            {
                error = $"仓库文件解析失败：{filePath}";
                return false;
            }

            count = Math.Max(0, record.Count);
            return true;
        }
        catch (Exception ex)
        {
            error = $"仓库文件解析异常：{filePath}，{ex.Message}";
            return false;
        }
    }

    private static Dictionary<string, OriginalFileState> CaptureOriginalStates(GraphAnalyser analyser, string packID, List<WarehouseChange> changes)
    {
        var states = new Dictionary<string, OriginalFileState>();

        foreach (var change in changes)
        {
            if (states.ContainsKey(change.FilePath))
                continue;

            var node = analyser.GetNode(packID, change.FilePath, PackAccessSubjects.Player);
            if (node is VFSNodeData vfsNode && vfsNode.IsFile)
            {
                states[change.FilePath] = new OriginalFileState
                {
                    Exists = true,
                    DataJson = vfsNode.DataJson
                };
            }
            else
            {
                states[change.FilePath] = new OriginalFileState
                {
                    Exists = false,
                    DataJson = null
                };
            }
        }

        return states;
    }

    private static bool ApplyChange(GraphAnalyser analyser, string packID, WarehouseChange change, string reason, out string error)
    {
        error = null;

        if (change.AfterCount <= 0)
        {
            if (!analyser.PathExists(packID, change.FilePath, PackAccessSubjects.SystemMin))
                return true;

            if (!analyser.Delete(packID, change.FilePath, PackAccessSubjects.SystemMin))
            {
                error = $"删除仓库文件失败：{change.FilePath}";
                return false;
            }

            return true;
        }

        var record = new WarehouseItemRecord
        {
            ItemType = change.ItemType,
            Count = change.AfterCount,
            UpdatedAt = DateTimeOffset.Now.ToUnixTimeSeconds(),
            LastReason = reason
        };

        string json = JsonUtility.ToJson(record, true);
        if (!analyser.WriteFile(packID, change.FilePath, json, PackAccessSubjects.SystemMin))
        {
            error = $"写入仓库文件失败：{change.FilePath}";
            return false;
        }

        return true;
    }

    private static void RestoreOriginalStates(GraphAnalyser analyser, string packID, Dictionary<string, OriginalFileState> originalStates)
    {
        foreach (var kvp in originalStates)
        {
            if (!kvp.Value.Exists)
            {
                analyser.Delete(packID, kvp.Key, PackAccessSubjects.SystemMin);
                continue;
            }

            analyser.WriteFile(packID, kvp.Key, kvp.Value.DataJson ?? string.Empty, PackAccessSubjects.SystemMin);
        }
    }

    private static void SyncPackToSave(string packID, GraphAnalyser analyser)
    {
        // GraphAnalyser.Packs 直接引用 UserModel.PackDataDict，写操作即时同步，无需额外 Sync 喵~
    }

    [Serializable]
    public class WarehouseBatch
    {
        public string PackID;
        public string ItemRootPath = DefaultItemsRootPath;
        public string Reason;
        public List<WarehouseOp> Operations = new List<WarehouseOp>();

        public WarehouseBatch Add(int itemType, long count)
        {
            Operations.Add(new WarehouseOp { Type = WarehouseOpType.Add, ItemType = itemType, Count = count });
            return this;
        }

        public WarehouseBatch Consume(int itemType, long count)
        {
            Operations.Add(new WarehouseOp { Type = WarehouseOpType.Consume, ItemType = itemType, Count = count });
            return this;
        }

        public WarehouseBatch Set(int itemType, long count)
        {
            Operations.Add(new WarehouseOp { Type = WarehouseOpType.Set, ItemType = itemType, Count = count });
            return this;
        }

        public WarehouseBatch Delete(int itemType)
        {
            Operations.Add(new WarehouseOp { Type = WarehouseOpType.Delete, ItemType = itemType, Count = 0 });
            return this;
        }
    }

    [Serializable]
    public class WarehouseOp
    {
        public WarehouseOpType Type;
        public int ItemType;
        public long Count;
    }

    public enum WarehouseOpType
    {
        Add,
        Consume,
        Set,
        Delete
    }

    [Serializable]
    public class WarehouseBatchPreview
    {
        public bool IsValid;
        public string PackID;
        public string ItemRootPath;
        public string Message;
        public List<WarehouseChange> Changes = new List<WarehouseChange>();
    }

    [Serializable]
    public class WarehouseBatchResult
    {
        public bool Success;
        public string PackID;
        public string ItemRootPath;
        public string Message;
        public List<WarehouseChange> Changes = new List<WarehouseChange>();
    }

    [Serializable]
    public class WarehouseChange
    {
        public int ItemType;
        public long BeforeCount;
        public long AfterCount;
        public long Delta;
        public string FilePath;
    }

    [Serializable]
    public class WarehouseChangedPayload
    {
        public string PackID;
        public string ItemRootPath;
        public string Reason;
        public List<WarehouseChange> Changes = new List<WarehouseChange>();
    }

    [Serializable]
    private class WarehouseItemRecord
    {
        public int ItemType;
        public long Count;
        public long UpdatedAt;
        public string LastReason;
    }

    private class OriginalFileState
    {
        public bool Exists;
        public string DataJson;
    }
}
