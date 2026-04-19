using System;
using System.Collections.Generic;
using NekoGraph;
using UnityEngine;

/// <summary>
/// 新档启动装配器。
/// 挂在 SaveManager 同一对象上，通过 Inspector 直接指定启动用的 .nekograph 资产或空包。
/// </summary>
public sealed class StartBoots : MonoBehaviour
{
    [SerializeField] private List<StartBootEntry> _entries = new();

    public bool HasEntries => _entries != null && _entries.Count > 0;

    public void ApplyTo(UserModel user)
    {
        if (user == null)
            throw new ArgumentNullException(nameof(user));

        user.PackDataDict ??= new Dictionary<string, BasePackData>();

        if (_entries == null)
            return;

        Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            null,
            "[start_boots] apply-to-save entries={0}",
            _entries.Count);

        foreach (var entry in _entries)
        {
            ApplyEntry(user, entry);
        }
    }

    public void ApplyHubBindings(GraphHub hub)
    {
        if (hub == null)
            return;

        Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            null,
            "[start_boots] apply-hub-bindings hub={0}",
            hub.GetType().Name);

        hub.ClearFacadeBindings();

        if (_entries == null)
            return;

        foreach (var entry in _entries)
        {
            if (entry?.Facade == null)
            {
                Debug.LogFormat(
                    LogType.Warning,
                    LogOption.NoStacktrace,
                    null,
                    "[start_boots] skip-entry reason=missing-facade source={0}",
                    entry?.Source);
                continue;
            }

            hub.RegisterFacade(entry.Facade);

            string packID = entry.ResolvePackID();
            if (string.IsNullOrWhiteSpace(packID))
            {
                Debug.LogFormat(
                    LogType.Warning,
                    LogOption.NoStacktrace,
                    null,
                    "[start_boots] facade-pack-unresolved facade={0} source={1}",
                    entry.Facade.GetType().Name,
                    entry.Source);
                continue;
            }

            entry.Facade.BindPack(packID);
            Debug.LogFormat(
                LogType.Log,
                LogOption.NoStacktrace,
                null,
                "[start_boots] bind facade={0} pack={1} source={2}",
                entry.Facade.GetType().Name,
                packID,
                entry.Source);
        }
    }

    private static void ApplyEntry(UserModel user, StartBootEntry entry)
    {
        if (entry == null)
            return;

        BasePackData pack = null;
        switch (entry.Source)
        {
            case StartBootSource.PackAsset:
                pack = LoadPackFromAsset(entry.PackAsset);
                break;

            case StartBootSource.EmptyPack:
                if (!string.IsNullOrWhiteSpace(entry.EmptyPackID))
                {
                    pack = new BasePackData
                    {
                        PackID = entry.EmptyPackID
                    };
                    pack.Initialize();
                }
                break;
        }

        if (pack == null)
        {
            ReportFailure($"Failed to prepare start boot entry: {entry.Describe()}");
            return;
        }

        PreparePack(pack);
        user.PackDataDict[pack.PackID] = pack;
        Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            null,
            "[start_boots] prepared-pack facade={0} pack={1} source={2}",
            entry.Describe(),
            pack.PackID,
            entry.Source);
    }

    private static BasePackData LoadPackFromAsset(TextAsset packAsset)
    {
        if (packAsset == null || string.IsNullOrWhiteSpace(packAsset.text))
            return null;

        try
        {
            return BasePackData.FromJson(packAsset.text);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[StartBoots] Failed to deserialize pack asset '{packAsset.name}': {ex.Message}");
            return null;
        }
    }

    private static void PreparePack(BasePackData pack)
    {
        pack.HasStarted = false;
        pack.ActiveSignals ??= new Queue<SignalContext>();
        pack.ActiveSignals.Clear();
        pack.Touch();
    }

    private static void ReportFailure(string message)
    {
        Debug.LogError($"[StartBoots] {message}");
    }
}

[Serializable]
public sealed class StartBootEntry
{
    [Tooltip("这份启动包要绑定到哪个 facade 实例。")]
    [SerializeReference]
    public PackFacadeBase Facade;

    [Tooltip("启动来源。固定启动包通常直接拖 .nekograph TextAsset。")]
    public StartBootSource Source = StartBootSource.PackAsset;

    [Tooltip("Inspector 绑定的 .nekograph / json TextAsset。其内部 PackID 将成为运行时 PackID。")]
    public TextAsset PackAsset;

    [Tooltip("当来源为 EmptyPack 时使用的空包 PackID。")]
    public string EmptyPackID;

    public string Describe()
    {
        return Facade != null ? Facade.GetType().Name : "(Missing Facade)";
    }

    public string ResolvePackID()
    {
        return Source switch
        {
            StartBootSource.PackAsset => ResolvePackIDFromAsset(PackAsset),
            StartBootSource.EmptyPack => string.IsNullOrWhiteSpace(EmptyPackID) ? null : EmptyPackID,
            _ => null
        };
    }

    private static string ResolvePackIDFromAsset(TextAsset packAsset)
    {
        if (packAsset == null || string.IsNullOrWhiteSpace(packAsset.text))
            return null;

        try
        {
            return BasePackData.FromJson(packAsset.text)?.PackID;
        }
        catch
        {
            return null;
        }
    }
}

public enum StartBootSource
{
    PackAsset,
    EmptyPack
}
