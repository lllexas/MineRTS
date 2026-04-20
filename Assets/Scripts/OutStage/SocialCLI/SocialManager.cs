using System;
using UnityEngine;
using NekoGraph;

/// <summary>
/// 社交后台管理服务 - 负责发信与状态同步喵~
/// 基于 SingletonData 纯数据单例架构喵！
/// </summary>
public class SocialManager : SingletonData<SocialManager>
{
    public const string MESSAGES_FOLDER = SocialBoxFacade.MessagesFolder;
    public const string SOCIAL_PACK_ID = SocialBoxFacade.DefaultFrontendPackID;

    private static SocialBoxFacade GetSocialBoxFacade()
    {
        return GraphHub.Instance?.GetFacade<SocialBoxFacade>();
    }

    /// <summary>
    /// 发送一条社交消息给玩家喵~
    /// </summary>
    /// <param name="packID">对话图的 PackID</param>
    /// <param name="sender">发送者显示名称</param>
    /// <param name="vfsPath">可选的 VFS 路径，如果为空则使用默认路径喵~</param>
    public void SendMessage(string packID, string sender = "系统", string vfsPath = null)
    {
        var analyser = GraphHub.Instance?.DefaultAnalyser;
        if (analyser == null) return;
        var facade = GetSocialBoxFacade();
        if (facade == null) return;

        if (facade.EnsureFrontendPack(analyser) == null)
        {
            Debug.LogError($"[SocialManager] VFS 未挂载：{SOCIAL_PACK_ID}，请检查存档加载流程喵！");
            return;
        }

        var meta = MetaLib.GetMeta(packID);
        if (meta == null)
        {
            Debug.LogError($"[SocialManager] 发送失败，找不到 PackID 为 '{packID}' 的元数据");
            return;
        }

        // 1. 构造 VFS 路径
        string fullVfsPath;
        if (!string.IsNullOrEmpty(vfsPath))
        {
            fullVfsPath = facade.ResolveMessagePath(vfsPath);
        }
        else
        {
            fullVfsPath = facade.BuildMessageFilePath(packID);
        }

        // 2. 直接从 MetaLib 获取原始 JSON 字符串喵！✨
        string json = MetaLib.GetMetaString(packID);
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogError($"[SocialManager] 获取 Pack JSON 失败：{packID}");
            return;
        }

        // 3. 动态写入 VFS 喵！✨
        if (facade.WriteMessage(analyser, fullVfsPath, json, PackAccessSubjects.SystemMin))
        {
            // 写入成功后，虽然 SyncAll 会在存档时做，但这里我们可以主动触发局部同步（可选）
            // vfsManager.SyncAll(SOCIAL_PACK_ID);

            Debug.Log($"[SocialManager] 新消息已存入 VFS：{fullVfsPath} (PackID: {packID})");
            PostSystem.Instance.Send("Social.NewMessageNotification", sender);
        }
    }

    /// <summary>
    /// 标记某条消息为已读喵~
    /// </summary>
    public void MarkAsRead(string vfsPath)
    {
        var analyser = GraphHub.Instance?.DefaultAnalyser;
        if (analyser == null) return;
        var facade = GetSocialBoxFacade();
        if (facade == null) return;

        var node = facade.GetMessageNode(analyser, vfsPath, PackAccessSubjects.Player);
        if (node is VFSNodeData vfs)
        {
            var data = JsonUtility.FromJson<SocialMessageVFSData>(vfs.DataJson);
            if (data != null && !data.IsRead)
            {
                data.IsRead = true;
                vfs.DataJson = JsonUtility.ToJson(data);
                facade.WriteMessage(analyser, vfsPath, vfs.DataJson, PackAccessSubjects.SystemMin);
                Debug.Log($"[SocialManager] 消息已标记为已读：{vfsPath}");
            }
        }
    }

    [Serializable]
    public class SocialMessageVFSData
    {
        public string PackID;
        public string Sender;
        public bool IsRead;
        public long Timestamp;
    }
}
