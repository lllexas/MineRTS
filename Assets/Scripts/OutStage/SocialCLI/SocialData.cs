using System;
using System.Collections.Generic;

/// <summary>
/// 好友条目数据
/// </summary>
[Serializable]
public class FriendEntry
{
    public string userId;
    public string displayName;
    public long lastOnlineTime;
    public bool isOnline;
    public string location; // 当前所在关卡/位置
}

/// <summary>
/// 好友请求数据
/// </summary>
[Serializable]
public class FriendRequest
{
    public string requestId;
    public string fromUserId;
    public string fromDisplayName;
    public long timestamp;
    public string message;
}

/// <summary>
/// 群组数据
/// </summary>
[Serializable]
public class GroupData
{
    public string groupId;
    public string groupName;
    public string description;
    public List<string> memberIds = new List<string>();
    public List<string> adminIds = new List<string>();
    public long createTime;
}

/// <summary>
/// 消息条目数据
/// </summary>
[Serializable]
public class MessageEntry
{
    public string senderId;
    public string senderDisplayName;
    public string content;
    public long timestamp;
    public bool isRead;
}

/// <summary>
/// 社交系统数据模型
/// <para>存储在 UserModel 中，随存档保存喵~</para>
/// </summary>
[Serializable]
public class SocialData
{
    /// <summary>
    /// 当前用户的 ID
    /// </summary>
    public string myUserId = "player_" + Guid.NewGuid().ToString("N").Substring(0, 8);

    /// <summary>
    /// 当前用户的显示名称
    /// </summary>
    public string myDisplayName = "指挥官";

    /// <summary>
    /// 好友列表
    /// </summary>
    public List<FriendEntry> friends = new List<FriendEntry>();

    /// <summary>
    /// 收到的好友请求
    /// </summary>
    public List<FriendRequest> incomingRequests = new List<FriendRequest>();

    /// <summary>
    /// 发出的好友请求
    /// </summary>
    public List<FriendRequest> outgoingRequests = new List<FriendRequest>();

    /// <summary>
    /// 黑名单用户 ID 列表
    /// </summary>
    public List<string> blockedUsers = new List<string>();

    /// <summary>
    /// 加入的群组列表
    /// </summary>
    public List<GroupData> groups = new List<GroupData>();

    /// <summary>
    /// 消息历史记录
    /// <para>Key: 对方用户 ID, Value: 消息列表</para>
    /// </summary>
    public Dictionary<string, List<MessageEntry>> messageHistory = new Dictionary<string, List<MessageEntry>>();

    // ==================== 好友管理方法 ====================

    /// <summary>
    /// 添加好友
    /// </summary>
    public void AddFriend(FriendEntry entry)
    {
        if (!IsFriend(entry.userId))
        {
            friends.Add(entry);
        }
    }

    /// <summary>
    /// 移除好友
    /// </summary>
    public void RemoveFriend(string userId)
    {
        friends.RemoveAll(f => f.userId == userId);
    }

    /// <summary>
    /// 检查是否是好友
    /// </summary>
    public bool IsFriend(string userId)
    {
        return friends.Exists(f => f.userId == userId);
    }

    /// <summary>
    /// 获取好友条目
    /// </summary>
    public FriendEntry GetFriend(string userId)
    {
        return friends.Find(f => f.userId == userId);
    }

    // ==================== 好友请求管理方法 ====================

    /// <summary>
    /// 添加收到的好友请求
    /// </summary>
    public void AddIncomingRequest(FriendRequest request)
    {
        if (!incomingRequests.Exists(r => r.fromUserId == request.fromUserId))
        {
            incomingRequests.Add(request);
        }
    }

    /// <summary>
    /// 移除收到的好友请求
    /// </summary>
    public void RemoveIncomingRequest(string fromUserId)
    {
        incomingRequests.RemoveAll(r => r.fromUserId == fromUserId);
    }

    /// <summary>
    /// 添加发出的好友请求
    /// </summary>
    public void AddOutgoingRequest(FriendRequest request)
    {
        if (!outgoingRequests.Exists(r => r.fromUserId == request.fromUserId))
        {
            outgoingRequests.Add(request);
        }
    }

