/// <summary>
/// Pack 启动来源。
/// </summary>
public enum PackBootstrapSource
{
    MetaLib,
    EmptyPack
}

/// <summary>
/// Facade 暴露给启动系统的最小启动描述。
/// 用来表达“这个包叫什么、从哪里来、是否必须”。
/// </summary>
public sealed class PackBootstrapDescriptor
{
    public string PackID { get; set; }
    public PackBootstrapSource Source { get; set; }
    public bool Required { get; set; }

    public bool IsMetaPack => Source == PackBootstrapSource.MetaLib;

    public static PackBootstrapDescriptor FromMetaLib(string packID, bool required = true)
    {
        return new PackBootstrapDescriptor
        {
            PackID = packID,
            Source = PackBootstrapSource.MetaLib,
            Required = required
        };
    }

    public static PackBootstrapDescriptor Empty(string packID, bool required = true)
    {
        return new PackBootstrapDescriptor
        {
            PackID = packID,
            Source = PackBootstrapSource.EmptyPack,
            Required = required
        };
    }
}
