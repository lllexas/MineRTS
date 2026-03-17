using System;
using UnityEngine;
using NekoGraph;

/// <summary>
/// 社交后台管理服务 - 负责发信与状态同步喵~
/// 基于 SingletonData 纯数据单例架构喵！
/// </summary>
public class SocialManager : SingletonData<SocialManager>
{
    public const string MESSAGES_FOLDER = "/social/messages/";
    public const string SOCIAL_VFS_ID = "Social_01";

    /// <summary>
    /// 发送一条社交消息给玩家喵~
    /// </summary>
    /// <param name="packID">对话图的 PackID</param>
    /// <param name="sender">发送者显示名称</param>
    /// <param name="vfsPath">可选的 VFS 路径，如果为空则使用默认路径喵~</param>
    public void SendMessage(string packID, string sender = "系统", string vfsPath = null)
    {
        var analyser = GraphAnalyser.Instance;
        if (analyser == null) return;

        var meta = MetaLib.GetMeta(packID);
        if (meta == null)
        {
            Debug.LogError($"[SocialManager] 发送失败，找不到 PackID 为 '{packID}' 的元数据");
            return;
        }

        // 1. 构造 VFS 路径（支持自定义路径喵~）
        string fullVfsPath;
        if (!string.IsNullOrEmpty(vfsPath))
        {
            // 用户指定了路径，直接使用
            fullVfsPath = vfsPath.StartsWith("/") ? vfsPath : VFSPathResolver.Combine(MESSAGES_FOLDER, vfsPath);
        }
        else
        {
            // 使用默认路径：/social/messages/{packID}.msg
            string fileName = packID + ".msg";
            fullVfsPath = VFSPathResolver.Combine(MESSAGES_FOLDER, fileName);
        }

        // 2. 构造肚子里的 JSON（存 PackID）
        var msgData = new SocialMessageVFSData
        {
            PackID = packID,
            Sender = sender,
            IsRead = false,
            Timestamp = DateTimeOffset.Now.ToUnixTimeSeconds()
        };
        string json = JsonUtility.ToJson(msgData);

        // 3. 动态写入 VFS 喵！✨
        if (analyser.WriteFile(SOCIAL_VFS_ID, fullVfsPath, json))
        {
            Debug.Log($"[SocialManager] 新消息已存入 VFS：{fullVfsPath} (PackID: {packID})");
            PostSystem.Instance.Send("Social.NewMessageNotification", sender);
        }
    }

    /// <summary>
    /// 标记某条消息为已读喵~
    /// </summary>
    public void MarkAsRead(string vfsPath)
    {
        var analyser = GraphAnalyser.Instance;
        var node = analyser?.GetNode(SOCIAL_VFS_ID, vfsPath);
        if (node is VFSNodeData vfs)
        {
            var data = JsonUtility.FromJson<SocialMessageVFSData>(vfs.DataJson);
            if (data != null && !data.IsRead)
            {
                data.IsRead = true;
                // 注意：这里我们不需要修改 vfs.DataJson，因为 IsRead 只是一个状态，
                // 我们可以在别处（比如一个全局的已读列表）记录它，
                // 或者如果确实要改，需要重新序列化并调用 WriteFile。
                // 为了简单，我们先只在日志里体现。
                vfs.DataJson = JsonUtility.ToJson(data); // 还是写回去吧，保持数据一致性
                analyser.WriteFile(SOCIAL_VFS_ID, vfsPath, vfs.DataJson); // 确保写入
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
