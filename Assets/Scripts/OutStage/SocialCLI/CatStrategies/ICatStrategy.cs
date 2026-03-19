using System;

namespace CatStrategies
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

        /// <summary>
        /// 处理上下箭头键（用于选项导航）喵~
        /// </summary>
        void OnArrowKey(bool isUp);

        /// <summary>
        /// 处理回车确认（确认当前高亮选项）喵~
        /// </summary>
        void OnConfirm();
    }

    /// <summary>
    /// CatStrategy 抽象基类，为 OnArrowKey 和 OnConfirm 提供空默认实现喵~
    /// 继承此类即可只重写需要的方法，不破坏已有策略
    /// </summary>
    public abstract class CatStrategyBase : ICatStrategy
    {
        public abstract void Execute(string vfsPath, string graphPath = null);
        public abstract void OnInput(string input);
        public abstract void Close();

        /// <summary>默认空实现：不处理方向键喵~</summary>
        public virtual void OnArrowKey(bool isUp) { }

        /// <summary>默认空实现：不处理确认键喵~</summary>
        public virtual void OnConfirm() { }
    }
}
