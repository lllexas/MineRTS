using System;
using System.Collections.Generic;
using NekoGraph;
using UnityEngine;

/// <summary>
/// Minimal bootstrap helpers for packs that a new save should own.
/// </summary>
public static class SaveBootstrapRegistry
{
    private sealed class GeneratedPackData : BasePackData
    {
    }

    public static void ApplyDefaultPacks(UserModel user)
    {
        if (user == null)
            throw new ArgumentNullException(nameof(user));

        AddMetaPack(user, "social_tree_default", required: true);
        AddGeneratedPack(user, CreateWarehousePack(), required: true);
    }

    public static bool AddMetaPack(UserModel user, string packID, bool required = true)
    {
        return AddPack(user, MetaLib.GetPack<BasePackData>(packID), required, $"MetaLib:{packID}");
    }

    public static bool AddGeneratedPack(UserModel user, BasePackData pack, bool required = true)
    {
        return AddPack(user, pack, required, "Generated");
    }

    private static bool AddPack(UserModel user, BasePackData pack, bool required, string sourceLabel)
    {
        if (user == null)
            throw new ArgumentNullException(nameof(user));

        if (pack == null)
            return ReportFailure(required, $"Failed to create pack from {sourceLabel}");

        PreparePack(pack);
        user.PackDataDict ??= new Dictionary<string, BasePackData>();
        user.PackDataDict[Guid.NewGuid().ToString("N")] = pack;
        return true;
    }

    private static void PreparePack(BasePackData pack)
    {
        pack.HasStarted = false;
        pack.ActiveSignals ??= new Queue<SignalContext>();
        pack.ActiveSignals.Clear();
        pack.Touch();
    }

    private static bool ReportFailure(bool required, string message)
    {
        if (required)
            Debug.LogError($"[SaveBootstrapRegistry] {message}");
        else
            Debug.LogWarning($"[SaveBootstrapRegistry] {message}");
        return false;
    }

    private static BasePackData CreateWarehousePack()
    {
        var pack = new GeneratedPackData
        {
            PackID = PlayerWarehouseManager.DefaultWarehousePackID,
            DisplayName = "Player Warehouse",
            Description = "Generated private warehouse pack for a new save.",
            Author = "SaveBootstrapRegistry",
            Version = "1.0.0",
            ReadableFrom = PackAccessSubjects.Player,
            WritableFrom = PackAccessSubjects.Player,
            System = NodeSystem.VFS,
            SidePara = new Dictionary<string, string>(),
            ActiveSignals = new Queue<SignalContext>()
        };
        return pack;
    }
}