    /// <summary>
    /// 移除发出的好友请求
    /// </summary>
    public void RemoveOutgoingRequest(string toUserId)
    {
        outgoingRequests.RemoveAll(r => r.fromUserId == toUserId);
    }

    // ==================== 黑名单管理方法 ====================

    /// <summary>
    /// 添加到黑名单
    /// </summary>
    public void AddToBlock(string userId)
    {
        if (!blockedUsers.Contains(userId))
        {
            blockedUsers.Add(userId);
            // 同时移除好友关系
            RemoveFriend(userId);
        }
    }

    /// <summary>
    /// 从黑名单移除
    /// </summary>
    public void RemoveFromBlock(string userId)
    {
        blockedUsers.Remove(userId);
    }

    /// <summary>
    /// 检查是否在黑名单中
    /// </summary>
    public bool IsBlocked(string userId)
    {
        return blockedUsers.Contains(userId);
    }

    // ==================== 群组管理方法 ====================

    /// <summary>
    /// 创建群组
    /// </summary>
    public GroupData CreateGroup(string name, string description)
    {
        var group = new GroupData
        {
            groupId = "group_" + Guid.NewGuid().ToString("N").Substring(0, 8),
            groupName = name,
            description = description,
            createTime = DateTimeOffset.Now.ToUnixTimeSeconds()
        };
        group.adminIds.Add(myUserId);
        group.memberIds.Add(myUserId);
        groups.Add(group);
        return group;
    }

    /// <summary>
    /// 获取群组
    /// </summary>
    public GroupData GetGroup(string groupId)
    {
        return groups.Find(g => g.groupId == groupId);
    }

    /// <summary>
    /// 离开群组
    /// </summary>
    public void LeaveGroup(string groupId)
    {
        var group = GetGroup(groupId);
        if (group != null)
        {
            group.memberIds.Remove(myUserId);
            group.adminIds.Remove(myUserId);
            if (group.memberIds.Count == 0)
            {
                groups.Remove(group);
            }
        }
    }

    // ==================== 消息管理方法 ====================

    /// <summary>
    /// 添加消息
    /// </summary>
    public void AddMessage(string targetUserId, string content, bool isFromMe)
    {
        if (!messageHistory.TryGetValue(targetUserId, out var messages))
        {
            messages = new List<MessageEntry>();
            messageHistory[targetUserId] = messages;
        }

        messages.Add(new MessageEntry
        {
            senderId = isFromMe ? myUserId : targetUserId,
            senderDisplayName = isFromMe ? myDisplayName : "对方",
            content = content,
            timestamp = DateTimeOffset.Now.ToUnixTimeSeconds(),
            isRead = isFromMe // 自己发的消息默认已读
        });
    }

    /// <summary>
    /// 获取与某人的消息历史
    /// </summary>
    public List<MessageEntry> GetMessages(string userId)
    {
        if (messageHistory.TryGetValue(userId, out var messages))
        {
            return messages;
        }
        return new List<MessageEntry>();
    }

    /// <summary>
    /// 清除与某人的消息历史
    /// </summary>
    public void ClearMessages(string userId)
    {
        messageHistory.Remove(userId);
    }

    /// <summary>
    /// 标记消息为已读
    /// </summary>
    public void MarkMessagesRead(string userId)
    {
        if (messageHistory.TryGetValue(userId, out var messages))
        {
            foreach (var msg in messages)
            {
                if (msg.senderId != myUserId)
                {
                    msg.isRead = true;
                }
            }
        }
    }

    /// <summary>
    /// 获取未读消息数量
    /// </summary>
    public int GetUnreadCount(string userId)
    {
        if (messageHistory.TryGetValue(userId, out var messages))
        {
            int count = 0;
            foreach (var msg in messages)
            {
                if (!msg.isRead && msg.senderId != myUserId)
                {
                    count++;
                }
            }
            return count;
        }
        return 0;
    }
}
