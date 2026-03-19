using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using NekoGraph;

namespace NekoGraph
{
    /// <summary>
    /// ═══════════════════════════════════════════════════════════════
    /// PersistentVFSManager - VFS 持久化守护者喵~
    /// ═══════════════════════════════════════════════════════════════
    ///
    /// 设计理念：
    /// 1. 它是 GraphAnalyser 的持久化插件，专门负责 VFS 状态与存档（UserModel）的同步。
    /// 2. 彻底拥护 MetaLib.PackID 体系，不再使用 VFSLoader 的路径加载。
    /// 3. 暴露原子化 API（挂载/卸载/保存），每个方法只做一件事。
    ///    VFS 实例的本质是磁盘，插盘是原子操作，无条件强制覆盖同名实例。
    ///
    /// ═══════════════════════════════════════════════════════════════
    /// </summary>
    public class PersistentVFSManager : SingletonData<PersistentVFSManager>
    {
        private UserModel _currentUser;

        /// <summary>游戏始终需要的 VFS 包列表（Inspector 配置）喵~</summary>
        [Tooltip("游戏启动/读档后自动挂载的 VFS 包 ID 列表")]
        public string[] DefaultPackIDs = { "social_tree_default" };

        /// <summary>
        /// VFS 持久化系统是否已就绪（已绑定存档）
        /// </summary>
        public bool IsReady => _currentUser != null;

        /// <summary>
        /// 【读档】从已有存档恢复 VFS 状态喵~
        /// 只做两件事：清空旧盘，从快照挨个挂载。绝无其他行为。
        /// </summary>
        public void LoadFromSave(UserModel userModel)
        {
            _currentUser = userModel;

            UnmountAll();

            foreach (var kvp in userModel.VFSSnapshots)
                MountVFSFromPack(kvp.Value);

            Debug.Log($"<color=cyan>[PersistentVFS]</color> 读档完成：{userModel.Metadata.PlayerName}，恢复 {userModel.VFSSnapshots.Count} 个 VFS 实例");
            PostSystem.Instance.Send("VFS.IO_Ready", userModel);
        }

        /// <summary>
        /// 【新档】从 MetaLib 拉取默认包，挂载后写入存档快照喵~
        /// 只在创建全新存档时调用。
        /// </summary>
        public void InitForNewSave(UserModel userModel)
        {
            _currentUser = userModel;

            UnmountAll();

            foreach (var packID in DefaultPackIDs)
            {
                var instance = MountVFS(packID);
                if (instance != null)
                    SyncToSave(packID);
            }

            Debug.Log($"<color=cyan>[PersistentVFS]</color> 新档初始化完成：{userModel.Metadata.PlayerName}，挂载 {DefaultPackIDs.Length} 个默认 VFS 包");
            PostSystem.Instance.Send("VFS.IO_Ready", userModel);
        }

        // =========================================================
        //  原子化挂载 / 卸载 API 喵~
        // =========================================================

        /// <summary>从 MetaLib 加载指定包并强制挂载（盘符一致则覆盖）喵~</summary>
        public VFSInstance MountVFS(string packID)
        {
            var template = MetaLib.GetPack<VFSPackData>(packID);
            if (template == null)
            {
                Debug.LogError($"[PersistentVFS] MetaLib 找不到包：{packID}");
                return null;
            }
            return GraphAnalyser.Instance.LoadVFSFromPack(template);
        }

        /// <summary>从 PackData 强制挂载（盘符一致则覆盖）喵~</summary>
        public VFSInstance MountVFSFromPack(VFSPackData pack)
        {
            return GraphAnalyser.Instance.LoadVFSFromPack(pack);
        }

        /// <summary>卸载指定盘符的 VFS 实例（从内存销毁）喵~</summary>
        public void UnmountVFS(string packID)
        {
            GraphAnalyser.Instance.UnregisterInstance(packID);
            Debug.Log($"[PersistentVFS] 已卸载：{packID}");
        }

        /// <summary>卸载所有 VFS 实例喵~</summary>
        public void UnmountAll()
        {
            var ids = GraphAnalyser.Instance.GetAllInstanceIds();
            foreach (var id in ids)
                GraphAnalyser.Instance.UnregisterInstance(id);
            Debug.Log("[PersistentVFS] 已卸载全部 VFS 实例");
        }

        /// <summary>
        /// 将指定 VFS 实例序列化到 StreamingAssets 并注册到 MetaLib（运行时内存注册；编辑器下同步写 MetaLib.json）喵~
        /// </summary>
        public bool SaveVFSToDisk(string packID)
        {
            var instance = GraphAnalyser.Instance.GetInstance(packID);
            if (instance == null)
            {
                Debug.LogError($"[PersistentVFS] 找不到实例：{packID}，无法保存喵！");
                return false;
            }

            var pack = instance.ToPackData();
            string relativePath = $"NekoGraph/{packID}.json";
            string fullPath = Path.Combine(Application.streamingAssetsPath, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            VFSLoader.SavePackToFile(pack, fullPath);

            // 注册到 MetaLib（内存）
            MetaLib.Register(packID, new MetaLib.MetaEntry
            {
                PackID = packID,
                Storage = MetaLib.StorageType.StreamingAssets,
                ResourcePath = relativePath,
                DisplayName = pack.DisplayName ?? packID
            });

#if UNITY_EDITOR
            MetaLib.Save(); // 编辑器下持久化 MetaLib.json
#endif

            Debug.Log($"[PersistentVFS] 已保存到 StreamingAssets：{relativePath}");
            return true;
        }

        /// <summary>
        /// 把当前内存中的运行时状态序列化到 UserModel 的快照字典中
        /// </summary>
        public void SyncToSave(string packID)
        {
            if (_currentUser == null) return;

            var instance = GraphAnalyser.Instance.GetInstance(packID);
            if (instance == null) return;

            // 调用运行时快照转换方法
            _currentUser.VFSSnapshots[packID] = instance.ToPackData();
            Debug.Log($"[PersistentVFS] 已将 VFS {packID} 同步到存档内存快照");
        }

        /// <summary>
        /// 全量同步（存档落盘前的最后一步）
        /// </summary>
        public void SyncAll()
        {
            if (_currentUser == null) return;

            var activeIds = GraphAnalyser.Instance.GetAllInstanceIds();
            foreach (var id in activeIds)
            {
                SyncToSave(id);
            }
        }
    }
}
