using System;

namespace CatStrategies
{
    public interface ICatStrategy
    {
        void Execute(string vfsPath, string graphPath = null);
        void OnInput(string input);
        void Close();
        void OnArrowKey(bool isUp);
        void OnConfirm();
    }

    public abstract class CatStrategyBase : ICatStrategy
    {
        public abstract void Execute(string vfsPath, string graphPath = null);
        public abstract void OnInput(string input);
        public abstract void Close();

        public virtual void OnArrowKey(bool isUp) { }
        public virtual void OnConfirm() { }
    }
}
