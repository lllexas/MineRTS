using System;

namespace GAL
{
    /// <summary>
    /// 对话序列条目接口 - 支持对话文本和演出效果的统一抽象
    /// </summary>
    public interface ISequenceEntry { }

    /// <summary>
    /// 对话条目 - 最小原子单元，仅含 id/speaker/content
    /// </summary>
    [Serializable]
    public class DialogEntry : ISequenceEntry
    {
        /// <summary>有规律的唯一 ID，如 intro_001</summary>
        public string Id;

        /// <summary>说话者标识</summary>
        public string Speaker;

        /// <summary>对话内容</summary>
        public string Content;
    }
}
