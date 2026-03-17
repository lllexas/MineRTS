using System;

namespace SocialCLIStrategies
{
    /// <summary>
    /// Cat 命令执行策略接口喵~
    /// 负责处理不同类型文件的展示逻辑与交互接管
    /// </summary>
    public interface ICatStrategy
    {
        /// <summary>
        /// 启动策略喵~
        /// </summary>
        /// <param name="vfsPath">VFS 文件路径</param>
        /// <param name="graphPath">关联的剧情图路径（如果是 msg 类型）</param>
        void Execute(string vfsPath, string graphPath = null);

        /// <summary>
        /// 当处于该策略激活期间，处理用户输入喵~
        /// </summary>
        void OnInput(string input);

        /// <summary>
        /// 关闭并清理策略喵~
        /// </summary>
        void Close();
    }
}
